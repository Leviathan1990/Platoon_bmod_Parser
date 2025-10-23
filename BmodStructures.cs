using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace BmodReader
{
    public struct Color4
    {
        public float R, G, B, A;

        public Color4(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public override string ToString() => $"RGBA({R:F3}, {G:F3}, {B:F3}, {A:F3})";
    }

    // ============================================================================
    // HEADER
    // ============================================================================

    public struct BmodHeader
    {
        public string Magic;        // "OMOD" or "BMOD" (4 bytes)
        public uint FileSize;       // Total file size
        public ushort VersionMinor; // e.g., 13 for v1.13
        public ushort VersionMajor; // e.g., 1 for v1.13

        public string VersionString => $"v{VersionMajor}.{VersionMinor}";

        public const int SizeInBytes = 12;
    }

    // ============================================================================
    // BASE CHUNK
    // ============================================================================

    public class BmodChunk
    {
        public string ChunkId;      // FourCC (4 bytes)
        public uint ChunkSize;      // Size INCLUDING 8-byte header
        public long ChunkDataStart; // File offset where data starts
        public uint ChunkDataSize => ChunkSize - 8; // Actual data size

        public byte[] RawData;      // Raw chunk data (for unknown chunks)

        public const int HeaderSize = 8;
    }

    // ============================================================================
    // GEOMETRY CHUNKS
    // ============================================================================

    public class MboxChunk : BmodChunk
    {
        public Vector3 Min;
        public Vector3 Max;
    }

    public class OboxChunk : BmodChunk
    {
        public Vector3 Center;
        public Vector3 Normal;
    }

    public class Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
        public uint Color;
        //  Tangent space (normal mapping)

        public Vector3 Tangent;     // 12 bytes
        public Vector3 Bitangent;   // 12 bytes

        public const int SizeInBytes = 40; // 40 bytes
        public const int TangentSizeInBytes = 24; //    Tangent + Bitangent
    }

    // ============================================================================
    // VERTEX FORMATS
    // ============================================================================

    public enum VertexFormat
    {
        Standard = 0,       // 32 bytes: Position + Normal + UV
        Simple = 1,         // 24 bytes: Position + Data
        Bump = 2,           // 56 bytes: Position + UV + Tangent + Bitangent + Binormal
        Skinned = 3,        // With bone weights
        Morph = 4,          // Face morphing (multiple streams)
    }

    public class VertexBump
    {
        public Vector3 Position;
        public Vector2 UV;
        public Vector3 TangentS;
        public Vector3 TangentT;
        public Vector3 Binormal;

        public const int SizeInBytes = 56; // 14 floats × 4 bytes
    }

    public class VertexMorph
    {
        public Vector3 DefaultPosition;
        public Vector3 DefaultNormal;
        public Vector2 UV;

        // Morph targets (deltas)
        public Vector3[] MorphPositions = new Vector3[6];
        public Vector3[] MorphNormals = new Vector3[6];

        // Total size depends on active morph channels
    }

    public class VertexFormat4
    {
        // Alternative vertex format (24 bytes)
        public Vector3 Position;
        public Vector3 Data; // Normal or other data

        public const int SizeInBytes = 24; // 6 floats × 4 bytes
    }

    public class VertChunk : BmodChunk
    {
        public byte Unknown1;
        public byte Unknown2;
        public uint StartIndex;
        public uint EndIndex;
        public uint TriangleCount; // Or index count
        public uint Unknown3;
        public uint Unknown4;
        public uint Unknown5;

        // Vertices are read by sub-chunk parser
        public List<Vertex> Vertices = new List<Vertex>();

        // Entry data (after vertices)
        public uint EntryCount;
        public List<uint> Entries = new List<uint>();
    }

    public class IstrChunk : BmodChunk
    {
        public uint IndexCount;
        public List<ushort> Indices = new List<ushort>();
    }

    public class FsecChunk : BmodChunk
    {
        public uint Null1;
        public uint Null2;
        public uint Null3;
        public uint FaceIndexCount;
        public uint Unknown;
        public uint FirstFaceIndex;
        public uint LastFaceIndex;

        public const int DataSizeInBytes = 34;
    }

    public class LodpKeyframe
    {
        public float Time;
        public Quaternion Rotation;
        public Vector3 Position;    // Or other 3-component data
        public float[] ExtraData;   // Additional floats

        public const int SizeInBytes = 40; // 10 floats × 4 bytes
    }

    public class LodpChunk : BmodChunk
    {
        public uint KeyframeCount;
        public uint Param1;
        public uint Param2;
        public uint Param3;
        public uint Param4;

        public List<LodpKeyframe> Keyframes = new List<LodpKeyframe>();

        // Spline data
        public object QuaternionSpline; // cQuaternionSpline (16 bytes)
    }

    public class FaceChunk : BmodChunk
    {
        public uint FaceCount;
        public List<LodpChunk> LodpChunks = new List<LodpChunk>();
    }

    public class MeshChunk : BmodChunk
    {
        public uint Unknown;
        public uint VertexCount;
        public uint FaceCount;

        public VertChunk VertChunk;
        public IstrChunk IstrChunk;
        public FaceChunk FaceChunk;
    }

    public class ObmoChunk : BmodChunk
    {
        // cBaseMorphed3dObject (564 bytes total)
        public string Name;

        // Sub-chunks (similar to MESH)
        public VertChunk VertChunk;
        public IstrChunk IstrChunk;
        public FaceChunk FaceChunk;

        // Additional morph data
        public byte[] MorphData; // 3 uints at offset 556-560
    }

    public class ObskBone
    {
        public uint BoneId;
        public int ParentId; // -1 = root
        public string Name;
    }

    public class ObskChunk : BmodChunk
    {
        public uint BoneCount;
        public List<ObskBone> Bones = new List<ObskBone>();
    }

    // ============================================================================
    // ANIMATION CHUNKS
    // ============================================================================

    public class BoneKeyframe
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public const int SizeInBytes = 44; // 11 floats × 4 bytes
    }

    public class BoneAnimation
    {
        public uint BoneId;
        public int ParentId;
        public string Name;

        public List<BoneKeyframe> Keyframes = new List<BoneKeyframe>();
    }

    public class BoneChunk : BmodChunk
    {
        // cBaseBone (284 bytes object)
        public uint BoneCount;
        public uint KeyframeCount;

        public List<BoneAnimation> Animations = new List<BoneAnimation>();
    }

    // ============================================================================
    // MATERIAL & TEXTURE CHUNKS
    // ============================================================================

    public class TextChunk : BmodChunk
    {
        public uint Null;
        public string TexturePath;
    }

    public class MaterialEntry
    {
        //public byte[] Data; // 20 bytes

        public string TypeName;
        public List<MaterialProperty> Properties = new List<MaterialProperty>();
    }


    /// <summary>
    /// Material property for BMOD MATE chunks
    /// (Name, TextureValue for "TEXTURE" properties)
    /// </summary>
    public class MaterialProperty
    {
        public string Name;             // Property name: "TEXTURE", "Size", "Color", "Roll" (260 bytes)
        public string TextureValue;     // Texture file name (if TEXTURE property) (260 bytes)

        // Additional property values (for Size, Color, Roll, etc.)
        public float[] FloatValues;     // Numeric values
        public int[] IntValues;         // Integer values
    }

    public class MateChunk : BmodChunk
    {
        public List<MaterialEntry> Entries = new List<MaterialEntry>();
    }

    // ============================================================================
    // EFFECT CHUNKS
    // ============================================================================

    public class BlstChunk : BmodChunk
    {
        public uint Null;
    }

    public class ClouChunk : BmodChunk
    {
        // Structure unknown
    }

    public class OmniChunk : BmodChunk
    {
        // Structure unknown
    }

    public class FlarChunk : BmodChunk
    {
        // Structure unknown
    }

    // ============================================================================
    // SCENE & ASSET CHUNKS
    // ============================================================================

    public class AssetString
    {
        public uint StringId;
        public uint Unknown;
        public string String;
    }

    public class AselChunk : BmodChunk
    {
        public uint StringCount;
        public List<AssetString> Strings = new List<AssetString>();
    }

    public class AsecChunk : BmodChunk
    {
        public uint Null;
    }

    public class ObstChunk : BmodChunk
    {
        public uint ObjectNumber;
        public string Name;
        public byte[] UnknownData; // 68 bytes

        // ✅ Sub-chunks inside OBST
        public MeshChunk MeshChunk;
        public MboxChunk MboxChunk;
        public OboxChunk OboxChunk;
    }

    // ============================================================================
    // DUMMY CHUNKS
    // ============================================================================

    public class DumyChunk : BmodChunk
    {
        // cBaseDummy (284 bytes object)
        public uint ObjectNumber;
        public string Name;
        public byte[] UnknownData; // 68 bytes
    }

    // ============================================================================
    // OTHER CHUNKS
    // ============================================================================

    public class TimeChunk : BmodChunk
    {
        public string Timestamp; // 24 characters (e.g., "Mon Mar 18 18:55:26 2002")
    }

    // ============================================================================
    // HELPER STRUCTURES
    // ============================================================================

    public struct Vector2
    {
        public float X, Y;

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }

    public struct Vector3
    {
        public float X, Y, Z;

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }

    public struct Quaternion
    {
        public float X, Y, Z, W;

        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3}, {W:F3})";
    }



}
