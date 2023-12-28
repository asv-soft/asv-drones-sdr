using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents a calibration item.
/// </summary>
public interface ICalibrationItem
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    /// <returns>The name.</returns>
    string Name { get; }

    /// <summary>
    /// Gets the size of the property.
    /// </summary>
    /// <returns>The size of the property.</returns>
    ushort Size { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the property is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the property is enabled; otherwise, <c>false</c>.
    /// </value>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Gets the metadata associated with the calibration table. </summary> <returns>
    /// The calibration table metadata. </returns>
    /// /
    public CalibrationTableMetadata Metadata { get; }

    /// <summary>
    /// Creates a default collection of CalibrationTableRow objects.
    /// </summary>
    /// <returns>
    /// An IEnumerable of CalibrationTableRow objects that represents the default collection.
    /// </returns>
    IEnumerable<CalibrationTableRow> CreateDefault();

    /// <summary>
    /// Updates the calibration table with the provided metadata and data.
    /// </summary>
    /// <param name="metadata">The metadata for the calibration table.</param>
    /// <param name="dataMetadata">The array of calibration table rows.</param>
    /// <returns>Returns an updated array of calibration table rows.</returns>
    CalibrationTableRow[] Update(CalibrationTableMetadata metadata, CalibrationTableRow[] dataMetadata);

    /// <summary>
    /// Tries to read a calibration table row at the specified index.
    /// </summary>
    /// <param name="rowindex">The index of the calibration table row to read.</param>
    /// <param name="row">When this method returns, contains the calibration table row read,
    /// or null if the rowindex is out of range or the row could not be read.</param>
    /// <returns>
    /// true if the calibration table row was successfully read; otherwise, false.
    /// </returns>
    bool TryReadCalibrationTableRow(ushort rowindex, out CalibrationTableRow? row);

    /// <summary>
    /// Sets the mode for the device with the specified frequency and reference power.
    /// </summary>
    /// <param name="freq">The frequency of the device.</param>
    /// <param name="refPower">The reference power for the device.</param>
    /// <remarks>
    /// The SetMode method allows you to set the mode for the device using the specified frequency and reference power.
    /// The frequency parameter represents the frequency of the device, and the refPower parameter represents the reference power for the device.
    /// </remarks>
    void SetMode(ulong freq, float refPower);

    /// <summary>
    /// Gets the value of the requested measured value.
    /// </summary>
    /// <param name="measuredValue">The measured value for which to retrieve the value.</param>
    /// <returns>
    /// The value of the requested measured value.
    /// </returns>
    double this[double measuredValue] { get; }
}