using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using Asv.Cfg;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// This class is responsible for initializing the configuration for the work mode check.
/// </summary>
[ExportModule(Name)]
[PartCreationPolicy(CreationPolicy.Shared)]
public class WorkModeCheckConfigModule:IModule
{
    /// <summary>
    /// The constant representing the name of the WorkModeCheckConfigModule.
    /// </summary>
    public const string Name = "WorkModeCheckConfigModule";

    /// <summary>
    /// Class to check and configure the work mode.
    /// </summary>
    [ImportingConstructor]
    public WorkModeCheckConfigModule(IConfiguration config, CompositionContainer container)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (container == null) throw new ArgumentNullException(nameof(container));
        var cfg = config.Get<WorkModeBaseConfig>();
        foreach (var item in Enum.GetValues<AsvSdrCustomMode>())
        {
            if (item == AsvSdrCustomMode.AsvSdrCustomModeIdle) continue;
            if (cfg.Analyzers.TryGetValue(item.ToString("G"), out var value) == false)
            {
                cfg.Analyzers.Add(item.ToString("G"),value = new Dictionary<string, bool>());
            }
            var implementations = container.GetExports<IAnalyzer,IAnalyzerMetadata>(ExportAnalyzerAttribute.GetContractName(item));
            var implHashSet = new HashSet<string>(implementations.Select(_ => _.Metadata.Name));
            foreach (var implementation in implHashSet)
            {
                if (value.TryGetValue(implementation, out var isEnabled) == false)
                {
                    value.Add(implementation, false);
                }
            }

            foreach (var pair in value.ToArray())
            {
                if (implHashSet.Contains(pair.Key) == false)
                {
                    value.Remove(pair.Key);
                }
            }
            if (value.Count == 0) continue;
            if (value.All(_ => _.Value == false))
            {
                value[implHashSet.First()] = true;
            }
        }
        config.Set(cfg);
        
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        
    }
    
    /// <summary>
    /// Initializes the module.
    /// </summary>
    public void Init()
    {
        
    }
}