using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Asv.Cfg;
using Asv.Common;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;
using DynamicData;
using DynamicData.Binding;
using NLog;
using MavCmd = Asv.Mavlink.V2.AsvSdr.MavCmd;

namespace Asv.Drones.Sdr.Core
{

    public class DeviceModeSwitcherConfig
    {
        public int RecordSendDelayMs { get; set; } = 30;
        public string SdrRecordStoreFolder { get; set; } = "records";
        public int FileCacheTimeMs { get; set; } = 5_000;
    }

    [ExportModule(Name,WorkModeCheckConfigModule.Name)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DeviceModeSwitcherModule : DisposableOnceWithCancel, IModule, IMissionActions
    {
        public const string Name = "ModeSwitcher";

        private readonly ISdrMavlinkService _svc;
        private readonly CompositionContainer _container;
        private readonly ITimeService _time;
        private readonly ICalibrationProvider _calibration;
        private readonly IHierarchicalStore<Guid,IListDataFile<AsvSdrRecordFileMetadata>> _store;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private IWorkMode _currentMode;
        private readonly DeviceModeSwitcherConfig _config;
        private Timer? _timer;
        private double _recordIsBusy;
        private readonly Stopwatch _stopwatch = new();
        private readonly CircularBuffer2<double> _recordTickElapsedTime = new(100);
        private int _skippedRecordTick;
        private int _errorRecordTick;
        private readonly object _sync = new();
        private Guid _currentRecordId;
        private uint _recordCounter;
        private readonly Stopwatch _recordStopwatch = new();
        private readonly MD5 _md5 = MD5.Create();
        private ICachedFile<Guid,IListDataFile<AsvSdrRecordFileMetadata>>? _currentRecord;
        private readonly IObservableCollection<ServerMissionItem> _missionItems = new ObservableCollectionExtended<ServerMissionItem>();


        [ImportingConstructor]
        public DeviceModeSwitcherModule(ISdrMavlinkService svc, CompositionContainer container,IConfiguration config, ITimeService time, IUavMissionSource uav,ICalibrationProvider calibration)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _calibration = calibration ?? throw new ArgumentNullException(nameof(calibration));
            _config = config.Get<DeviceModeSwitcherConfig>();
            if (Directory.Exists(_config.SdrRecordStoreFolder) == false)
            {
                Directory.CreateDirectory(_config.SdrRecordStoreFolder);
            }
            _store = new AsvSdrStore(_config.SdrRecordStoreFolder, TimeSpan.FromMilliseconds(_config.FileCacheTimeMs))
                .DisposeItWith(Disposable);
            _md5.DisposeItWith(Disposable);
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

            #region Mission

            var missionExecutor = new MissionExecutor(_svc.Server, this, uav).DisposeItWith(Disposable); 
            _svc.Server.SdrEx.StartMission = missionExecutor.StartMission;
            _svc.Server.SdrEx.StopMission = missionExecutor.StopMission;
            _store.Count.Subscribe(count => _svc.Server.SdrEx.Base.Set(_ => _.RecordCount = count)).DisposeItWith(Disposable);
            _store.Size.Subscribe(size => _svc.Server.SdrEx.Base.Set(_ => _.Size = size)).DisposeItWith(Disposable);
            _svc.Server.Missions.Items.Bind(_missionItems).Subscribe().DisposeItWith(Disposable);

            #endregion
            #region Calibration

            _svc.Server.SdrEx.TryReadCalibrationTableInfo = calibration.TryReadCalibrationTableInfo;            
            _svc.Server.SdrEx.TryReadCalibrationTableRow = calibration.TryReadCalibrationTableRow;
            _svc.Server.SdrEx.StartCalibration = calibration.StartCalibration;
            _svc.Server.SdrEx.StopCalibration = calibration.StopCalibration;
            _svc.Server.SdrEx.WriteCalibrationTable = calibration.WriteCalibrationTable;
            _svc.Server.SdrEx.Base.Set(s => s.CalibTableCount = calibration.TableCount);
            _calibration.StopCalibration(CancellationToken.None);
            if (calibration.TableCount == 0)
            {
                _svc.Server.SdrEx.Base.Set(s => s.CalibState = AsvSdrCalibState.AsvSdrCalibStateNotSupported);
            }
            else
            {
                calibration.IsInProgress
                    .Subscribe(x=> _svc.Server.SdrEx.Base.Set(s => s.CalibState = x ? AsvSdrCalibState.AsvSdrCalibStateProgress: AsvSdrCalibState.AsvSdrCalibStateOk))
                    .DisposeItWith(Disposable);
            }

            #endregion
           
            
            Disposable.AddAction(() =>
            {
                _timer?.Dispose();
                _currentMode.Dispose();
            });
        }

        private async void OnRecordDataRequest(AsvSdrRecordDataRequestPayload req)
        {
            Logger.Trace($"<={nameof(OnRecordDataRequest)}[{req.RequestId:000}]<=(skip:{req.Skip} count:{req.Count})");
            try
            {
                var recordId = new Guid(req.RecordGuid);
                if (_store.TryGetFile(recordId, out var entry) == false)
                {
                    Logger.Error($"=>{nameof(OnRecordDataRequest)}[{req.RequestId:000}]=>ERROR:NOT EXIST");
                    _svc.Server.StatusText.Error("Record not exist");
                    await _svc.Server.SdrEx.Base.SendRecordDataResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                    return;
                }
                using var reader = _store.OpenFile(entry.Id);
                var count = reader.File.GetItemsCount(req.Skip, req.Count);
                var metadata = reader.File.ReadMetadata();
                Logger.Trace($"=>{nameof(OnRecordDataRequest)}[{req.RequestId:000}]=>SUCCESS:BEGIN SEND({count})");
                await _svc.Server.SdrEx.Base.SendRecordDataResponseSuccess(req,count);
                for (var i = req.Skip; i < req.Skip + count; i++)
                {
                    await Task.Delay(_config.RecordSendDelayMs);
                    var ii = i;
                    Logger.Trace($"=>{nameof(OnRecordDataRequest)}[{req.RequestId:000}]=>SUCCESS:DATA({ii})");
                    await _svc.Server.SdrEx.Base.SendRecordData(metadata.Info.DataType,x=>
                    {
                        var result = reader.File.Read(ii, x);
                    });
                }
                Logger.Trace($"=>{nameof(OnRecordDataRequest)}[{req.RequestId:000}]=>SUCCESS: END SEND({count})");
            }
            catch (Exception e)
            {
                Logger.Error($"=>{nameof(OnRecordDataRequest)}[{req.RequestId:000}]=>ERROR:UNHANDLED");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordDataResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
          
        }
        private async void OnRecordTagDeleteRequest(AsvSdrRecordTagDeleteRequestPayload req)
        {
            try
            {
                var recordId = new Guid(req.RecordGuid);
                if (_store.TryGetFile(recordId, out var entry) == false)
                {
                    _svc.Server.StatusText.Error("Record not exist");
                    await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                }
                var tagId = new Guid(req.TagGuid);
                using var reader = _store.OpenFile(entry.Id);
                if (reader.File.DeleteTag(tagId))
                {
                    await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseSuccess(req);
                }
                else
                {
                    _svc.Server.StatusText.Error("Tag not exist");
                    await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                }
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Request tags error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordTagDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
         
        }
        private async void OnRecordDeleteRequest(AsvSdrRecordDeleteRequestPayload req)
        {
            try
            {
                var recId = new Guid(req.RecordGuid);
                _store.DeleteFile(recId);
                _svc.Server.StatusText.Info($"Delete {recId}");
                await _svc.Server.SdrEx.Base.SendRecordDeleteResponseSuccess(req);
            }
            catch (Exception e)
            {
                _svc.Server.StatusText.Error("Request tags error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordDeleteResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
     
        }
        private async void OnRecordTagRequest(AsvSdrRecordTagRequestPayload req)
        {
            Logger.Trace($"<={nameof(OnRecordTagRequest)}[{req.RequestId:000}]<=(skip:{req.Skip} count:{req.Count})");
            try
            {
                var recordId = new Guid(req.RecordGuid);
                if (_store.TryGetEntry(recordId, out var entry) == false || entry == null)
                {
                    _svc.Server.StatusText.Error("Record not exist");
                    Logger.Error($"=>{nameof(OnRecordTagRequest)}[{req.RequestId:000}]=>ERROR:NOT EXIST");
                    await _svc.Server.SdrEx.Base.SendRecordTagResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
                    return;
                }
                using var reader = _store.OpenFile(entry.Id);
                var items = reader.File.GetTagIds(req.Skip, req.Count).ToArray();
                await _svc.Server.SdrEx.Base.SendRecordTagResponseSuccess(req,(ushort)items.Length);
                Logger.Trace($"=>{nameof(OnRecordTagRequest)}[{req.RequestId:000}]=>SUCCESS:COUNT({items.Length})");
                foreach (var tag in items)
                {
                    await Task.Delay(_config.RecordSendDelayMs);
                    await _svc.Server.SdrEx.Base.SendRecordTag(_=>
                    {
                        reader.File.ReadTag(tag, _);
                        Logger.Trace($"=>{nameof(OnRecordTagRequest)}[{req.RequestId:000}]=>SUCCESS:TAG({MavlinkTypesHelper.GetString(_.TagName)})");
                    });
                }
            }
            catch (Exception e)
            {
                Logger.Error($"=>{nameof(OnRecordTagRequest)}[{req.RequestId:000}]=>ERROR:UNHANDLED");
                _svc.Server.StatusText.Error("Request tags error");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordTagResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
       
        }
        private async void OnRecordRequest(AsvSdrRecordRequestPayload req)
        {
            Logger.Trace($"<={nameof(OnRecordRequest)}[{req.RequestId:000}]<=(skip:{req.Skip} count:{req.Count})");
            try
            {
                var items = _store.GetFiles().Skip(req.Skip).Take(req.Count).ToArray();
                Logger.Trace($"=>{nameof(OnRecordRequest)}[{req.RequestId:000}]=>SUCCESS:COUNT({items.Length})");
                await _svc.Server.SdrEx.Base.SendRecordResponseSuccess(req, (ushort)items.Length);
                foreach (var item in items)
                {
                    //delay between sending records
                    await Task.Delay(_config.RecordSendDelayMs);
                    using var reader = _store.OpenFile(item.Id);
                    await _svc.Server.SdrEx.Base.SendRecord(_=>
                    {
                        reader.File.Write(_);
                        Logger.Trace($"=>{nameof(OnRecordRequest)}[{req.RequestId:000}]=>SUCCESS:REC({MavlinkTypesHelper.GetString(_.RecordName)})");
                    });
                }
            }
            catch (Exception e)
            {
                Logger.Error($"=>{nameof(OnRecordRequest)}[{req.RequestId:000}]=>ERROR:UNHANDLED");
                _svc.Server.StatusText.Error(e.Message);
                await _svc.Server.SdrEx.Base.SendRecordResponseFail(req, AsvSdrRequestAck.AsvSdrRequestAckFail);
            }
           
        }

        public async Task<MavResult> SetMode(AsvSdrCustomMode mode, ulong frequencyHz, float recordRate,uint sendingThinningRatio, float refPower, CancellationToken cancel)
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
                _svc.Server.SdrEx.Base.Set(_ =>
                {
                    _.RefPower = Single.NaN;
                    _.SignalOverflow = Single.NaN;
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
                _calibration.SetMode(frequencyHz, refPower);
                await _currentMode.Init(frequencyHz, refPower,_calibration,cancel);
                // we no need to dispose this subscription because it will be disposed with _currentMode
                _currentMode.SignalOverflowIndicator.Subscribe(v =>
                    _svc.Server.SdrEx.Base.Set(i => i.SignalOverflow = v));
                
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
                _svc.Server.SdrEx.Base.Set(_ =>
                {
                    _.RefPower = Single.NaN;
                    _.SignalOverflow = Single.NaN;
                });
                return MavResult.MavResultFailed;
            }
            _svc.Server.Heartbeat.Set(_ =>
            {
                _.CustomMode = (uint)mode;
            });    
            _svc.Server.StatusText.Info($"Set mode '{mode:G}'({frequencyHz:N} Hz)");
            return MavResult.MavResultAccepted;
        }

        public Task<MavResult> StopRecord(CancellationToken token)
        {
            if (_currentRecord == null) return Task.FromResult(MavResult.MavResultAccepted);

            string name = null;
            uint recCount = 0;
            uint recDurationSec = 0;
            lock (_sync)
            {
                if (_currentRecord == null) return Task.FromResult(MavResult.MavResultAccepted);
                _recordStopwatch.Stop();
                _currentRecord.File.EditMetadata(x =>
                {
                    name = MavlinkTypesHelper.GetString(x.Info.RecordName);
                    recDurationSec = x.Info.DurationSec = (uint)_recordStopwatch.Elapsed.TotalSeconds;
                    // ReSharper disable once AccessToDisposedClosure
                    x.Info.Size = (uint)_currentRecord.File.ByteSize;
                    // ReSharper disable once AccessToDisposedClosure
                    recCount = x.Info.DataCount = _currentRecord.File.Count;
                    x.Info.TagCount = (ushort)x.Tags.Count;
                });
                _currentRecord.Dispose();
                _currentRecord = null;
                _currentRecordId = Guid.Empty;
                //Interlocked.Exchange(ref _recordCounter, 0);
            }
            _svc.Server.SdrEx.Base.Set(_ =>
            {
                Guid.Empty.TryWriteBytes(_.CurrentRecordGuid);
                MavlinkTypesHelper.SetString(_.CurrentRecordName,string.Empty);
            });
            Debug.Assert(name!=null);
            var duration = TimeSpan.FromSeconds(recDurationSec);
            _svc.Server.StatusText.Info($"Rec stop '{name}' ({recCount}, {duration.TotalMinutes:F0}:{duration.Seconds})");
            return Task.FromResult(MavResult.MavResultAccepted);
        }

        public Task<MavResult> StartRecord(string recordName, CancellationToken cancel)
        {
            if (_currentMode.Mode == AsvSdrCustomMode.AsvSdrCustomModeIdle)
            {
                _svc.Server.StatusText.Error("Set work mode before start record");
                return Task.FromResult(MavResult.MavResultDenied);
            }
                
            if (_currentRecord != null) return Task.FromResult(MavResult.MavResultAccepted);
            lock (_sync)
            {
                if (_currentRecord != null) return Task.FromResult(MavResult.MavResultAccepted);
                _currentRecordId = Guid.NewGuid();
                _recordStopwatch.Restart();
                Interlocked.Exchange(ref _recordCounter, 0);
                _currentRecord = _store.CreateFile(_currentRecordId, recordName, _store.RootFolderId);
                    _currentRecord.File.EditMetadata(metadata=>
                {
                    metadata.Info.DataType = _currentMode.Mode;
                    metadata.Info.CreatedUnixUs = MavlinkTypesHelper.ToUnixTimeUs(_time.Now);
                    MavlinkTypesHelper.SetGuid(metadata.Info.RecordGuid, _currentRecordId);
                    MavlinkTypesHelper.SetString(metadata.Info.RecordName,recordName);
                    metadata.Info.Frequency = _currentMode.FrequencyHz;
                });
                _svc.Server.StatusText.Info($"Rec start '{recordName}'");
            }
            _svc.Server.SdrEx.Base.Set(_ =>
            {
                _currentRecordId.TryWriteBytes(_.CurrentRecordGuid);
                MavlinkTypesHelper.SetString(_.CurrentRecordName,recordName);
            });
            return Task.FromResult(MavResult.MavResultAccepted);
        }

        public Task<MavResult> CurrentRecordSetTag(AsvSdrRecordTagType type, string name, byte[] value, CancellationToken cancel)
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
                    var hashString = $"{name}{_currentRecordId:N}";
                    var tagId = new Guid(_md5.ComputeHash(Encoding.ASCII.GetBytes(hashString)));
                    
                    _svc.Server.StatusText.Debug($"Add tag '{name}'");
                    _currentRecord.File.WriteTag(tagId,_currentRecordId, type, name, value);
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
                    case AsvSdrSystemControlAction.AsvSdrSystemControlActionRestart:
                        Environment.Exit(0);
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
                        Process.Start("/usr/bin/sudo", "/bin/systemctl reboot");
                        return Task.FromResult(MavResult.MavResultAccepted);
                    case AsvSdrSystemControlAction.AsvSdrSystemControlActionShutdown:
                        Process.Start("/usr/bin/sudo", "/bin/systemctl poweroff");
                        return Task.FromResult(MavResult.MavResultAccepted);
                    case AsvSdrSystemControlAction.AsvSdrSystemControlActionRestart:
                        Environment.Exit(0);
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
            var ratio = (uint)state!;
            try
            {
                var mode = _currentMode;
                var writer = _currentRecord;
               
                var dataIndex = Interlocked.Increment(ref _recordCounter) - 1; // start from 0 index
                if (dataIndex % ratio == 0)
                {
                    // need save and send
                    await _svc.Server.SdrEx.Base.SendRecordData(_currentMode.Mode, payload =>
                    {
                        mode.ReadData(_currentRecordId,dataIndex, payload);
                        writer?.File.Write(dataIndex, payload);
                    });
                }
                else
                {
                    // need save only
                    var data = _svc.Server.SdrEx.Base.CreateRecordData(_currentMode.Mode);
                    if (writer != null)
                    {
                        mode.ReadData(_currentRecordId,dataIndex, data.Payload);
                        writer.File.Write(dataIndex, data.Payload);
                    }
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
