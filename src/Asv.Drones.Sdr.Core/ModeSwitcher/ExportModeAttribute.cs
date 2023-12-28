using System.ComponentModel.Composition;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core
{
    /// <summary>
    /// Represents a custom attribute for exporting types into the MEF (Managed Extensibility Framework).
    /// </summary>
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportModeAttribute : ExportAttribute, IWorkModeMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportModeAttribute"/> class.
        /// </summary>
        /// <param name="mode">Specifies the custom mode for ASV SDR (Software Defined Radio).</param>
        /// <param name="flag">Specifies an additional flag for the custom mode.</param>
        public ExportModeAttribute(AsvSdrCustomMode mode, AsvSdrCustomModeFlag flag)
            :base(GetContractName(mode),typeof(IWorkMode))
        {
            Mode = mode;
            Flag = flag;
        }

        /// <summary>
        /// Gets the custom mode for ASV SDR (Software Defined Radio).
        /// </summary>
        public AsvSdrCustomMode Mode { get; }

        /// <summary>
        /// Gets an additional flag for the custom mode.
        /// </summary>
        public AsvSdrCustomModeFlag Flag { get; }

        /// <summary>
        /// Retrieves the string representation of the contract name based on the specified item.
        /// </summary>
        /// <param name="item">The target item for which the contract name should be generated.</param>
        /// <returns>The contract name.</returns>
        public static string? GetContractName(AsvSdrCustomMode item)
        {
            return $"{item:G}";
        }
    }

    /// <summary>
    /// Represents an interface for defining metadata about work modes.
    /// </summary>
    public interface IWorkModeMetadata
    {
        /// <summary>
        /// Gets the custom mode for ASV SDR (Software Defined Radio).
        /// </summary>
        AsvSdrCustomMode Mode { get; }

        /// <summary>
        /// Gets an additional flag for the custom mode.
        /// </summary>
        AsvSdrCustomModeFlag Flag { get; }
    }
}