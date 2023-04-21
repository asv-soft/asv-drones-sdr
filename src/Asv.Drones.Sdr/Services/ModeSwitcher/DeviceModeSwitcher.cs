using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Reactive.Linq;
using Asv.Cfg;
using Asv.Common;
using Asv.Drones.Sdr.GnssSource;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;
using NLog;

namespace Asv.Drones.Sdr
{

    public class DeviceModeSwitcherConfig
    {
        public int RecordSendDelayMs { get; set; } = 100;
        public int StatUpdateMs { get; set; } = 5000;
        public string DeviceClass { get; set; } = "Virtual";
    }
    
    [Export(typeof(IModule))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DeviceModeSwitcher : DisposableOnceWithCancel, IModule
    {
        
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
        public DeviceModeSwitcher(ISdrMavlinkService svc, CompositionContainer container,IRecordStore store,  IConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _config = config.Get<DeviceModeSwitcherConfig>();
            if (_config.DeviceClass.IsNullOrWhiteSpace())
            {
                _config.DeviceClass = DeviceClass.Virtual;
            }
            foreach (var item in Enum.GetValues(typeof(AsvSdrCustomMode)).Cast<AsvSdrCustomMode>())
            {
                var lazyImpl = _container.GetExport<IWorkMode,IWorkModeMetadata>(ExportModeAttribute.GetContractName(item,_config.DeviceClass));
                if (lazyImpl == null) continue;
                _svc.Server.SdrEx.Base.Set(_ =>
                {
                    // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                    _.SupportedModes |= lazyImpl.Metadata.Flag;
                });
            }
            
            _currentMode = IdleWorkMode.Instance;
            
            _recordTickElapsedTime.PushFront(0);
            if (_config.StatUpdateMs > 0)
            {
                Observable.Timer(TimeSpan.FromMilliseconds(_config.StatUpdateMs), TimeSpan.FromMilliseconds(_config.StatUpdateMs))
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
            _svc.Server.SdrEx.Base.OnRecordRequest.Subscribe(OnRecordRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordTagRequest.Subscribe(OnRecordTagRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordDeleteRequest.Subscribe(OnRecordDeleteRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordTagDeleteRequest.Subscribe(OnRecordTagDeleteRequest).DisposeItWith(Disposable);
            _svc.Server.SdrEx.Base.OnRecordDataRequest.Subscribe(OnRecordDataRequest).DisposeItWith(Disposable);

            #region Status suscriptions

            _store.Count.Subscribe(count => _svc.Server.SdrEx.Base.Set(_ => _.RecordCount = count)).DisposeItWith(Disposable);
            _store.Size.Subscribe(size => _svc.Server.SdrEx.Base.Set(_ => _.Size = size)).DisposeItWith(Disposable);

            
            #endregion
            
            Disposable.AddAction(() =>
            {
                _timer?.Dispose();
                _currentRecord?.Dispose();
                _currentMode.Dispose();
            });
        }

      


        #region Store implementation
        private async void OnRecordDataRequest(AsvSdrRecordDataRequestPayload req)
        {
            if (await CheckStoreIsBusy(req.RequestId)) return;
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    await SendRequestInProgress(req.RequestId);
                    return;
                }
                var items = _store.GetData(new RecordId(req), req.Skip, req.Count);
                await _svc.Server.SdrEx.Base.SendRecordDataResponse(_ =>
                {
                    _.RequestId = req.RequestId;
                    _.Result = AsvSdrRequestAck.AsvSdrRequestAckOk;
                    _.ItemsCount = (ushort)items.Count;
                });
                await Task.Delay(_config.RecordSendDelayMs);
                foreach (var item in items)
                {
                    await _svc.Server.SdrEx.Base.SendRecordData(item.Mode, item.Fill);
                    await Task.Delay(_config.RecordSendDelayMs);
                }
            }
            catch (Exception e)
            {
                await SendRequestError(req.RequestId, e);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordTagDeleteRequest(AsvSdrRecordTagDeleteRequestPayload req)
        {
            if (await CheckStoreIsBusy(req.RequestId)) return;
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    await SendRequestInProgress(req.RequestId);
                    return;
                }

                var tagId = new TagId(req);
                _svc.Server.StatusText.Info($"Delete {tagId}");
                _store.DeleteTag(tagId);
                await _svc.Server.SdrEx.Base.SendRecordDeleteResponse(_ =>
                {
                    _.RequestId = req.RequestId;
                    _.Result = AsvSdrRequestAck.AsvSdrRequestAckOk;
                    req.RecordName.CopyTo(_.RecordName,0);
                });
            }
            catch (Exception e)
            {
                await SendRequestError(req.RequestId, e);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordDeleteRequest(AsvSdrRecordDeleteRequestPayload req)
        {
            if (await CheckStoreIsBusy(req.RequestId)) return;
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    await SendRequestInProgress(req.RequestId);
                    return;
                }

                var recId = new RecordId(req);
                _svc.Server.StatusText.Info($"Delete {recId}");
                _store.DeleteRecord(recId);
                await _svc.Server.SdrEx.Base.SendRecordDeleteResponse(_ =>
                {
                    _.RequestId = req.RequestId;
                    _.Result = AsvSdrRequestAck.AsvSdrRequestAckOk;
                    req.RecordName.CopyTo(_.RecordName,0);
                });
            }
            catch (Exception e)
            {
                await SendRequestError(req.RequestId, e);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordTagRequest(AsvSdrRecordTagRequestPayload req)
        {
            if (await CheckStoreIsBusy(req.RequestId)) return;
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    await SendRequestInProgress(req.RequestId);
                    return;
                }
                
                var items = _store.GetTags(new RecordId(req), req.Skip, req.Count);
                await _svc.Server.SdrEx.Base.SendRecordTagResponse(_ =>
                {
                    _.RequestId = req.RequestId;
                    _.Result = AsvSdrRequestAck.AsvSdrRequestAckOk;
                    _.ItemsCount = (ushort)items.Count;
                });
                await Task.Delay(_config.RecordSendDelayMs);
                foreach (var item in items)
                {
                    await _svc.Server.SdrEx.Base.SendRecordTag(item.Fill);
                    await Task.Delay(_config.RecordSendDelayMs);
                }
            }
            catch (Exception e)
            {
                await SendRequestError(req.RequestId, e);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }
        private async void OnRecordRequest(AsvSdrRecordRequestPayload req)
        {
            if (await CheckStoreIsBusy(req.RequestId)) return;
            try
            {
                if (Interlocked.CompareExchange(ref _isBusy,1,0) != 0)
                {
                    await SendRequestInProgress(req.RequestId);
                    return;
                }
                var items = _store.GetRecords(req.Skip, req.Count);
                await _svc.Server.SdrEx.Base.SendRecordResponse(_ =>
                {
                    _.RequestId = req.RequestId;
                    _.Result = AsvSdrRequestAck.AsvSdrRequestAckOk;
                    _.ItemsCount = (ushort)items.Count;
                });
                await Task.Delay(_config.RecordSendDelayMs);
                foreach (var item in items)
                {
                    await _svc.Server.SdrEx.Base.SendRecord(item.Fill);
                    await Task.Delay(_config.RecordSendDelayMs);
                }
            }
            catch (Exception e)
            {
                await SendRequestError(req.RequestId, e);
            }
            finally
            {
                Interlocked.Exchange(ref _isBusy,0);
            }
        }

        private async Task SendRequestInProgress(ushort reqId)
        {
            _svc.Server.StatusText.Error("Request in progress");
            await _svc.Server.SdrEx.Base.SendRecordResponse(_ =>
            {
                _.RequestId = reqId;
                _.Result = AsvSdrRequestAck.AsvSdrRequestAckInProgress;
            });
        }
        private async Task SendRequestError(ushort reqId, Exception e)
        {
            _svc.Server.StatusText.Error(e.Message);
            await _svc.Server.SdrEx.Base.SendRecordResponse(_ =>
            {
                _.RequestId = reqId;
                _.Result = AsvSdrRequestAck.AsvSdrRequestAckFail;
            });
        }
        private async Task<bool> CheckStoreIsBusy(ushort reqId)
        {
            if (_currentRecord != null)
            {
                _svc.Server.StatusText.Error("Stop current record before request");
                return false;
            }
            await _svc.Server.SdrEx.Base.SendRecordResponse(_ =>
            {
                _.RequestId = reqId;
                _.Result = AsvSdrRequestAck.AsvSdrRequestAckInProgress;
            });
            return true;
        }
        #endregion
        
        private async Task<MavResult> SetMode(AsvSdrCustomMode mode, ulong frequencyHz, float recordRate,int sendingThinningRatio, CancellationToken cancel)
        {
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
                newMode = _container.GetExport<IWorkMode,IWorkModeMetadata>(ExportModeAttribute.GetContractName(mode,_config.DeviceClass));
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
            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            _svc.Server.SdrEx.Base.Set(_ => _.RecordState &= ~AsvSdrRecordStateFlag.AsvSdrRecordFlagStarted);
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
                var recordId = new RecordId(recordName);
                if (_store.Exists(recordId))
                {
                    _svc.Server.StatusText.Error("Record with same name already exists");
                    return Task.FromResult(MavResult.MavResultDenied);
                }
                _currentRecord = _store.OpenWrite(recordId);
            }
            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            _svc.Server.SdrEx.Base.Set(_ => _.RecordState |= AsvSdrRecordStateFlag.AsvSdrRecordFlagStarted);
            return Task.FromResult(MavResult.MavResultAccepted);
        }

        private Task<MavResult> CurrentRecordSetTag(ServerRecordTag tag, CancellationToken cancel)
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
                    _currentRecord.SetTag(tag);
                    return Task.FromResult(MavResult.MavResultAccepted);
                }
                catch (Exception e)
                {
                    _svc.Server.StatusText.Error($"Can't set tag: {e.Message}");
                    return Task.FromResult(MavResult.MavResultFailed);
                }
            }
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
                        mode.Fill(dataIndex, payload);
                        writer?.Write(dataIndex,_currentMode.Mode, payload);
                    });
                }
                else
                {
                    // need save only
                    var data = _svc.Server.SdrEx.Base.CreateRecordData(_currentMode.Mode);
                    mode.Fill(dataIndex, data.Payload);
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
