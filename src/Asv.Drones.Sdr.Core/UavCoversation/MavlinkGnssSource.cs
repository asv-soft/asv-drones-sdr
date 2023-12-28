using System.ComponentModel.Composition;
using System.Reactive.Linq;
using Asv.Cfg;
using Asv.Common;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;
using Asv.Mavlink.V2.Minimal;
using NLog;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents the configuration for a MAVLink GNSS source.
/// </summary>
public class MavlinkGnssSourceConfig
{
    /// <summary>
    /// Gets or sets the timeout value in milliseconds for the device.
    /// </summary>
    /// <remarks>
    /// This property represents the maximum time the device will wait for a response before considering the operation as timed-out.
    /// The default value is 10,000 milliseconds (10 seconds).
    /// </remarks>
    public int DeviceTimeoutMs { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the GNSS system ID.
    /// </summary>
    /// <value>
    /// The GNSS system ID is a byte indicating the identification of the GNSS system.
    /// </value>
    public byte GnssSystemId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the GNSS component ID.
    /// </summary>
    /// <remarks>
    /// The GNSS component ID is a byte value used to identify the specific GNSS component.
    /// This property can be used to customize or identify different GNSS components in a system.
    /// </remarks>
    public byte GnssComponentId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the request message rate.
    /// </summary>
    /// <value>
    /// The request message rate in messages per second.
    /// </value>
    public ushort ReqMessageRate { get; set; } = 5;
}

/// <summary>
/// Represents a class that provides GNSS data from Mavlink packets. </summary> <remarks>
/// This class is used to retrieve GNSS data from Mavlink packets and provide access to various properties related to GNSS such as position, attitude, and waypoint index. </remarks>
/// /
[Export(typeof(IGnssSource))]
[Export(typeof(IUavMissionSource))]
[Export(typeof(ITimeService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class MavlinkGnssSource : DisposableOnceWithCancel, IGnssSource,ITimeService,IUavMissionSource
{
    /// <summary>
    /// Represents a private readonly instance of the <see cref="ISdrMavlinkService"/> interface.
    /// </summary>
    private readonly ISdrMavlinkService _svc;

    /// <summary>
    /// The private readonly RxValue object representing the GNSS (Global Navigation Satellite System) data.
    /// </summary>
    /// <remarks>
    /// This object holds the GpsRawIntPayload data type, which encapsulates the raw GNSS information obtained from the navigation system.
    /// </remarks>
    private readonly RxValue<GpsRawIntPayload?> _gnss;

    /// <summary>
    /// Represents the configuration settings for a Mavlink GNSS source.
    /// </summary>
    private readonly MavlinkGnssSourceConfig _config;

    /// <summary>
    /// Represents a reactive value for attitude payload.
    /// </summary>
    /// <remarks>
    /// This value holds an optional AttitudePayload object, which represents the attitude data.
    /// Use this value to get or set the attitude payload.
    /// </remarks>
    private readonly RxValue<AttitudePayload?> _attitude;

    /// <summary>
    /// The current position value.
    /// </summary>
    /// <remarks>
    /// This variable is an instance of <see cref="RxValue{T}"/> with a generic type argument of <see cref="GlobalPositionIntPayload?"/>.
    /// It represents the global position of an object in an integer format.
    /// </remarks>
    private readonly RxValue<GlobalPositionIntPayload?> _position;

    /// <summary>
    /// Represents a logging utility for the current class.
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Represents a link indicator.
    /// </summary>
    private readonly LinkIndicator _link = new(3);

    /// <summary>
    /// Represents a flag indicating whether a request needs to be made again.
    /// </summary>
    /// <remarks>
    /// By default, the value of this flag is <c>false</c>. It is used to track whether
    /// a request needs to be made again based on certain conditions. When this flag is
    /// <c>true</c>, it indicates that a new request should be sent.
    /// </remarks>
    private bool _needToRequestAgain;

    /// <summary>
    /// The variable _isRequestInfoIsInProgressOrAlreadySuccess is used to indicate whether a request for information is in progress or has already been successful.
    /// A value of 0 indicates that no request is currently in progress.
    /// A value of 1 indicates that a request is currently in progress.
    /// A value of 2 indicates that a request has already been successfully completed.
    /// </summary>
    private int _isRequestInfoIsInProgressOrAlreadySuccess;

    /// Represents the correction value in 100 nanosecond ticks.
    /// /
    private long _correctionIn100NanosecondTicks = 0;

    /// <summary>
    /// The current index of the reached waypoint.
    /// </summary>
    private readonly RxValue<ushort> _reachedWaypointIndex;

    /// <summary>
    /// Initializes a new instance of the MavlinkGnssSource class.
    /// </summary>
    /// <param name="svc">The ISdrMavlinkService object.</param>
    /// <param name="config">The IConfiguration object.</param>
    /// <exception cref="ArgumentNullException">Thrown when svc or config is null.</exception>
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
        _gnss.Subscribe(v =>
        {
            /*if (v != null)
                SetCorrection((MavlinkTypesHelper.FromUnixTimeUs(v.TimeUsec) - DateTime.Now).Ticks);*/
        }).DisposeItWith(Disposable);
        _reachedWaypointIndex = new RxValue<ushort>().DisposeItWith(Disposable);
        pkts.Filter<MissionItemReachedPacket>().Select(p=>p.Payload.Seq).Subscribe(_reachedWaypointIndex).DisposeItWith(Disposable);
    }

    /// <summary>
    /// Tries to request data from the service asynchronously.
    /// </summary>
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

    /// <summary>
    /// Gets the GPS Raw Integer Payload value.
    /// </summary>
    /// <remarks>
    /// The GNSS property represents the GPS Raw Integer Payload value.
    /// </remarks>
    /// <returns>An <see cref="IRxValue{T}"/> object containing the GPS Raw Integer Payload value.</returns>
    public IRxValue<GpsRawIntPayload?> Gnss => _gnss;

    /// <summary>
    /// Gets the observable property for the Position value.
    /// </summary>
    /// <value>
    /// The observable property for the Position value.
    /// </value>
    public IRxValue<GlobalPositionIntPayload?> Position => _position;

    /// <summary>
    /// Gets the attitude property.
    /// </summary>
    /// <value>
    /// The attitude property as an <see cref="IRxValue{T}"/> of type <see cref="AttitudePayload"/> that can be observed and updated.
    /// </value>
    public IRxValue<AttitudePayload?> Attitude => _attitude;
    public void SetCorrection(long correctionIn100NanosecondsTicks)
    {
        var origin = Interlocked.Exchange(ref _correctionIn100NanosecondTicks, correctionIn100NanosecondsTicks);
        if (origin != correctionIn100NanosecondsTicks)
        {
            Logger.Trace($"Correction changed from {origin} to {correctionIn100NanosecondsTicks} ns");
        }
    }

    /// <summary>
    /// Gets the current date and time adjusted by the correction value. </summary> <remarks>
    /// The Now property returns the current date and time adjusted by the correction
    /// value stored in the _correctionIn100NanosecondTicks field. </remarks>
    /// <value>
    /// A DateTime object that represents the current date and time adjusted by the
    /// correction value. </value>
    /// /
    public DateTime Now => DateTime.Now + TimeSpan.FromTicks(Interlocked.Read(ref _correctionIn100NanosecondTicks));

    /// <summary>
    /// Gets the current index of the reached waypoint.
    /// </summary>
    /// <returns>The current index of the reached waypoint.</returns>
    public IRxValue<ushort> ReachedWaypointIndex => _reachedWaypointIndex;
}

