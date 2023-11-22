using System.ComponentModel.Composition;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public class IdleWorkMode : IWorkMode
{
    public static IWorkMode Instance { get; } = new IdleWorkMode();

    public ulong FrequencyHz => 0;

    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    public AsvSdrCustomMode Mode => AsvSdrCustomMode.AsvSdrCustomModeIdle;
    public void ReadData(Guid writerRecordId, uint dataIndex, IPayload payload)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        
    }
}