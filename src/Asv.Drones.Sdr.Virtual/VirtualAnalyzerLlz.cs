using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Drones.Sdr.Core;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Virtual;

/// <summary>
/// Represents a virtual analyzer for the LLZ custom mode in the ASV SDR system.
/// </summary>
[ExportAnalyzer(AsvSdrCustomMode.AsvSdrCustomModeLlz, "Virtual")]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VirtualAnalyzerLlz : IAnalyzerLlz
{
    /// <summary>
    /// Represents a normal random number generator.
    /// </summary>
    private readonly NormalRandom _random;

    /// <summary>
    /// A readonly RxValue variable that represents the signal overflow indicator.
    /// </summary>
    private readonly RxValue<float> _signalOverflowIndicator;

    /// <summary>
    /// This class represents a virtual analyzer for Llz.
    /// </summary>
    [ImportingConstructor]
    public VirtualAnalyzerLlz()
    {
        _random = new NormalRandom();
        _signalOverflowIndicator = new RxValue<float>(Single.NaN);
    }

    /// <summary>
    /// Gets the signal overflow indicator.
    /// </summary>
    /// <remarks>
    /// This property returns the signal overflow indicator, which is a read-only object implementing the <see cref="IRxValue{T}"/> interface where T is <see cref="float"/>.
    /// </remarks>
    /// <returns>
    /// An object implementing the <see cref="IRxValue{T}"/> interface where T is <see cref="float"/>. This object can be used to observe the value of the signal overflow indicator.
    /// </returns>
    public IRxValue<float> SignalOverflowIndicator => _signalOverflowIndicator;

    /// <summary>
    /// Initializes the system with specified frequency, reference power, calibration provider, and cancellation token.
    /// </summary>
    /// <param name="frequencyHz">The frequency in hertz.</param>
    /// <param name="refPower">The reference power.</param>
    /// <param name="calibration">The calibration provider.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task that represents the initialization process.</returns>
    /// <remarks>
    /// This method initializes the system with the specified frequency, reference power, calibration provider,
    /// and cancellation token. Once initialized, the system is ready to perform operations at the specified frequency
    /// and reference power levels. The calibration provider is used to calibrate the system for accurate measurements.
    /// The cancellation token can be used to cancel the initialization process.
    /// </remarks>
    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fills the given payload with random values for CrsAm90 and TotalAm90 properties.
    /// </summary>
    /// <param name="payload">The payload to be filled.</param>
    public void Fill(AsvSdrRecordDataLlzPayload payload)
    {
        // payload.CrsCarrierOffset = .CrsCarrierOffset;
        // payload.CrsPower = _gnssSource.CrsPower;
        payload.CrsAm90 = _random.NextSingle();
        // payload.CrsAm150 = _gnssSource.CrsAm150;
        // payload.CrsFreq90 = _gnssSource.CrsFreq90;
        // payload.CrsFreq150 = _gnssSource.CrsFreq150;
        //
        // payload.ClrCarrierOffset = _gnssSource.ClrCarrierOffset;
        // payload.ClrPower = _gnssSource.ClrPower;
        // payload.ClrAm90 = _gnssSource.ClrAm90;
        // payload.ClrAm150 = _gnssSource.ClrAm150;
        // payload.ClrFreq90 = _gnssSource.ClrFreq90;
        // payload.ClrFreq150 = _gnssSource.ClrFreq150;
        //
        // payload.TotalCarrierOffset = _gnssSource.TotalCarrierOffset;
        // payload.TotalFreq = _gnssSource.TotalFreq;
        // payload.TotalPower = _gnssSource.TotalPower;
        // payload.TotalFieldStrength = _gnssSource.TotalFieldStrength;
        payload.TotalAm90 = _random.NextSingle();;
        // payload.TotalAm150 = _gnssSource.TotalAm150;
        //
        //
        // payload.TotalFreq90 = _gnssSource.TotalFreq90;
        // payload.TotalFreq150 = _gnssSource.TotalFreq150;
        // payload.CodeIdFreq1020 = _gnssSource.CodeIdFreq1020;
        //
        // payload.Phi90CrsVsClr = _gnssSource.Phi90CrsVsClr;
        // payload.Phi150CrsVsClr = _gnssSource.Phi150CrsVsClr;
        // payload.CodeIdAm1020 = _gnssSource.CodeIdAm1020;
        //
        // payload.MeasureTime = _gnssSource.MeasureTime;
    }

    /// <summary>
    /// Disposes the resources used by the current object.
    /// </summary>
    public void Dispose()
    {
        _signalOverflowIndicator.Dispose();
    }
}