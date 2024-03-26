using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Virtual;




public static class SimulationParams
{
    /// <summary>
    /// Represents the group name.
    /// </summary>
    public const string Group = "SIM";

    /// <summary>
    /// The category of a common element.
    /// </summary>
    public const string Category = "Simulation";
    
    
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimDdmSd = new MavParamTypeMetadata("SIM_DDM_SD", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "DDM standard deviation",
        LongDesc = "Standard deviation of DDM",
        Units = null,
        RebootRequired = false,
        MinValue = -100.0f,
        MaxValue = +100.0f,
        DefaultValue = 0.001f,
        Increment = 0.001f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimLlzLat = new MavParamTypeMetadata("SIM_LLZ_LAT", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "LLZ latitude",
        LongDesc = "Latitude of LLZ",
        Units = null,
        RebootRequired = false,
        MinValue = -90.0f,
        MaxValue = +90.0f,
        DefaultValue = -22.498889f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimLlzLon = new MavParamTypeMetadata("SIM_LLZ_LON", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "LLZ longitude",
        LongDesc = "Longitude of LLZ",
        Units = null,
        RebootRequired = false,
        MinValue = -180.0f,
        MaxValue = +180.0f,
        DefaultValue = -68.920680f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimLlzAlt = new MavParamTypeMetadata("SIM_LLZ_ALT", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "LLZ altitude",
        LongDesc = "Altitude of LLZ",
        Units = null,
        RebootRequired = false,
        MinValue = -10_000f,
        MaxValue = +10_000f,
        DefaultValue = 2400f,
        Increment = 1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimGpLat = new MavParamTypeMetadata("SIM_GP_LAT", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "GP latitude",
        LongDesc = "Latitude of GP",
        Units = null,
        RebootRequired = false,
        MinValue = -90.0f,
        MaxValue = +90.0f,
        DefaultValue = -22.499924f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimGpLon = new MavParamTypeMetadata("SIM_GP_LON", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "GP longitude",
        LongDesc = "Longitude of GP",
        Units = null,
        RebootRequired = false,
        MinValue = -180.0f,
        MaxValue = +180.0f,
        DefaultValue = -68.892426f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimGpAlt = new MavParamTypeMetadata("SIM_GP_ALT", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "GP altitude",
        LongDesc = "Altitude of GP",
        Units = null,
        RebootRequired = false,
        MinValue = -10_000f,
        MaxValue = +10_000f,
        DefaultValue = 2400f,
        Increment = 1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimTrhLat = new MavParamTypeMetadata("SIM_TRH_LAT", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Threshold latitude",
        LongDesc = "Latitude of Threshold",
        Units = null,
        RebootRequired = false,
        MinValue = -90.0f,
        MaxValue = +90.0f,
        DefaultValue = -22.499924f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimTrhLon = new MavParamTypeMetadata("SIM_TRH_LON", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Threshold longitude",
        LongDesc = "Longitude of Threshold",
        Units = null,
        RebootRequired = false,
        MinValue = -180.0f,
        MaxValue = +180.0f,
        DefaultValue = -68.892426f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimTrhAlt = new MavParamTypeMetadata("SIM_TRH_ALT", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Threshold altitude",
        LongDesc = "Altitude of Threshold",
        Units = null,
        RebootRequired = false,
        MinValue = -10_000f,
        MaxValue = +10_000f,
        DefaultValue = 2400f,
        Increment = 1f,
    };
   
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimGpAngle = new MavParamTypeMetadata("SIM_GP_ANG", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Glide angle",
        LongDesc = "Angle of glide path",
        Units = null,
        RebootRequired = false,
        MinValue = 0f,
        MaxValue = +5f,
        DefaultValue = 3f,
        Increment = 0.1f,
    };
    
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimGpUpperWidthMin = new MavParamTypeMetadata("SIM_GP_UP_WID", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Upper width",
        LongDesc = "Glide path upper width",
        Units = null,
        RebootRequired = false,
        MinValue = 0f,
        MaxValue = 60f,
        DefaultValue = 21.6f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimGpLowerWidthMin = new MavParamTypeMetadata("SIM_GP_LOW_WID", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "Lower width",
        LongDesc = "Glide path lower width",
        Units = null,
        RebootRequired = false,
        MinValue = 0f,
        MaxValue = 60f,
        DefaultValue = 21.6f,
        Increment = 0.1f,
    };
  
}