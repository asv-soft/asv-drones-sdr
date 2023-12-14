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

[ExportMode(AsvSdrCustomMode.AsvSdrCustomModeGp, AsvSdrCustomModeFlag.AsvSdrCustomModeFlagGp)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class GpWorkMode : WorkModeBase<IAnalyzerGp, AsvSdrRecordDataGpPayload>
{
    private float _totalAm90;
    private float _totalAm150;

    private IDisposable _osdTelemTimerDisposable;
    private readonly ISdrMavlinkService _svc;

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