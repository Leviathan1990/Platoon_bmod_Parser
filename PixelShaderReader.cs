using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BmodReader
{
    /// <summary>
    /// DirectX Pixel Shader (PS 1.x - 3.0) reader for Platoon/Haegemonia
    /// Supports: .psh (source), .pso (compiled bytecode)
    /// </summary>
    public class PixelShader
    {
        public string Version { get; set; }                          // ps.1.1, ps.2.0, etc.
        public List<string> Constants { get; set; }                  // def c0, 1.0, 0.5, ...
        public List<string> Instructions { get; set; }               // tex, mul, add, etc.
        public Dictionary<string, string> Registers { get; set; }    // r0, t0, v0, c0
        public string SourceFile { get; set; }

        public PixelShader()
        {
            Constants = new List<string>();
            Instructions = new List<string>();
            Registers = new Dictionary<string, string>();
        }

        /// <summary>
        /// Load pixel shader source file (.psh)
        /// </summary>
        public static PixelShader Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Pixel shader file not found: {filePath}");

            var ps = new PixelShader();
            ps.SourceFile = Path.GetFileName(filePath);

            try
            {
                var lines = File.ReadAllLines(filePath);
                ParseShaderSource(ps, lines);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse pixel shader: {ex.Message}", ex);
            }

            return ps;
        }

        /// <summary>
        /// Parse pixel shader source code
        /// </summary>
        private static void ParseShaderSource(PixelShader ps, string[] lines)
        {
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith(";") ||
                    trimmed.StartsWith("//"))
                    continue;

                // Parse version: ps.1.1, ps.1.4, ps.2.0
                if (trimmed.StartsWith("ps."))
                {
                    ps.Version = trimmed;
                    continue;
                }

                // Parse constants: def c0, 1.0, 0.5, 0.0, 1.0
                if (trimmed.StartsWith("def "))
                {
                    ps.Constants.Add(trimmed);
                    ParseConstantRegister(ps, trimmed);
                    continue;
                }

                // Parse texture declarations: tex t0, tex t1
                if (trimmed.StartsWith("tex "))
                {
                    ParseTextureRegister(ps, trimmed);
                    ps.Instructions.Add(trimmed);
                    continue;
                }

                // All other instructions (mul, add, mad, dp3, etc.)
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    ps.Instructions.Add(trimmed);
                }
            }
        }

        /// <summary>
        /// Parse constant register definition
        /// Example: def c0, 1.0, 0.5, 0.0, 1.0
        /// </summary>
        private static void ParseConstantRegister(PixelShader ps, string line)
        {
            var match = Regex.Match(line, @"def\s+(c\d+)");
            if (match.Success)
            {
                string register = match.Groups[1].Value;
                ps.Registers[register] = "constant";
            }
        }

        /// <summary>
        /// Parse texture register
        /// Example: tex t0
        /// </summary>
        private static void ParseTextureRegister(PixelShader ps, string line)
        {
            var match = Regex.Match(line, @"tex\s+(t\d+)");
            if (match.Success)
            {
                string register = match.Groups[1].Value;
                ps.Registers[register] = "texture";
            }
        }

        /// <summary>
        /// Get shader complexity (instruction count)
        /// </summary>
        public int InstructionCount => Instructions.Count;

        /// <summary>
        /// Get shader version as float (1.1, 2.0, 3.0)
        /// </summary>
        public float VersionNumber
        {
            get
            {
                if (string.IsNullOrEmpty(Version))
                    return 0.0f;

                var match = Regex.Match(Version, @"ps\.(\d+)\.(\d+)");
                if (match.Success)
                {
                    int major = int.Parse(match.Groups[1].Value);
                    int minor = int.Parse(match.Groups[2].Value);
                    return major + (minor / 10.0f);
                }

                return 0.0f;
            }
        }

        /// <summary>
        /// Check if shader uses specific instruction
        /// </summary>
        public bool UsesInstruction(string instruction)
        {
            return Instructions.Exists(i => i.StartsWith(instruction + " "));
        }

        /// <summary>
        /// Get all texture samplers used (t0, t1, t2, ...)
        /// </summary>
        public List<string> GetTextureSamplers()
        {
            var samplers = new List<string>();
            foreach (var instruction in Instructions)
            {
                var match = Regex.Match(instruction, @"tex\s+(t\d+)");
                if (match.Success)
                {
                    string sampler = match.Groups[1].Value;
                    if (!samplers.Contains(sampler))
                        samplers.Add(sampler);
                }
            }
            return samplers;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Pixel Shader: {SourceFile}");
            sb.AppendLine($"  Version:      {Version ?? "unknown"}");
            sb.AppendLine($"  Constants:    {Constants.Count}");
            sb.AppendLine($"  Instructions: {Instructions.Count}");
            sb.AppendLine($"  Registers:    {Registers.Count}");

            if (GetTextureSamplers().Count > 0)
            {
                sb.AppendLine($"  Textures:     {string.Join(", ", GetTextureSamplers())}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Export shader to text format
        /// </summary>
        public void ExportToText(string outputPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"; Pixel Shader: {SourceFile}");
            sb.AppendLine($"; Exported from BMOD Parser");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(Version))
                sb.AppendLine(Version);

            if (Constants.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("; Constants");
                foreach (var constant in Constants)
                    sb.AppendLine(constant);
            }

            if (Instructions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("; Instructions");
                foreach (var instruction in Instructions)
                    sb.AppendLine(instruction);
            }

            File.WriteAllText(outputPath, sb.ToString());
        }
    }

    /// <summary>
    /// Compiled pixel shader bytecode (.pso)
    /// </summary>
    public class CompiledPixelShader
    {
        public ushort Version { get; set; }
        public byte[] Bytecode { get; set; }
        public string SourceFile { get; set; }

        /// <summary>
        /// Load compiled pixel shader (.pso)
        /// </summary>
        public static CompiledPixelShader Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Compiled shader file not found: {filePath}");

            var pso = new CompiledPixelShader();
            pso.SourceFile = Path.GetFileName(filePath);

            var bytes = File.ReadAllBytes(filePath);

            if (bytes.Length < 4)
                throw new InvalidDataException("PSO file too small (minimum 4 bytes)");

            // First 2 bytes = version (0x0101 = ps.1.1, 0x0200 = ps.2.0)
            pso.Version = BitConverter.ToUInt16(bytes, 0);

            // Skip header (usually 4 bytes)
            int headerSize = 4;
            pso.Bytecode = new byte[bytes.Length - headerSize];
            Array.Copy(bytes, headerSize, pso.Bytecode, 0, bytes.Length - headerSize);

            return pso;
        }

        public string VersionString
        {
            get
            {
                int major = (Version >> 8) & 0xFF;
                int minor = Version & 0xFF;
                return $"ps.{major}.{minor}";
            }
        }

        public override string ToString()
        {
            return $"Compiled Pixel Shader: {SourceFile}\n" +
                   $"  Version:  {VersionString}\n" +
                   $"  Bytecode: {Bytecode.Length} bytes";
        }

        /// <summary>
        /// Export bytecode to file
        /// </summary>
        public void ExportBytecode(string outputPath)
        {
            File.WriteAllBytes(outputPath, Bytecode);
        }

        /// <summary>
        /// Get hex dump of bytecode (for debugging)
        /// </summary>
        public string GetHexDump(int maxBytes = 256)
        {
            var sb = new StringBuilder();
            int bytesToShow = Math.Min(Bytecode.Length, maxBytes);

            for (int i = 0; i < bytesToShow; i += 16)
            {
                sb.Append($"{i:X4}: ");

                // Hex values
                for (int j = 0; j < 16 && i + j < bytesToShow; j++)
                {
                    sb.Append($"{Bytecode[i + j]:X2} ");
                }

                // ASCII representation
                sb.Append("  ");
                for (int j = 0; j < 16 && i + j < bytesToShow; j++)
                {
                    byte b = Bytecode[i + j];
                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    sb.Append(c);
                }

                sb.AppendLine();
            }

            if (Bytecode.Length > maxBytes)
                sb.AppendLine($"... ({Bytecode.Length - maxBytes} more bytes)");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Pixel shader instruction set helper
    /// </summary>
    public static class PixelShaderInstructions
    {
        // PS 1.x instructions
        public static readonly string[] PS1_Instructions =
        {
            "add", "sub", "mul", "mad", "dp3", "dp4",
            "lrp", "mov", "cnd", "cmp",
            "tex", "texcoord", "texkill", "texbem", "texbeml",
            "texreg2ar", "texreg2gb", "texm3x2pad", "texm3x2tex",
            "texm3x3pad", "texm3x3tex", "texm3x3spec", "texm3x3vspec"
        };

        // PS 2.x instructions
        public static readonly string[] PS2_Instructions =
        {
            "abs", "add", "cmp", "crs", "dp3", "dp4",
            "frc", "mad", "max", "min", "mov", "mul",
            "nrm", "pow", "rcp", "rsq", "sge", "slt",
            "sub", "tex", "texbias", "texgrad", "texldd",
            "texldl", "texldp", "sincos", "log", "exp"
        };

        public static bool IsValidPS1Instruction(string instruction)
        {
            return Array.Exists(PS1_Instructions, i => instruction.StartsWith(i + " "));
        }

        public static bool IsValidPS2Instruction(string instruction)
        {
            return Array.Exists(PS2_Instructions, i => instruction.StartsWith(i + " "));
        }
    }
}
