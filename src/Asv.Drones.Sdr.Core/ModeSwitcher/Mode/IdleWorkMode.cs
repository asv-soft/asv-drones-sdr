using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents the idle work mode for a device.
/// </summary>
public class IdleWorkMode : IWorkMode
{
    /// <summary>
    /// Represents a signal overflow indicator that holds a floating-point value.
    /// </summary>
    private readonly RxValue<float> _signalOverflowIndicator = new(Single.NaN);

    /// <summary>
    /// Gets the singleton instance of the work mode.
    /// </summary>
    /// <value>
    /// The singleton instance of the work mode.
    /// </value>
    public static IWorkMode Instance { get; } = new IdleWorkMode();

    /// <summary>
    /// The frequency of the property in hertz (Hz).
    /// This property represents the frequency of a certain component or signal in hertz, which is the unit of frequency in the International System of Units (SI).
    /// </summary>
    /// <remarks>
    /// This property is read-only and cannot be modified. It returns a ulong value representing the frequency in hertz.
    /// The initial value of this property is 0.
    /// </remarks>
    /// <returns>
    /// The frequency of the property in hertz (Hz).
    /// </returns>
    public ulong FrequencyHz => 0;

    /// <summary>
    /// Initializes the software with the provided parameters.
    /// </summary>
    /// <param name="frequencyHz">The frequency in Hertz.</param>
    /// <param name="refPower">The reference power.</param>
    /// <param name="calibration">The calibration provider instance.</param>
    /// <param name="cancel">A cancellation token to cancel the initialization.</param>
    /// <returns>A Task representing the initialization process.</returns>
    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the signal overflow indicator.
    /// The signal overflow indicator is an IRxValue<float> property.
    /// </summary>
    /// <value>The signal overflow indicator.</value>
    public IRxValue<float> SignalOverflowIndicator => _signalOverflowIndicator;

    /// <summary>
    /// Gets or sets the Mode property of the object.
    /// </summary>
    /// <value>
    /// The current mode of the object.
    /// </value>
    public AsvSdrCustomMode Mode => AsvSdrCustomMode.AsvSdrCustomModeIdle;

    /// <summary>
    /// Reads data from a writer record using the specified writerRecordId, dataIndex, and payload.
    /// </summary>
    /// <param name="writerRecordId">The unique identifier of the writer record.</param>
    /// <param name="dataIndex">The index of the data.</param>
    /// <param name="payload">The payload containing the data to be read.</param>
    public void ReadData(Guid writerRecordId, uint dataIndex, IPayload payload)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        _signalOverflowIndicator.Dispose();
    }
}