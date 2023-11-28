using Asv.Common;

namespace Asv.Drones.Sdr.Core;



public interface IAnalyzer:IDisposable
{
    IRxValue<float> SignalOverflowIndicator { get; }
    Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel);
}