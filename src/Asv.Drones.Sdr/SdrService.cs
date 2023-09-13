using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Reflection;
using Asv.Cfg;
using Asv.Common;
using Asv.Drones.Sdr.Core;
using Asv.Drones.Sdr.Virtual;
using Asv.Mavlink;
using NLog;

namespace Asv.Drones.Sdr;

internal class SdrService : DisposableOnceWithCancel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


    public SdrService(IConfiguration config)
    {
        var config1 = config ?? throw new ArgumentNullException(nameof(config));
        var container = new CompositionContainer(new AggregateCatalog(Catalogs().ToArray()), CompositionOptions.IsThreadSafe)
            .DisposeItWith(Disposable);
        var batch = new CompositionBatch();
        batch.AddExportedValue(config1);
        batch.AddExportedValue<IPacketSequenceCalculator>(new PacketSequenceCalculator());
        batch.AddExportedValue(container);
        container.Compose(batch);

        var modules = container.GetExports<IModule, IModuleMetadata>().ToArray();
        var sort = modules.ToDictionary(_=>_.Metadata.Name, _=>_.Metadata.Dependency);
        Logger.Info($"Begin loading modules [{modules.Length} items]");
        foreach (var moduleName in DepthFirstSearch.Sort(sort))
        {
            try
            {
                Logger.Trace($"Init {moduleName}");
                var module = modules.First(_ => _.Metadata.Name == moduleName).Value;
                module.Init();
                module.DisposeItWith(Disposable);
            }
            catch (Exception e)
            {
                Logger.Error($"Error to init module '{moduleName}':{e.Message}");
                throw;
            }
        }
        
    }
    
    private IEnumerable<ComposablePartCatalog> Catalogs()
    {
        foreach (var asm in Assemblies.Distinct().Select(assembly => new AssemblyCatalog(assembly)))
        {
            yield return asm;
        }
        
        // Enable this feature to load plugins from folder
        var dir = Path.GetFullPath("./"); //Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var cat = new DirectoryCatalog(dir, "Asv.Drones.Sdr.Plugin.*.dll");
        cat.Refresh();
        Logger.Trace($"Search plugin in {cat.Path}");
        foreach (var file in cat.LoadedFiles)
        {
            Logger.Info($"Found plugin '{Path.GetFileName(file)}'");
        }
        yield return cat;

    }
    
    private IEnumerable<Assembly> Assemblies
    {
        get
        {
            yield return GetType().Assembly;                    // [this]
            yield return typeof(IModule).Assembly;              // [Asv.Drones.Sdr.Core]
            yield return typeof(VirtualAnalyzerLlz).Assembly;   // [Asv.Drones.Sdr.Virtual]
        }
    }
}