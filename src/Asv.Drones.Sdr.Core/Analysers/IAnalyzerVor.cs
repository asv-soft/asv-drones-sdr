using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents an interface for analyzing VOR data.
/// </summary>
public interface IAnalyzerVor : IAnalyzer
{
    /// <summary>
    /// Fills the specified AsvSdrRecordDataVorPayload with data. </summary> <param name="payload">The AsvSdrRecordDataVorPayload to be filled with data.</param>
    /// /
    void Fill(AsvSdrRecordDataVorPayload payload);
}