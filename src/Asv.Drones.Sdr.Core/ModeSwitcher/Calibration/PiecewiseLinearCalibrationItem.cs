using Asv.Common;
using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Abstract class representing a piecewise linear calibration item.
/// </summary>
public abstract class PiecewiseLinearCalibrationItem : ICalibrationItem
{
    /// <summary>
    /// Represents a collection of tables containing piecewise linear functions.
    /// </summary>
    private readonly SortedDictionary<ulong,SortedDictionary<float,PiecewiseLinearFunction>> _tables = new();

    /// <summary>
    /// An object used for synchronization in multi-threaded scenarios.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// Represents the frequency of a particular event or value.
    /// </summary>
    private ulong _freq;

    /// <summary>
    /// The reference power value.
    /// </summary>
    private float _refPower;

    /// <summary>
    /// Represents the currently selected piecewise linear function table.
    /// </summary>
    private PiecewiseLinearFunction? _selectedTable;

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    /// <remarks>
    /// This property is abstract and must be implemented in derived classes.
    /// </remarks>
    /// <value>
    /// The name of the property.
    /// </value>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the size of the property.
    /// </summary>
    /// <value>
    /// The size of the property.
    /// </value>
    public ushort Size { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the property is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets the metadata for the CalibrationTable.
    /// </summary>
    /// <value>
    /// The CalibrationTable metadata.
    /// </value>
    public CalibrationTableMetadata Metadata { get; private set; } = null!;

    /// <summary>
    /// Creates a default set of <see cref="CalibrationTableRow"/> objects.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerable<CalibrationTableRow>"/> representing the default set of calibration table rows.
    /// </returns>
    public abstract IEnumerable<CalibrationTableRow> CreateDefault();

    /// Updates the calibration table with the given metadata and data.
    /// @param metadata The calibration table metadata.
    /// @param dataMetadata The calibration table data metadata.
    /// @return The updated dataMetadata array.
    /// /
    public CalibrationTableRow[] Update(CalibrationTableMetadata metadata, CalibrationTableRow[] dataMetadata)
    {
        lock (_sync)
        {
            if (dataMetadata.Length == 0)
            {
                dataMetadata = CreateDefault().ToArray();
            }
            Metadata = metadata;
            Size = (ushort)dataMetadata.Length;
            _selectedTable = null;
            SortedDictionary<ulong,SortedDictionary<float,SortedDictionary<double,double>>> pointTable = new();
            foreach (var point in dataMetadata)
            {
                if (pointTable.TryGetValue(point.FrequencyHz,out var freqTable))
                {
                
                }
                else
                {
                    pointTable.Add(point.FrequencyHz, freqTable = new SortedDictionary<float, SortedDictionary<double,double>>());
                }
                if (freqTable.TryGetValue(point.RefPower,out var table))
                {
                    table.Add(point.RefValue - point.Adjustment,point.RefValue);
                }
                else
                {
                    freqTable.Add(point.RefPower, new SortedDictionary<double,double>
                    {
                        {point.RefValue - point.Adjustment,point.RefValue}
                    });
                }
            }
            _tables.Clear();
            foreach (var subItem in pointTable)
            {
                var subItemTable = new SortedDictionary<float, PiecewiseLinearFunction>();
                _tables.Add(subItem.Key, subItemTable);
                foreach (var subSubItems in subItem.Value)
                {
                    var array = new double[subSubItems.Value.Count,2];
                    var i = 0;
                    foreach (var item in subSubItems.Value)
                    {
                        array[i, 0] = item.Key;
                        array[i, 1] = item.Value;
                        ++i;
                    }
                    subItemTable.Add(subSubItems.Key, new PiecewiseLinearFunction(array));
                }
            }
        }
        SetMode(_freq, _refPower);
        return dataMetadata;
    }

    /// <summary>
    /// Tries to read a calibration table row at the specified index.
    /// </summary>
    /// <param name="rowindex">The index of the calibration table row.</param>
    /// <param name="row">The output calibration table row if found, otherwise null.</param>
    /// <returns>True if the calibration table row was found, otherwise false.</returns>
    public bool TryReadCalibrationTableRow(ushort rowindex, out CalibrationTableRow? row)
    {
        row = default;
        if (rowindex >= Size) return false;
        var i = 0;
        lock (_sync)
        {
            foreach (var freq in _tables)
            {
                foreach (var pow in freq.Value)
                {
                    foreach (var point in pow.Value)
                    {
                        if (i == rowindex)
                        {
                            row = new CalibrationTableRow
                            {
                                FrequencyHz = freq.Key,
                                RefPower = pow.Key,
                                RefValue = (float)point.Value,
                                Adjustment = (float)(point.Value - point.Key),
                            };
                            return true;
                        }
                        ++i;
                    }
                }
            }
        }
        return false;
        
    }

    /// <summary>
    /// Sets the mode of the object based on the provided frequency and reference power level.
    /// </summary>
    /// <param name="freq">The frequency value to be set.</param>
    /// <param name="refPower">The reference power level to be set.</param>
    public void SetMode(ulong freq, float refPower)
    {
        _freq = freq;
        _refPower = refPower;
        lock (_sync)
        {
            var subTable = _tables.Select(x => new {table = x.Value, delta = Math.Abs((long)x.Key - (long)freq)}).MinBy(x => x.delta)?.table;
            if (subTable == null)
            {
                _selectedTable = null;
                return;
            }
            _selectedTable = subTable.Select(x => new {table = x.Value, delta = Math.Abs(x.Key - refPower)}).MinBy(x => x.delta)?.table;
        }
    }

    /// <summary>
    /// Gets the value from the selected table if it exists, otherwise returns the measured value.
    /// </summary>
    /// <param name="measuredValue">The measured value to retrieve from the table, if available.</param>
    /// <returns>The value from the selected table if it exists; otherwise, the measured value.</returns>
    public double this[double measuredValue]
    {
        get
        {
            if (IsEnabled == false) return measuredValue;
            lock (_sync)
            {
                return (float)(_selectedTable != null ? _selectedTable[measuredValue] : measuredValue);
            }
        }
    }
}