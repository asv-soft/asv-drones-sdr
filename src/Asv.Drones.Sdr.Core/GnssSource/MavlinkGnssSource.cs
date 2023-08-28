using System.ComponentModel.Composition;
using System.Reactive.Linq;
using Asv.Cfg;
using Asv.Common;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;
using NLog;

namespace Asv.Drones.Sdr.Core;

public class MavlinkGnssSourceConfig
{
    public int DeviceTimeoutMs { get; set; } = 10_000;
    public byte GnssSystemId { get; set; } = 1;
    public byte GnssComponentId { get; set; } = 1;
    public ushort ReqMessageRate { get; set; } = 1;
}

[Export(typeof(IGnssSource))]
[Export(typeof(ITimeService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class MavlinkGnssSource : DisposableOnceWithCancel, IGnssSource,ITimeService
{
    private readonly ISdrMavlinkService _svc;
    private readonly RxValue<GpsRawIntPayload?> _gnss;
    private readonly MavlinkGnssSourceConfig _config;
    private readonly RxValue<AttitudePayload?> _attitude;
    private readonly RxValue<GlobalPositionIntPayload?> _position;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly LinkIndicator _link = new(3);
    private bool _needToRequestAgain;
    private int _isRequestInfoIsInProgressOrAlreadySuccess;

    [ImportingConstructor]
    public MavlinkGnssSource(ISdrMavlinkService svc,IConfiguration config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        _svc = svc ?? throw new ArgumentNullException(nameof(svc)); 
        
        _config = config.Get<MavlinkGnssSourceConfig>();
        var pkts = svc.Router.FilterVehicle(_config.GnssSystemId, _config.GnssComponentId).Publish().RefCount();
        _position = new RxValue<GlobalPositionIntPayload?>().DisposeItWith(Disposable);
        pkts.Filter<GlobalPositionIntPacket>().Select(_=>_.Payload).Subscribe(_position).DisposeItWith(Disposable);
        _gnss = new RxValue<GpsRawIntPayload?>().DisposeItWith(Disposable);
        pkts.Filter<GpsRawIntPacket>().Select(_=>_.Payload).Subscribe(_gnss).DisposeItWith(Disposable);
        _attitude = new RxValue<AttitudePayload?>().DisposeItWith(Disposable);
        pkts.Filter<AttitudePacket>().Select(_=>_.Payload).Subscribe(_attitude).DisposeItWith(Disposable);
        pkts.Filter<HeartbeatPacket>().Where(_=>_.Payload.Autopilot == MavAutopilot.MavAutopilotArdupilotmega).Subscribe(_=>
        {
            _link.Upgrade();
        }).DisposeItWith(Disposable);
        _link.DisposeItWith(Disposable);
        _link.DistinctUntilChanged().Where(_ => _ == LinkState.Disconnected).Subscribe(_ => _needToRequestAgain = true).DisposeItWith(Disposable);
        _link.DistinctUntilChanged().Where(_ => _needToRequestAgain).Where(_ => _ == LinkState.Connected)
            // only one time
            .Subscribe(_ => Task.Factory.StartNew(TryToRequestData, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach)).DisposeItWith(Disposable);

    }

    private async void TryToRequestData()
    {
        if (Interlocked.CompareExchange(ref _isRequestInfoIsInProgressOrAlreadySuccess, 1, 0) == 1) return;
        try
        {
                
            await _svc.Router.Send(new RequestDataStreamPacket()
            {
                ComponentId = _svc.Server.Identity.ComponentId,
                SystemId = _svc.Server.Identity.SystemId,
                Sequence = _svc.Server.Seq.GetNextSequenceNumber(),
                Payload =
                {
                    ReqMessageRate = _config.ReqMessageRate,
                    TargetSystem = _config.GnssSystemId,
                    TargetComponent = _config.GnssComponentId,
                    StartStop = 1,
                    ReqStreamId = (int)MavDataStream.MavDataStreamAll
                }
            }, DisposeCancel);

                
        }
        catch (Exception e)
        {
            if (Disposable.IsDisposed) return; // no need to replay since the instance was already disposed
            Logger.Error($"Error to read all vehicle info:{e.Message}");
            Observable.Timer(TimeSpan.FromMilliseconds(5))
                .Subscribe(_ => TryToRequestData()).DisposeItWith(Disposable);
        }
        finally
        {
            Interlocked.Exchange(ref _isRequestInfoIsInProgressOrAlreadySuccess, 0);
        }
    }

    public IRxValue<GpsRawIntPayload?> Gnss => _gnss;

    public IRxValue<GlobalPositionIntPayload?> Position => _position;

    public IRxValue<AttitudePayload?> Attitude => _attitude;
    public void SetCorrection(long correctionIn100NanosecondsTicks)
    {
        throw new NotImplementedException();
    }

    public DateTime Now => _gnss.Value == null ? DateTime.Now : MavlinkTypesHelper.FromUnixTimeUs(_gnss.Value.TimeUsec);
}

