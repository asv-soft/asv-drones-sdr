using System.ComponentModel.Composition;
using Asv.Cfg;
using Asv.Cfg.Json;
using Asv.Common;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;




namespace Asv.Drones.Sdr.Core;

public class CalibrationProviderConfig
{
    public string CalibrationFolder { get; set; } = "calibration";
}

[Export(typeof(ICalibrationProvider))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CalibrationProvider:DisposableOnceWithCancel, ICalibrationProvider
{
    private readonly RxValue<bool> _isInProgress;
    private readonly JsonConfiguration _file;
    private readonly List<ICalibrationItem> _tables;

    [ImportingConstructor]
    public CalibrationProvider([ImportMany]IEnumerable<ICalibrationItem> tables, IConfiguration config)
    {
        _isInProgress = new RxValue<bool>(false).DisposeItWith(Disposable);
        var cfg = config.Get<CalibrationProviderConfig>();
        CalibrationFolder = cfg.CalibrationFolder;
        _file = new JsonConfiguration(cfg.CalibrationFolder).DisposeItWith(Disposable);
        ushort counter = 0;
        _tables = new List<ICalibrationItem>();
        foreach (var table in tables)
        {
            CalibrationTablePod data;
            if (_file.Exist(table.Name) == false)
            {
                _file.Set(table.Name, data = new CalibrationTablePod
                {
                    Name = table.Name,
                    Metadata = new CalibrationTableMetadata
                    {
                        Updated = DateTime.Now,
                    },
                    Rows = table.CreateDefault().ToArray(),
                });
            }
            else
            {
                data = _file.Get<CalibrationTablePod>(table.Name);
            }
            table.Update(data.Metadata, data.Rows);
            _tables.Add(table);
            counter++;
        }
        TableCount = counter;
    }

    public string CalibrationFolder { get; }
    public IRxValue<bool> IsInProgress => _isInProgress;
    public ushort TableCount { get; }
    public Task<MavResult> StartCalibration(CancellationToken cancel)
    {
        _isInProgress.OnNext(true);
        _tables.ForEach(x => x.IsEnabled = false);
        return Task.FromResult(MavResult.MavResultAccepted);
    }

    public Task<MavResult> StopCalibration(CancellationToken cancel)
    {
        _isInProgress.OnNext(false);
        _tables.ForEach(x => x.IsEnabled = true);
        return Task.FromResult(MavResult.MavResultAccepted);
    }

    public void WriteCalibrationTable(ushort tableIndex,CalibrationTableMetadata metadata, CalibrationTableRow[] items)
    {
        if (tableIndex >= _tables.Count)
        {
            throw new AsvSdrException("Invalid table index");
        }
        var table = _tables[tableIndex];
        items = table.Update(metadata, items);
        _file.Set(table.Name, new CalibrationTablePod
        {
            Name = table.Name,
            Metadata = metadata,
            Rows = items,
        });
    }

    public bool TryReadCalibrationTableInfo(ushort tableIndex, out string? name, out ushort? size,
        out CalibrationTableMetadata? metadata)
    {
        if (tableIndex >= _tables.Count)
        {
            name = null;
            size = null;
            metadata = null;
            return false;
        }

        var table = _tables[tableIndex];
        name = table.Name;
        size = table.Size;
        metadata = table.Metadata;
        return true;
    }

    public bool TryReadCalibrationTableRow(ushort tableIndex, ushort rowindex, out CalibrationTableRow? row)
    {
        if (tableIndex >= _tables.Count)
        {
            row = null;
            return false;
        }
        var table = _tables[tableIndex];
        return table.TryReadCalibrationTableRow(rowindex, out row);
    }

    public void SetMode(ulong freq, float refPower)
    {
        foreach (var table in _tables)
        {
            table.SetMode(freq, refPower);
        }
    }

    public ICalibrationItem? this[string tableName]
    {
        get
        {
            return _tables.FirstOrDefault(x => x.Name == tableName);
        }
    }
}