using System.ComponentModel.Composition;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ExportModeAttribute : ExportAttribute,IWorkModeMetadata
{
    public ExportModeAttribute(AsvSdrCustomMode mode, AsvSdrCustomModeFlag flag)
        :base(GetContractName(mode),typeof(IWorkMode))
    {
        Mode = mode;
        Flag = flag;
    }

    public AsvSdrCustomMode Mode { get; }
    public AsvSdrCustomModeFlag Flag { get; }

    public static string? GetContractName(AsvSdrCustomMode item)
    {
        return $"{item:G}";
    }
}

public interface IWorkModeMetadata
{
    AsvSdrCustomMode Mode { get; }
    AsvSdrCustomModeFlag Flag { get; }
}