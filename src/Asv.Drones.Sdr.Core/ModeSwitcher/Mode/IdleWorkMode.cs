using System.ComponentModel.Composition;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public class IdleWorkMode : IWorkMode
{
    public static IWorkMode Instance { get; } = new IdleWorkMode();
    
    public Task Init(ulong frequencyHz, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    public AsvSdrCustomMode Mode => AsvSdrCustomMode.AsvSdrCustomModeIdle;
    public void Fill(RecordId writerRecordId, uint dataIndex, IPayload payload)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        
    }
}