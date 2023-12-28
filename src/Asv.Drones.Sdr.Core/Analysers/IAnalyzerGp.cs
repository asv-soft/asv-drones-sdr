using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents an analyzer for GP data.
/// </summary>
public interface IAnalyzerGp : IAnalyzer
{
    /// <summary>
    /// Fills the data payload of the ASV SDR record.
    /// </summary>
    /// <param name="payload">The data payload to fill.</param>
    /// <remarks>
    /// This method is responsible for populating the data payload of the ASV SDR record
    /// with the provided payload. This data will be used for further processing.
    /// </remarks>
    void Fill(AsvSdrRecordDataGpPayload payload);
}