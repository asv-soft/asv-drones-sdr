using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core;

public interface ICalibrationItem
{
    string Name { get; }
    ushort Size { get; }
    bool IsEnabled { get; set; }
    public CalibrationTableMetadata Metadata { get; }
    CalibrationTableRow[] CreateDefault();
    void Update(CalibrationTableMetadata metadata, CalibrationTableRow[] dataMetadata);
    bool TryReadCalibrationTableRow(ushort rowindex, out CalibrationTableRow? row);
    void SetMode(ulong freq, float refPower);
    double this[double measuredValue] { get; }
}