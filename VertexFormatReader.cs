using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BmodReader
{
    public class VertexFormatDefinition
    {
        public Dictionary<string, string> Defines = new Dictionary<string, string>();
        public List<VertexElement> Elements = new List<VertexElement>();
        public int TotalSize;

        public static VertexFormatDefinition LoadVF(string filePath)
        {
            var vf = new VertexFormatDefinition();

            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";"))
                    continue;

                // Parse #define Position v0
                var match = Regex.Match(trimmed, @"#define\s+(\w+)\s+(\w+)");
                if (match.Success)
                {
                    vf.Defines[match.Groups[1].Value] = match.Groups[2].Value;
                }
            }

            return vf;
        }

        public static VertexFormatDefinition LoadVFD(string filePath)
        {
            var vf = new VertexFormatDefinition();
            int currentStream = 0;
            int offset = 0;

            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith(";") ||
                    trimmed == "[VertexFormat]" ||
                    trimmed == "{" ||
                    trimmed == "}")
                    continue;

                // Parse Stream = 0
                var streamMatch = Regex.Match(trimmed, @"Stream\s*=\s*(\d+)");
                if (streamMatch.Success)
                {
                    currentStream = int.Parse(streamMatch.Groups[1].Value);
                    if (currentStream > 0)
                        offset = 0; // New stream resets offset
                    continue;
                }

                // Parse Reg = VSDT_FLOAT3
                var regMatch = Regex.Match(trimmed, @"Reg\s*=\s*(\w+)");
                if (regMatch.Success)
                {
                    string type = regMatch.Groups[1].Value;
                    int size = GetTypeSize(type);

                    vf.Elements.Add(new VertexElement
                    {
                        Stream = currentStream,
                        Type = type,
                        Size = size,
                        Offset = offset
                    });

                    offset += size;
                }
            }

            vf.TotalSize = offset;
            return vf;
        }

        private static int GetTypeSize(string type)
        {
            return type switch
            {
                "VSDT_FLOAT1" => 4,
                "VSDT_FLOAT2" => 8,
                "VSDT_FLOAT3" => 12,
                "VSDT_FLOAT4" => 16,
                "VSDT_D3DCOLOR" => 4,
                "VSDT_UBYTE4" => 4,
                "VSDT_SHORT2" => 4,
                "VSDT_SHORT4" => 8,
                _ => 4
            };
        }
    }

    public class VertexElement
    {
        public int Stream;
        public string Type;
        public int Size;
        public int Offset;

        public override string ToString()
        {
            return $"Stream {Stream}: {Type} ({Size} bytes) @ offset {Offset}";
        }
    }
}