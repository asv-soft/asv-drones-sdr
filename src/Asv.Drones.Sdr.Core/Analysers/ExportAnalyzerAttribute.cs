using System.ComponentModel.Composition;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ExportAnalyzerAttribute : ExportAttribute,IAnalyzerMetadata
{
    public ExportAnalyzerAttribute(AsvSdrCustomMode mode, string name)
        :base(GetContractName(mode),typeof(IAnalyzer))
    {
        Mode = mode;
        Name = name;
    }

    public AsvSdrCustomMode Mode { get; }
    public string Name { get; }

    public static string? GetContractName(AsvSdrCustomMode item)
    {
        return $"{item:G}";
    }
}

public interface IAnalyzerMetadata
{
    AsvSdrCustomMode Mode { get; }
    string Name { get; }

}