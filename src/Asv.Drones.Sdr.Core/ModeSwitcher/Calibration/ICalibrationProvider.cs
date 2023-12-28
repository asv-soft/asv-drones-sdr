using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents a provider for calibration functionality.
/// </summary>
public interface ICalibrationProvider:IDisposable
{
    /// <summary>
    /// Gets the path of the calibration folder.
    /// </summary>
    /// <remarks>
    /// The calibration folder is the directory where the calibrated data is stored.
    /// </remarks>
    /// <value>
    /// A <see cref="System.String"/> representing the path of the calibration folder.
    /// </value>
    string CalibrationFolder { get; }

    /// <summary>
    /// Gets the observable value indicating whether the process is in progress or not.
    /// </summary>
    /// <remarks>
    /// Use this property to subscribe to changes in the process state.
    /// The value is represented by an <see cref="IRxValue{T}"/> object of type <see cref="bool"/>.
    /// </remarks>
    /// <returns>
    /// The observable value representing the process state.
    /// </returns>
    IRxValue<bool> IsInProgress { get; }

    /// <summary>
    /// Gets the count of tables.
    /// </summary>
    /// <value>
    /// The count of tables as an unsigned short.
    /// </value>
    ushort TableCount { get; }

    /// <summary>
    /// Starts the calibration process.
    /// </summary>
    /// <param name="cancel">A cancellation token that can be used to cancel the calibration process.</param>
    /// <returns>A task representing the completion of the calibration process. The task will return a MavResult indicating the success or failure of the calibration.</returns>
    Task<MavResult> StartCalibration(CancellationToken cancel);

    /// <summary>
    /// Stops the calibration process.
    /// </summary>
    /// <param name="cancel">The cancellation token used to cancel the calibration process.</param>
    /// <returns>A task representing the asynchronous operation. The task result represents the result of the calibration process.</returns>
    /// <remarks>
    /// Use this method to stop the calibration process. It takes a cancellation token as input to cancel the ongoing calibration process.
    /// The task returned by this method represents the result of the calibration process, indicating whether it was successful or not.
    /// </remarks>
    Task<MavResult> StopCalibration(CancellationToken cancel);

    /// <summary>
    /// Writes a calibration table to a specific index in the memory. </summary> <param name="tableIndex">The index at which the calibration table will be written.</param> <param name="metadata">The metadata associated with the calibration table.</param> <param name="items">The array of calibration table rows.</param>
    /// /
    void WriteCalibrationTable(ushort tableIndex,CalibrationTableMetadata metadata, CalibrationTableRow[] items);

    /// <summary>
    /// Tries to read calibration table information.
    /// </summary>
    /// <param name="tableIndex">The index of the calibration table.</param>
    /// <param name="name">The name of the calibration table (output).</param>
    /// <param name="size">The size of the calibration table (output).</param>
    /// <param name="metadata">The metadata of the calibration table (output).</param>
    /// <returns>
    /// <c>true</c> if the calibration table information was successfully read;
    /// otherwise, <c>false</c>.
    /// </returns>
    bool TryReadCalibrationTableInfo(ushort tableIndex, out string? name, out ushort? size, out CalibrationTableMetadata? metadata);

    /// <summary>
    /// Tries to read a calibration table row based on the specified table index and row index. </summary> <param name="tableIndex">The index of the calibration table.</param> <param name="rowIndex">The index of the row to read.</param> <param name="row">When this method returns, contains the requested calibration table row if it exists; otherwise, null.</param> <returns>
    /// <c>true</c> if the calibration table row was successfully read; otherwise, <c>false</c>. </returns>
    /// /
    bool TryReadCalibrationTableRow(ushort tableIndex, ushort rowindex, out CalibrationTableRow? row);

    /// <summary>
    /// Sets the mode of the device with the given frequency and reference power.
    /// </summary>
    /// <param name="freq">The frequency to set the device mode to.</param>
    /// <param name="refPower">The reference power value to set the device mode to.</param>
    /// <remarks>
    /// Frequency should be a non-negative integer value representing the frequency in hertz (Hz).
    /// refPower should be a floating-point value representing the reference power in decibels (dB).
    /// </remarks>
    void SetMode(ulong freq, float refPower);

    /// <summary>
    /// Gets the calibration item for a given table name.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <returns>
    /// The calibration item for the specified table name, or null if no calibration item exists for the given table name.
    /// </returns>
    ICalibrationItem? this[string tableName] { get; }
}