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

    protected WorkModeBase(AsvSdrCustomMode mode, IGnssSource gnssSource, IConfiguration configuration, CompositionContainer container)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (container == null) throw new ArgumentNullException(nameof(container));
        Mode = mode;
        _gnssSource = gnssSource ?? throw new ArgumentNullException(nameof(gnssSource));
        var cfg = configuration.Get<WorkModeBaseConfig>();
        var implDict = cfg.Analyzers[mode.ToString("G")];
        if (implDict.Count == 0) throw new Exception($"Cfg: {nameof(TAnalyzer)} implementation not found");
        var deviceImplName = implDict.First(_ => _.Value).Key;
        var availableDevices = container.GetExports<IAnalyzer, IAnalyzerMetadata>(ExportAnalyzerAttribute.GetContractName(mode));
        var deviceImpl = availableDevices.FirstOrDefault(_ => _.Metadata.Name == deviceImplName);
        if (deviceImpl == null)
        {
            throw new Exception($"Cfg: {nameof(TAnalyzer)} with name {deviceImplName} not found");
        }
        if (deviceImpl.Value is not TAnalyzer analyzer)
        {
            throw new Exception($"Cfg: {deviceImpl.Value.GetType().Name} not implement {nameof(TAnalyzer)}");
        }
        _analyser = analyzer;
    }

    protected TAnalyzer Analyzer => _analyser;
    
    public Task Init(ulong frequencyHz, CancellationToken cancel)
    {
        return _analyser.Init(frequencyHz, cancel);
    }

    public AsvSdrCustomMode Mode { get; }

    public void Fill(RecordId writerRecordId, uint dataIndex, IPayload payload)
    {
        InternalFill((TPayload)payload,writerRecordId, dataIndex, _gnssSource.Gnss.Value, _gnssSource.Attitude.Value, _gnssSource.Position.Value);
    }

    protected abstract void InternalFill(TPayload payload, RecordId record, uint dataIndex,
        GpsRawIntPayload gnss, AttitudePayload attitude, GlobalPositionIntPayload position);
}

