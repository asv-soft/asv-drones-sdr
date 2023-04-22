using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core.Mavlink;

public interface ISdrMavlinkService
{
    IMavlinkRouter Router { get; }
    ISdrServerDevice Server { get; }
}