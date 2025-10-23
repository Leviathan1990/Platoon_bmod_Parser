using System;
using System.IO;
using System.Text;

namespace BmodReader
{
    public class BmodWriter
    {
        private BinaryWriter _writer;

        public BmodWriter(BinaryWriter writer)
        {
            _writer = writer;
        }

        public void Write(BmodFile bmod)
        {
            WriteHeader(bmod.Header);

            foreach (var chunk in bmod.Chunks)
            {
                WriteChunk(chunk);
            }
        }

        private void WriteHeader(BmodHeader header)
        {
            WriteFourCC(header.Magic);
            _writer.Write(header.FileSize);
            _writer.Write(header.VersionMinor);
            _writer.Write(header.VersionMajor);
        }

        private void WriteChunk(BmodChunk chunk)
        {
            WriteFourCC(chunk.ChunkId);
            _writer.Write(chunk.ChunkSize);

            // Write chunk-specific data
            switch (chunk)
            {
                case MboxChunk mbox:
                    WriteMboxChunk(mbox);
                    break;
                case OboxChunk obox:
                    WriteOboxChunk(obox);
                    break;
                case TextChunk text:
                    WriteTextChunk(text);
                    break;
                case TimeChunk time:
                    WriteTimeChunk(time);
                    break;
                default:
                    if (chunk.RawData != null)
                        _writer.Write(chunk.RawData);
                    break;
            }
        }

        private void WriteMboxChunk(MboxChunk chunk)
        {
            WriteVector3(chunk.Min);
            WriteVector3(chunk.Max);
        }

        private void WriteOboxChunk(OboxChunk chunk)
        {
            WriteVector3(chunk.Center);
            WriteVector3(chunk.Normal);
        }

        private void WriteTextChunk(TextChunk chunk)
        {
            _writer.Write(chunk.Null);
            _writer.Write((uint)chunk.TexturePath.Length);
            WriteString(chunk.TexturePath);
        }

        private void WriteTimeChunk(TimeChunk chunk)
        {
            WriteString(chunk.Timestamp, 24);
        }

        private void WriteFourCC(string fourCC)
        {
            var bytes = Encoding.ASCII.GetBytes(fourCC);
            _writer.Write(bytes, 0, 4);
        }

        private void WriteString(string str, int? fixedLength = null)
        {
            var bytes = Encoding.ASCII.GetBytes(str);
            if (fixedLength.HasValue)
            {
                var buffer = new byte[fixedLength.Value];
                Array.Copy(bytes, buffer, Math.Min(bytes.Length, fixedLength.Value));
                _writer.Write(buffer);
            }
            else
            {
                _writer.Write(bytes);
            }
        }

        private void WriteVector2(Vector2 v)
        {
            _writer.Write(v.X);
            _writer.Write(v.Y);
        }

        private void WriteVector3(Vector3 v)
        {
            _writer.Write(v.X);
            _writer.Write(v.Y);
            _writer.Write(v.Z);
        }

        private void WriteQuaternion(Quaternion q)
        {
            _writer.Write(q.X);
            _writer.Write(q.Y);
            _writer.Write(q.Z);
            _writer.Write(q.W);
        }
    }
}
