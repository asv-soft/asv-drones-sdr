using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core.Mavlink;

/// <summary>
/// Interface for a SDR MAVLink Service.
/// </summary>
public interface ISdrMavlinkService
{
    /// <summary>
    /// Gets the Mavlink router.
    /// </summary>
    /// <returns>
    /// The Mavlink router.
    /// </returns>
    IMavlinkRouter Router { get; }

    /// <summary>
    /// Gets the SDR server device.
    /// </summary>
    ISdrServerDevice Server { get; }
}