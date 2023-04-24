using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;

namespace Asv.Drones.Sdr.Core;

public interface IRecordStore
{
    IRxValue<ushort> Count { get; }
    IRxValue<ulong> Size { get; }
    IReadOnlyList<Guid> GetRecords(ushort skip, ushort count);
    bool DeleteRecord(Guid recordId);
    IRecordDataWriter OpenWrite(string recordName, AsvSdrCustomMode mode, ulong frequency);
    IRecordDataReader OpenRead(Guid recordId);
    bool Exist(Guid recordId);
}

public interface IRecordDataReader:IDisposable
{
    void Fill(AsvSdrRecordPayload payload);
    void Fill(Guid tag, AsvSdrRecordTagPayload asvSdrRecordTagPayload);
    IReadOnlyList<Guid> GetTags(ushort skip, ushort count);
    bool DeleteTag(Guid tagId);
    uint GetRecordsCount(uint skip, uint count);
    void Fill(uint index, IPayload payload);
    AsvSdrCustomMode Mode { get; }
}

public interface IRecordDataWriter:IDisposable
{
    Guid RecordId { get; }
    void Write(uint dataIndex, AsvSdrCustomMode currentModeMode, IPayload payload);
    Guid SetTag(AsvSdrRecordTagType type, string name, byte[] value);
}
