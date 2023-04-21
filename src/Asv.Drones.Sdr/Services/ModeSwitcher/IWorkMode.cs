using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr;



public interface IWorkMode:IDisposable
{
    AsvSdrCustomMode Mode { get; }
    Task Init(ulong frequencyHz, CancellationToken cancel);
    void Fill(uint dataIndex, IPayload payload);
   
}