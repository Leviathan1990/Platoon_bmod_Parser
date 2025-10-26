using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BmodReader
{



    public class MatFile
    {
        public uint Magic;              // 0xFFFFA5AD
        public uint FileSize;
        public uint Unknown1;
        public uint TechniqueCount;
        public uint Unknown2;
        public uint Unknown3;
        public uint PassCount;
        public uint PropertyCount;

        public List<MatProperty> Properties = new List<MatProperty>();
        public Dictionary<string, object> PropertyDict = new Dictionary<string, object>();

        public static MatFile Load(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            using (var reader = new BinaryReader(fs))
            {
                return Parse(reader);
            }
        }

        public static MatFile Load(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
            {
                return Parse(reader);
            }
        }

        private static MatFile Parse(BinaryReader reader)
        {
            var mat = new MatFile();

            // Read header
            mat.Magic = reader.ReadUInt32();
            mat.FileSize = reader.ReadUInt32();
            mat.Unknown1 = reader.ReadUInt32();
            mat.TechniqueCount = reader.ReadUInt32();
            mat.Unknown2 = reader.ReadUInt32();
            mat.Unknown3 = reader.ReadUInt32();
            mat.PassCount = reader.ReadUInt32();
            mat.PropertyCount = reader.ReadUInt32();

            // Validate magic
            if (mat.Magic != 0xFFFFA5AD)
            {
                throw new InvalidDataException($"Invalid MAT file magic: 0x{mat.Magic:X8}");
            }

            // SKIP 4-byte padding before properties
            reader.ReadUInt32();

            // Read properties
            for (int i = 0; i < mat.PropertyCount; i++)
            {
                var prop = ReadProperty(reader);
                mat.Properties.Add(prop);
            }

            // Try to read string table at the end
            try
            {
                if (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    uint stringCount = reader.ReadUInt32();

                    var propertyNames = new List<string>();
                    for (int i = 0; i < stringCount; i++)
                    {
                        string name = ReadNullTerminatedString(reader);
                        propertyNames.Add(name);
                    }

                    // Map property names to properties
                    for (int i = 0; i < Math.Min(mat.Properties.Count, propertyNames.Count); i++)
                    {
                        string propName = propertyNames[i];
                        if (!string.IsNullOrEmpty(propName) && !mat.PropertyDict.ContainsKey(propName))
                        {
                            mat.PropertyDict[propName] = mat.Properties[i].Value;
                        }
                    }
                }
            }
            catch
            {
                // String table not found or corrupted - use fallback names
                for (int i = 0; i < mat.Properties.Count; i++)
                {
                    var prop = mat.Properties[i];
                    string propName = GetPropertyName(prop.PropertyId);
                    if (!string.IsNullOrEmpty(propName) && !mat.PropertyDict.ContainsKey(propName))
                    {
                        mat.PropertyDict[propName] = prop.Value;
                    }
                }
            }

            return mat;
        }

        private static string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        private static MatProperty ReadProperty(BinaryReader reader)
        {
            var prop = new MatProperty();

            // Read property header (4 bytes total)
            prop.PropertyId = reader.ReadUInt16();  // 2 bytes
            prop.Type = reader.ReadUInt16();        // 2 bytes

            // Determine value type based on BOTH PropertyId AND Type
            if (prop.PropertyId == 0x0E && prop.Type == 0x0104)
            {
                // TextureFactor - ALWAYS Color4!
                prop.Value = new Color4(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
            }
            else if (prop.Type == 0x0101 || prop.Type == 0x0104)
            {
                // Float (most common)
                prop.Value = reader.ReadSingle();
            }
            else if (prop.Type == 0x000A)
            {
                // Layer/Array with size prefix
                uint layerSize = reader.ReadUInt32();
                if (layerSize > 0)
                {
                    byte[] layerData = reader.ReadBytes((int)layerSize);
                    prop.Value = $"<layer {layerSize} bytes>";
                }
                else
                {
                    prop.Value = "<empty layer>";
                }
            }
            else if (prop.Type == 0x0005)
            {
                // Array with size prefix
                uint arraySize = reader.ReadUInt32();
                byte[] arrayData = reader.ReadBytes((int)arraySize);
                prop.Value = $"<array {arraySize} bytes>";
            }
            else if (prop.Type == 0x0000)
            {
                // Skip/Null
                prop.Value = 0.0f;
            }
            else
            {
                // Unknown type - try to read as float
                try
                {
                    prop.Value = reader.ReadSingle();
                }
                catch
                {
                    prop.Value = $"<unknown type 0x{prop.Type:X4}>";
                }
            }

            return prop;
        }

        public static string GetPropertyName(uint propId)
        {
            return propId switch
            {
                0x01 => "Technique",
                0x02 => "FillMode",
                0x03 => "Cull",
                0x04 => "DitherEnable",
                0x05 => "SpecularEnable",
                0x06 => "LightingEnable",
                0x07 => "NormalizeNormals",
                0x08 => "LocalViewer",
                0x09 => "ColorVertexEnable",
                0x0A => "AlphaTestEnable",
                0x0B => "AlphaFunc",
                0x0C => "AlphaRef",
                0x0D => "AlphaBlendEnable",
                0x0E => "TextureFactor",           // or DiffuseMaterialSource
                0x0F => "DestBlend",               // or DiffuseMaterialSource  
                0x10 => "ZWriteEnable",            // or SpecularMaterialSource
                0x11 => "ZFunc",                   // or AmbientMaterialSource
                0x12 => "Layer",                   // or EmissiveMaterialSource
                0x13 => "TextureSource",           // or Layer
                0x14 => "TexCoordIndex",           // or TextureSource
                0x15 => "ColorArg1",               // or TexCoordIndex
                0x16 => "ColorArg2",
                0x17 => "ColorOp",
                0x18 => "AlphaArg1",
                0x19 => "AlphaOp",
                0x1A => "TextureAddressU",         // ✅ FIXED!
                0x1B => "TextureAddressV",         // ✅ FIXED!
                0x1C => "Filter",                  // ✅ FIXED!
                0x1D => "Filter",                  // ✅ duplicate OK
                0x1E => "SrcBlend",
                0x1F => "DestBlend",
                0x20 => "AlphaRef",
                _ => $"Property{propId:X2}"
            };
        }

        // Decode property value to human-readable string
        public static string DecodePropertyValue(uint propId, object value)
        {
            if (value is float floatVal)
            {
                // Decode common property types
                switch (propId)
                {
                    case 0x02: // FillMode
                        if (floatVal == 1.0f) return "FILL_POINT (1)";
                        if (floatVal == 2.0f) return "FILL_WIREFRAME (2)";
                        if (floatVal == 3.0f) return "FILL_SOLID (3)";
                        break;

                    case 0x03: // Cull
                        if (floatVal == 1.0f) return "CULL_NONE (1)";
                        if (floatVal == 2.0f) return "CULL_CW (2)";
                        if (floatVal == 3.0f) return "CULL_CCW (3)";
                        break;

                    case 0x0D: // ZFunc
                        if (floatVal == 1.0f) return "CMP_NEVER (1)";
                        if (floatVal == 2.0f) return "CMP_LESS (2)";
                        if (floatVal == 3.0f) return "CMP_EQUAL (3)";
                        if (floatVal == 4.0f) return "CMP_LESSEQUAL (4)";
                        if (floatVal == 5.0f) return "CMP_GREATER (5)";
                        if (floatVal == 6.0f) return "CMP_NOTEQUAL (6)";
                        if (floatVal == 7.0f) return "CMP_GREATEREQUAL (7)";
                        if (floatVal == 8.0f) return "CMP_ALWAYS (8)";
                        break;

                    case 0x18: // ColorOp
                        if (floatVal == 1.0f) return "TOP_DISABLE (1)";
                        if (floatVal == 2.0f) return "TOP_SELECTARG1 (2)";
                        if (floatVal == 3.0f) return "TOP_SELECTARG2 (3)";
                        if (floatVal == 4.0f) return "TOP_MODULATE (4)";
                        if (floatVal == 5.0f) return "TOP_MODULATE2X (5)";
                        if (floatVal == 6.0f) return "TOP_MODULATE4X (6)";
                        if (floatVal == 7.0f) return "TOP_ADD (7)";
                        break;

                    case 0x1A: // AlphaOp
                        if (floatVal == 1.0f) return "TOP_DISABLE (1)";
                        if (floatVal == 2.0f) return "TOP_SELECTARG1 (2)";
                        if (floatVal == 4.0f) return "TOP_MODULATE (4)";
                        break;

                    case 0x16: // ColorArg1
                    case 0x17: // ColorArg2
                    case 0x19: // AlphaArg1
                        if (floatVal == 0.0f) return "TA_DIFFUSE (0)";
                        if (floatVal == 1.0f) return "TA_CURRENT (1)";
                        if (floatVal == 2.0f) return "TA_TEXTURE (2)";
                        if (floatVal == 3.0f) return "TA_TFACTOR (3)";
                        break;

                    case 0x1B: // TextureAddressU
                    case 0x1C: // TextureAddressV
                        if (floatVal == 1.0f) return "TADDRESS_WRAP (1)";
                        if (floatVal == 2.0f) return "TADDRESS_MIRROR (2)";
                        if (floatVal == 3.0f) return "TADDRESS_CLAMP (3)";
                        if (floatVal == 4.0f) return "TADDRESS_BORDER (4)";
                        break;

                    case 0x1D: // Filter
                        if (floatVal == 0.0f) return "TF_POINT (0)";
                        if (floatVal == 1.0f) return "TF_POINT_MIP_POINT (1)";
                        if (floatVal == 2.0f) return "TF_POINT_MIP_LINEAR (2)";
                        if (floatVal == 3.0f) return "TF_LINEAR (3)";
                        if (floatVal == 4.0f) return "TF_LINEAR_MIP_POINT (4)";
                        if (floatVal == 5.0f) return "TF_LINEAR_MIP_LINEAR (5)";
                        if (floatVal == 6.0f) return "TF_ANISOTROPIC (6)";
                        break;

                    case 0x1E: // SrcBlend
                    case 0x1F: // DestBlend
                        if (floatVal == 1.0f) return "BLEND_ZERO (1)";
                        if (floatVal == 2.0f) return "BLEND_ONE (2)";
                        if (floatVal == 5.0f) return "BLEND_SRCALPHA (5)";
                        if (floatVal == 6.0f) return "BLEND_INVSRCALPHA (6)";
                        break;

                    // Boolean properties
                    case 0x04:
                    case 0x05:
                    case 0x06:
                    case 0x07:
                    case 0x08:
                    case 0x09:
                    case 0x0A:
                    case 0x0B:
                    case 0x0C: // DitherEnable through ZWriteEnable
                        return floatVal != 0.0f ? "TRUE (1)" : "FALSE (0)";
                }

                // Default: show as number
                return floatVal.ToString("F3");
            }
            else if (value is Color4 color)
            {
                return color.ToString();
            }
            else if (value is string str)
            {
                return str;
            }

            return value?.ToString() ?? "null";
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"MAT File:");
            sb.AppendLine($"  Magic: 0x{Magic:X8}");
            sb.AppendLine($"  Size: {FileSize} bytes");
            sb.AppendLine($"  Techniques: {TechniqueCount}");
            sb.AppendLine($"  Passes: {PassCount}");
            sb.AppendLine($"  Properties: {PropertyCount}");
            sb.AppendLine();

            foreach (var prop in Properties)
            {
                string name = GetPropertyName(prop.PropertyId);
                string decodedValue = DecodePropertyValue(prop.PropertyId, prop.Value);
                sb.AppendLine($"  {name,-20} = {decodedValue}");
            }

            return sb.ToString();
        }
    }

    public class MatProperty
    {
    
        public uint PropertyId;
        public ushort Type;
        public ushort Flags;
        public object Value;
 
    }


}
