using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr;

public class IdleWorkMode : WorkModeBase
{
    public static IWorkMode Instance { get; } = new IdleWorkMode();
    
    public override Task Init(ulong frequencyHz, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    public override AsvSdrCustomMode Mode => AsvSdrCustomMode.AsvSdrCustomModeIdle;
    public override void Fill(uint dataIndex, IPayload payload)
    {
        throw new NotImplementedException();
    }
}