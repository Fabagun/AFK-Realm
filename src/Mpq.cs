using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CoAInstaller
{
    /// <summary>
    /// Just enough of the MPQ format of the game client to read a file out of its archives and to write a small
    /// archive of our own: format 1 headers, classic hash and block tables, files stored whole or compressed with
    /// zlib. What it cannot read (encrypted files, other compressions) it says so.
    /// </summary>
    static class MpqCrypt
    {
        static readonly uint[] Table = Build();

        static uint[] Build()
        {
            var table = new uint[0x500];
            uint seed = 0x00100001;
            for (uint index1 = 0; index1 < 0x100; index1++)
                for (uint index2 = index1, i = 0; i < 5; i++, index2 += 0x100)
                {
                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    uint high = (seed & 0xFFFF) << 0x10;
                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    table[index2] = high | (seed & 0xFFFF);
                }
            return table;
        }

        public const uint TableOffset = 0, HashA = 1, HashB = 2, FileKey = 3;

        public static uint Hash(string text, uint type)
        {
            uint seed1 = 0x7FED7FED, seed2 = 0xEEEEEEEE;
            foreach (char raw in text.ToUpperInvariant().Replace('/', '\\'))
            {
                uint ch = (byte)raw;
                seed1 = Table[(type << 8) + ch] ^ (seed1 + seed2);
                seed2 = ch + seed1 + seed2 + (seed2 << 5) + 3;
            }
            return seed1;
        }

        public static void Decrypt(uint[] data, uint key)
        {
            uint seed = 0xEEEEEEEE;
            for (int i = 0; i < data.Length; i++)
            {
                seed += Table[0x400 + (key & 0xFF)];
                uint ch = data[i] ^ (key + seed);
                key = ((~key << 0x15) + 0x11111111) | (key >> 0x0B);
                seed = ch + seed + (seed << 5) + 3;
                data[i] = ch;
            }
        }

        /// <summary>Decrypts the whole 32-bit words of a part of a byte array in place (a trailing rest stays as it is).</summary>
        public static void Decrypt(byte[] bytes, int from, int count, uint key)
        {
            var words = new uint[count / 4];
            Buffer.BlockCopy(bytes, from, words, 0, words.Length * 4);
            Decrypt(words, key);
            Buffer.BlockCopy(words, 0, bytes, from, words.Length * 4);
        }

        public static void Encrypt(uint[] data, uint key)
        {
            uint seed = 0xEEEEEEEE;
            for (int i = 0; i < data.Length; i++)
            {
                seed += Table[0x400 + (key & 0xFF)];
                uint ch = data[i];
                data[i] = ch ^ (key + seed);
                key = ((~key << 0x15) + 0x11111111) | (key >> 0x0B);
                seed = ch + seed + (seed << 5) + 3;
            }
        }
    }

    class MpqArchive : IDisposable
    {
        const uint FileExists = 0x80000000, FileCompress = 0x00000200, FileImplode = 0x00000100, FileEncrypted = 0x00010000, FileFixKey = 0x00020000,
            FileSingleUnit = 0x01000000, FileSectorCrc = 0x04000000, FileDeleteMarker = 0x02000000;

        struct HashEntry { public uint A, B; public ushort Locale; public uint Block; }
        struct BlockEntry { public long Offset; public uint Packed, Size, Flags; }

        readonly FileStream stream;
        readonly long start;
        readonly int sectorSize;
        readonly HashEntry[] hashes;
        readonly BlockEntry[] blocks;
        public readonly string Path;

        public MpqArchive(string path)
        {
            Path = path;
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var r = new BinaryReader(stream);
            start = -1;
            for (long at = 0; at + 32 <= stream.Length && at < 0x10000000; at += 0x200)
            {
                stream.Position = at;
                if (r.ReadUInt32() == 0x1A51504D) { start = at; break; }
            }
            if (start < 0) throw new InvalidDataException(System.IO.Path.GetFileName(path) + " is not an MPQ archive.");
            uint headerSize = r.ReadUInt32();
            r.ReadUInt32();                         // archive size
            ushort format = r.ReadUInt16();
            sectorSize = 0x200 << r.ReadUInt16();
            long hashPos = r.ReadUInt32(), blockPos = r.ReadUInt32();
            uint hashCount = r.ReadUInt32(), blockCount = r.ReadUInt32();
            long hiBlockPos = 0;
            if (format >= 1 && headerSize >= 44)
            {
                hiBlockPos = r.ReadInt64();
                hashPos |= (long)r.ReadUInt16() << 32;
                blockPos |= (long)r.ReadUInt16() << 32;
            }

            var h = ReadTable(start + hashPos, hashCount, MpqCrypt.Hash("(hash table)", MpqCrypt.FileKey));
            hashes = new HashEntry[hashCount];
            for (int i = 0; i < hashCount; i++)
                hashes[i] = new HashEntry { A = h[i * 4], B = h[i * 4 + 1], Locale = (ushort)(h[i * 4 + 2] & 0xFFFF), Block = h[i * 4 + 3] };
            var b = ReadTable(start + blockPos, blockCount, MpqCrypt.Hash("(block table)", MpqCrypt.FileKey));
            ushort[] hi = null;
            if (hiBlockPos != 0)
            {
                stream.Position = start + hiBlockPos;
                hi = new ushort[blockCount];
                for (int i = 0; i < blockCount; i++) hi[i] = r.ReadUInt16();
            }
            blocks = new BlockEntry[blockCount];
            for (int i = 0; i < blockCount; i++)
                blocks[i] = new BlockEntry { Offset = b[i * 4] | (hi == null ? 0 : (long)hi[i] << 32), Packed = b[i * 4 + 1], Size = b[i * 4 + 2], Flags = b[i * 4 + 3] };
        }

        uint[] ReadTable(long pos, uint entries, uint key)
        {
            stream.Position = pos;
            var bytes = new byte[entries * 16];
            int got = 0;
            while (got < bytes.Length) { int n = stream.Read(bytes, got, bytes.Length - got); if (n <= 0) break; got += n; }
            var data = new uint[entries * 4];
            Buffer.BlockCopy(bytes, 0, data, 0, got);
            MpqCrypt.Decrypt(data, key);
            return data;
        }

        int Find(string name)
        {
            if (hashes.Length == 0) return -1;
            uint index = MpqCrypt.Hash(name, MpqCrypt.TableOffset), a = MpqCrypt.Hash(name, MpqCrypt.HashA), b = MpqCrypt.Hash(name, MpqCrypt.HashB);
            int mask = hashes.Length - 1, first = (int)(index & mask), found = -1;
            for (int i = first; ; i = (i + 1) & mask)
            {
                var e = hashes[i];
                if (e.Block == 0xFFFFFFFF) break;
                if (e.A == a && e.B == b && e.Block < blocks.Length)
                {
                    // the neutral locale first, any other one if there is no neutral copy
                    if (e.Locale == 0) return (int)e.Block;
                    if (found < 0) found = (int)e.Block;
                }
                if (((i + 1) & mask) == first) break;
            }
            return found;
        }

        public bool Contains(string name)
        {
            int block = Find(name);
            return block >= 0 && (blocks[block].Flags & FileExists) != 0 && (blocks[block].Flags & FileDeleteMarker) == 0;
        }

        /// <summary>The unpacked size of a file, or -1.</summary>
        public long SizeOf(string name)
        {
            int block = Find(name);
            if (block < 0 || (blocks[block].Flags & FileExists) == 0 || (blocks[block].Flags & FileDeleteMarker) != 0) return -1;
            return blocks[block].Size;
        }

        public byte[] Read(string name)
        {
            int index = Find(name);
            if (index < 0) return null;
            var block = blocks[index];
            if ((block.Flags & FileExists) == 0 || (block.Flags & FileDeleteMarker) != 0) return null;
            string label = name + " in " + System.IO.Path.GetFileName(Path);
            if ((block.Flags & FileImplode) != 0) throw new InvalidDataException(label + " is packed with PKWARE implode; AFK Realm cannot read it.");

            stream.Position = start + block.Offset;
            var packed = new byte[block.Packed];
            ReadExactly(packed);
            bool encrypted = (block.Flags & FileEncrypted) != 0;
            uint key = 0;
            if (encrypted)
            {
                // the key comes from the file's own name, without its folder
                key = MpqCrypt.Hash(name.Substring(name.Replace('/', '\\').LastIndexOf('\\') + 1), MpqCrypt.FileKey);
                if ((block.Flags & FileFixKey) != 0) key = (key + (uint)block.Offset) ^ block.Size;
            }
            if ((block.Flags & FileSingleUnit) != 0 || (block.Flags & (FileCompress | FileImplode)) == 0)
            {
                if ((block.Flags & FileSingleUnit) == 0)
                {
                    // stored in sectors without compression
                    if (encrypted) for (int s0 = 0; s0 * sectorSize < packed.Length; s0++) MpqCrypt.Decrypt(packed, s0 * sectorSize, Math.Min(sectorSize, packed.Length - s0 * sectorSize), key + (uint)s0);
                    return Fit(packed, block.Size);
                }
                if (encrypted) MpqCrypt.Decrypt(packed, 0, packed.Length, key);
                if ((block.Flags & FileCompress) == 0) return Fit(packed, block.Size);
                return block.Packed < block.Size ? Decompress(packed, 0, (int)block.Packed, (int)block.Size, label) : Fit(packed, block.Size);
            }

            int sectors = (int)((block.Size + sectorSize - 1) / sectorSize);
            int entries = sectors + 1 + ((block.Flags & FileSectorCrc) != 0 ? 1 : 0);
            var offsets = new uint[entries];
            Buffer.BlockCopy(packed, 0, offsets, 0, Math.Min(packed.Length, entries * 4));
            if (encrypted) MpqCrypt.Decrypt(offsets, key - 1);
            var output = new byte[block.Size];
            for (int s = 0; s < sectors; s++)
            {
                int from = (int)offsets[s], to = (int)offsets[s + 1];
                int want = (int)Math.Min(sectorSize, block.Size - (long)s * sectorSize);
                if (from < 0 || to > packed.Length || to < from) throw new InvalidDataException(label + " is damaged.");
                if (encrypted) MpqCrypt.Decrypt(packed, from, to - from, key + (uint)s);
                byte[] sector = to - from < want ? Decompress(packed, from, to - from, want, label) : Slice(packed, from, want);
                Buffer.BlockCopy(sector, 0, output, s * sectorSize, Math.Min(sector.Length, want));
            }
            return output;
        }

        void ReadExactly(byte[] buffer)
        {
            int got = 0;
            while (got < buffer.Length) { int n = stream.Read(buffer, got, buffer.Length - got); if (n <= 0) throw new EndOfStreamException(); got += n; }
        }

        static byte[] Fit(byte[] data, uint size) { return data.Length == size ? data : Slice(data, 0, (int)Math.Min(size, data.Length)); }

        static byte[] Slice(byte[] data, int from, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(data, from, result, 0, count);
            return result;
        }

        static byte[] Decompress(byte[] data, int from, int count, int size, string label)
        {
            byte type = data[from];
            if (type != 0x02) throw new InvalidDataException(label + " uses a compression AFK Realm cannot read (0x" + type.ToString("X2") + ").");
            // zlib: a type byte, then a two-byte zlib header before the deflate stream
            using (var input = new MemoryStream(data, from + 3, count - 3))
            using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                var output = new byte[size];
                int got = 0;
                while (got < size) { int n = inflate.Read(output, got, size - got); if (n <= 0) break; got += n; }
                return output;
            }
        }

        public void Dispose() { stream.Dispose(); }

        /// <summary>
        /// Writes an archive of format 1 from files on disk (streamed, stored whole) and a few files in memory, with a
        /// (listfile). For the archives of imported races, which hold hundreds of megabytes.
        /// </summary>
        public static void WriteFromFiles(string path, IDictionary<string, string> sources, IDictionary<string, byte[]> extra)
        {
            WriteFromFiles(path, sources, extra, null);
        }

        /// <summary>The same, saying after each file how many of how many are written.</summary>
        public static void WriteFromFiles(string path, IDictionary<string, string> sources, IDictionary<string, byte[]> extra, Action<int, int> progress)
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in sources.Keys) names.Add(k.Replace('/', '\\'));
            foreach (var k in extra.Keys) names.Add(k.Replace('/', '\\'));
            var list = new StringBuilder();
            foreach (var name in names) list.Append(name).Append("\r\n");
            var memory = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in extra) memory[kv.Key.Replace('/', '\\')] = kv.Value;
            memory["(listfile)"] = Encoding.ASCII.GetBytes(list.ToString());
            names.Add("(listfile)");
            var disk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in sources) disk[kv.Key.Replace('/', '\\')] = kv.Value;

            int hashCount = 16;
            while (hashCount < names.Count * 2) hashCount *= 2;
            var hashTable = new uint[hashCount * 4];
            for (int i = 0; i < hashTable.Length; i++) hashTable[i] = 0xFFFFFFFF;
            var blockTable = new uint[names.Count * 4];
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                const int headerSize = 32;
                fs.Position = headerSize;
                int block = 0;
                var buffer = new byte[1 << 20];
                foreach (var name in names)
                {
                    long offset = fs.Position;
                    long length;
                    byte[] data;
                    if (memory.TryGetValue(name, out data)) { w.Write(data); length = data.Length; }
                    else
                    {
                        using (var input = File.OpenRead(disk[name]))
                        {
                            int n;
                            while ((n = input.Read(buffer, 0, buffer.Length)) > 0) fs.Write(buffer, 0, n);
                            length = input.Length;
                        }
                    }
                    if (fs.Position > uint.MaxValue) throw new InvalidOperationException("The archive " + System.IO.Path.GetFileName(path) + " would be larger than 4 GB.");
                    blockTable[block * 4] = (uint)offset;
                    blockTable[block * 4 + 1] = (uint)length;
                    blockTable[block * 4 + 2] = (uint)length;
                    blockTable[block * 4 + 3] = FileExists;
                    uint index = MpqCrypt.Hash(name, MpqCrypt.TableOffset) & (uint)(hashCount - 1);
                    while (hashTable[index * 4 + 3] != 0xFFFFFFFF) index = (index + 1) & (uint)(hashCount - 1);
                    hashTable[index * 4] = MpqCrypt.Hash(name, MpqCrypt.HashA);
                    hashTable[index * 4 + 1] = MpqCrypt.Hash(name, MpqCrypt.HashB);
                    hashTable[index * 4 + 2] = 0;
                    hashTable[index * 4 + 3] = (uint)block;
                    block++;
                    if (progress != null && (block % 25 == 0 || block == names.Count)) progress(block, names.Count);
                }
                uint hashPos = (uint)fs.Position;
                MpqCrypt.Encrypt(hashTable, MpqCrypt.Hash("(hash table)", MpqCrypt.FileKey));
                foreach (var v in hashTable) w.Write(v);
                uint blockPos = (uint)fs.Position;
                MpqCrypt.Encrypt(blockTable, MpqCrypt.Hash("(block table)", MpqCrypt.FileKey));
                foreach (var v in blockTable) w.Write(v);
                uint size = (uint)fs.Position;
                fs.Position = 0;
                w.Write(0x1A51504Du);
                w.Write((uint)headerSize);
                w.Write(size);
                w.Write((ushort)0);
                w.Write((ushort)3);
                w.Write(hashPos);
                w.Write(blockPos);
                w.Write((uint)hashCount);
                w.Write((uint)names.Count);
            }
        }

        /// <summary>Writes an archive of format 1 with the given files stored whole, and a (listfile).</summary>
        public static void Write(string path, IDictionary<string, byte[]> files)
        {
            var all = new SortedDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files) all[f.Key.Replace('/', '\\')] = f.Value;
            var list = new StringBuilder();
            foreach (var name in all.Keys) list.Append(name).Append("\r\n");
            all["(listfile)"] = Encoding.ASCII.GetBytes(list.ToString());

            int hashCount = 16;
            while (hashCount < all.Count * 2) hashCount *= 2;
            var hashTable = new uint[hashCount * 4];
            for (int i = 0; i < hashCount; i++) { hashTable[i * 4] = 0xFFFFFFFF; hashTable[i * 4 + 1] = 0xFFFFFFFF; hashTable[i * 4 + 2] = 0xFFFFFFFF; hashTable[i * 4 + 3] = 0xFFFFFFFF; }
            var blockTable = new uint[all.Count * 4];

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                const int headerSize = 32;
                fs.Position = headerSize;
                int block = 0;
                foreach (var file in all)
                {
                    uint offset = (uint)fs.Position;
                    w.Write(file.Value);
                    blockTable[block * 4] = offset;
                    blockTable[block * 4 + 1] = (uint)file.Value.Length;
                    blockTable[block * 4 + 2] = (uint)file.Value.Length;
                    blockTable[block * 4 + 3] = FileExists;

                    uint index = MpqCrypt.Hash(file.Key, MpqCrypt.TableOffset) & (uint)(hashCount - 1);
                    while (hashTable[index * 4 + 3] != 0xFFFFFFFF) index = (index + 1) & (uint)(hashCount - 1);
                    hashTable[index * 4] = MpqCrypt.Hash(file.Key, MpqCrypt.HashA);
                    hashTable[index * 4 + 1] = MpqCrypt.Hash(file.Key, MpqCrypt.HashB);
                    hashTable[index * 4 + 2] = 0;       // neutral locale, platform 0
                    hashTable[index * 4 + 3] = (uint)block;
                    block++;
                }

                uint hashPos = (uint)fs.Position;
                MpqCrypt.Encrypt(hashTable, MpqCrypt.Hash("(hash table)", MpqCrypt.FileKey));
                foreach (var v in hashTable) w.Write(v);
                uint blockPos = (uint)fs.Position;
                MpqCrypt.Encrypt(blockTable, MpqCrypt.Hash("(block table)", MpqCrypt.FileKey));
                foreach (var v in blockTable) w.Write(v);
                uint size = (uint)fs.Position;

                fs.Position = 0;
                w.Write(0x1A51504Du);
                w.Write((uint)headerSize);
                w.Write(size);
                w.Write((ushort)0);                    // format 0
                w.Write((ushort)3);                    // sectors of 4096 bytes
                w.Write(hashPos);
                w.Write(blockPos);
                w.Write((uint)hashCount);
                w.Write((uint)all.Count);
            }
        }
    }

    /// <summary>A DBC table of the client: records of fixed size and a string block.</summary>
    class DbcTable
    {
        public int Fields, RecordSize;
        public List<byte[]> Records = new List<byte[]>();
        byte[] strings = new byte[0];
        readonly List<byte> pending = new List<byte>();
        Dictionary<string, uint> index;
        /// <summary>The string block. Texts added with AddString are appended to it on first use.</summary>
        public byte[] Strings
        {
            get
            {
                if (pending.Count > 0) { var grown = new byte[strings.Length + pending.Count]; Buffer.BlockCopy(strings, 0, grown, 0, strings.Length); pending.CopyTo(grown, strings.Length); strings = grown; pending.Clear(); }
                return strings;
            }
            set { strings = value ?? new byte[0]; pending.Clear(); index = null; }
        }

        public static DbcTable Parse(byte[] data, string label)
        {
            if (data.Length < 20 || Encoding.ASCII.GetString(data, 0, 4) != "WDBC") throw new InvalidDataException(label + " is not a DBC table.");
            var t = new DbcTable();
            int count = BitConverter.ToInt32(data, 4);
            t.Fields = BitConverter.ToInt32(data, 8);
            t.RecordSize = BitConverter.ToInt32(data, 12);
            int stringSize = BitConverter.ToInt32(data, 16);
            if (20L + (long)count * t.RecordSize + stringSize > data.Length) throw new InvalidDataException(label + " is damaged.");
            for (int i = 0; i < count; i++)
            {
                var rec = new byte[t.RecordSize];
                Buffer.BlockCopy(data, 20 + i * t.RecordSize, rec, 0, t.RecordSize);
                t.Records.Add(rec);
            }
            t.Strings = new byte[stringSize];
            Buffer.BlockCopy(data, 20 + count * t.RecordSize, t.Strings, 0, stringSize);
            return t;
        }

        /// <summary>A field of a row, in the width the table stores its fields in (4 bytes; 2 or 1 in a few client
        /// tables such as CharBaseInfo.dbc).</summary>
        public uint Field(byte[] record, int field)
        {
            int width = Fields > 0 && RecordSize % Fields == 0 ? RecordSize / Fields : 4;
            if (width == 4) return BitConverter.ToUInt32(record, field * 4);
            if (width == 2) return BitConverter.ToUInt16(record, field * 2);
            if (width == 1) return record[field];
            return BitConverter.ToUInt32(record, field * 4);
        }

        /// <summary>A text at the end of the table's strings (or where it is already); its offset, for a text field.</summary>
        public uint AddString(string text)
        {
            if (index == null)
            {
                // every text of the block by its offset (a text starts at 0 or after a NUL); the first offset wins
                index = new Dictionary<string, uint>();
                var block = Strings;
                int start = 0;
                for (int i = 0; i < block.Length; i++)
                    if (block[i] == 0)
                    {
                        string t = Encoding.UTF8.GetString(block, start, i - start);
                        if (!index.ContainsKey(t) && (start > 0 || t.Length > 0)) index[t] = (uint)start;
                        start = i + 1;
                    }
            }
            uint at;
            if (index.TryGetValue(text, out at)) return at;
            if (strings.Length + pending.Count == 0) pending.Add(0);
            at = (uint)(strings.Length + pending.Count);
            pending.AddRange(Encoding.UTF8.GetBytes(text));
            pending.Add(0);
            index[text] = at;
            return at;
        }

        /// <summary>Writes a field of a row, in the width the table stores its fields in.</summary>
        public void SetField(byte[] record, int field, uint value)
        {
            int width = Fields > 0 && RecordSize % Fields == 0 ? RecordSize / Fields : 4;
            if (width != 1 && width != 2) width = 4;
            if (width < 4 && value >= (1u << (8 * width)))
                throw new InvalidDataException("The value " + value + " does not fit into a field of " + width + " byte(s).");
            for (int b = 0; b < width; b++) record[field * width + b] = (byte)(value >> (8 * b));
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("WDBC"));
                w.Write(Records.Count); w.Write(Fields); w.Write(RecordSize); w.Write(Strings.Length);
                foreach (var r in Records) w.Write(r);
                w.Write(Strings);
                return ms.ToArray();
            }
        }
    }
}
