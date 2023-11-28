using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Drones.Sdr.Core;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Virtual;

[ExportAnalyzer(AsvSdrCustomMode.AsvSdrCustomModeGp, "Virtual")]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VirtualAnalyzerGp : IAnalyzerGp
{
    private readonly RxValue<float> _signalOverflowIndicator;

    public VirtualAnalyzerGp()
    {
        _signalOverflowIndicator = new RxValue<float>(Single.NaN);
    }

    public IRxValue<float> SignalOverflowIndicator => _signalOverflowIndicator;

    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    public void Fill(AsvSdrRecordDataGpPayload payload)
    {
    }

    public void Dispose()
    {
        _signalOverflowIndicator.Dispose();
    }
}