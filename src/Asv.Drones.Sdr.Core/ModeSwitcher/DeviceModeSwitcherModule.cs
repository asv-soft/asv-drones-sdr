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
    /// <summary>
    /// Configuration class for the DeviceModeSwitcher.
    /// </summary>
    public class DeviceModeSwitcherConfig
    {
        /// <summary>
        /// Gets or sets the delay in milliseconds between sending each record.
        /// </summary>
        /// <value>
        /// The delay in milliseconds between sending each record.
        /// </value>
        public int RecordSendDelayMs { get; set; } = 30;

        /// <summary>
        /// Gets or sets the folder path where the SDR records are stored.
        /// </summary>
        public string SdrRecordStoreFolder { get; set; } = "records";

        /// <summary>
        /// Gets or sets the time, in milliseconds, that a file is kept in the cache.
        /// </summary>
        /// <value>
        /// The time, in milliseconds, that a file is kept in the cache. The default value is 5000 milliseconds (5 seconds).
        /// </value>
        public int FileCacheTimeMs { get; set; } = 5_000;
    }

    /// <summary>
    /// Represents a module responsible for switching the device's mode.
    /// </summary>
    [ExportModule(Name,WorkModeCheckConfigModule.Name)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DeviceModeSwitcherModule : DisposableOnceWithCancel, IModule, IMissionActions
    {
        public const string Name = "ModeSwitcher";

        /// <summary>
        /// Represents an instance of the SdrMavlinkService interface.
        /// </summary>
        private readonly ISdrMavlinkService _svc;

        /// <summary>
        /// Represents a private readonly instance of a <see cref="CompositionContainer"/>.
        /// The CompositionContainer is used for handling dependencies and performing composition in a managed application.
        /// </summary>
        private readonly CompositionContainer _container;

        /// <summary>
        /// Represents an object that provides access to time-related functionality.
        /// </summary>
        /// <remarks>
        /// This interface is used to abstract the retrieval of time to allow for easier testing and mocking.
        /// </remarks
        private readonly ITimeService _time;

        /// <summary>
        /// Represents a private readonly variable that provides calibration data.
        /// </summary>
        private readonly ICalibrationProvider _calibration;

        /// <summary>
        /// Represents a hierarchical store to store a list of data files with metadata.
        /// </summary>
        private readonly IHierarchicalStore<Guid,IListDataFile<AsvSdrRecordFileMetadata>> _store;

        /// <summary>
        /// Represents a logger for logging messages and events.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Represents the current work mode.
        /// </summary>
        private IWorkMode _currentMode;
        private readonly DeviceModeSwitcherConfig _config;

        /// <summary>
        /// The private Timer object used for tracking time. </summary>
        private Timer? _timer;

        /// <summary>
        /// This is a private variable used to store the information of whether a record is busy or not. It has a double data type.
        /// A value of 1 indicates that the record is busy, while a value of 0 indicates that the record is not busy.
        /// </summary>
        private double _recordIsBusy;
        private readonly Stopwatch _stopwatch = new();

        /// <summary>
        /// This variable represents a circular buffer used for storing the elapsed time of ticks.
        /// </summary>
        /// <remarks>
        /// A circular buffer is a fixed-size buffer that wraps around when the end is reached, allowing
        /// for efficient read and write operations without the need to shift elements. In this case, the
        /// buffer is instantiated with a capacity of 100 elements, and each element is of type double,
        /// representing the elapsed time in ticks. The buffer will store the elapsed time of the 100
        /// latest ticks.
        /// </remarks>
        private readonly CircularBuffer2<double> _recordTickElapsedTime = new(100);

        /// <summary>
        /// This private variable represents the number of skipped ticks for a record.
        /// </summary>
        private int _skippedRecordTick;

        /// <summary>
        /// Represents the tick value when an error occurred.
        /// </summary>
        private int _errorRecordTick;

        /// <summary>
        /// Object used for synchronization to ensure thread safety.
        /// </summary>
        private readonly object _sync = new();

        /// <summary>
        /// Represents the unique identifier of the current record.
        /// </summary>
        private Guid _currentRecordId;

        /// <summary>
        /// Represents a counter for tracking the number of records processed.
        /// </summary>
        private uint _recordCounter;

        /// <summary>
        /// Represents a Stopwatch used for recording time durations.
        /// </summary>
        private readonly Stopwatch _recordStopwatch = new();
        private readonly MD5 _md5 = MD5.Create();

        /// <summary>
        /// Represents the current record being processed.
        /// </summary>
        private ICachedFile<Guid,IListDataFile<AsvSdrRecordFileMetadata>>? _currentRecord;

        /// <summary>
        /// Collection of server mission items.
        /// </summary>
        private readonly IObservableCollection<ServerMissionItem> _missionItems = new ObservableCollectionExtended<ServerMissionItem>();


        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceModeSwitcherModule"/> class.
        /// </summary>
        /// <param name="svc">The SdrMavlinkService.</param>
        /// <param name="container">The CompositionContainer.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="time">The time service.</param>
        /// <param name="uav">The UAV mission source.</param>
        /// <param name="calibration">The calibration provider.</param>
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

        /// <summary>
        /// Handles a record data request.
        /// </summary>
        /// <param name="req">The payload containing the request data.</param>
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

        /// <summary>
        /// Event handler for deleting a tag associated with a record.
        /// </summary>
        /// <param name="req">The payload containing the necessary information for the delete request.</param>
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

        /// <summary>
        /// Event handler method for handling record delete requests.
        /// </summary>
        /// <param name="req">The payload of the delete request containing the GUID of the record to be deleted.</param>
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

        /// <summary>
        /// Handles record tag request.
        /// </summary>
        /// <param name="req">The record tag request payload.</param>
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

        /// <summary>
        /// Handles a record request.
        /// </summary>
        /// <param name="req">The record request payload.</param>
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

        /// <summary>
        /// Sets the mode of the software-defined radio (SDR) by initializing a new work mode and disposing the current work mode if necessary.
        /// </summary>
        /// <param name="mode">The desired work mode.</param>
        /// <param name="frequencyHz">The desired frequency in Hz.</param>
        /// <param name="recordRate">The desired record rate in Hz.</param>
        /// <param name="sendingThinningRatio">The desired thinning ratio for sending.</param>
        /// <param name="refPower">The reference power.</param>
        /// <param name="cancel">The cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation with a result of type <see cref="MavResult"/>.</returns>
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

        /// <summary>
        /// Stops the recording process.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The task representing the operation completion, returning a MavResult.</returns>
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

        /// <summary>
        /// Starts recording with the specified record name and cancellation token.
        /// </summary>
        /// <param name="recordName">The name of the record to start.</param>
        /// <param name="cancel">The cancellation token.</param>
        /// <returns>Returns a Task of MavResult. If the record start is successful, it returns MavResultAccepted; otherwise, it returns MavResultDenied.</returns>
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

        /// <summary>
        /// Sets a tag for the current record. </summary> <param name="type">The type of the tag.</param> <param name="name">The name of the tag.</param> <param name="value">The value of the tag.</param> <param name="cancel">The cancellation token.</param> <returns>A Task with a MavResult indicating the result of setting the tag.</returns>
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

        /// <summary>
        /// Executes a system control action based on the given parameters.
        /// </summary>
        /// <param name="action">The system control action to perform.</param>
        /// <param name="cancel">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the result of the system control action.</returns>
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

        /// <summary>
        /// Records tick based on the state and saves/sends the data according to the ratio.
        /// </summary>
        /// <param name="state">The state that determines the tick ratio.</param>
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

        /// <summary>
        /// Initializes the method.
        /// </summary>
        public void Init()
        {
            
        }
    }
}
