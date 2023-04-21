using Asv.Mavlink;

namespace Asv.Drones.Sdr;

public interface ISdrMavlinkService
{
    IMavlinkRouter Router { get; }
    ISdrServerDevice Server { get; }
}