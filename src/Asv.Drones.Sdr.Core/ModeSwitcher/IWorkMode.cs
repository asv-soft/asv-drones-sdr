using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;



public interface IWorkMode:IDisposable
{
    IRxValue<float> SignalOverflowIndicator { get; }
    AsvSdrCustomMode Mode { get; }
    ulong FrequencyHz { get; }
    Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel);
    void ReadData(Guid writerRecordId, uint dataIndex, IPayload payload);
}