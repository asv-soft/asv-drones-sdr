using System.Reflection;

namespace Asv.Drones.Sdr;

/// A utility class that provides extension methods for accessing assembly information.
/// /
public static class AssemblyInfoExt
    {
        /// <summary>
        /// Retrieves the version of the specified assembly.
        /// </summary>
        /// <param name="src">The assembly from which to retrieve the version.</param>
        /// <returns>The version of the assembly.</returns>
        public static Version GetVersion(this Assembly src)
        {
            return src.GetName().Version;
        }

        /// <summary>
        /// Gets the informational version of the specified assembly.
        /// </summary>
        /// <param name="src">The assembly from which to retrieve the informational version.</param>
        /// <returns>The informational version of the assembly. Returns an empty string if no informational version attribute is found.</returns>
        public static string GetInformationalVersion(this Assembly src)
        {
            var attributes = src.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
            return attributes.Length == 0 ? "" : ((AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion;
        }

        /// <summary>
        /// Retrieves the title of the given assembly.
        /// </summary>
        /// <param name="src">The assembly to retrieve the title from.</param>
        /// <returns>The title of the assembly. If the title is not specified, it returns the filename of the assembly without extension.</returns>
        public static string GetTitle(this Assembly src)
        {
            var attributes = src.GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
            if (attributes.Length > 0)
            {
                var titleAttribute = (AssemblyTitleAttribute)attributes[0];
                if (titleAttribute.Title.Length > 0) return titleAttribute.Title;
            }
            return System.IO.Path.GetFileNameWithoutExtension(src.CodeBase);
        }

        /// <summary>
        /// Retrieves the product name of the given assembly.
        /// </summary>
        /// <param name="src">The assembly to retrieve the product name from.</param>
        /// <returns>The product name of the assembly. Returns an empty string if the assembly does not have a product name.</returns>
        public static string GetProductName(this Assembly src)
        {
            var attributes = src.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
            return attributes.Length == 0 ? "" : ((AssemblyProductAttribute)attributes[0]).Product;
        }

        /// <summary>
        /// Get the description of the given assembly.
        /// </summary>
        /// <param name="src">The assembly to retrieve the description from.</param>
        /// <returns>The description of the assembly, or an empty string if no description is found.</returns>
        public static string GetDescription(this Assembly src)
        {
            var attributes = src.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
            return attributes.Length == 0 ? "" : ((AssemblyDescriptionAttribute) attributes[0]).Description;
        }

        /// <summary>
        /// Returns the copyright holder of the specified assembly.
        /// </summary>
        /// <param name="src">The assembly to retrieve the copyright holder from.</param>
        /// <returns>The copyright holder of the specified assembly, or an empty string if not defined.</returns>
        public static string GetCopyrightHolder(this Assembly src)
        {
            var attributes = src.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            return attributes.Length == 0 ? "" : ((AssemblyCopyrightAttribute) attributes[0]).Copyright;
        }

        /// <summary>
        /// Retrieve the company name of the given assembly.
        /// </summary>
        /// <param name="src">The assembly to retrieve the company name from.</param>
        /// <returns>The company name of the assembly. Returns an empty string if the assembly does not have a company name attribute.</returns>
        public static string GetCompanyName(this Assembly src)
        {
            var attributes = src.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
            return attributes.Length == 0 ? "" : ((AssemblyCompanyAttribute)attributes[0]).Company;
        }
    }