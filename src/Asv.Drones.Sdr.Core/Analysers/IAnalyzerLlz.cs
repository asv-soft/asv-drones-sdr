using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents an analyzer for LLZ records.
/// </summary>
public interface IAnalyzerLlz:IAnalyzer
{
    /// <summary>
    /// Fills the specified payload with data provided by the AsvSdrRecordDataLlzPayload object.
    /// </summary>
    /// <param name="payload">The AsvSdrRecordDataLlzPayload object to fill.</param>
    void Fill(AsvSdrRecordDataLlzPayload payload);
}