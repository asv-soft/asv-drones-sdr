using System.ComponentModel.Composition.Hosting;
using Asv.Cfg;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

public class WorkModeBaseConfig
{
    public Dictionary<string, Dictionary<string,bool>> Analyzers { get; } = new();
}

public abstract class WorkModeBase<TAnalyzer,TPayload>: DisposableOnceWithCancel,IWorkMode
        where TAnalyzer:IAnalyzer
{
    private readonly IGnssSource _gnssSource;
    private readonly TAnalyzer _analyser;

    protected WorkModeBase(AsvSdrCustomMode mode, IGnssSource gnssSource, ITimeService timeService,
        IConfiguration configuration, CompositionContainer container)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (container == null) throw new ArgumentNullException(nameof(container));
        Mode = mode;
        _gnssSource = gnssSource ?? throw new ArgumentNullException(nameof(gnssSource));
        TimeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
        var cfg = configuration.Get<WorkModeBaseConfig>();
        var implDict = cfg.Analyzers[mode.ToString("G")];
        if (implDict.Count == 0) throw new Exception($"Cfg: {typeof(TAnalyzer).Name} implementation not found");
        var deviceImplName = implDict.First(_ => _.Value).Key;
        var availableDevices = container.GetExports<IAnalyzer, IAnalyzerMetadata>(ExportAnalyzerAttribute.GetContractName(mode));
        var deviceImpl = availableDevices.FirstOrDefault(_ => _.Metadata.Name == deviceImplName);
        if (deviceImpl == null)
        {
            throw new Exception($"Cfg: {typeof(TAnalyzer).Name} with name {deviceImplName} not found");
        }
        if (deviceImpl.Value is not TAnalyzer analyzer)
        {
            throw new Exception($"Cfg: {deviceImpl.Value.GetType().Name} not implement {typeof(TAnalyzer).Name}");
        }
        Disposable.Add(deviceImpl.Value);
        _analyser = analyzer;
    }

    protected TAnalyzer Analyzer => _analyser;
    protected ITimeService TimeService { get; }

    public ulong FrequencyHz { get; private set; }

    public Task Init(ulong frequencyHz, CancellationToken cancel)
    {
        FrequencyHz = frequencyHz;
        return _analyser.Init(frequencyHz, cancel);
    }

    public AsvSdrCustomMode Mode { get; }

    public void ReadData(Guid writerRecordId, uint dataIndex, IPayload payload)
    {
        InternalFill((TPayload)payload,writerRecordId, dataIndex, _gnssSource.Gnss.Value, _gnssSource.Attitude.Value, _gnssSource.Position.Value);
    }

    protected abstract void InternalFill(TPayload payload, Guid record, uint dataIndex,
        GpsRawIntPayload? gnss, AttitudePayload? attitude, GlobalPositionIntPayload? position);
}

