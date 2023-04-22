using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public interface IRecordStore
{
    IRxValue<ushort> Count { get; }
    IRxValue<ulong> Size { get; }
    IList<IRecordInfo> GetRecords(ushort reqSkip, ushort reqCount);
    IList<ITagInfo> GetTags(RecordId recordId, ushort reqSkip, ushort reqCount);
    bool DeleteRecord(RecordId recordId);
    bool DeleteTag(TagId tagId);
    IList<IRecordData> GetData(RecordId recordId, uint reqSkip, uint reqCount);
    void SetTag(ServerRecordTag tag);
    IRecordDataWriter OpenWrite(RecordId recordId);
    bool Exists(RecordId recordId);
}

public interface ITagInfo
{
    void Fill(AsvSdrRecordTagPayload obj);
}

public interface IRecordInfo
{
    void Fill(AsvSdrRecordPayload obj);
}

public interface IRecordData
{
    AsvSdrCustomMode Mode { get; }
    void Fill(IPayload obj);
}

public interface IRecordDataWriter:IDisposable
{
    RecordId RecordId { get; }
    void SetTag(ServerRecordTag tag);
    void Write(uint dataIndex, AsvSdrCustomMode currentModeMode, IPayload payload);
    
}
