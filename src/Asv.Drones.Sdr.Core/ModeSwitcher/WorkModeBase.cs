using System.ComponentModel.Composition.Hosting;
using Asv.Cfg;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents the base configuration for work mode.
/// </summary>
public class WorkModeBaseConfig
{
    /// <summary>
    /// Gets the analyzers dictionary.
    /// The dictionary contains a collection of analyzers.
    /// </summary>
    /// <remarks>
    /// The outer dictionary stores analyzers by their names.
    /// The inner dictionary stores information about each analyzer.
    /// The key of the inner dictionary represents the name of the property being analyzed.
    /// The value of the inner dictionary indicates whether the analyzer is enabled or disabled for the specified property.
    /// </remarks>
    /// <value>
    /// The analyzers dictionary.
    /// </value>
    public Dictionary<string, Dictionary<string,bool>> Analyzers { get; } = new();
}

/// <summary>
/// Represents a base class for work modes in the application.
/// </summary>
/// <typeparam name="TAnalyzer">The type of the analyzer.</typeparam>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public abstract class WorkModeBase<TAnalyzer,TPayload>: DisposableOnceWithCancel,IWorkMode
        where TAnalyzer:IAnalyzer
{
    /// <summary>
    /// Represents an instance of a GNSS (Global Navigation Satellite System) source.
    /// </summary>
    private readonly IGnssSource _gnssSource;
    
    /// <summary>
    /// Represents a private readonly instance of a TAnalyser.
    /// </summary>
    private readonly TAnalyzer _analyser;

    /// <summary>
    /// Initializes an instance of the WorkModeBase class.
    /// </summary>
    /// <param name="mode">The custom mode.</param>
    /// <param name="gnssSource">The GNSS source.</param>
    /// <param name="timeService">The time service.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="container">The composition container.</param>
    /// <exception cref="ArgumentNullException">Thrown when the configuration or container is null.</exception>
    /// <exception cref="Exception">Thrown when the analyzer implementation is not found in the configuration or composition container.</exception>
    /// <exception cref="Exception">Thrown when the analyzer implementation does not match the specified type.</exception>
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

    /// <summary>
    /// Gets the protected property Analyzer.
    /// </summary>
    /// <typeparam name="TAnalyzer">The type of the analyzer.</typeparam>
    /// <returns>The analyzer instance.</returns>
    protected TAnalyzer Analyzer => _analyser;

    /// <summary>
    /// Represents the TimeService property.
    /// </summary>
    /// <remarks>
    /// This property provides access to an instance of the ITimeService interface,
    /// which is responsible for providing time-related functionality.
    /// </remarks>
    /// <value>
    /// An instance of the ITimeService interface.
    /// </value>
    protected ITimeService TimeService { get; }

    /// <summary>
    /// Gets the frequency value in hertz.
    /// </summary>
    /// <value>The frequency value in hertz.</value>
    public ulong FrequencyHz { get; private set; }

    /// <summary>
    /// Gets or sets the reference power.
    /// </summary>
    /// <value>
    /// The reference power.
    /// </value>
    public float RefPower { get; private set; }

    /// <summary>
    /// Initializes the analyser with the specified frequency, reference power, calibration provider, and cancellation token.
    /// </summary>
    /// <param name="frequencyHz">The frequency in Hz.</param>
    /// <param name="refPower">The reference power.</param>
    /// <param name="calibration">The calibration provider.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>
    /// A task representing the asynchronous initialization operation.
    /// </returns>
    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        FrequencyHz = frequencyHz;
        RefPower = refPower;
        return _analyser.Init(frequencyHz,refPower,calibration, cancel);
    }


    /// <summary>
    /// Gets the signal overflow indicator of the analyser.
    /// </summary>
    /// <value>
    /// The signal overflow indicator.
    /// </value>
    public IRxValue<float> SignalOverflowIndicator => _analyser.SignalOverflowIndicator;

    /// <summary>
    /// Gets the current custom mode of the AsvSdrCustomMode class.
    /// </summary>
    /// <returns>
    /// The custom mode of the AsvSdrCustomMode class.
    /// </returns>
    public AsvSdrCustomMode Mode { get; }

    /// <summary>
    /// Reads the data from a certain writer record using the provided writer record ID, data index, and payload.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <param name="writerRecordId">The ID of the writer record to read data from.</param>
    /// <param name="dataIndex">The index of the data to read.</param>
    /// <param name="payload">The payload containing the data to read.</param>
    public void ReadData(Guid writerRecordId, uint dataIndex, IPayload payload)
    {
        InternalFill((TPayload)payload,writerRecordId, dataIndex, _gnssSource.Gnss.Value, _gnssSource.Attitude.Value, _gnssSource.Position.Value);
    }

    /// <summary>
    /// Fills the payload data into the record at the given dataIndex.
    /// </summary>
    /// <param name="payload">The payload data to be filled.</param>
    /// <param name="record">The record in which the data will be filled.</param>
    /// <param name="dataIndex">The index at which the data will be filled.</param>
    /// <param name="gnss">The GNSS payload data (optional).</param>
    /// <param name="attitude">The attitude payload data (optional).</param>
    /// <param name="position">The global position payload data (optional).</param>
    protected abstract void InternalFill(TPayload payload, Guid record, uint dataIndex,
        GpsRawIntPayload? gnss, AttitudePayload? attitude, GlobalPositionIntPayload? position);
}

