using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reactive.Linq;
using Asv.Cfg;
using Asv.Common;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents the GpWorkMode class.
/// </summary>
[ExportMode(AsvSdrCustomMode.AsvSdrCustomModeGp, AsvSdrCustomModeFlag.AsvSdrCustomModeFlagGp)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class GpWorkMode : WorkModeBase<IAnalyzerGp, AsvSdrRecordDataGpPayload>
{
    /// <summary>
    /// Represents the total amount with precision up to two decimal places.
    /// </summary>
    private float _totalAm90;

    /// <summary>
    /// Represents the total amount (in float) for a particular item with a value of 150.
    /// </summary>
    private float _totalAm150;

    /// <summary>
    /// Represents a disposable object that allows cancellation of a timer for OSD telemetry.
    /// </summary>
    private IDisposable _osdTelemTimerDisposable;

    /// <summary>
    /// Represents an instance of the SdrMavlinkService.
    /// </summary>
    /// <remarks>
    /// The SdrMavlinkService provides functionality for communicating with MAVLink devices and sending/receiving MAVLink messages.
    /// This variable is readonly, meaning it cannot be reassigned after initialization.
    /// </remarks>
    private readonly ISdrMavlinkService _svc;

    /// <summary>
    /// Initializes a new instance of the GpWorkMode class with the specified dependencies.
    /// </summary>
    /// <param name="svc">The ISdrMavlinkService instance to use.</param>
    /// <param name="gnssSource">The IGnssSource instance to use.</param>
    /// <param name="time">The ITimeService instance to use.</param>
    /// <param name="configuration">The IConfiguration instance to use.</param>
    /// <param name="container">The CompositionContainer instance to use.</param>
    [ImportingConstructor]
    public GpWorkMode(ISdrMavlinkService svc, IGnssSource gnssSource,ITimeService time, IConfiguration configuration, CompositionContainer container) 
        : base(AsvSdrCustomMode.AsvSdrCustomModeGp , gnssSource,time, configuration, container)
    {
        _svc = svc;
        
        UpdateOsdTelemetryTimer(_svc.Server.Params[MavlinkDefaultParams.OsdTelemetryRate]);
        
        _svc.Server.Params.OnUpdated.Subscribe(_ =>
        {
            if (_.Metadata.Name == MavlinkDefaultParams.OsdTelemetryRate.Name)
            {
                UpdateOsdTelemetryTimer(_.NewValue);
            }
        }).DisposeItWith(Disposable);
        
        Disposable.AddAction(() => _osdTelemTimerDisposable?.Dispose());
    }

    /// <summary>
    /// Updates OSD (On-screen Display) telemetry timer with the specified rate.
    /// </summary>
    /// <param name="rate">The rate (in seconds) at which the telemetry timer should be updated. Setting rate to a value greater than 0 will enable the timer, while setting it to 0 or a negative value will disable the timer.</param>
    private void UpdateOsdTelemetryTimer(int rate)
    {
        _osdTelemTimerDisposable?.Dispose();

        if (rate > 0)
        {
            _osdTelemTimerDisposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(rate))
                .Subscribe(_ =>
                {
                    _svc.Server.StatusText.Info($"Total DDM: {_totalAm90 - _totalAm150}");
                    _svc.Server.StatusText.Info($"Total SDM: {_totalAm90 + _totalAm150}");
                });
        }
    }

    /// <summary>
    /// Fills the given payload with data from various sources.
    /// </summary>
    /// <param name="payload">The payload to fill.</param>
    /// <param name="record">The record GUID.</param>
    /// <param name="dataIndex">The data index.</param>
    /// <param name="gnss">The GNSS data. Can be null.</param>
    /// <param name="attitude">The attitude data. Can be null.</param>
    /// <param name="position">The global position data. Can be null.</param>
    protected override void InternalFill(AsvSdrRecordDataGpPayload payload, Guid record, uint dataIndex,
        GpsRawIntPayload? gnss, AttitudePayload? attitude,
        GlobalPositionIntPayload? position)
    {
        payload.DataIndex = dataIndex;
        record.TryWriteBytes(payload.RecordGuid);
        // GNSS
        if (gnss != null)
        {
            payload.TimeUnixUsec = MavlinkTypesHelper.ToUnixTimeUs(TimeService.Now);
            payload.GnssFixType = gnss.FixType;
            payload.GnssLat = gnss.Lat;
            payload.GnssLon = gnss.Lon;
            payload.GnssAlt = gnss.Alt;
            payload.GnssEph = gnss.Eph;
            payload.GnssEpv = gnss.Epv;
            payload.GnssVel = gnss.Vel;
            payload.GnssSatellitesVisible = gnss.SatellitesVisible;
            payload.GnssAltEllipsoid = gnss.AltEllipsoid;
            payload.GnssHAcc = gnss.HAcc;
            payload.GnssVAcc = gnss.VAcc;
            payload.GnssVelAcc = gnss.VelAcc;
        }
        else
        {
            payload.GnssFixType = GpsFixType.GpsFixTypeNoGps;
            payload.GnssLat = 0;
            payload.GnssLon = 0;
            payload.GnssAlt = 0;
            payload.GnssEph = 0;
            payload.GnssEpv = 0;
            payload.GnssVel = 0;
            payload.GnssSatellitesVisible = 0;
            payload.GnssAltEllipsoid = 0;
            payload.GnssHAcc = 0;
            payload.GnssVAcc = 0;
            payload.GnssVelAcc = 0;
        }
        // Global position
        if (position != null)
        {
            payload.Lat = position.Lat;
            payload.Lon = position.Lon;
            payload.Alt = position.Alt;
            payload.RelativeAlt = position.RelativeAlt;
            payload.Vx = position.Vx;
            payload.Vy = position.Vy;
            payload.Vz = position.Vz;
            payload.Hdg = position.Hdg;
        }
        else
        {
            payload.Lat = 0;
            payload.Lon = 0;
            payload.Alt = 0;
            payload.RelativeAlt = 0;
            payload.Vx = 0;
            payload.Vy = 0;
            payload.Vz = 0;
            payload.Hdg = 0;
        }
        // Attitude
        if (attitude != null)
        {
            payload.Roll = attitude.Roll;
            payload.Pitch = attitude.Pitch;
            payload.Yaw = attitude.Yaw;
        }
        else
        {
            payload.Roll = 0;
            payload.Pitch = 0;
            payload.Yaw = 0;
        }
        // Measure
        Analyzer.Fill(payload);
        
        _totalAm90 = payload.TotalAm90;
        _totalAm150 = payload.TotalAm150;
    }
}