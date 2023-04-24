using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public interface IAnalyzerIls:IAnalyzer
{
    void Fill(AsvSdrRecordDataLlzPayload payload);
}