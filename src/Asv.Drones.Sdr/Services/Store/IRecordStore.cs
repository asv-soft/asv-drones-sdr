using System.Collections;
using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr;

public interface IRecordStore
{
    IRxValue<ushort> Count { get; }
    IRxValue<ulong> Size { get; }
    IList<RecordInfo> GetRecords(ushort reqSkip, ushort reqCount);
    IList<TagInfo> GetTags(RecordId recordId, ushort reqSkip, ushort reqCount);
    bool DeleteRecord(RecordId recordId);
    bool DeleteTag(TagId tagId);
    IList<RecordData> GetData(RecordId recordId, uint reqSkip, uint reqCount);
    void SetTag(ServerRecordTag tag);
    IRecordDataWriter OpenWrite(RecordId recordId);
    bool Exists(RecordId recordId);
}


public interface IRecordDataWriter:IDisposable
{
    void SetTag(ServerRecordTag tag);
    void Write(uint dataIndex, AsvSdrCustomMode currentModeMode, IPayload payload);
}

public class RecordData
{
    public AsvSdrCustomMode Mode { get; }
    public void Fill(IPayload obj)
    {
        throw new NotImplementedException();
    }
}

public class TagInfo
{
    public void Fill(AsvSdrRecordTagPayload obj)
    {
        
    }
}

public class RecordInfo
{
    public void Fill(AsvSdrRecordPayload obj)
    {
        
    }
}