using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Drones.Sdr.Core;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Virtual;

[ExportAnalyzer(AsvSdrCustomMode.AsvSdrCustomModeLlz, "Virtual")]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VirtualAnalyzerLlz : IAnalyzerLlz
{
    private readonly NormalRandom _random;

    [ImportingConstructor]
    public VirtualAnalyzerLlz()
    {
        _random = new NormalRandom();
    }
    public Task Init(ulong frequencyHz, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    public void Fill(AsvSdrRecordDataLlzPayload payload)
    {
        // payload.CrsCarrierOffset = .CrsCarrierOffset;
        // payload.CrsPower = _gnssSource.CrsPower;
        payload.CrsAm90 = _random.NextSingle();
        // payload.CrsAm150 = _gnssSource.CrsAm150;
        // payload.CrsFreq90 = _gnssSource.CrsFreq90;
        // payload.CrsFreq150 = _gnssSource.CrsFreq150;
        //
        // payload.ClrCarrierOffset = _gnssSource.ClrCarrierOffset;
        // payload.ClrPower = _gnssSource.ClrPower;
        // payload.ClrAm90 = _gnssSource.ClrAm90;
        // payload.ClrAm150 = _gnssSource.ClrAm150;
        // payload.ClrFreq90 = _gnssSource.ClrFreq90;
        // payload.ClrFreq150 = _gnssSource.ClrFreq150;
        //
        // payload.TotalCarrierOffset = _gnssSource.TotalCarrierOffset;
        // payload.TotalFreq = _gnssSource.TotalFreq;
        // payload.TotalPower = _gnssSource.TotalPower;
        // payload.TotalFieldStrength = _gnssSource.TotalFieldStrength;
        payload.TotalAm90 = _random.NextSingle();;
        // payload.TotalAm150 = _gnssSource.TotalAm150;
        //
        //
        // payload.TotalFreq90 = _gnssSource.TotalFreq90;
        // payload.TotalFreq150 = _gnssSource.TotalFreq150;
        // payload.CodeIdFreq1020 = _gnssSource.CodeIdFreq1020;
        //
        // payload.Phi90CrsVsClr = _gnssSource.Phi90CrsVsClr;
        // payload.Phi150CrsVsClr = _gnssSource.Phi150CrsVsClr;
        // payload.CodeIdAm1020 = _gnssSource.CodeIdAm1020;
        //
        // payload.MeasureTime = _gnssSource.MeasureTime;
    }

    public void Dispose()
    {
    }
}