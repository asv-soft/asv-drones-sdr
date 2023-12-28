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

/// <summary>
/// This class represents a service for SDR (Software Defined Radio) applications.
/// </summary>
internal class SdrService : DisposableOnceWithCancel
{
    /// <summary>
    /// Represents a logger instance to perform logging in the current class.
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


    /// <summary>
    /// Represents a service for handling software defined radio (SDR) modules.
    /// </summary>
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

    /// <summary>
    /// Generates a sequence of ComposablePartCatalog objects.
    /// </summary>
    /// <returns>
    /// A sequence of ComposablePartCatalog objects.
    /// </returns>
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

    /// <summary>
    /// Gets the enumerable collection of assemblies.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of type <see cref="Assembly"/> containing the assemblies.
    /// </returns>
    /// <remarks>
    /// The assemblies are retrieved using the following logic:
    /// 1. The assembly of the current instance's type (<see cref="GetType().Assembly"/>).
    /// 2. The assembly of the <see cref="IModule"/> type (<see cref="typeof(IModule).Assembly"/>).
    /// 3. The assembly of the <see cref="VirtualAnalyzerLlz"/> type (<see cref="typeof(VirtualAnalyzerLlz).Assembly"/>).
    /// </remarks>
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