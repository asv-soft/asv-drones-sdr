using System.ComponentModel.Composition;
using Asv.Drones.Sdr.GnssSource;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.V2.Common;

namespace Asv.Drones.Sdr;

public static class DeviceClass
{
    public const string Virtual = "Virtual";
}
    

[ExportMode(AsvSdrCustomMode.AsvSdrCustomModeLlz, AsvSdrCustomModeFlag.AsvSdrCustomModeFlagLlz, DeviceClass.Virtual)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class LlzWorkMode : WorkModeBase
{
    private readonly IGnssSource _gnssSource;
    private ulong _freq;

    [ImportingConstructor]
    public LlzWorkMode(IGnssSource gnssSource)
    {
        _gnssSource = gnssSource;
    }
    
    public override async Task Init(ulong frequencyHz, CancellationToken cancel)
    {
        _freq = frequencyHz;
        await Task.Delay(1000, cancel);
    }

    public override AsvSdrCustomMode Mode => AsvSdrCustomMode.AsvSdrCustomModeLlz;
    public override void Fill(uint dataIndex, IPayload payload)
    {
        var data = (AsvSdrRecordDataLlzPayload)payload;
        data.DataIndex = dataIndex;
        
        
    }
}