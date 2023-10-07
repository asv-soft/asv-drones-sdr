using System.ComponentModel.Composition;
using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core.Mavlink;

public interface IMavlinkParamsProvider
{
    IEnumerable<IMavParamTypeMetadata> GetParams();
}

[Export(typeof(IMavlinkParamsProvider))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class StaticMavlinkParamsProvider : IMavlinkParamsProvider
{
    private readonly IEnumerable<IMavParamTypeMetadata> _paramList;

    [ImportingConstructor]
    public StaticMavlinkParamsProvider([ImportMany]IEnumerable<IMavParamTypeMetadata> paramList)
    {
        _paramList = paramList;
    }
    
    public IEnumerable<IMavParamTypeMetadata> GetParams()
    {
        return _paramList;
    }
}