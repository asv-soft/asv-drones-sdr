using System.ComponentModel.Composition;

namespace Asv.Drones.Sdr.Core;

/// <summary>
/// Represents an attribute that specifies the export of a module.
/// </summary>
/// <remarks>
/// This attribute is used to mark a class as an exported module, indicating that it can be discovered and used by the application.
/// </remarks>
[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
public class ExportModuleAttribute : ExportAttribute,IModuleMetadata
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    /// <returns>The name of the property.</returns>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the array of dependencies.
    /// </summary>
    /// <remarks>
    /// Use this property to manage the dependencies of an object or component.
    /// The dependencies are represented as a string array, where each string represents a dependency.
    /// </remarks>
    /// <value>
    /// The array of dependencies.
    /// </value>
    public string[] Dependency { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportModuleAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the exported module.</param>
    /// <param name="dependency">The dependencies of the exported module.</param>
    public ExportModuleAttribute(string name, params string[] dependency)
        :base(typeof(IModule))
    {
        Name = name;
        Dependency = dependency;
    }

}

/// <summary>
/// Represents the metadata of a module.
/// </summary>
public interface IModuleMetadata
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    /// <value>
    /// The name of the property.
    /// </value>
    string Name { get; }

    /// <summary>
    /// Gets or sets the dependencies of the current object.
    /// </summary>
    /// <value>
    /// The dependencies of the current object, represented as an array of strings.
    /// </value>
    string[] Dependency { get; }
}

/// <summary>
/// Represents a module that can be initialized and disposed.
/// </summary>
public interface IModule:IDisposable
{
    /// <summary>
    /// Initializes the current instance.
    /// </summary>
    /// <remarks>
    /// This method is responsible for initializing the current instance before any other operations can be performed.
    /// </remarks>
    void Init();
}