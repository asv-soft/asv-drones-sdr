using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using Asv.Cfg;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

[ExportMode(AsvSdrCustomMode.AsvSdrCustomModeVor, AsvSdrCustomModeFlag.AsvSdrCustomModeFlagVor)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VorWorkMode : WorkModeBase<IAnalyzerVor, AsvSdrRecordDataVorPayload>
{
    [ImportingConstructor]
    public VorWorkMode(IGnssSource gnssSource, IConfiguration configuration, CompositionContainer container) 
        : base(AsvSdrCustomMode.AsvSdrCustomModeGp , gnssSource, configuration, container)
    {
    }
    protected override void InternalFill(AsvSdrRecordDataVorPayload payload, Guid record, uint dataIndex,
        GpsRawIntPayload gnss, AttitudePayload attitude,
        GlobalPositionIntPayload position)
    {
        payload.DataIndex = dataIndex;
        record.TryWriteBytes(payload.RecordGuid);
        // GNSS
        payload.GnssFixType = gnss.FixType;
        payload.GnssLat = gnss.Lat;
        payload.GnssLon = gnss.Lon;
        payload.GnssAlt = gnss.Alt;
        payload.GnssEph = gnss.Eph;
        payload.GnssEpv = gnss.Epv;
        payload.GnssVel = gnss.Vel;
        payload.GnssVel = gnss.Vel;
        payload.GnssSatellitesVisible = gnss.SatellitesVisible;
        payload.GnssAltEllipsoid = gnss.AltEllipsoid;
        payload.GnssHAcc = gnss.HAcc;
        payload.GnssVAcc = gnss.VAcc;
        payload.GnssVelAcc = gnss.VelAcc;
        // Global position
        payload.Lat = position.Lat;
        payload.Lon = position.Lon;
        payload.Alt = position.Alt;
        payload.RelativeAlt = position.RelativeAlt;
        payload.Vx = position.Vx;
        payload.Vy = position.Vy;
        payload.Vz = position.Vz;
        payload.Hdg = position.Hdg;
        // Attitude
        payload.Roll = attitude.Roll;
        payload.Pitch = attitude.Pitch;
        payload.Yaw = attitude.Yaw;
        // Measure
        Analyzer.Fill(payload);
    }   
}