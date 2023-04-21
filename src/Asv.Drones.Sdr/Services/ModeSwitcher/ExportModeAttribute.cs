using System.ComponentModel.Composition;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
sealed class ExportModeAttribute : ExportAttribute,IWorkModeMetadata
{
    public ExportModeAttribute(AsvSdrCustomMode mode, AsvSdrCustomModeFlag flag, string deviceClass)
        :base(GetContractName(mode,deviceClass),typeof(IWorkMode))
    {
        Mode = mode;
        Flag = flag;
    }

    public AsvSdrCustomMode Mode { get; }
    public AsvSdrCustomModeFlag Flag { get; }

    public static string? GetContractName(AsvSdrCustomMode item, string deviceClass)
    {
        return $"{item:G}:{deviceClass}";
    }
}

public interface IWorkModeMetadata
{
    AsvSdrCustomMode Mode { get; }
    AsvSdrCustomModeFlag Flag { get; }
}