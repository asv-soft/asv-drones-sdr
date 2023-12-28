using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;
using DynamicData;
using DynamicData.Binding;
using NLog;
using AsvSdrHelper = Asv.Mavlink.AsvSdrHelper;
using MavCmd = Asv.Mavlink.V2.AsvSdr.MavCmd;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents an interface for executing missions.
/// </summary>
public interface IMissionExecutor
{
    /// <summary>
    /// Gets the current mission state.
    /// </summary>
    /// <value>
    /// The mission state.
    /// </value>
    AsvSdrMissionState MissionState { get; }
}

/// <summary>
/// Interface for performing mission actions.
/// </summary>
public interface IMissionActions
{
    /// <summary>
    /// Sets the mode of the ASV SDR.
    /// </summary>
    /// <param name="mode">The custom mode to be set.</param>
    /// <param name="frequencyHz">The frequency in Hz.</param>
    /// <param name="recordRate">The record rate.</param>
    /// <param name="sendingThinningRatio">The sending thinning ratio.</param>
    /// <param name="refPower">The reference power.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the result of the operation (MavResult).
    /// </returns>
    Task<MavResult> SetMode(AsvSdrCustomMode mode, ulong frequencyHz, float recordRate,uint sendingThinningRatio, float refPower, CancellationToken cancel);

    /// <summary>
    /// Stops the recording process.
    /// </summary>
    /// <param name="token">The cancellation token used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation and containing the result of the operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="MavException">Thrown if there is an error stopping the recording.</exception>
    Task<MavResult> StopRecord(CancellationToken token);

    /// <summary>
    /// Starts recording with the given record name.
    /// </summary>
    /// <param name="recordName">The name of the record.</param>
    /// <param name="cancel">A cancellation token to stop the recording process.</param>
    /// <returns>A task that represents the asynchronous recording operation. The task result contains the recording result.</returns>
    /// <remarks>
    /// Use this method to start recording the specified record with the provided name.
    /// <para>The <paramref name="cancel"/> parameter can be used to cancel the recording process.</para>
    /// <para>The task returned by this method represents the ongoing recording operation.
    /// The task will complete when the recording is finished or cancelled.</para>
    /// <para>The task result contains the result of the recording operation.</para>
    /// </remarks>
    Task<MavResult> StartRecord(string recordName, CancellationToken cancel);

    /// <summary>
    /// Sets the tag for the current record set.
    /// </summary>
    /// <param name="type">The type of the tag.</param>
    /// <param name="name">The name of the tag.</param>
    /// <param name="value">The value of the tag.</param>
    /// <param name="cancel">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The result of the task is a MavResult object.</returns>
    Task<MavResult> CurrentRecordSetTag(AsvSdrRecordTagType type, string name, byte[] value, CancellationToken cancel);
}

/// <summary>
/// Class for executing missions.
/// </summary>
public class MissionExecutor : DisposableOnceWithCancel, IMissionExecutor
{
    /// <summary>
    /// Represents an instance of the ISdrServerDevice interface.
    /// </summary>
    private readonly ISdrServerDevice _device;

    /// <summary>
    /// The interface for performing mission actions.
    /// </summary>
    private readonly IMissionActions _actions;

    /// <summary>
    /// Represents a UAV (Unmanned Aerial Vehicle) mission source.
    /// </summary>
    private readonly IUavMissionSource _uav;

    /// <summary>
    /// This is a private, read-only variable that holds a collection of <see cref="ServerMissionItem"/> objects.
    /// </summary>
    private readonly IObservableCollection<ServerMissionItem> _missionItems = new ObservableCollectionExtended<ServerMissionItem>();

    /// <summary>
    /// Represents the current state of an ASV SDR mission.
    /// </summary>
    private AsvSdrMissionState _missionState;

    /// <summary>
    /// Private variable used to store the mission thread.
    /// </summary>
    private Thread? _missionThread;

    /// <summary>
    /// Gets or sets the CancellationTokenSource object used to cancel a mission.
    /// </summary>
    private CancellationTokenSource? _missionCancellationTokenSource;

