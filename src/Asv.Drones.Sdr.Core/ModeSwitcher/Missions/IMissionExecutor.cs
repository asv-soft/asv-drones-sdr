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

public interface IMissionExecutor
{
    AsvSdrMissionState MissionState { get; }
}

public interface IMissionActions
{
    Task<MavResult> SetMode(AsvSdrCustomMode mode, ulong frequencyHz, float recordRate,uint sendingThinningRatio, float refPower, CancellationToken cancel);
    Task<MavResult> StopRecord(CancellationToken token);
    Task<MavResult> StartRecord(string recordName, CancellationToken cancel);
    Task<MavResult> CurrentRecordSetTag(AsvSdrRecordTagType type, string name, byte[] value, CancellationToken cancel);
}

public class MissionExecutor : DisposableOnceWithCancel, IMissionExecutor
{
    private readonly ISdrServerDevice _device;
    private readonly IMissionActions _actions;
    private readonly IUavMissionSource _uav;
    private readonly IObservableCollection<ServerMissionItem> _missionItems = new ObservableCollectionExtended<ServerMissionItem>();
    private AsvSdrMissionState _missionState;
    private Thread? _missionThread;
    private CancellationTokenSource? _missionCancellationTokenSource;
    private ushort _currentMissionIndex;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

    private async Task SdrDelay(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrDelay(item, out var delayMs);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrDelay:G}(delayMs:{delayMs})");
        _device.StatusText.Info($"MISSION[{item.Seq}]: Delay {delayMs} ms");
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cs.Token);
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrDelay:G}(delayMs:{delayMs})");
    }

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

    private async Task SetMode(ServerMissionItem item, CancellationToken cancel)
    {
        using var cs = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        AsvSdrHelper.GetArgsForSdrSetMode(item, out var mode, out var freq, out var rate, out var sendingThinningRatio, out var refPower);
        Logger.Debug($"Mission item [{item.Seq}]: Begin {MavCmd.MavCmdAsvSdrSetMode:G}(mode:{mode}, freq:{freq}, rate:{rate}, sendingThinningRatio:{sendingThinningRatio}, refPower:{refPower})");
        var result = await _actions.SetMode(mode,freq, rate,sendingThinningRatio,refPower, cs.Token).ConfigureAwait(false);
        CheckResult(result);
        Logger.Debug($"Mission item [{item.Seq}]: End {MavCmd.MavCmdAsvSdrSetMode:G}(mode:{mode}, freq:{freq}, rate:{rate}, sendingThinningRatio:{sendingThinningRatio}, refPower:{refPower})");
    }
    private static void CheckResult(MavResult result)
    {
        if (result != MavResult.MavResultAccepted)
        {
            throw new Exception($"Set tag failed. Result: {result}");
        }
    }

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