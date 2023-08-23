using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using Asv.Cfg;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;
using NLog;

namespace Asv.Drones.Sdr.Core.Mavlink;

public class GbsServerServiceConfig
{
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

    public byte ComponentId { get; set; } = 15;
    public byte SystemId { get; set; } = 1;
    public SdrServerDeviceConfig Server { get; set; } = new();
    public bool WrapToV2ExtensionEnabled { get; set; } = true;
}

[Export(typeof(ISdrMavlinkService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class SdrMavlinkService : DisposableOnceWithCancel, ISdrMavlinkService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    [ImportingConstructor]
    public SdrMavlinkService(IConfiguration config, IPacketSequenceCalculator sequenceCalculator, CompositionContainer container)
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
            }, cfg.Server,Scheduler.Default, Array.Empty<IMavParamTypeMetadata>(),
                new MavParamCStyleEncoding(), config)
            .DisposeItWith(Disposable);
        Server.Start();
        
        Observable.Timer(TimeSpan.FromSeconds(5)).Subscribe(_ =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Server.StatusText.Log(MavSeverity.MavSeverityInfo, $"SDR version: {version}");
        });
    }

    public IMavlinkRouter Router { get; }
    public ISdrServerDevice Server { get; }
}