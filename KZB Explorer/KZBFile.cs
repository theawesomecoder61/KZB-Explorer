using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KZB
{
    public class KZBFile : IDisposable
    {
        public class Entry
        {
            public string FullPath { get; private set; }
            public string Path { get; private set; }
            public string Name { get; private set; }
            public uint ID { get; private set; }
            public uint Size { get; private set; }
            public uint Offset { get; private set; }

            public Entry(string fullPath, uint id, uint offset, uint size)
            {
                FullPath = fullPath;
                ID = id;
                Size = size;
                Offset = offset;

                Path = FullPath.Substring(0, FullPath.LastIndexOf('/') + 1);
                Name = FullPath.Substring(FullPath.LastIndexOf('/') + 1);
            }

            public override string ToString()
            {
                return FullPath;
            }

            public string[] GetPathParts()
            {
                return FullPath.Split('/');
            }
        }

        public List<Entry> Entries { get => _entries; }
        private List<Entry> _entries;

        public string Name { get; private set; }

        private FileStream stream;
        private BinaryReader reader;

        struct Vertex
        {
            public float PX, PY, PZ, NX, NY, NZ, U, V;
        }

        public KZBFile(string file)
        {
            stream = new FileStream(file, FileMode.Open, FileAccess.Read);
            reader = new BinaryReader(stream);
            _entries = new List<Entry>();
        }

        public void Dispose()
        {
            _entries.Clear();
            reader.Close();
            reader.Dispose();
        }

        public void Read()
        {
            if (reader.ReadInt32() != 0x46425a4b)
                throw new Exception("Not a valid KZB file.");

            reader.ReadInt32(); // unknown
            int fileNameSize = reader.ReadInt32(); // file name size
            Name = new(reader.ReadChars(fileNameSize));

            int entryCount = reader.ReadInt32();
            string[] fullPaths = new string[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                fullPaths[i] = $"{Name}{ReadString(reader)}";
            }

            entryCount = reader.ReadInt32();
            for (int i = 0; i < entryCount; i++)
            {
                uint id = reader.ReadUInt32();
                reader.ReadInt32(); // seems to be ffffffff
                uint offset = reader.ReadUInt32();
                uint size = reader.ReadUInt32();
                reader.ReadInt32(); // padding
                reader.ReadInt32(); // repeat of size
                _entries.Add(new(fullPaths[i], id, offset, size));
            }
        }

        public byte[] GetDataBytes(Entry entry)
        {
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
            return reader.ReadBytes((int)entry.Size);
        }

        public byte[] GetDataBytes(Entry entry, uint size)
        {
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
            return reader.ReadBytes((int)size);
        }

        public Stream GetDataStream(Entry entry)
        {
            MemoryStream memoryStream = new((int)entry.Size);
            stream.Seek(entry.Offset, SeekOrigin.Begin);
            stream.CopyTo(memoryStream, (int)entry.Size);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        public string GetEntryType(Entry entry)
        {
            //int[] arr = new int[3];
            //Buffer.BlockCopy(GetDataBytes(entry, 12), 0, arr, 0, 12);
            //if (arr[0] == 0 && (arr[1] == 0xf || arr[1] == 0x11) && arr[2] == 0x011b) return "Animation Clip/Data";
            //if (arr[0] == 0 && (arr[1] == 0x022e || arr[1] == 0x022f) && arr[2] == 0x011b) return "Brush";
            //if (arr[0] == 1 && arr[1] == 0 && (arr[2] == 0x72657623 || arr[2] == 0x72747461)) return "Material Type";
            //if (arr[0] == 0 && arr[1] == 0 && arr[2] == 0) return "Mesh Data";
            return entry.GetPathParts()[1];
        }

        #region Mesh Data
        public void SaveMeshAsOBJ(Entry entry, string file)
        {
            using (Stream entryStream = GetDataStream(entry))
            {
                using (BinaryReader br = new(entryStream))
                {
                    if (!entry.FullPath.Contains("Mesh Data"))
                    {
                        throw new Exception("Not mesh data.");
                    }
                    if (!br.ReadBytes(12).All(x => x == 0))
                    {
                        throw new Exception("Not mesh data.");
                    }

                    int a = br.ReadInt32(); // seems to determine of vertex size; 0x3: 16, 0x4: 20
                    int b = 0;
                    int extraVertexSize = 0; // if a=0x4, b=0xf, and a502 exists, each vertex is 4 or 8 bytes longer
                    int vertexCount = 0; // number of vertices
                    while (vertexCount == 0 && br.BaseStream.Position < 0x64)
                    {
                        int id = br.ReadInt32();
                        // arrived at vertex count
                        if (a == 0x3 && br.BaseStream.Position == 0x50)
                        {
                            vertexCount = id;
                            break;
                        }
                        if (a == 0x4 && br.BaseStream.Position == 0x64)
                        {
                            vertexCount = id;
                            break;
                        }
                        // handle each known id 
                        switch (id)
                        {
                            case 0x029a:
                            case 0x03c7:
                            case 0x02a7:
                                b = br.ReadInt32(); // seems to determine data type used; 0xf: half, 0x10: float
                                br.ReadInt32(); // 3
                                br.ReadInt32(); // 0
                                br.ReadInt32(); // 0
                                break;
                            case 0x029b:
                            case 0x01c2:
                            case 0x02a8:
                                br.ReadInt32(); // repeat of "b"
                                br.ReadInt32(); // 3
                                br.ReadInt32(); // 1
                                br.ReadInt32(); // 0
                                break;
                            case 0x029c:
                            case 0x03c8:
                            case 0x02ad:
                            case 0x02a9:
                                br.ReadInt32(); // repeat of "b"
                                br.ReadInt32(); // 2
                                br.ReadInt32(); // 3
                                br.ReadInt32(); // 0
                                break;
                            case 0x02a5: // in some files, this exists and preceeds 9c02
                            case 0x03d7: // in some files, this exists and preceeds c803
                            case 0x02b1: // in some files, this exists and preceeds a902
                                br.ReadInt32(); // repeat of "b"
                                br.ReadInt32(); // repeat of "a"
                                br.ReadInt32(); // repeat of "a"
                                br.ReadInt32(); // 0
                                extraVertexSize = 4;
                                break;
                            case 0x02a1:
                                br.ReadInt32(); // repeat of "b"
                                br.ReadInt32(); // 2
                                br.ReadInt32(); // 3
                                br.ReadInt32(); // 1
                                break;
                            case 0x02bc:
                                br.ReadInt32(); // repeat of "b"
                                br.ReadInt32(); // 3
                                br.ReadInt32(); // 2
                                br.ReadInt32(); // 0
                                break;
                            case 0x03c9: // its purpose is unknown, seems to indicate that each vertex is 8 bytes longer when b=0xf
                                if (b == 0xf)
                                    extraVertexSize += 8;
                                br.ReadInt32(); // 0x10
                                br.ReadInt32(); // 3
                                br.ReadInt32(); // 2
                                br.ReadInt32(); // 0
                                break;
                            default:
                                br.BaseStream.Seek(16, SeekOrigin.Current);
                                break;
                        }
                    }

                    if (vertexCount == 0)
                        throw new Exception("Failed to parse mesh data.");

                    // vertices
                    Vertex[] vertices = new Vertex[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        vertices[i] = new Vertex
                        {
                            PX = ReadVertexValue(br, b),
                            PY = ReadVertexValue(br, b),
                            PZ = ReadVertexValue(br, b),
                            NX = ReadVertexValue(br, b),
                            NY = ReadVertexValue(br, b),
                            NZ = ReadVertexValue(br, b),
                            U = ReadVertexValue(br, b),
                            V = ReadVertexValue(br, b)
                        };
                        if (a == 0x4)
                        {
                            if (b == 0xf)
                                br.BaseStream.Seek(4 + extraVertexSize, SeekOrigin.Current);
                            if (b == 0x10)
                                br.BaseStream.Seek(12 + extraVertexSize, SeekOrigin.Current);
                        }
                    }

                    // indices
                    br.ReadInt32(); // seems to be 1
                    br.ReadInt32(); // id
                    int indexCount = br.ReadInt32(); // number of indices
                    int indexSize = br.ReadInt32(); // size of an index
                    uint[] indices = new uint[indexCount];
                    for (int i = 0; i < indexCount; i++)
                    {
                        if (indexSize == 2)
                            indices[i] = br.ReadUInt16();
                        else
                            indices[i] = br.ReadUInt32();
                    }

                    // create obj
                    StringBuilder builder = new();
                    foreach (Vertex vertex in vertices)
                        builder.AppendFormat("v {0:F5} {1:F4} {2:F5}\n", vertex.PX, vertex.PY, vertex.PZ);
                    foreach (Vertex vertex in vertices)
                        builder.AppendFormat("vn {0:F5} {1:F4} {2:F5}\n", vertex.NX, vertex.NY, vertex.NZ);
                    foreach (Vertex vertex in vertices)
                        builder.AppendFormat("vt {0:F5} {1:F5}\n", vertex.U, vertex.V);
                    for (int i = 0; i < indexCount; i += 3)
                        builder.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", indices[i] + 1, indices[i + 1] + 1, indices[i + 2] + 1);
                    File.WriteAllText(file, builder.ToString());
                }
            }
        }
        #endregion

        #region Helpers
        private float ReadVertexValue(BinaryReader br, int b)
        {
            switch (b)
            {
                case 0xf:
                default:
                    byte[] buf = br.ReadBytes(2);
                    return ToTwoByteFloat(buf[0], buf[1]);
                case 0x10:
                    return br.ReadSingle();
            }
        }

        private static string ReadString(BinaryReader br)
        {
            StringBuilder builder = new();
            do
            {
                builder.Append(br.ReadChar());
            } while (br.PeekChar() != 0);
            br.ReadChar(); // null
            return builder.ToString();
        }

        // https://stackoverflow.com/a/37761168
        private static float ToTwoByteFloat(byte hi, byte lo)
        {
            int intVal = BitConverter.ToInt32(new byte[] { hi, lo, 0, 0 }, 0);

            int mant = intVal & 0x03ff;
            int exp = intVal & 0x7c00;
            if (exp == 0x7c00) exp = 0x3fc00;
            else if (exp != 0)
            {
                exp += 0x1c000;
                if (mant == 0 && exp > 0x1c400)
                    return BitConverter.ToSingle(BitConverter.GetBytes((intVal & 0x8000) << 16 | exp << 13 | 0x3ff), 0);
            }
            else if (mant != 0)
            {
                exp = 0x1c400;
                do
                {
                    mant <<= 1;
                    exp -= 0x400;
                } while ((mant & 0x400) == 0);
                mant &= 0x3ff;
            }
            return BitConverter.ToSingle(BitConverter.GetBytes((intVal & 0x8000) << 16 | (exp | mant) << 13), 0);
        }
        #endregion
    }
}
