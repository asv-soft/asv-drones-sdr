using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;



public interface IWorkMode:IDisposable
{
    AsvSdrCustomMode Mode { get; }
    Task Init(ulong frequencyHz, CancellationToken cancel);
    void Fill(RecordId writerRecordId, uint dataIndex, IPayload payload);
   
}