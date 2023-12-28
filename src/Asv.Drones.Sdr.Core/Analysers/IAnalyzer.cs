using Asv.Common;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents an analyzer for signal overflow detection and initialization.
/// </summary>
public interface IAnalyzer:IDisposable
{
    /// <summary>
    /// Gets the signal overflow indicator value.
    /// </summary>
    /// <remarks>
    /// This property represents the signal overflow indicator value
    /// which indicates if the signal has exceeded its maximum limit.
    /// </remarks>
    /// <returns>
    /// An instance implementing the IRxValue interface that represents
    /// the signal overflow indicator value. The value is of type float.
    /// </returns>
    IRxValue<float> SignalOverflowIndicator { get; }

    /// <summary>
    /// Initializes the system with the specified frequency, reference power, calibration provider, and cancellation token.
    /// </summary>
    /// <param name="frequencyHz">The frequency in Hertz.</param>
    /// <param name="refPower">The reference power as a float value.</param>
    /// <param name="calibration">The calibration provider to be used.</param>
    /// <param name="cancel">The cancellation token to cancel the initialization.</param>
    /// <returns>A Task representing the initialization process.</returns>
    /// <remarks>
    /// This method initializes the system with the given frequency, reference power, calibration provider, and cancellation token.
    /// It returns a Task that represents the initialization process, which can be awaited to determine when the initialization is completed.
    /// </remarks>
    Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel);
}