using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public interface IAnalyzerVor : IAnalyzer
{
    void Fill(AsvSdrRecordDataVorPayload payload);
}