using System.ComponentModel.Composition;

namespace Asv.Drones.Sdr.Core;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
sealed class ExportModuleAttribute : ExportAttribute,IModuleMetadata
{
    public string Name { get; }
    public string[] Dependency { get; }

    public ExportModuleAttribute(string name, params string[] dependency)
        :base(typeof(IModule))
    {
        Name = name;
        Dependency = dependency;
    }

}

public interface IModuleMetadata
{
    string Name { get; }
    string[] Dependency { get; }
}

public interface IModule:IDisposable
{
    void Init();
}