    /// <summary>
    /// Represents the index of the current mission.
    /// </summary>
    private ushort _currentMissionIndex;

    /// <summary>
    /// Represents a logger object for logging information, warnings, and errors.
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Initializes a new instance of the MissionExecutor class.
    /// </summary>
    /// <param name="device">The ISdrServerDevice object used to interact with the server device.</param>
    /// <param name="actions">The IMissionActions object used to perform mission actions.</param>
    /// <param name="uav">The IUavMissionSource object used to source UAV missions.</param>
    public MissionExecutor(ISdrServerDevice device, IMissionActions actions, IUavMissionSource uav)
    {
        _device = device;
        _actions = actions;
        _uav = uav;
        device.Missions.Items.Bind(_missionItems).Subscribe().DisposeItWith(Disposable);
        Disposable.AddAction(() =>
        {
            _missionCancellationTokenSource?.Cancel();
            _missionCancellationTokenSource?.Dispose();
        });
    }

    /// <summary>
    /// Gets or sets the state of the mission.
    /// </summary>
    /// <value>
    /// The current state of the mission.
    /// </value>
    public AsvSdrMissionState MissionState
    {
        get => _missionState;
        private set
        {
            _missionState = value;
            _device.SdrEx.Base.Set(_ =>
            {
                _.MissionState = _missionState;
            });
        }
    }

    /// <summary>
    /// Starts the mission at the specified index.
    /// </summary>
    /// <param name="missionIndex">The index of the mission to start.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task representing the mission start operation with the result as <see cref="MavResult"/>.</returns>
    public Task<MavResult> StartMission(ushort missionIndex, CancellationToken cancel)
    {
        Logger.Trace($"<={nameof(StartMission)}(missionIndex:{missionIndex})");
        var item = _missionItems.FirstOrDefault(m => m.Seq == missionIndex);
        if (item == null)
        {
            Logger.Error($"=>{nameof(StartMission)}(missionIndex:{missionIndex})=>ERROR:NOT EXIST");
            _device.StatusText.Error($"Mission '{missionIndex}' not exist");
            return Task.FromResult(MavResult.MavResultFailed);
        }

        if (MissionState == AsvSdrMissionState.AsvSdrMissionStateProgress)
        {
            _device.StatusText.Error($"Mission already started");
            return Task.FromResult(MavResult.MavResultAccepted);
        }

        _currentMissionIndex = missionIndex;
        _missionThread = new Thread(MissionTick);
        _missionCancellationTokenSource?.Cancel();
        _missionCancellationTokenSource?.Dispose();
        _missionCancellationTokenSource = new CancellationTokenSource();
        _missionThread.Start();
        return Task.FromResult(MavResult.MavResultAccepted);
    }

    /// <summary>
    /// Stops the current mission.
    /// </summary>
    /// <param name="cancel">A cancellation token to stop the mission.</param>
    /// <returns>A task that represents the asynchronous operation to stop the mission.</returns>
    public Task<MavResult> StopMission(CancellationToken cancel)
    {
        if (MissionState == AsvSdrMissionState.AsvSdrMissionStateIdle)
        {
            _device.StatusText.Error($"Mission already stopped");
            return Task.FromResult(MavResult.MavResultAccepted);
        }

        try
        {
            _missionCancellationTokenSource?.Cancel();
            _missionCancellationTokenSource?.Dispose();
        }
        catch (Exception e)
        {
            Logger.Error("Error to stop mission: " + e.Message);
        }
        _missionThread = null;
        _missionCancellationTokenSource = null;
        MissionState = AsvSdrMissionState.AsvSdrMissionStateIdle;
        return Task.FromResult(MavResult.MavResultAccepted);
    }

