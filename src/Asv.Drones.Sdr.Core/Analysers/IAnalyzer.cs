namespace Asv.Drones.Sdr.Core;



public interface IAnalyzer
{
    Task Init(ulong frequencyHz, CancellationToken cancel);
}