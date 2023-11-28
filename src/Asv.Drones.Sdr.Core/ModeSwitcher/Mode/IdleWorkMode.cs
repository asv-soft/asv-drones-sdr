using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public class IdleWorkMode : IWorkMode
{
    private readonly RxValue<float> _signalOverflowIndicator = new(Single.NaN);

    public static IWorkMode Instance { get; } = new IdleWorkMode();

    public ulong FrequencyHz => 0;

    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    public IRxValue<float> SignalOverflowIndicator => _signalOverflowIndicator;

    public AsvSdrCustomMode Mode => AsvSdrCustomMode.AsvSdrCustomModeIdle;
    public void ReadData(Guid writerRecordId, uint dataIndex, IPayload payload)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _signalOverflowIndicator.Dispose();
    }
}