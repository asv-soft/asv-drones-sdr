using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public interface IAnalyzerLlz:IAnalyzer
{
    void Fill(AsvSdrRecordDataLlzPayload payload);
}