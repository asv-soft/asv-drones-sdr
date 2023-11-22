using Asv.Common;
using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core;

public abstract class PiecewiseLinearCalibrationItem : ICalibrationItem
{
    private readonly SortedDictionary<ulong,SortedDictionary<float,PiecewiseLinearFunctionDelete>> _tables = new();
    private readonly object _sync = new();
    private ulong _freq;
    private float _refPower;
    private PiecewiseLinearFunctionDelete? _selectedTable;
    public abstract string Name { get; }
    public ushort Size { get; private set; }
    public bool IsEnabled { get; set; }
    public CalibrationTableMetadata Metadata { get; private set; } = null!;
    public abstract CalibrationTableRow[] CreateDefault();
    public void Update(CalibrationTableMetadata metadata, CalibrationTableRow[] dataMetadata)
    {
        lock (_sync)
        {
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
                var subItemTable = new SortedDictionary<float, PiecewiseLinearFunctionDelete>();
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
                    subItemTable.Add(subSubItems.Key, new PiecewiseLinearFunctionDelete(array));
                }
            }
        }
        SetMode(_freq, _refPower);
    }

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