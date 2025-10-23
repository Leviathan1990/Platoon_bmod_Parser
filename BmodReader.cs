using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace BmodReader
{
    public class BmodFile
    {
        public BmodHeader Header;
        public List<BmodChunk> Chunks = new List<BmodChunk>();

        // Quick access to specific chunks
        public List<MeshChunk> MeshChunks => GetChunks<MeshChunk>();
        public List<ObmoChunk> ObmoChunks => GetChunks<ObmoChunk>();
        public List<BoneChunk> BoneChunks => GetChunks<BoneChunk>();
        public List<TextChunk> TextChunks => GetChunks<TextChunk>();
        public List<MateChunk> MateChunks => GetChunks<MateChunk>();
        public MboxChunk MboxChunk => GetChunk<MboxChunk>();
        public OboxChunk OboxChunk => GetChunk<OboxChunk>();

        // JAVÍTÁS: private → public
        public List<T> GetChunks<T>() where T : BmodChunk
        {
            var result = new List<T>();

            foreach (var chunk in Chunks)
            {
                // Add chunk if it matches type
                if (chunk is T typedChunk)
                {
                    result.Add(typedChunk);
                }

                // ✅ ÚJ: Check OBST sub-chunks
                if (chunk is ObstChunk obst)
                {
                    if (obst.MeshChunk is T meshAsT)
                        result.Add(meshAsT);
                }
            }

            return result;
        }

        // JAVÍTÁS: private → public
        public T GetChunk<T>() where T : BmodChunk
        {
            foreach (var chunk in Chunks)
                if (chunk is T typedChunk)
                    return typedChunk;
            return null;
        }

        // Load from file
        public static BmodFile Load(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            using (var reader = new BinaryReader(fs))
            {
                var parser = new BmodParser(reader);
                return parser.Parse();
            }
        }

        // Load from stream
        public static BmodFile Load(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                var parser = new BmodParser(reader);
                return parser.Parse();
            }
        }
    }

    internal class BmodParser
    {
        private BinaryReader _reader;

        public BmodParser(BinaryReader reader)
        {
            _reader = reader;
        }

        public BmodFile Parse()
        {
            var bmod = new BmodFile();

            // Read header
            bmod.Header = ReadHeader();

            // Validate
            if (bmod.Header.Magic != "OMOD" && bmod.Header.Magic != "BMOD")
                throw new InvalidDataException($"Invalid magic: {bmod.Header.Magic}");

            if (bmod.Header.VersionMajor != 1 || bmod.Header.VersionMinor < 10 || bmod.Header.VersionMinor > 13)
                throw new InvalidDataException($"Unsupported version: {bmod.Header.VersionString}");

            // Read chunks
            while (_reader.BaseStream.Position < _reader.BaseStream.Length)
            {
                var chunk = ReadChunk();
                if (chunk != null)
                    bmod.Chunks.Add(chunk);
            }

            return bmod;
        }

        private BmodHeader ReadHeader()
        {
            var header = new BmodHeader();
            header.Magic = ReadFourCC();
            header.FileSize = _reader.ReadUInt32();
            header.VersionMinor = _reader.ReadUInt16();
            header.VersionMajor = _reader.ReadUInt16();
            return header;
        }

        private BmodChunk ReadChunk()
        {
            if (_reader.BaseStream.Position >= _reader.BaseStream.Length)
                return null;

            var chunkId = ReadFourCC();
            var chunkSize = _reader.ReadUInt32();
            var dataStart = _reader.BaseStream.Position;
            var dataSize = chunkSize - 8;

            BmodChunk chunk = chunkId switch
            {
                "MBOX" => ReadMboxChunk(chunkId, chunkSize, dataStart),
                "OBOX" => ReadOboxChunk(chunkId, chunkSize, dataStart),
                "MESH" => ReadMeshChunk(chunkId, chunkSize, dataStart),
                "MEMS" => ReadMeshChunk(chunkId, chunkSize, dataStart), // Mirrored mesh
                "OBMO" => ReadObmoChunk(chunkId, chunkSize, dataStart),
                "OBSK" => ReadObskChunk(chunkId, chunkSize, dataStart),
                "BONE" => ReadBoneChunk(chunkId, chunkSize, dataStart),
                "TEXT" => ReadTextChunk(chunkId, chunkSize, dataStart),
                "MATE" => ReadMateChunk(chunkId, chunkSize, dataStart),
                "BLST" => ReadBlstChunk(chunkId, chunkSize, dataStart),
                "CLOU" => ReadClouChunk(chunkId, chunkSize, dataStart),
                "OMNI" => ReadOmniChunk(chunkId, chunkSize, dataStart),
                "FLAR" => ReadFlarChunk(chunkId, chunkSize, dataStart),
                "ASEL" => ReadAselChunk(chunkId, chunkSize, dataStart),
                "ASEC" => ReadAsecChunk(chunkId, chunkSize, dataStart),
                "OBST" => ReadObstChunk(chunkId, chunkSize, dataStart),
                "DUMY" => ReadDumyChunk(chunkId, chunkSize, dataStart),
                "TIME" => ReadTimeChunk(chunkId, chunkSize, dataStart),
                _ => ReadUnknownChunk(chunkId, chunkSize, dataStart)
            };

            // Ensure we're at the correct position after chunk
            _reader.BaseStream.Position = dataStart + dataSize;

            return chunk;
        }

        // ========================================================================
        // GEOMETRY CHUNK READERS
        // ========================================================================

        private MboxChunk ReadMboxChunk(string id, uint size, long dataStart)
        {
            var chunk = new MboxChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.Min = ReadVector3();
            chunk.Max = ReadVector3();
            return chunk;
        }

        private OboxChunk ReadOboxChunk(string id, uint size, long dataStart)
        {
            var chunk = new OboxChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.Center = ReadVector3();
            chunk.Normal = ReadVector3();
            return chunk;
        }

        private MeshChunk ReadMeshChunk(string id, uint size, long dataStart)
        {
            long startPos = _reader.BaseStream.Position;
            var chunk = new MeshChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };

            try
            {
                chunk.Unknown = _reader.ReadUInt32();
                chunk.VertexCount = _reader.ReadUInt32();  // ✅ EZ A HELYES SZÁM!
                chunk.FaceCount = _reader.ReadUInt32();

                Console.WriteLine($"  → Vertices: {chunk.VertexCount}, Faces: {chunk.FaceCount}");

                long meshEnd = dataStart + size - 8;

                while (_reader.BaseStream.Position < meshEnd)
                {
                    long subChunkStart = _reader.BaseStream.Position;
                    long remaining = meshEnd - subChunkStart;

                    if (remaining < 8) break;

                    string subChunkId = ReadFourCC();
                    uint subChunkSize = _reader.ReadUInt32();

                    if (subChunkSize == 0 || subChunkSize < 8 || subChunkSize > remaining)
                    {
                        Console.WriteLine($"    ⚠ Invalid sub-chunk size: {subChunkSize}, stopping");
                        _reader.BaseStream.Position = subChunkStart;
                        break;
                    }

                    Console.WriteLine($"    Sub-chunk: {subChunkId} ({subChunkSize} bytes)");

                    switch (subChunkId)
                    {
                        case "VERT":
                            // ✅ PASS THE EXPECTED VERTEX COUNT!
                            chunk.VertChunk = ReadVertChunkInMesh(subChunkId, subChunkSize, subChunkStart + 8, chunk.VertexCount);
                            break;

                        case "ISTR":
                            chunk.IstrChunk = ReadIstrChunkInMesh(subChunkId, subChunkSize, subChunkStart + 8);
                            break;

                        case "FACE":
                            _reader.BaseStream.Seek(subChunkStart + subChunkSize, SeekOrigin.Begin);
                            break;

                        default:
                            Console.WriteLine($"    ⚠ Unknown MESH sub-chunk: {subChunkId}");
                            _reader.BaseStream.Seek(subChunkStart + subChunkSize, SeekOrigin.Begin);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Error parsing MESH: {ex.Message}");
                _reader.BaseStream.Seek(dataStart + size - 8, SeekOrigin.Begin);
            }

            return chunk;
        }

        private IstrChunk ReadIstrChunkInMesh(string id, uint size, long dataStart)
        {
            var chunk = new IstrChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };

            chunk.IndexCount = _reader.ReadUInt32();
            Console.WriteLine($"      ISTR: {chunk.IndexCount} indices");

            for (uint i = 0; i < chunk.IndexCount; i++)
            {
                chunk.Indices.Add(_reader.ReadUInt16());
            }

            return chunk;
        }

        private VertChunk ReadVertChunkInMesh(string id, uint size, long dataStart, uint expectedVertexCount)
        {
            var chunk = new VertChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };

            try
            {
                // ✅ VERT header: 12 bytes
                uint faceCount = _reader.ReadUInt32();
                uint unknown1 = _reader.ReadUInt32();
                uint unknown2 = _reader.ReadUInt32();

                Console.WriteLine($"      VERT header: [faces={faceCount}, unk1={unknown1}, unk2={unknown2}]");
                Console.WriteLine($"      Reading {expectedVertexCount} vertices (40 bytes each)...");

                // ✅ Read vertices: 40 bytes each
                for (int i = 0; i < expectedVertexCount; i++)
                {
                    var vertex = new Vertex();
                    vertex.Position = ReadVector3();
                    vertex.Normal = ReadVector3();
                    vertex.Color = _reader.ReadUInt32();
                    vertex.UV = ReadVector2();

                    chunk.Vertices.Add(vertex);
                }

                Console.WriteLine($"      ✓ Read {chunk.Vertices.Count} vertices");

                // ✅ Calculate remaining bytes
                long bytesRead = 12 + (expectedVertexCount * 40);
                long totalChunkData = size - 8;
                long remaining = totalChunkData - bytesRead;

                if (remaining > 0)
                {
                    Console.WriteLine($"      ℹ Remaining {remaining} bytes (tangent space data)");

                    // ✅ TRY to read tangent/bitangent data (24 bytes per vertex)
                    long expectedTangentSize = expectedVertexCount * 24;

                    if (remaining >= expectedTangentSize - 100)  // Allow some padding tolerance
                    {
                        Console.WriteLine($"      Reading tangent/bitangent data (24 bytes/vertex)...");

                        int tangentCount = (int)(remaining / 24);

                        for (int i = 0; i < tangentCount && i < expectedVertexCount; i++)
                        {
                            Vector3 tangent = ReadVector3();      // 12 bytes
                            Vector3 bitangent = ReadVector3();    // 12 bytes

                            // ✅ Store in vertex
                            if (i < chunk.Vertices.Count)
                            {
                                chunk.Vertices[i].Tangent = tangent;
                                chunk.Vertices[i].Bitangent = bitangent;
                            }

                            if (i == 0)
                            {
                                Console.WriteLine($"        First tangent: {tangent}");
                                Console.WriteLine($"        First bitangent: {bitangent}");
                            }
                        }

                        long afterTangents = _reader.BaseStream.Position - dataStart - 8;
                        long finalRemaining = totalChunkData - afterTangents;

                        Console.WriteLine($"      ✓ Read tangent data for {tangentCount} vertices");

                        if (finalRemaining > 0)
                        {
                            Console.WriteLine($"      ℹ {finalRemaining} bytes padding at end");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"      Not enough data for full tangent set (need {expectedTangentSize}, have {remaining})");
                    }
                }

                // Seek to end of chunk
                _reader.BaseStream.Position = dataStart + size - 8;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ✗ Error: {ex.Message}");
                _reader.BaseStream.Seek(dataStart + size - 8, SeekOrigin.Begin);
            }

            return chunk;
        }

        private IstrChunk ReadIstrChunk(string id, uint size, long dataStart)
        {
            var chunk = new IstrChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.IndexCount = _reader.ReadUInt32();

            for (int i = 0; i < chunk.IndexCount; i++)
            {
                chunk.Indices.Add(_reader.ReadUInt16());
            }

            return chunk;
        }

        private FaceChunk ReadFaceChunk(string id, uint size, long dataStart)
        {
            var chunk = new FaceChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.FaceCount = _reader.ReadUInt32();

            var endPos = dataStart + chunk.ChunkDataSize;
            while (_reader.BaseStream.Position < endPos)
            {
                var subChunkId = ReadFourCC();
                var subChunkSize = _reader.ReadUInt32();
                var subDataStart = _reader.BaseStream.Position;
                var subDataSize = subChunkSize - 8;

                if (subChunkId == "LODP")
                {
                    chunk.LodpChunks.Add(ReadLodpChunk(subChunkId, subChunkSize, subDataStart));
                }
                else
                {
                    _reader.BaseStream.Position += subDataSize;
                }

                _reader.BaseStream.Position = subDataStart + subDataSize;
            }

            return chunk;
        }

        private LodpChunk ReadLodpChunk(string id, uint size, long dataStart)
        {
            var chunk = new LodpChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.KeyframeCount = _reader.ReadUInt32();
            chunk.Param1 = _reader.ReadUInt32();
            chunk.Param2 = _reader.ReadUInt32();
            chunk.Param3 = _reader.ReadUInt32();
            chunk.Param4 = _reader.ReadUInt32();

            // Read keyframes (40 bytes each)
            for (int i = 0; i < chunk.KeyframeCount; i++)
            {
                var keyframe = new LodpKeyframe();
                keyframe.Time = _reader.ReadSingle();
                keyframe.Rotation = ReadQuaternion();
                keyframe.Position = ReadVector3();

                // Read remaining floats
                keyframe.ExtraData = new float[3];
                for (int j = 0; j < 3; j++)
                {
                    keyframe.ExtraData[j] = _reader.ReadSingle();
                }

                chunk.Keyframes.Add(keyframe);
            }

            return chunk;
        }

        private ObmoChunk ReadObmoChunk(string id, uint size, long dataStart)
        {
            var chunk = new ObmoChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };

            try
            {
                // Read name first
                var nameLen = _reader.ReadUInt32();
                chunk.Name = ReadString((int)nameLen);

                Console.WriteLine($"  → OBMO: {chunk.Name}");

                // ✅ ÚJ: Read vertex/face counts (similar to MESH header)
                uint unknown = _reader.ReadUInt32();
                uint vertexCount = _reader.ReadUInt32();
                uint faceCount = _reader.ReadUInt32();

                Console.WriteLine($"  → Vertices: {vertexCount}, Faces: {faceCount}");

                // Read sub-chunks
                var endPos = dataStart + chunk.ChunkDataSize;

                while (_reader.BaseStream.Position < endPos)
                {
                    long subChunkStart = _reader.BaseStream.Position;
                    long remaining = endPos - subChunkStart;

                    if (remaining < 8)
                        break;

                    var subChunkId = ReadFourCC();
                    var subChunkSize = _reader.ReadUInt32();
                    var subDataStart = _reader.BaseStream.Position;
                    var subDataSize = subChunkSize - 8;

                    // Validate
                    if (subChunkSize < 8 || subChunkSize > remaining)
                    {
                        Console.WriteLine($"    ⚠ Invalid OBMO sub-chunk size: {subChunkSize}");
                        break;
                    }

                    Console.WriteLine($"    Sub-chunk: {subChunkId} ({subChunkSize} bytes)");

                    switch (subChunkId)
                    {
                        case "VERT":
                            // ✅ PASS VERTEX COUNT!
                            chunk.VertChunk = ReadVertChunkInMesh(subChunkId, subChunkSize, subDataStart, vertexCount);
                            break;

                        case "ISTR":
                            chunk.IstrChunk = ReadIstrChunk(subChunkId, subChunkSize, subDataStart);
                            break;

                        case "FACE":
                            chunk.FaceChunk = ReadFaceChunk(subChunkId, subChunkSize, subDataStart);
                            break;

                        default:
                            _reader.BaseStream.Position += subDataSize;
                            break;
                    }

                    _reader.BaseStream.Position = subDataStart + subDataSize;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Error parsing OBMO: {ex.Message}");
                _reader.BaseStream.Seek(dataStart + size - 8, SeekOrigin.Begin);
            }

            return chunk;
        }

        private ObskChunk ReadObskChunk(string id, uint size, long dataStart)
        {
            var chunk = new ObskChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.BoneCount = _reader.ReadUInt32();

            for (int i = 0; i < chunk.BoneCount; i++)
            {
                var bone = new ObskBone();
                bone.BoneId = _reader.ReadUInt32();
                bone.ParentId = _reader.ReadInt32();

                var nameLen = _reader.ReadUInt32();
                bone.Name = ReadString((int)nameLen);

                chunk.Bones.Add(bone);
            }

            return chunk;
        }

        // ========================================================================
        // ANIMATION CHUNK READERS
        // ========================================================================

        private BoneChunk ReadBoneChunk(string id, uint size, long dataStart)
        {
            var chunk = new BoneChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.BoneCount = _reader.ReadUInt32();
            chunk.KeyframeCount = _reader.ReadUInt32();

            for (int i = 0; i < chunk.BoneCount; i++)
            {
                var anim = new BoneAnimation();
                anim.BoneId = _reader.ReadUInt32();
                anim.ParentId = _reader.ReadInt32();

                var nameLen = _reader.ReadUInt32();
                anim.Name = ReadString((int)nameLen);

                for (int j = 0; j < chunk.KeyframeCount; j++)
                {
                    var keyframe = new BoneKeyframe();
                    keyframe.Time = _reader.ReadSingle();
                    keyframe.Position = ReadVector3();
                    keyframe.Rotation = ReadQuaternion();
                    keyframe.Scale = ReadVector3();
                    anim.Keyframes.Add(keyframe);
                }

                chunk.Animations.Add(anim);
            }

            return chunk;
        }

        // ========================================================================
        // MATERIAL & TEXTURE CHUNK READERS
        // ========================================================================

        private TextChunk ReadTextChunk(string id, uint size, long dataStart)
        {
            var chunk = new TextChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.Null = _reader.ReadUInt32();

            var pathLen = _reader.ReadUInt32();
            chunk.TexturePath = ReadString((int)pathLen);

            return chunk;
        }

        private MateChunk ReadMateChunk(string id, uint size, long dataStart)
        {
            long startPos = _reader.BaseStream.Position;
            var chunk = new MateChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };

            try
            {
                uint materialId = _reader.ReadUInt32();
                uint nameLength = _reader.ReadUInt32();
                string textureName = "";

                if (nameLength > 0 && nameLength < 1024)  // Sanity check
                {
                    textureName = ReadString((int)nameLength);
                }

                Console.WriteLine($"  → Material ID: {materialId}, Texture: '{textureName}'");

                // Create entry in new structure format
                if (!string.IsNullOrEmpty(textureName))
                {
                    var entry = new MaterialEntry();
                    entry.TypeName = "MATERIAL";

                    var property = new MaterialProperty();
                    property.Name = "TEXTURE";
                    property.TextureValue = textureName;

                    entry.Properties.Add(property);
                    chunk.Entries.Add(entry);
                }

                // Read remaining data
                long bytesRead = _reader.BaseStream.Position - startPos;
                long remaining = (size - 8) - bytesRead;

                if (remaining > 0)
                {
                    // Skip unknown data
                    uint entryCount = _reader.ReadUInt32();

                    if (remaining >= 100)  // Has 96-byte block
                    {
                        byte[] unknownData = _reader.ReadBytes(96);
                        remaining -= 100;
                    }

                    // Read entry data (20 bytes each)
                    for (int i = 0; i < entryCount && remaining >= 20; i++)
                    {
                        _reader.ReadBytes(20);
                        remaining -= 20;
                    }

                    // Skip any remaining bytes
                    if (remaining > 0)
                    {
                        _reader.ReadBytes((int)remaining);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Error parsing MATE chunk: {ex.Message}");

                // Skip to end of chunk
                long expectedEnd = dataStart + size - 8;
                if (_reader.BaseStream.Position < expectedEnd)
                {
                    _reader.BaseStream.Seek(expectedEnd, SeekOrigin.Begin);
                }
            }

            return chunk;
        }


        // ========================================================================
        // EFFECT CHUNK READERS
        // ========================================================================

        private BlstChunk ReadBlstChunk(string id, uint size, long dataStart)
        {
            var chunk = new BlstChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.Null = _reader.ReadUInt32();
            return chunk;
        }

        private ClouChunk ReadClouChunk(string id, uint size, long dataStart)
        {
            var chunk = new ClouChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.RawData = _reader.ReadBytes((int)chunk.ChunkDataSize);
            return chunk;
        }

        private OmniChunk ReadOmniChunk(string id, uint size, long dataStart)
        {
            var chunk = new OmniChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.RawData = _reader.ReadBytes((int)chunk.ChunkDataSize);
            return chunk;
        }

        private FlarChunk ReadFlarChunk(string id, uint size, long dataStart)
        {
            var chunk = new FlarChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.RawData = _reader.ReadBytes((int)chunk.ChunkDataSize);
            return chunk;
        }

        // ========================================================================
        // SCENE & ASSET CHUNK READERS
        // ========================================================================

        private AselChunk ReadAselChunk(string id, uint size, long dataStart)
        {
            var chunk = new AselChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.StringCount = _reader.ReadUInt32();

            for (int i = 0; i < chunk.StringCount; i++)
            {
                var assetString = new AssetString();
                assetString.StringId = _reader.ReadUInt32();
                assetString.Unknown = _reader.ReadUInt32();

                var strLen = _reader.ReadUInt32();
                assetString.String = ReadString((int)strLen);

                chunk.Strings.Add(assetString);
            }

            return chunk;
        }

        private AsecChunk ReadAsecChunk(string id, uint size, long dataStart)
        {
            var chunk = new AsecChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.Null = _reader.ReadUInt32();
            return chunk;
        }

        private ObstChunk ReadObstChunk(string id, uint size, long dataStart)
        {
            long startPos = _reader.BaseStream.Position;
            var chunk = new ObstChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };

            try
            {
                chunk.ObjectNumber = _reader.ReadUInt32();

                // Variable-length string
                uint nameLength = _reader.ReadUInt32();
                chunk.Name = "";
                if (nameLength > 0 && nameLength < 1024)  // Sanity check
                {
                    chunk.Name = ReadString((int)nameLength);
                }

                Console.WriteLine($"  → Object #{chunk.ObjectNumber}: {chunk.Name}");

                // Read unknown data (68 bytes)
                chunk.UnknownData = _reader.ReadBytes(68);

                // Parse sub-chunks
                long obstEnd = dataStart + size - 8;

                while (_reader.BaseStream.Position < obstEnd)
                {
                    long subChunkStart = _reader.BaseStream.Position;

                    // Check if enough space for chunk header
                    long remaining = obstEnd - subChunkStart;
                    if (remaining < 8)
                    {
                        Console.WriteLine($"    (End of OBST, {remaining} bytes remaining)");
                        break;
                    }

                    // Read chunk header
                    string subChunkId = ReadFourCC();
                    uint subChunkSize = _reader.ReadUInt32();

                    // ✅ CRITICAL: Validate chunk size
                    if (subChunkSize == 0 || subChunkSize < 8 || subChunkSize > remaining)
                    {
                        Console.WriteLine($"    ⚠ Invalid sub-chunk size: {subChunkSize} (remaining: {remaining}), stopping OBST parsing");
                        _reader.BaseStream.Position = subChunkStart; // Reset position
                        break;
                    }

                    Console.WriteLine($"    Sub-chunk: {subChunkId} ({subChunkSize} bytes)");

                    // Reset position to parse full chunk
                    _reader.BaseStream.Position = subChunkStart;
                    var subChunk = ReadChunk();

                    // Store important sub-chunks
                    if (subChunk is MeshChunk mesh)
                    {
                        chunk.MeshChunk = mesh;
                        Console.WriteLine($"    ✓ MESH stored in OBST");
                    }
                    else if (subChunk is MboxChunk mbox)
                    {
                        chunk.MboxChunk = mbox;
                    }
                    else if (subChunk is OboxChunk obox)
                    {
                        chunk.OboxChunk = obox;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Error parsing OBST: {ex.Message}");
                _reader.BaseStream.Seek(dataStart + size - 8, SeekOrigin.Begin);
            }

            return chunk;
        }

        private DumyChunk ReadDumyChunk(string id, uint size, long dataStart)
        {
            var chunk = new DumyChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.ObjectNumber = _reader.ReadUInt32();

            var nameLen = _reader.ReadUInt32();
            chunk.Name = ReadString((int)nameLen);

            chunk.UnknownData = _reader.ReadBytes(68);

            return chunk;
        }

        // ========================================================================
        // OTHER CHUNK READERS
        // ========================================================================

        private TimeChunk ReadTimeChunk(string id, uint size, long dataStart)
        {
            var chunk = new TimeChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.Timestamp = ReadString(24);
            return chunk;
        }

        private BmodChunk ReadUnknownChunk(string id, uint size, long dataStart)
        {
            var chunk = new BmodChunk { ChunkId = id, ChunkSize = size, ChunkDataStart = dataStart };
            chunk.RawData = _reader.ReadBytes((int)chunk.ChunkDataSize);
            return chunk;
        }

        // ========================================================================
        // HELPER METHODS
        // ========================================================================

        private string ReadFourCC()
        {
            var bytes = _reader.ReadBytes(4);
            return Encoding.ASCII.GetString(bytes);
        }

        private string ReadString(int length)
        {
            var bytes = _reader.ReadBytes(length);
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }

        private Vector2 ReadVector2()
        {
            return new Vector2(_reader.ReadSingle(), _reader.ReadSingle());
        }

        private Vector3 ReadVector3()
        {
            return new Vector3(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
        }

        private Quaternion ReadQuaternion()
        {
            return new Quaternion(
                _reader.ReadSingle(),
                _reader.ReadSingle(),
                _reader.ReadSingle(),
                _reader.ReadSingle()
            );
        }

        private string ReadFixedString(int length)
        {
            var bytes = _reader.ReadBytes(length);
            int nullIndex = Array.IndexOf(bytes, (byte)0);

            if (nullIndex >= 0)
            {
                return Encoding.ASCII.GetString(bytes, 0, nullIndex);
            }

            return Encoding.ASCII.GetString(bytes);
        }

    }
}
