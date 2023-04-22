using Asv.Common;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

public interface IGnssSource
{
    IRxValue<GpsRawIntPayload> Gnss { get; }
    IRxValue<GlobalPositionIntPayload> Position { get;  }
    IRxValue<AttitudePayload> Attitude { get; }
}