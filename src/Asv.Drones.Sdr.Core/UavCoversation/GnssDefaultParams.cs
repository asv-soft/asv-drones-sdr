using System.ComponentModel.Composition;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core.Mavlink;

public class GnssMavlinkDefaultParams
{
    /// <summary>
    /// Represents the group name.
    /// </summary>
    public const string Group = "SDR";

    /// <summary>
    /// The category of a common element.
    /// </summary>
    public const string Category = "Common";
    
    /// <summary>
    /// facilitates the precise identification of the GNSS system responsible for receiving and recording coordinate data during data acquisition.
    /// </summary>
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata GnssSystemId = new MavParamTypeMetadata("GNSS_SYS_ID", MavParamType.MavParamTypeInt32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "GNSS System ID",
        LongDesc = "System identification for GNSS",
        Units = null,
        RebootRequired = false,
        MinValue = Int32.MinValue,
        MaxValue = Int32.MaxValue,
        DefaultValue = 1,
        Increment = 1,
    };
    
    /// <summary>
    /// to identify the UAV from which the coordinates are listened to when recording data
    /// </summary>
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata GnssComponentId = new MavParamTypeMetadata("GNSS_COM_ID", MavParamType.MavParamTypeInt32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "GNSS Component ID",
        LongDesc = "Component identification for GNSS",
        Units = null,
        RebootRequired = false,
        MinValue = Int32.MinValue,
        MaxValue = Int32.MaxValue,
        DefaultValue = 1,
        Increment = 1,
    };
}