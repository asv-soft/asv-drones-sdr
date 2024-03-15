using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Drones.Sdr.Core;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Virtual;

/// <summary>
/// The VirtualAnalyzerGp class implements the IAnalyzerGp interface and serves as a virtual analyzer.
/// </summary>
[ExportAnalyzer(AsvSdrCustomMode.AsvSdrCustomModeGp, "Virtual")]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VirtualAnalyzerGp : IAnalyzerGp
{
    private readonly ISdrMavlinkService _device;

    /// <summary>
    /// The signal overflow indicator for the given float value.
    /// </summary>
    private readonly RxValue<float> _signalOverflowIndicator;

    private readonly NormalRandom _random;

    /// <summary>
    /// This class represents a virtual analyzer for GP (General Purpose) signals.
    /// </summary>
    [ImportingConstructor]
    public VirtualAnalyzerGp(ISdrMavlinkService device)
    {
        _device = device;
        _random = new NormalRandom();
        _signalOverflowIndicator = new RxValue<float>(Single.NaN);
    }

    /// <summary>
    /// Represents an indicator of signal overflow.
    /// </summary>
    /// <value>
    /// The signal overflow indicator value.
    /// </value>
    public IRxValue<float> SignalOverflowIndicator => _signalOverflowIndicator;

    /// <summary>
    /// Initializes the system with the specified parameters.
    /// </summary>
    /// <param name="frequencyHz">The frequency in Hz.</param>
    /// <param name="refPower">The reference power.</param>
    /// <param name="calibration">The calibration provider.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fills the given payload with the specified data.
    /// </summary>
    /// <param name="payload">The payload to be filled.</param>
    public void Fill(AsvSdrRecordDataGpPayload payload)
    {
        var ddmMean = (float)_device.Server.Params[SimulationParams.SimDdmMean];
        var ddmSd = (float)_device.Server.Params[SimulationParams.SimDdmSd];
        var sdm = (float)_device.Server.Params[SimulationParams.SimSdm];
        var ddm = ddmMean + (_random.NextDouble() - 0.5) * ddmSd;
        var am90 = (sdm - ddm) / 2.0 / 100.0;
        var am150 = (sdm + ddm) / 2.0 / 100.0;
        payload.CrsAm90 = (float)am90;
        payload.TotalAm90 = (float)am90;
        payload.ClrAm90 = (float)am90;
        
        payload.CrsAm150 = (float)am150;
        payload.TotalAm150 = (float)am150;
        payload.ClrAm150 = (float)am150;
        
        
        
    }

    /// <summary>
    /// Releases the resources used by the object.
    /// </summary>
    public void Dispose()
    {
        _signalOverflowIndicator.Dispose();
    }
}