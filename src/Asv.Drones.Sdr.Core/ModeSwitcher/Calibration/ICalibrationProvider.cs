using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr.Core;

public interface ICalibrationProvider:IDisposable
{
    IRxValue<bool> IsInProgress { get; }
    ushort TableCount { get; }
    Task<MavResult> StartCalibration(CancellationToken cancel);
    Task<MavResult> StopCalibration(CancellationToken cancel);
    void WriteCalibrationTable(ushort tableIndex,CalibrationTableMetadata metadata, CalibrationTableRow[] items);
    bool TryReadCalibrationTableInfo(ushort tableIndex, out string? name, out ushort? size, out CalibrationTableMetadata? metadata);
    bool TryReadCalibrationTableRow(ushort tableIndex, ushort rowindex, out CalibrationTableRow? row);
    void SetMode(ulong freq, float refPower);
    ICalibrationItem? this[string tableName] { get; }
}