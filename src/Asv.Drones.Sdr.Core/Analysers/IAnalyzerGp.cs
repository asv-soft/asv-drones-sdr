using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public interface IAnalyzerGp : IAnalyzer
{
    void Fill(AsvSdrRecordDataGpPayload payload);
}