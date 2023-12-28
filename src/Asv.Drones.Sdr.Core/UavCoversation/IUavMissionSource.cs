using Asv.Common;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Interface for providing UAV mission source data.
/// </summary>
public interface IUavMissionSource
{
    /// <summary>
    /// Gets the value of the ReachedWaypointIndex property.
    /// </summary>
    /// <returns>The value of the ReachedWaypointIndex property.</returns>
    IRxValue<ushort> ReachedWaypointIndex { get; }
}