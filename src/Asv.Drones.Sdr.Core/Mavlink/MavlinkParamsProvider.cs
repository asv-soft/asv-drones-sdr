using System.ComponentModel.Composition;
using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core.Mavlink;

/// <summary>
/// Interface for providing access to MAVLink parameters.
/// </summary>
public interface IMavlinkParamsProvider
{
    /// <summary>
    /// Retrieves a collection of parameter types metadata.
    /// </summary>
    /// <returns>
    /// An enumerable collection of <see cref="IMavParamTypeMetadata"/> representing the parameter types metadata.
    /// </returns>
    IEnumerable<IMavParamTypeMetadata> GetParams();
}

/// <summary>
/// Represents a static provider of MAVLink parameter types.
/// </summary>
[Export(typeof(IMavlinkParamsProvider))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class StaticMavlinkParamsProvider : IMavlinkParamsProvider
{
    /// <summary>
    /// The list of parameter types for a MAV object.
    /// </summary>
    /// <remarks>
    /// This variable stores a collection of objects that implement the IMavParamTypeMetadata interface.
    /// Each object represents a parameter type for a MAV (Micro Air Vehicle) object.
    /// </remarks>
    private readonly IEnumerable<IMavParamTypeMetadata> _paramList;

    /// <summary>
    /// Represents a static Mavlink Params Provider.
    /// </summary>
    [ImportingConstructor]
    public StaticMavlinkParamsProvider([ImportMany]IEnumerable<IMavParamTypeMetadata> paramList)
    {
        _paramList = paramList;
    }

    /// <summary>
    /// Returns a collection of parameters.
    /// </summary>
    /// <returns>
    /// A collection of objects implementing the <see cref="IMavParamTypeMetadata"/> interface.
    /// </returns>
    public IEnumerable<IMavParamTypeMetadata> GetParams()
    {
        return _paramList;
    }
}