using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Drones.Sdr.Core;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Virtual;

/// <summary>
/// Represents a virtual analyzer for VOR signals.
/// </summary>
/// <remarks>
/// This class implements the <see cref="IAnalyzerVor"/> interface and is used
/// to analyze and process VOR signals in a virtual environment.
/// </remarks>
[ExportAnalyzer(AsvSdrCustomMode.AsvSdrCustomModeVor, "Virtual")]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VirtualAnalyzerVor : IAnalyzerVor
{
    /// <summary>
    /// Variable to store the signal overflow indicator value.
    /// </summary>
    private readonly RxValue<float> _signalOverflowIndicator;

    /// <summary>
    /// This method initializes a new instance of the VirtualAnalyzerVor class.
    /// </summary>
    public VirtualAnalyzerVor()
    {
        _signalOverflowIndicator = new RxValue<float>(Single.NaN);
    }

    /// <summary>
    /// Gets the signal overflow indicator.
    /// </summary>
    /// <remarks>
    /// This property represents an IRxValue of type float, which can be used to monitor
    /// whether the signal has overflowed or not.
    /// </remarks>
    public IRxValue<float> SignalOverflowIndicator => _signalOverflowIndicator;

    /// <summary>
    /// Initializes the system with the specified parameters.
    /// </summary>
    /// <param name="frequencyHz">The frequency in hertz.</param>
    /// <param name="refPower">The reference power.</param>
    /// <param name="calibration">The calibration provider.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fills the payload with data from AsvSdrRecordDataVorPayload object.
    /// </summary>
    /// <param name="payload">The AsvSdrRecordDataVorPayload object containing the data to be filled.</param>
    public void Fill(AsvSdrRecordDataVorPayload payload)
    {
        
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting resources.
    /// </summary>
    public void Dispose()
    {
        _signalOverflowIndicator.Dispose();
    }
}