using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Asv.Cfg;
using Asv.Common;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;
using NLog;

namespace Asv.Drones.Sdr.Core
{

    public class DeviceModeSwitcherConfig
    {
        public int RecordSendDelayMs { get; set; } = 50;
        public int StatisticUpdateMs { get; set; } = 30_000;
        public int MaxRecordSendPerRequest { get; set; } = 500;
    }
    
    [ExportModule(Name,WorkModeCheckConfigModule.Name)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DeviceModeSwitcherModule : DisposableOnceWithCancel, IModule
    {
        public const string Name = "ModeSwitcher";

        private readonly ISdrMavlinkService _svc;
        private readonly CompositionContainer _container;
        private readonly IRecordStore _store;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private IWorkMode _currentMode;
        private int _isBusy;
        private readonly DeviceModeSwitcherConfig _config;
        private Timer? _timer;
        private double _recordIsBusy;
        
        private readonly Stopwatch _stopwatch = new();
        private readonly CircularBuffer2<double> _recordTickElapsedTime = new(100);
        private int _skippedRecordTick;
        private int _errorRecordTick;
        
        private readonly object _sync = new();
        private IRecordDataWriter? _currentRecord;
        private uint _recordCounter;

        [ImportingConstructor]
        public DeviceModeSwitcherModule(ISdrMavlinkService svc, CompositionContainer container,IRecordStore store, IConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _config = config.Get<DeviceModeSwitcherConfig>();
           
            foreach (var item in Enum.GetValues(typeof(AsvSdrCustomMode)).Cast<AsvSdrCustomMode>())
            {
                var items = _container.GetExports<IWorkMode,IWorkModeMetadata>(ExportModeAttribute.GetContractName(item)).ToArray();
                switch (items.Length)
                {
                    case 0:
                        continue;
                    case > 1:
                        throw new Exception("Too may implementations for mode: " + item);
                    default:
                        _svc.Server.SdrEx.Base.Set(_ =>
                        {
                            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                            _.SupportedModes |= items.First().Metadata.Flag;
                        });
                        break;
                }
            }
            
            _currentMode = IdleWorkMode.Instance;
            
            _recordTickElapsedTime.PushFront(0);
            if (_config.StatisticUpdateMs > 0)
            {
                Observable.Timer(TimeSpan.FromMilliseconds(_config.StatisticUpdateMs), TimeSpan.FromMilliseconds(_config.StatisticUpdateMs))
                .Subscribe(_ =>
                {
                    var recordTick = _recordTickElapsedTime.Average();
                    _svc.Server.SdrEx.Base.Set(_ =>
                    {
                        _svc.Server.StatusText.Debug($"Rec:{recordTick:F0}ms, skipped: {_skippedRecordTick}, error: {_errorRecordTick}");
                    });
                }).DisposeItWith(Disposable);
                
            }

            _svc.Server.SdrEx.StartRecord = StartRecord;
            _svc.Server.SdrEx.StopRecord = StopRecord;  
            _svc.Server.SdrEx.CurrentRecordSetTag = CurrentRecordSetTag;
            _svc.Server.SdrEx.SetMode = SetMode;
            _svc.Server.SdrEx.SystemControlAction = SystemControlAction;
            _svc.Server.SdrEx.Base.OnRecordRequest.Subscribe(OnRecordRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordTagRequest.Subscribe(OnRecordTagRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordDeleteRequest.Subscribe(OnRecordDeleteRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordTagDeleteRequest.Subscribe(OnRecordTagDeleteRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordDataRequest.Subscribe(OnRecordDataRequest).DisposeItWith(Disposable);

            _store.Count.Subscribe(count => _svc.Server.SdrEx.Base.Set(_ => _.RecordCount = count)).DisposeItWith(Disposable);
            _store.Size.Subscribe(size => _svc.Server.SdrEx.Base.Set(_ => _.Size = size)).DisposeItWith(Disposable);
            
            Disposable.AddAction(() =>
            {
                _timer?.Dispose();
                _currentRecord?.Dispose();
                _currentMode.Dispose();
            });
        }

        private async void OnRecordDataRequest(AsvSdrRecordDataRequestPayload req)
        {
            // check if record is started
            if (_currentRecord != null)
            {
                _svc.Server.StatusText.Error("Stop record before ane request");
                await _svc.Server.SdrEx.Base.SendRecordDataResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                return;
            }
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    _svc.Server.StatusText.Error("Request in progress");
                    await _svc.Server.SdrEx.Base.SendRecordDataResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckInProgress);
                    return;
                }
                var recordId = new Guid(req.RecordGuid);
                if (_store.Exist(recordId) == false)
                {
                    _svc.Server.StatusText.Error("Record not exist");
                    await _svc.Server.SdrEx.Base.SendRecordDataResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                }
                
                using (var reader = _store.OpenRead(recordId))
                {
                    var count = reader.GetRecordsCount(req.Skip, req.Count);
                    
                    await _svc.Server.SdrEx.Base.SendRecordDataResponseSuccess(req,count);
                    for (var i = req.Skip; i < req.Skip + count; i++)
                    {
                        var ii = i;
                        await _svc.Server.SdrEx.Base.SendRecordData(reader.Mode,_=>reader.Fill(ii,_));
                        await Task.Delay(_config.RecordSendDelayMs);
                    }
                }
               
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Request tags error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordDataResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordTagDeleteRequest(AsvSdrRecordTagDeleteRequestPayload req)
        {
            // check if record is started
            if (_currentRecord != null)
            {
                _svc.Server.StatusText.Error("Stop record before ane request");
                await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                return;
            }
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    _svc.Server.StatusText.Error("Request in progress");
                    await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckInProgress);
                    return;
                }
                var recordId = new Guid(req.RecordGuid);
                if (_store.Exist(recordId) == false)
                {
                    _svc.Server.StatusText.Error("Record not exist");
                    await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                }
                var tagId = new Guid(req.TagGuid);
                using (var reader = _store.OpenRead(recordId))
                {
                    if (reader.DeleteTag(tagId))
                    {
                        await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseSuccess(req);
                    }
                    else
                    {
                        _svc.Server.StatusText.Error("Tag not exist");
                        await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                    }
                    
                }
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Request tags error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordDeleteRequest(AsvSdrRecordDeleteRequestPayload req)
        {
            // check if record is started
            if (_currentRecord != null)
            {
                _svc.Server.StatusText.Error("Stop record before ane request");
                await _svc.Server.SdrEx.Base.SendRecordDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                return;
            }
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    _svc.Server.StatusText.Error("Request in progress");
                    await _svc.Server.SdrEx.Base.SendRecordDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckInProgress);
                    return;
                }
                var recId = new Guid(req.RecordGuid);
                
                if (_store.DeleteRecord(recId))
                {
                    _svc.Server.StatusText.Info($"Delete {recId}");
                    await _svc.Server.SdrEx.Base.SendRecordDeleteResponseSuccess(req);
                }
                else
                {   
                    _svc.Server.StatusText.Info($"Rec not found {recId}");
                    await _svc.Server.SdrEx.Base.SendRecordDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                    
                }
                
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Request tags error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordTagRequest(AsvSdrRecordTagRequestPayload req)
        {
            // check if record is started
            if (_currentRecord != null)
            {
                _svc.Server.StatusText.Error("Stop record before ane request");
                await _svc.Server.SdrEx.Base.SendRecordTagResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                return;
            }
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    _svc.Server.StatusText.Error("Request in progress");
                    await _svc.Server.SdrEx.Base.SendRecordTagResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckInProgress);
                    return;
                }
                var recordId = new Guid(req.RecordGuid);
                if (_store.Exist(recordId) == false)
                {
                    _svc.Server.StatusText.Error("Record not exist");
                    await _svc.Server.SdrEx.Base.SendRecordTagResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                }
                using (var reader = _store.OpenRead(recordId))
                {
                    var items = reader.GetTags(req.Skip, req.Count);
                    await _svc.Server.SdrEx.Base.SendRecordTagResponseSuccess(req,(ushort)items.Count);
                    foreach (var tag in items)
                    {
                        await _svc.Server.SdrEx.Base.SendRecordTag(_=>reader.Fill(tag,_));
                        await Task.Delay(_config.RecordSendDelayMs);
                    }
                }
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Request tags error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordTagResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordRequest(AsvSdrRecordRequestPayload req)
        {
            // check if record is started
            if (_currentRecord != null)
            {
                _svc.Server.StatusText.Error("Stop record before ane request");
                await _svc.Server.SdrEx.Base.SendRecordResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                return;
            }
            
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    _svc.Server.StatusText.Error("Request in progress");
                    await _svc.Server.SdrEx.Base.SendRecordResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckInProgress);
                    return;
                }
                var items = _store.GetRecords(req.Skip, req.Count);
                await _svc.Server.SdrEx.Base.SendRecordResponseSuccess(req, (ushort)items.Count);
                await Task.Delay(_config.RecordSendDelayMs);
                foreach (var item in items)
                {
                    using (var reader = _store.OpenRead(item))
                    {
                        await _svc.Server.SdrEx.Base.SendRecord(reader.Fill);
                    }
                    //delay between sending records
                    await Task.Delay(_config.RecordSendDelayMs);
                }
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Request record error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        
        private async Task<MavResult> SetMode(AsvSdrCustomMode mode, ulong frequencyHz, float recordRate,int sendingThinningRatio, CancellationToken cancel)
        {
            if (_currentMode.Mode == mode) return MavResult.MavResultAccepted;
            
            Logger.Info($"Set mode {mode:G} Freq:{frequencyHz} Hz, recordRate:{recordRate:F1}Hz thinning:{sendingThinningRatio}");

            var recordDelay = (int)(1000.0 / recordRate);
            if (recordDelay < 30)
            {
                _svc.Server.StatusText.Warning("Record rate too high, set to 30ms");
                recordDelay = 30;
            }
            if (sendingThinningRatio < 1)
            {
                _svc.Server.StatusText.Warning("Sending thinning ratio too low, set to 1");
                sendingThinningRatio = 1;
            }

            try
            {
                await StopRecord(default);
                if (_timer != null) await _timer.DisposeAsync();
                _skippedRecordTick = 0;
                _errorRecordTick = 0;
                _currentMode.Dispose();
            }
            catch (Exception e)
            {
                Logger.Error($"Error to dispose work mode {_currentMode}: {e.Message}");
                _svc.Server.StatusText.Error("Disable old mode error: " + e.Message);
            }

            if (mode == AsvSdrCustomMode.AsvSdrCustomModeIdle)
            {
                _currentMode = IdleWorkMode.Instance;
                _svc.Server.Heartbeat.Set(_ =>
                {
                    _.CustomMode = (uint)AsvSdrCustomMode.AsvSdrCustomModeIdle;
                });
                return MavResult.MavResultAccepted;
            }
            
            Lazy<IWorkMode,IWorkModeMetadata>? newMode = null;
            try
            {
                newMode = _container.GetExport<IWorkMode,IWorkModeMetadata>(ExportModeAttribute.GetContractName(mode));
            }
            catch (Exception e)
            {
                Logger.Error($"Error to create mode {mode}: {e.Message}");
                _svc.Server.StatusText.Error($"Error to load {mode:G}");
                return MavResult.MavResultFailed;
            }
            
            if (newMode == null)
            {
                _svc.Server.StatusText.Error($"Mode {mode:G} not implemented");
                return MavResult.MavResultUnsupported;
            }
            
            try
            {
                _currentMode = newMode.Value;
                await _currentMode.Init(frequencyHz,cancel);
                _timer = new Timer(RecordTick, sendingThinningRatio, 1000, recordDelay);
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Init mode error: " + e.Message);
                _currentMode?.Dispose();
                _currentMode = IdleWorkMode.Instance;
                _svc.Server.Heartbeat.Set(_ =>
                {
                    _.CustomMode = (uint)AsvSdrCustomMode.AsvSdrCustomModeIdle;
                });    
                return MavResult.MavResultFailed;
            }
            _svc.Server.Heartbeat.Set(_ =>
            {
                _.CustomMode = (uint)mode;
            });    
            return MavResult.MavResultAccepted;
        }

        private Task<MavResult> StopRecord(CancellationToken token)
        {
            if (_currentRecord == null) return Task.FromResult(MavResult.MavResultAccepted);
            lock (_sync)
            {
                if (_currentRecord == null) return Task.FromResult(MavResult.MavResultAccepted);
                _currentRecord.Dispose();
                _currentRecord = null;
                Interlocked.Exchange(ref _recordCounter, 0);
            }
            _svc.Server.SdrEx.Base.Set(_ => Guid.Empty.TryWriteBytes(_.CurrentRecordGuid));
            return Task.FromResult(MavResult.MavResultAccepted);
        }

        private Task<MavResult> StartRecord(string recordName, CancellationToken cancel)
        {
            if (_currentMode.Mode == AsvSdrCustomMode.AsvSdrCustomModeIdle)
            {
                _svc.Server.StatusText.Error("Need set work mode before start record");
                return Task.FromResult(MavResult.MavResultDenied);
            }
                
            if (_currentRecord != null) return Task.FromResult(MavResult.MavResultAccepted);
            lock (_sync)
            {
                if (_currentRecord != null) return Task.FromResult(MavResult.MavResultAccepted);
                _currentRecord = _store.OpenWrite(recordName, _currentMode.Mode, _currentMode.FrequencyHz);
            }
            _svc.Server.SdrEx.Base.Set(_ => _currentRecord.RecordId.TryWriteBytes(_.CurrentRecordGuid) );
            return Task.FromResult(MavResult.MavResultAccepted);
        }

        private Task<MavResult> CurrentRecordSetTag(AsvSdrRecordTagType type, string name, byte[] value, CancellationToken cancel)
        {
            if (_currentRecord == null)
            {
                _svc.Server.StatusText.Error("Can't set tag: record is not started");
                return Task.FromResult(MavResult.MavResultDenied);
            }

            lock (_sync)
            {
                if (_currentRecord == null)
                {
                    _svc.Server.StatusText.Error("Can't set tag: record is not started");
                    return Task.FromResult(MavResult.MavResultDenied);
                }
                try
                {
                    var guid = _currentRecord.SetTag(type, name, value);
                    return Task.FromResult(MavResult.MavResultAccepted);
                }
                catch (Exception e)
                {
                    _svc.Server.StatusText.Error($"Can't set tag: {e.Message}");
                    return Task.FromResult(MavResult.MavResultFailed);
                }
            }
        }

        private Task<MavResult> SystemControlAction(AsvSdrSystemControlAction action, CancellationToken cancel)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                switch (action)
                {
                    case AsvSdrSystemControlAction.AsvSdrSystemControlActionReboot:
                        Process.Start("shutdown", "/r /t 0");
                        return Task.FromResult(MavResult.MavResultAccepted);
                    case AsvSdrSystemControlAction.AsvSdrSystemControlActionShutdown:
                        Process.Start("shutdown", "/s /t 0");
                        return Task.FromResult(MavResult.MavResultAccepted);
                    default:
                        return Task.FromResult(MavResult.MavResultFailed);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                switch (action)
                {
                    case AsvSdrSystemControlAction.AsvSdrSystemControlActionReboot:
                        Process.Start("sudo reboot");
                        return Task.FromResult(MavResult.MavResultAccepted);
                    case AsvSdrSystemControlAction.AsvSdrSystemControlActionShutdown:
                        Process.Start("sudo shutdown -h now");
                        return Task.FromResult(MavResult.MavResultAccepted);
                    default:
                        return Task.FromResult(MavResult.MavResultFailed);
                }
            }
            
            return Task.FromResult(MavResult.MavResultFailed);
        }
        
