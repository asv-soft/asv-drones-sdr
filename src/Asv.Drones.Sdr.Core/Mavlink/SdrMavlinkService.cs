using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using Asv.Cfg;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;
using DynamicData;
using DynamicData.Binding;
using NLog;

namespace Asv.Drones.Sdr.Core.Mavlink;

/// <summary>
/// Configuration class for the GbsServerService.
/// </summary>
public class GbsServerServiceConfig
{
    /// <summary>
    /// Gets or sets the array of MavlinkPortConfig objects representing the ports used for communication.
    /// </summary>
    /// <value>
    /// The array of MavlinkPortConfig objects.
    /// </value>
    public MavlinkPortConfig[] Ports { get; set; } = new[]
    {
           
#if DEBUG
        new MavlinkPortConfig
        {
            ConnectionString = "tcp://127.0.0.1:5762",
            Name = "Debug to SITL",
            IsEnabled = true
        }
#else
            new MavlinkPortConfig
            {
                ConnectionString = "serial:/dev/ttyS1?br=115200",
                Name = "Modem",
                IsEnabled = true
            }
#endif               
           
            
    };

    /// <summary>
    /// Gets or sets the component ID.
    /// </summary>
    /// <value>The component ID.</value>
    public byte ComponentId { get; set; } = 15;

    /// <summary>
    /// Gets or sets the identifier of the system.
    /// </summary>
    /// <value>
    /// The identifier of the system.
    /// </value>
    public byte SystemId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the configuration for the Server device.
    /// </summary>
    /// <value>
    /// The configuration for the Server device.
    /// </value>
    public SdrServerDeviceConfig Server { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the WrapToV2Extension is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WrapToV2Extension is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool WrapToV2ExtensionEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the mission items for the server.
    /// </summary>
    /// <value>
    /// The mission items for the server.
    /// </value>
    public ServerMissionItem[] MissionItems { get; set; } = { };
}

/// <summary>
/// Represents a service for handling Mavlink communication with an SDR device.
/// </summary>
[Export(typeof(ISdrMavlinkService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class SdrMavlinkService : DisposableOnceWithCancel, ISdrMavlinkService
{
    /// <summary>
    /// Represents a logging facility for the current class.
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Represents a read-only collection of server mission items.
    /// </summary>
    private ReadOnlyObservableCollection<ServerMissionItem> _missionItems;

    /// <summary>
    /// Represents a service for handling SDR MAVLink communication.
    /// </summary>
    /// <remarks>
    /// This service is responsible for configuring the MAVLink router, adding ports, creating the SDR server device,
    /// setting up mission items, starting the server, and logging the SDR version.
    /// </remarks>
    /// <param name="config">The configuration object used to determine settings for the service.</param>
    /// <param name="sequenceCalculator">The packet sequence calculator used by the server device.</param>
    /// <param name="container">The composition container for importing MAVLink parameters providers.</param>
    /// <param name="paramList">The list of MAVLink parameters providers.</param>
    [ImportingConstructor]
    public SdrMavlinkService(IConfiguration config, IPacketSequenceCalculator sequenceCalculator, CompositionContainer container, [ImportMany]IEnumerable<IMavlinkParamsProvider> paramList)
    {
        Router = new MavlinkRouter(MavlinkV2Connection.RegisterDefaultDialects).DisposeItWith(Disposable);
        var cfg = config.Get<GbsServerServiceConfig>();
        Router.WrapToV2ExtensionEnabled = cfg.WrapToV2ExtensionEnabled;
        foreach (var port in cfg.Ports)
        {
            Logger.Trace($"Add port {port.Name}: {port.ConnectionString}");
            Router.AddPort(port);
        }
        Logger.Trace($"Create device SYS:{cfg.SystemId}, COM:{cfg.ComponentId}");    
        Server = new SdrServerDevice(Router, sequenceCalculator, new MavlinkServerIdentity
            {
                ComponentId = cfg.ComponentId,
                SystemId = cfg.SystemId
            }, cfg.Server,Scheduler.Default, paramList.SelectMany(x=>x.GetParams()),
                MavParamHelper.ByteWiseEncoding, config)
            .DisposeItWith(Disposable);
        Server.SdrEx.Base.Set(_=>
        {
            _.SignalOverflow = Single.NaN;
            _.RefPower = Single.NaN;
        });

        Server.Missions.AddItems(cfg.MissionItems);
        
        Server.Missions.Items
            .Bind(out _missionItems)
            .Subscribe(_ =>
            {
                cfg.MissionItems = _missionItems.ToArray();
                config.Set(cfg);
            }).DisposeItWith(Disposable);
        
        Server.Start();
        
        Observable.Timer(TimeSpan.FromSeconds(5)).Subscribe(_ =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            Server.StatusText.Log(MavSeverity.MavSeverityInfo, $"SDR version: {version}");
        });
    }

    /// <summary>
    /// Gets the Mavlink router.
    /// </summary>
    /// <remarks>
    /// The Mavlink router is responsible for routing Mavlink messages between different components or devices.
    /// </remarks>
    /// <value>
    /// The Mavlink router.
    /// </value>
    public IMavlinkRouter Router { get; }

    /// <summary>
    /// Gets the ISdrServerDevice associated with the property.
    /// </summary>
    /// <returns>
    /// The ISdrServerDevice associated with the property.
    /// </returns>
    public ISdrServerDevice Server { get; }
}