using System.Buffers;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Asv.Cfg;
using Asv.Common;
using Asv.IO;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Newtonsoft.Json;
using NLog;

namespace Asv.Drones.Sdr.Core;

public class FileRecordStoreConfig
{
    public string RootFolder { get; set; } = "records";
}

[Export(typeof(IRecordStore))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class FileRecordStore : DisposableOnceWithCancel, IRecordStore
{
    public const int OnePageSize = 256;
    private const string MetadataFileName = "metadata.json";
    private const string DataFileName = "data.bin";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly RxValue<ushort> _count;
    private readonly RxValue<ulong> _size;
    private readonly string _rootFolder;
    private readonly object _sync = new();
    private readonly List<Guid> _recordsList = new();
    private readonly HashSet<Guid> _recordsSet = new();
    private Guid _writeHandle = Guid.Empty;
    private readonly SortedList<Guid,RecordDataWrapper> _readerHandles = new();

    [ImportingConstructor]
    public FileRecordStore(IConfiguration config)
    {
        var storeConfig = config.Get<FileRecordStoreConfig>();
        _rootFolder = storeConfig.RootFolder;
        if (Directory.Exists(_rootFolder) == false)
        {
            Directory.CreateDirectory(_rootFolder);
        }

        long size = 0;
        foreach (var path in Directory.EnumerateDirectories(_rootFolder).Select(_=>new DirectoryInfo(_)).OrderBy(_=>_.CreationTimeUtc))
        {
            var recordId = Path.GetFileName(path.Name);
            if (Guid.TryParse(recordId, out var guid) == false) continue;
            var metadataFile = GetMetadataFileName(guid);
            var dataFile = GetRecordDataFileName(guid);
            if (File.Exists(metadataFile) == false || File.Exists(dataFile) == false) continue;
            size+=new FileInfo(metadataFile).Length;
            size+=new FileInfo(dataFile).Length;
            _recordsList.Add(guid);
            _recordsSet.Add(guid);
        }
        Debug.Assert(_recordsList.Count== _recordsSet.Count);
        _count = new RxValue<ushort>((ushort)_recordsList.Count).DisposeItWith(Disposable);
        _size = new RxValue<ulong>((ulong)size).DisposeItWith(Disposable);
        Disposable.AddAction(() =>
        {
            _readerHandles.Values.ForEach(_=>_.Dispose());
            _readerHandles.Clear();
        });
    }

    
    public IRxValue<ushort> Count => _count;
    public IRxValue<ulong> Size => _size;
    
    public IReadOnlyList<Guid> GetRecords(ushort skip, ushort count)
    {
        lock (_sync)
        {
            var result = new List<Guid>();
            for (int i = skip; i < skip + count; i++)
            {
                if (i >= _recordsList.Count) break;
                result.Add(_recordsList[i]);
            }
            return result;
        }
    }

    public bool DeleteRecord(Guid recordId)
    {
        lock (_sync)
        {
            if (_writeHandle == recordId)
                throw new Exception("Record is open for write");
            if (_recordsSet.TryGetValue(recordId, out var guid) == false) return false;
            var folder = GetRecordFolderName(guid);
            if (Directory.Exists(folder))
                Directory.Delete(folder,true);
            _recordsSet.Remove(guid);
            _recordsList.Remove(guid);
            Debug.Assert(_recordsList.Count == _recordsSet.Count);
            _count.Value = (ushort)_recordsList.Count;
            return true;
        }
    }

   
 
    public IRecordDataWriter OpenWrite(string recordName, AsvSdrCustomMode mode, ulong frequencyHz)
    {
        lock (_sync)
        {   
            if (_writeHandle != Guid.Empty)
                throw new Exception("Record is open for write");
            _writeHandle = Guid.NewGuid();
            _recordsList.Add(_writeHandle);
            _recordsSet.Add(_writeHandle);
            Debug.Assert(_recordsList.Count == _recordsSet.Count);
            _count.Value = (ushort)_recordsList.Count;
            var folder = GetRecordFolderName(_writeHandle);
            if (Directory.Exists(folder))
            {
                Debug.Assert(false,"Just create GUID and found exist directory");
            }
                
            Directory.CreateDirectory(folder);
            var metadata = new RecordJsonMetadata
            {
                Name = recordName,
                Mode = mode,
                Frequency = frequencyHz,
            };
            var metadataFile = GetMetadataFileName(_writeHandle);
            File.WriteAllText(metadataFile,JsonConvert.SerializeObject(metadata));
            var filePath = GetRecordDataFileName(_writeHandle);
            using (var file = File.Create(filePath))
            {
                // just for create empty file
            }
            return new RecordDataWrapper(_writeHandle,filePath,metadataFile,SafaDisposeWriter);
        }
        
    }

    private void SafaDisposeWriter(RecordDataWrapper obj)
    {
        _writeHandle = Guid.Empty;
    }

    public IRecordDataReader OpenRead(Guid recordId)
    {
        lock (_sync)
        {
            if (_recordsSet.Contains(recordId) == false) 
                throw new Exception("Record not found");
            if (_writeHandle == recordId)
                throw new Exception("Record is open for write");
            if (_readerHandles.TryGetValue(recordId, out var handle) == false)
            {
                _readerHandles.Add(recordId, handle = new RecordDataWrapper(recordId, GetRecordDataFileName(recordId),GetMetadataFileName(recordId),SafaDisposeReader));    
            }
            return handle;
        }
    }

    private void SafaDisposeReader(RecordDataWrapper wrapper  )
    {
        lock (_sync)
        {
            _readerHandles.Remove(wrapper.RecordId);
        }
    }

    public bool Exist(Guid recordId)
    {
        lock (_sync)
        {
            return _recordsSet.Contains(recordId);    
        }
        
    }

    private string GetRecordFolderName(Guid id)
    {
        return Path.Combine(_rootFolder, id.ToString());
    }
    private string GetMetadataFileName(Guid recordId)
    {
        return Path.Combine(GetRecordFolderName(recordId), MetadataFileName);
    }
    private string GetRecordDataFileName(Guid sessionId)
    {
        return Path.Combine(GetRecordFolderName(sessionId), DataFileName);
    }
}

public class RecordDataWrapper : DisposableOnceWithCancel, IRecordDataReader,IRecordDataWriter
{
    private readonly string _dataFileName;
    private readonly string _metadataFileName;
    private readonly object _sync = new();
    private FileStream? _dataFile;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private RecordJsonMetadata? _metadata;
    private DateTime? _startWrite;

    public RecordDataWrapper(Guid recordId, string dataFileName, string metadataFileName, Action<RecordDataWrapper> whenDisposed)
    {
        RecordId = recordId;
        _dataFileName = dataFileName;
        _metadataFileName = metadataFileName;
        Disposable.AddAction(()=>
        {
            whenDisposed(this);
            if (_startWrite == null) return;
            var time = DateTime.Now - _startWrite.Value;
            Metadata.DurationSec = time;
            SaveMetadata();
        });
    }

    private FileStream DataFile => _dataFile ??= File.Open(_dataFileName, FileMode.Open, FileAccess.ReadWrite).DisposeItWith(Disposable);

    private RecordJsonMetadata Metadata
    {
        get
        {
            return _metadata ??=
                JsonConvert.DeserializeObject<RecordJsonMetadata>(File.ReadAllText(_metadataFileName)) ??
                throw new Exception("Metadata not found");
        }
    }
    
    private void SaveMetadata()
    {
        File.WriteAllText(_metadataFileName,JsonConvert.SerializeObject(Metadata));
    }
    public AsvSdrCustomMode Mode => Metadata.Mode;
    public Guid RecordId { get; }
    public void Write(uint dataIndex, AsvSdrCustomMode currentModeMode, IPayload payload)
    {
        lock (_sync)
        {
            if (_startWrite == null) _startWrite = DateTime.Now;
            var data = ArrayPool<byte>.Shared.Rent(FileRecordStore.OnePageSize);
            try
            {
                var span = new Span<byte>(data, 0, FileRecordStore.OnePageSize);
                var crcSpan = span;
                payload.Serialize(ref span);
                var crc = Crc32Q.Calc(crcSpan, crcSpan.Length, 0);
                BinSerialize.WriteUInt(ref crcSpan, crc);
                var writeBuffer = new ReadOnlySpan<byte>(data, 0, FileRecordStore.OnePageSize);
                DataFile.Seek(dataIndex*FileRecordStore.OnePageSize, SeekOrigin.Begin);
                DataFile.Write(writeBuffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }
    }

    public Guid SetTag(AsvSdrRecordTagType type, string name, byte[] value)
    {
        lock (_sync)
        {
            var id = Guid.NewGuid();
            Metadata.Tags.Add(new TagJsonMetadata
            {
                Type = type,
                Name = name,
                Value = value,
            });
            SaveMetadata();
            return id;
        }
    }

    public void Fill(AsvSdrRecordPayload payload)
    {
        lock (_sync)
        {
            var fileInfo = new FileInfo(_dataFileName);
            RecordId.TryWriteBytes(payload.RecordGuid);
            MavlinkTypesHelper.SetString(payload.RecordName, Metadata.Name);
            payload.Frequency = Metadata.Frequency;
            payload.CreatedUnixUs =
                MavlinkTypesHelper.ToUnixTimeUs(fileInfo.CreationTimeUtc);
            payload.DataType = Metadata.Mode;
            payload.DurationSec = (uint)Metadata.DurationSec.TotalSeconds;
            payload.DataCount = (uint)(fileInfo.Length / FileRecordStore.OnePageSize);
            payload.Size = (uint)fileInfo.Length;
            payload.TagCount = (ushort)Metadata.Tags.Count;
        }
    }

    public void Fill(Guid tag, AsvSdrRecordTagPayload asvSdrRecordTagPayload)
    {
        lock (_sync)
        {
            Metadata.Tags.FirstOrDefault(_=>_.Id == tag)?.Fill(RecordId, asvSdrRecordTagPayload);
        }
    }

    public IReadOnlyList<Guid> GetTags(ushort skip, ushort count)
    {
        return Metadata.Tags.Select(_ => _.Id).ToImmutableList();
    }

    public bool DeleteTag(Guid tagId)
    {
        lock (_sync)
        {
            
            var tag = Metadata.Tags.FirstOrDefault(i => i.Id == tagId);
            if (tag == null) return false;
            Metadata.Tags.Remove(tag);
            SaveMetadata();
            return true;
        }
    }

    public uint GetRecordsCount(uint skip, uint count)
    {
        lock (_sync)
        {
            var start = skip * FileRecordStore.OnePageSize;
            var stop = count * FileRecordStore.OnePageSize;
            if (stop < start) return 0;
            var fileLength = new FileInfo(_dataFileName).Length;
            if (start > fileLength) return 0;
            stop = (uint)Math.Min(fileLength, stop);
            return (stop - start) / FileRecordStore.OnePageSize;
        }
    }

    public void Fill(uint index, IPayload payload)
    {
        lock (_sync)
        {
            var start = index * FileRecordStore.OnePageSize;
            var stop = start + FileRecordStore.OnePageSize;
            if (DataFile.Length > stop) throw new Exception("DataFile.Length > stop");
            var data = ArrayPool<byte>.Shared.Rent(FileRecordStore.OnePageSize);
            try
            {
                var span = new ReadOnlySpan<byte>(data, 0, FileRecordStore.OnePageSize);
                var buffer = new Span<byte>(data, 0, FileRecordStore.OnePageSize);
                DataFile.Seek(start, SeekOrigin.Begin);
                var readBytes = DataFile.Read(buffer);
                if (readBytes != FileRecordStore.OnePageSize) throw new Exception("Readed != FileRecordStore.OnePageSize");
                var readCrc = BinSerialize.ReadUInt(ref span);
                var crcSpan = span;
                payload.Deserialize(ref span);
                var calculatedCrc = Crc32Q.Calc(crcSpan, crcSpan.Length, 0);
                if (calculatedCrc != readCrc) throw new Exception("calculatedCrc != readCrc");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }
    }
    
}




public class RecordJsonMetadata
{
    public List<TagJsonMetadata> Tags { get; } = new();
    public AsvSdrCustomMode Mode { get; set; }
    public string Name { get; set; }
    public ulong Frequency { get; set; }
    public TimeSpan DurationSec { get; set; }
}

public class TagJsonMetadata
{
    public void Fill(Guid recordId, AsvSdrRecordTagPayload payload)
    {
        recordId.TryWriteBytes(payload.RecordGuid);
        Id.TryWriteBytes(payload.TagGuid);
        payload.TagType = Type;
        MavlinkTypesHelper.SetString(payload.TagName, Name);
        Value?.CopyTo(payload.TagValue,0);
    }
    public Guid Id { get; set; }
    public AsvSdrRecordTagType Type { get; set; }
    public string? Name { get; set; }
    public byte[]? Value { get; set; }
}