        private async void RecordTick(object? state)
        {
            if (Interlocked.CompareExchange(ref _recordIsBusy, 1, 0) != 0)
            {
                Interlocked.Increment(ref _skippedRecordTick);
                return;
            }
            _stopwatch.Restart();
            var ratio = (int)state!;
            try
            {
                var mode = _currentMode;
                var writer = _currentRecord;
               
                var dataIndex = Interlocked.Increment(ref _recordCounter);
                if (dataIndex % ratio == 0)
                {
                    // need save and send
                    await _svc.Server.SdrEx.Base.SendRecordData(_currentMode.Mode, payload =>
                    {
                        mode.Fill(writer?.RecordId ?? Guid.Empty,dataIndex, payload);
                        writer?.Write(dataIndex,_currentMode.Mode, payload);
                    });
                }
                else
                {
                    // need save only
                    var data = _svc.Server.SdrEx.Base.CreateRecordData(_currentMode.Mode);
                    mode.Fill(writer?.RecordId ?? Guid.Empty,dataIndex, data.Payload);
                    writer?.Write(dataIndex,_currentMode.Mode, data.Payload);
                }
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref _errorRecordTick);
            }
            finally
            {
                _stopwatch.Stop();
                _recordTickElapsedTime.PushFront(_stopwatch.Elapsed.TotalMilliseconds);
                Interlocked.Exchange(ref _recordIsBusy, 0);
            }
            
            
        }

        public void Init()
        {
            
        }
    }
}
