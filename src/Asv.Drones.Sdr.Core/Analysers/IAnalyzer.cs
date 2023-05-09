namespace Asv.Drones.Sdr.Core;



public interface IAnalyzer:IDisposable
{
    Task Init(ulong frequencyHz, CancellationToken cancel);
}