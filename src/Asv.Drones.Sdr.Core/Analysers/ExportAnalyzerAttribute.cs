using System.ComponentModel.Composition;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents an attribute used to export an analyzer with metadata.
/// </summary>
[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ExportAnalyzerAttribute : ExportAttribute,IAnalyzerMetadata
{
    /// <summary>
    /// Represents an attribute used for exporting analyzers.
    /// </summary>
    /// <remarks>
    /// This attribute is used to specify the custom mode and name of an analyzer to be exported.
    /// </remarks>
    /// <param name="mode">The custom mode of the analyzer.</param>
    /// <param name="name">The name of the analyzer.</param>
    public ExportAnalyzerAttribute(AsvSdrCustomMode mode, string name)
        :base(GetContractName(mode),typeof(IAnalyzer))
    {
        Mode = mode;
        Name = name;
    }

    /// <summary>
    /// Gets the custom mode of the ASV SDR.
    /// </summary>
    /// <returns>The custom mode.</returns>
    public AsvSdrCustomMode Mode { get; }

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    /// <returns>The name of the property.</returns>
    public string Name { get; }

    /// <summary>
    /// Retrieves the contract name for the given AsvSdrCustomMode item.
    /// </summary>
    /// <param name="item">The AsvSdrCustomMode item for which to retrieve the contract name.</param>
    /// <returns>The contract name as a string. Returns null if the item is null.</returns>
    public static string? GetContractName(AsvSdrCustomMode item)
    {
        return $"{item:G}";
    }
}

/// <summary>
/// Represents the metadata information about an analyzer.
/// </summary>
public interface IAnalyzerMetadata
{
    /// <summary>
    /// Gets the custom mode.
    /// </summary>
    /// <value>
    /// The custom mode.
    /// </value>
    AsvSdrCustomMode Mode { get; }

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    /// <returns>The name of the property.</returns>
    string Name { get; }

}