using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Mavlink;

namespace Asv.Drones.Sdr.Core;

[Export(typeof(IRecordStore))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class FileRecordStore : DisposableOnceWithCancel, IRecordStore
{
    private readonly RxValue<ushort> _count;
    private readonly RxValue<ulong> _size;

    [ImportingConstructor]
    public FileRecordStore()
    {
        _count = new RxValue<ushort>(0).DisposeItWith(Disposable);
        _size = new RxValue<ulong>(0).DisposeItWith(Disposable);
    }

    public IRxValue<ushort> Count => _count;

    public IRxValue<ulong> Size => _size;

    public IList<IRecordInfo> GetRecords(ushort reqSkip, ushort reqCount)
    {
        throw new NotImplementedException();
    }

    public IList<ITagInfo> GetTags(RecordId recordId, ushort reqSkip, ushort reqCount)
    {
        throw new NotImplementedException();
    }

    public bool DeleteRecord(RecordId recordId)
    {
        throw new NotImplementedException();
    }

    public bool DeleteTag(TagId tagId)
    {
        throw new NotImplementedException();
    }

    public IList<IRecordData> GetData(RecordId recordId, uint reqSkip, uint reqCount)
    {
        throw new NotImplementedException();
    }

    public void SetTag(ServerRecordTag tag)
    {
        throw new NotImplementedException();
    }

    public IRecordDataWriter OpenWrite(RecordId recordId)
    {
        throw new NotImplementedException();
    }

    public bool Exists(RecordId recordId)
    {
        throw new NotImplementedException();
    }
}