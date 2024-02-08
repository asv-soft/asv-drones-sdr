using System.ComponentModel.Composition;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core.Mavlink;

/// <summary>
/// MavlinkDefaultParams class contains default parameters for Mavlink.
/// </summary>
public static class SdrMavlinkDefaultParams
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
    /// 
    /// </summary>
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SystemId = new MavParamTypeMetadata("SYS_ID", MavParamType.MavParamTypeInt32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "System ID",
        LongDesc = "System identification for payload",
        Units = null,
        RebootRequired = false,
        MinValue = Int32.MinValue,
        MaxValue = Int32.MaxValue,
        DefaultValue = 1,
        Increment = 1,
    };
    
    /// <summary>
    /// 
    /// </summary>
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata ComponentId = new MavParamTypeMetadata("COM_ID", MavParamType.MavParamTypeInt32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Component ID",
        LongDesc = "Component identification for payload",
        Units = null,
        RebootRequired = false,
        MinValue = Int32.MinValue,
        MaxValue = Int32.MaxValue,
        DefaultValue = 13,
        Increment = 1,
    };

    /// <summary>
    /// Metadata for the BoardSerialNumber parameter.
    /// </summary>
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata BoardSerialNumber =  new MavParamTypeMetadata("BRD_SERIAL_NUM", MavParamType.MavParamTypeInt32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Serial number",
        LongDesc = "Board serial number",
        Units = null,
        RebootRequired = false,
        MinValue = Int32.MinValue,
        MaxValue = Int32.MaxValue,
        DefaultValue = 0,
        Increment = 1,
    };

    /// <summary>
    /// Represents the telemetry rate for the OSD (On-Screen Display).
    /// </summary>
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata OsdTelemetryRate = new MavParamTypeMetadata("OSD_TEL_RATE", MavParamType.MavParamTypeInt32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "OSD telemetry rate",
        LongDesc = "The frequency at which the display is updated to display telemetry in OSD.",
        Units = "Seconds",
        RebootRequired = false,
        MinValue = 0,
        MaxValue = Int32.MaxValue,
        DefaultValue = 0,
        Increment = 1,
    };
}