    /// Executes a mission item.
    /// @param item The mission item to execute.
    /// @param cancel The cancellation token to allow cancellation of the execution.
    /// @return A task representing the asynchronous execution of the mission item.
    /// /
    private Task ExecuteMissionItem(ServerMissionItem item, CancellationToken cancel)
    {
        var command = (MavCmd)item.Command;
        switch (command)
        {
            case MavCmd.MavCmdAsvSdrSetMode:
                return SetMode(item, cancel);
            case MavCmd.MavCmdAsvSdrStartRecord:
                return StartRecord(item, cancel);
            case MavCmd.MavCmdAsvSdrStopRecord:
                return StopRecord(item, cancel);
            case MavCmd.MavCmdAsvSdrSetRecordTag:
                return SetRecordTag(item, cancel);
            case MavCmd.MavCmdAsvSdrWaitVehicleWaypoint:
                return WaitVehicleWaypoint(item, cancel);
            case MavCmd.MavCmdAsvSdrDelay:
                return SdrDelay(item, cancel);
            case MavCmd.MavCmdAsvSdrSystemControlAction:
            case MavCmd.MavCmdAsvSdrStartMission:
            case MavCmd.MavCmdAsvSdrStopMission:
            default:
                _device.StatusText.Info($"Unknown mission command: {command:G}. Skip it...");
                return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Delays the execution of a server mission item by a specified number of milliseconds.
    /// </summary>
    /// <param name="item">The server mission item to delay.</param>
    /// <param name="cancel">The cancellation token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous delay operation.</returns>
    /// <remarks>
    /// This method uses the <c>GetArgsForSdrDelay</c> method from the <c>AsvSdrHelper</c> class
    /// to retrieve the delay duration in milliseconds from the server mission item. It then logs
    /// the start and end of the delay operation, and updates the device status with the delay duration.
    /// The actual delay is achieved using the <c>Task.Delay</c> method, which suspends the execution
    /// of the delay task for the specified number of milliseconds, or until cancellation is requested.
    /// </remarks>
    /// <example>
    /// <code>
    /// ServerMissionItem item = new ServerMissionItem(); // Initialize the server mission item
    /// CancellationToken cancel = CancellationToken.None; // Create a cancellation token
    /// await SdrDelay(item, cancel); // Delay the execution of the server mission item
    /// </code>
    /// </example>
    private async Task SdrDelay(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrDelay(item, out var delayMs);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrDelay:G}(delayMs:{delayMs})");
        _device.StatusText.Info($"MISSION[{item.Seq}]: Delay {delayMs} ms");
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cs.Token);
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrDelay:G}(delayMs:{delayMs})");
    }

    /// <summary>
    /// This method waits for the vehicle to reach a specific waypoint in the mission.
    /// </summary>
    /// <param name="item">The mission item representing the waypoint to wait for.</param>
    /// <param name="cancel">A cancellation token to stop waiting for the waypoint.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task WaitVehicleWaypoint(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrWaitVehicleWaypoint(item, out var requestedIndex);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrWaitVehicleWaypoint:G}(requestedIndex:{requestedIndex})");
        var tcs = new TaskCompletionSource();
        using var c = cancel.Register(() => tcs.TrySetCanceled());
        using var c1 = cancel.Register(() =>
        {
            tcs.TrySetCanceled();
        });
        using var reachedSubscribe = _uav.ReachedWaypointIndex.Subscribe(inx =>
        {
            Logger.Trace($"Mission item [{item.Seq}]: {MavCmd.MavCmdAsvSdrWaitVehicleWaypoint:G}(requestedIndex:{requestedIndex}) recv reached waypoint index: {inx}");
            if (inx == requestedIndex)
            {
                tcs.TrySetResult();
            }
        });
        _device.StatusText.Info($"MISSION[{item.Seq}]: Wait UAV {requestedIndex} waypoint");
        await tcs.Task;
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrWaitVehicleWaypoint:G}(requestedIndex:{requestedIndex})");
    }

    /// <summary>
    /// Sets the tag of a record in the server mission item.
    /// </summary>
    /// <param name="item">The server mission item.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SetRecordTag(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrCurrentRecordSetTag(item, out var name, out var tagType, out var valueArray);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrSetRecordTag:G}({AsvSdrHelper.PrintTag(name,tagType, valueArray)})");
        _device.StatusText.Info($"MISSION[{item.Seq}]: Set tag {AsvSdrHelper.PrintTag(name,tagType, valueArray)}");
        var result = await _actions.CurrentRecordSetTag(tagType,name,valueArray, cs.Token).ConfigureAwait(false);
        CheckResult(result);
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrSetRecordTag:G}({AsvSdrHelper.PrintTag(name,tagType, valueArray)})");
    }


    /// <summary>
    /// Stops the recording of a mission item.
    /// </summary>
    /// <param name="item">The mission item to stop recording.</param>
    /// <param name="cancel">A cancellation token to stop the recording.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task StopRecord(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrStopRecord(item);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrStopRecord:G}");
        _device.StatusText.Info($"MISSION[{item.Seq}]: Stop record");
        var result = await _actions.StopRecord(cs.Token).ConfigureAwait(false);
        CheckResult(result);
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrStopRecord:G}");
    }

    /// <summary>
    /// Starts recording using the specified server mission item. </summary> <param name="item">
    /// The server mission item to start recording with. </param> <param name="cancel">
    /// The cancellation token to stop the recording process. </param> <returns>
    /// A task representing the asynchronous operation. </returns>
    /// /
    private async Task StartRecord(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrStartRecord(item, out var name);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrStartRecord:G}(name:{name})");
        _device.StatusText.Info($"MISSION[{item.Seq}]: Start record {name}");
        var result = await _actions.StartRecord(name, cs.Token);
        CheckResult(result);
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrStartRecord:G}(name:{name})");
    }

    /// <summary>
    /// Sets the mode of the server mission item.
    /// </summary>
    /// <param name="item">The server mission item to set mode for.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task SetMode(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrSetMode(item, out var mode, out var freq, out var rate, out var sendingThinningRatio, out var refPower);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrSetMode:G}(mode:{mode}, freq:{freq}, rate:{rate}, sendingThinningRatio:{sendingThinningRatio}, refPower:{refPower})");
        var result = await _actions.SetMode(mode,freq, rate,sendingThinningRatio,refPower, cs.Token).ConfigureAwait(false);
        CheckResult(result);
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrSetMode:G}(mode:{mode}, freq:{freq}, rate:{rate}, sendingThinningRatio:{sendingThinningRatio}, refPower:{refPower})");
    }

    /// <summary>
    /// Checks the result of a MAVLink operation.
    /// </summary>
    /// <param name="result">The result of the MAVLink operation.</param>
    /// <exception cref="Exception">Thrown if the result is different from MavResult.MavResultAccepted.</exception>
    private static void CheckResult(MavResult result)
    {
        if (result != MavResult.MavResultAccepted)
        {
            throw new Exception($"Set tag failed. Result: {result}");
        }
    }

    /// <summary>
    /// Method that executes the missions for the AsvSdr device.
    /// </summary>
    private async void MissionTick()
    {
        MissionState = AsvSdrMissionState.AsvSdrMissionStateProgress;
        _device.StatusText.Error($"Mission started");
        try
        {
            while (_missionCancellationTokenSource is { IsCancellationRequested: false })
            {
                var item = _missionItems.FirstOrDefault(m => m.Seq == _currentMissionIndex);
                if (item == null)
                {
                    _device.StatusText.Error($"Mission '{_currentMissionIndex}' not exist");
                    MissionState = AsvSdrMissionState.AsvSdrMissionStateError;
                    return;
                }
                _device.Missions.Current.OnNext(_currentMissionIndex);
                await ExecuteMissionItem(item,_missionCancellationTokenSource.Token);
                _device.Missions.Reached.OnNext(_currentMissionIndex);
                _currentMissionIndex++;
                if (_missionItems.Any(m=>m.Seq == _currentMissionIndex) == false)
                {
                    _device.StatusText.Info($"Mission completed");
                    MissionState = AsvSdrMissionState.AsvSdrMissionStateIdle;
                    return;
                }
            }
                
        }
        catch (Exception e)
        {
            Logger.Error("Error to execute mission: " + e.Message);
            _device.StatusText.Error($"Mission error: {e.Message}");
            MissionState = AsvSdrMissionState.AsvSdrMissionStateError;
        }
    }
}