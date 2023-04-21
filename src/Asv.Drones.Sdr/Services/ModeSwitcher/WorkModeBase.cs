using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr;

public abstract class WorkModeBase: DisposableOnceWithCancel,IWorkMode
{
    public abstract Task Init(ulong frequencyHz, CancellationToken cancel);
    public abstract AsvSdrCustomMode Mode { get; }
    public abstract void Fill(uint dataIndex, IPayload payload);
}