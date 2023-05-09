using System.ComponentModel.Composition;
using Asv.Drones.Sdr.Core;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Virtual;

[ExportAnalyzer(AsvSdrCustomMode.AsvSdrCustomModeVor, "Virtual")]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VirtualAnalyzerVor : IAnalyzerVor
{
    public Task Init(ulong frequencyHz, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    public void Fill(AsvSdrRecordDataVorPayload payload)
    {
        
    }

    public void Dispose()
    {
    }
}