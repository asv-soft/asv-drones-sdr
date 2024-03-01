using System.ComponentModel.Composition;
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
    public static IMavParamTypeMetadata SimSdm = new MavParamTypeMetadata("SIM_SDM", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "SDM value",
        LongDesc = "Value of SDM",
        Units = null,
        RebootRequired = false,
        MinValue = -100.0f,
        MaxValue = +100.0f,
        DefaultValue = 40.0f,
        Increment = 0.1f,
    };
    [Export(typeof(IMavParamTypeMetadata))]
    public static IMavParamTypeMetadata SimDdmMean = new MavParamTypeMetadata("SIM_DDM_MEAN", MavParamType.MavParamTypeReal32)
    {
        Group = Group,
        Category = Category,
        ShortDesc = "DDM mean",
        LongDesc = "Mean value of DDM",
        Units = null,
        RebootRequired = false,
        MinValue = -100.0f,
        MaxValue = +100.0f,
        DefaultValue = 0f,
        Increment = 0.1f,
    };
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
        DefaultValue = 1f,
        Increment = 0.01f,
    };
    
    
}