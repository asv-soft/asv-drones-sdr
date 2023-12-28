using Asv.Common;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents a source of GPS, position, and attitude data.
/// </summary>
public interface IGnssSource
{
    /// Gets the GNSS property.
    /// The property provides access to the GNSS (Global Navigation Satellite System) data,
    /// including the GpsRawIntPayload. The value is of type IRxValue<GpsRawIntPayload?>.
    /// @return The GNSS property.
    /// /
    IRxValue<GpsRawIntPayload?> Gnss { get; }

    /// <summary>Gets the current position value.</summary>
    /// <value>An interface that represents a reactive value for the position, which can be observed and modified.</value>
    IRxValue<GlobalPositionIntPayload?> Position { get;  }

    /// <summary>
    /// Gets the attitude of the object.
    /// </summary>
    /// <remarks>
    /// The attitude represents the orientation of the object in space. It provides information about the object's pitch, roll, and yaw.
    /// </remarks>
    /// <returns>
    /// An <see cref="IRxValue{T}"/> instance of <see cref="AttitudePayload"/> that encapsulates the attitude information.
    /// </returns>
    IRxValue<AttitudePayload?> Attitude { get; }
}