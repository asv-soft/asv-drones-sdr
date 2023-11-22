namespace Asv.Drones.Sdr.Core;



public interface IAnalyzer:IDisposable
{
    Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel);
}