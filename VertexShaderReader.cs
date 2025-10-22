using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BmodReader
{
    public class VertexShader
    {
        public string Version;                      // vs.1.0
        public List<string> Includes = new List<string>();
        public Dictionary<string, string> Defines = new Dictionary<string, string>();
        public List<string> Instructions = new List<string>();

        public static VertexShader Load(string filePath)
        {
            var vs = new VertexShader();

            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                // Parse version: vs.1.0
                if (trimmed.StartsWith("vs."))
                {
                    vs.Version = trimmed;
                    continue;
                }

                // Parse includes: #include "..\VERTEXFORMATS\Bump0.vf"
                var includeMatch = Regex.Match(trimmed, @"#include\s+""(.+)""");
                if (includeMatch.Success)
                {
                    vs.Includes.Add(includeMatch.Groups[1].Value);
                    continue;
                }

                // Parse defines: #define INTENSITY1 c0.x
                var defineMatch = Regex.Match(trimmed, @"#define\s+(\w+)\s+(.+)");
                if (defineMatch.Success)
                {
                    vs.Defines[defineMatch.Groups[1].Value] = defineMatch.Groups[2].Value;
                    continue;
                }

                // Parse instructions (everything else)
                if (!trimmed.StartsWith(";") && !trimmed.StartsWith("//"))
                {
                    vs.Instructions.Add(trimmed);
                }
            }

            return vs;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Vertex Shader {Version}");
            sb.AppendLine($"  Includes: {Includes.Count}");
            sb.AppendLine($"  Defines: {Defines.Count}");
            sb.AppendLine($"  Instructions: {Instructions.Count}");
            return sb.ToString();
        }
    }

    public class CompiledVertexShader
    {
        public ushort Version;
        public byte[] Bytecode;

        public static CompiledVertexShader Load(string filePath)
        {
            var vso = new CompiledVertexShader();
            var bytes = File.ReadAllBytes(filePath);

            if (bytes.Length < 4)
                throw new InvalidDataException("VSO file too small");

            // First 2 bytes = version (0x0100 = 1.0)
            vso.Version = BitConverter.ToUInt16(bytes, 0);

            // Rest is bytecode
            vso.Bytecode = new byte[bytes.Length - 4];
            Array.Copy(bytes, 4, vso.Bytecode, 0, bytes.Length - 4);

            return vso;
        }

        public string VersionString => $"vs.{Version >> 8}.{Version & 0xFF}";

        public override string ToString()
        {
            return $"Compiled Vertex Shader {VersionString} ({Bytecode.Length} bytes)";
        }
    }
}