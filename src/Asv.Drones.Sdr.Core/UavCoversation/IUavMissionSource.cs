using Asv.Common;

namespace Asv.Drones.Sdr.Core;

public interface IUavMissionSource
{
    IRxValue<ushort> ReachedWaypointIndex { get; }
}