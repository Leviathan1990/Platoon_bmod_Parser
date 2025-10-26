using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace BmodReader
{
    public class ObjExporter
    {
        private static Dictionary<string, string> _textureCache;
        private static Dictionary<string, MatFile> _materialCache;

        public static void SetMaterialCache(Dictionary<string, MatFile> cache)
        {
            _materialCache = cache;
        }

        public static void SetTextureCache(Dictionary<string, string> cache)
        {
            _textureCache = cache;
        }

        public static void Export(BmodFile bmod, string outputPath)
        {
            var culture = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var mtlFileName = Path.ChangeExtension(Path.GetFileName(outputPath), ".mtl");

            sb.AppendLine("# Exported from BMOD file");
            sb.AppendLine($"# File: {Path.GetFileName(outputPath)}");
            sb.AppendLine($"# Version: {bmod.Header.VersionString}");
            sb.AppendLine($"# Meshes: {bmod.MeshChunks.Count}, OBMO: {bmod.ObmoChunks.Count}");
            sb.AppendLine($"mtllib {mtlFileName}");
            sb.AppendLine();

            int vertexOffset = 1;
            int meshIndex = 0;

            // Export all MESH chunks
            foreach (var mesh in bmod.MeshChunks)
            {
                if (mesh.VertChunk == null || mesh.IstrChunk == null)
                    continue;

                string materialName = "default";
                if (meshIndex < bmod.MateChunks.Count)
                {
                    var mate = bmod.MateChunks[meshIndex];
                    string textureName = GetTextureFromMate(mate);
                    if (!string.IsNullOrEmpty(textureName))
                    {
                        materialName = SanitizeMaterialName(textureName);
                    }
                }

                sb.AppendLine($"# Mesh {meshIndex} - {mesh.VertexCount} vertices, {mesh.FaceCount} faces");
                sb.AppendLine($"o Mesh_{meshIndex}");
                sb.AppendLine($"usemtl {materialName}");
                sb.AppendLine();

                // Write vertices
                foreach (var vertex in mesh.VertChunk.Vertices)
                {
                    sb.AppendLine($"v {vertex.Position.X.ToString(culture)} " +
                                  $"{vertex.Position.Y.ToString(culture)} " +
                                  $"{vertex.Position.Z.ToString(culture)}");
                }

                // Write normals
                foreach (var vertex in mesh.VertChunk.Vertices)
                {
                    sb.AppendLine($"vn {vertex.Normal.X.ToString(culture)} " +
                                  $"{vertex.Normal.Y.ToString(culture)} " +
                                  $"{vertex.Normal.Z.ToString(culture)}");
                }

                // Write UVs
                foreach (var vertex in mesh.VertChunk.Vertices)
                {
                    sb.AppendLine($"vt {vertex.UV.X.ToString(culture)} " +
                                  $"{(1.0f - vertex.UV.Y).ToString(culture)}");
                }

                // Write faces
                var indices = mesh.IstrChunk.Indices;
                for (int i = 0; i < indices.Count; i += 3)
                {
                    if (i + 2 >= indices.Count) break;

                    int v1 = indices[i] + vertexOffset;
                    int v2 = indices[i + 1] + vertexOffset;
                    int v3 = indices[i + 2] + vertexOffset;

                    sb.AppendLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
                }
                sb.AppendLine();

                vertexOffset += mesh.VertChunk.Vertices.Count;
                meshIndex++;
            }

            // Export OBMO chunks
            foreach (var obmo in bmod.ObmoChunks)
            {
                if (obmo.VertChunk == null || obmo.IstrChunk == null)
                    continue;

                string materialName = "default";
                string objectName = obmo.Name?.Replace(" ", "_") ?? $"OBMO_{meshIndex}";

                sb.AppendLine($"# OBMO - {obmo.Name}");
                sb.AppendLine($"o {objectName}");
                sb.AppendLine($"usemtl {materialName}");
                sb.AppendLine();

                // Write vertices
                foreach (var vertex in obmo.VertChunk.Vertices)
                {
                    sb.AppendLine($"v {vertex.Position.X.ToString(culture)} " +
                                  $"{vertex.Position.Y.ToString(culture)} " +
                                  $"{vertex.Position.Z.ToString(culture)}");
                }

                // Write normals
                foreach (var vertex in obmo.VertChunk.Vertices)
                {
                    sb.AppendLine($"vn {vertex.Normal.X.ToString(culture)} " +
                                  $"{vertex.Normal.Y.ToString(culture)} " +
                                  $"{vertex.Normal.Z.ToString(culture)}");
                }

                // Write UVs
                foreach (var vertex in obmo.VertChunk.Vertices)
                {
                    sb.AppendLine($"vt {vertex.UV.X.ToString(culture)} " +
                                  $"{(1.0f - vertex.UV.Y).ToString(culture)}");
                }

                // Write faces
                var indices = obmo.IstrChunk.Indices;
                for (int i = 0; i < indices.Count; i += 3)
                {
                    if (i + 2 >= indices.Count) break;

                    int v1 = indices[i] + vertexOffset;
                    int v2 = indices[i + 1] + vertexOffset;
                    int v3 = indices[i + 2] + vertexOffset;

                    sb.AppendLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
                }
                sb.AppendLine();

                vertexOffset += obmo.VertChunk.Vertices.Count;
                meshIndex++;
            }

            File.WriteAllText(outputPath, sb.ToString());
            Console.WriteLine($"✓ Exported OBJ: {outputPath}");

            // Export MTL
            string mtlPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", mtlFileName);
            ExportMTL(bmod, mtlPath);
            Console.WriteLine($"✓ Exported MTL: {mtlPath}");

            ExportTextures(bmod, outputPath);
        }

        private static void ExportMTL(BmodFile bmod, string mtlPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Material library exported from BMOD");
            sb.AppendLine($"# Materials: {bmod.MateChunks.Count}");
            sb.AppendLine();

            var processedMaterials = new HashSet<string>();

            for (int i = 0; i < bmod.MateChunks.Count; i++)
            {
                var mate = bmod.MateChunks[i];
                string matName = GetTextureFromMate(mate);

                if (string.IsNullOrEmpty(matName))
                    continue;

                string materialName = SanitizeMaterialName(matName);

                if (processedMaterials.Contains(materialName))
                    continue;

                processedMaterials.Add(materialName);

                sb.AppendLine($"newmtl {materialName}");

                // Material properties
                bool hasMatFile = false;
                if (_materialCache != null && _materialCache.TryGetValue(matName.ToLower(), out MatFile matFile))
                {
                    hasMatFile = true;

                    // ✅ FIX: Use Color4.ToMtlString() for proper formatting
                    if (matFile.PropertyDict.TryGetValue("TextureFactor", out object colorValue) && colorValue is Color4 color)
                    {
                        sb.AppendLine($"Ka {color.ToMtlString()}");
                        sb.AppendLine($"Kd {color.ToMtlString()}");
                    }
                    else
                    {
                        sb.AppendLine("Ka 1.0 1.0 1.0");
                        sb.AppendLine("Kd 0.8 0.8 0.8");
                    }

                    sb.AppendLine("Ks 0.2 0.2 0.2");
                }
                else
                {
                    sb.AppendLine("Ka 1.0 1.0 1.0");
                    sb.AppendLine("Kd 0.8 0.8 0.8");
                    sb.AppendLine("Ks 0.2 0.2 0.2");
                }

                sb.AppendLine("Ns 10.0");
                sb.AppendLine("d 1.0");
                sb.AppendLine("illum 2");

                // Find texture
                string textureFile = FindTextureForMaterial(bmod, matName, i);

                if (!string.IsNullOrEmpty(textureFile))
                {
                    // ✅ ALWAYS reference TGA (converted from DDS)
                    string outputTextureName = TextureConverter.GetOutputTextureName(
                        Path.GetFileName(textureFile),
                        convertDDS: true
                    );

                    sb.AppendLine($"map_Kd {outputTextureName}");

                    string ext = Path.GetExtension(textureFile).ToLower();
                    if (ext == ".dds")
                    {
                        sb.AppendLine($"# Converted from: {Path.GetFileName(textureFile)}");
                    }
                }
                else
                {
                    sb.AppendLine($"# No texture found for material: {matName}");
                }

                if (hasMatFile)
                {
                    sb.AppendLine($"# Properties loaded from: {matName}");
                }

                sb.AppendLine();
            }

            // Default material
            if (bmod.MateChunks.Count == 0)
            {
                sb.AppendLine("newmtl default");
                sb.AppendLine("Ka 1.0 1.0 1.0");
                sb.AppendLine("Kd 0.8 0.8 0.8");
                sb.AppendLine("Ks 0.0 0.0 0.0");
                sb.AppendLine("d 1.0");
                sb.AppendLine("illum 1");
                sb.AppendLine();
            }

            File.WriteAllText(mtlPath, sb.ToString());
        }

        private static string GetTextureFromMate(MateChunk mate)
        {
            var materialEntry = mate.Entries.FirstOrDefault(e => e.TypeName == "MATERIAL");

            if (materialEntry != null)
            {
                var textureProp = materialEntry.Properties.FirstOrDefault(p => p.Name == "TEXTURE");

                if (textureProp != null)
                {
                    return textureProp.TextureValue;
                }
            }

            return null;
        }

        private static string FindTextureForMaterial(BmodFile bmod, string materialName, int materialIndex)
        {
            // STRATEGY 0: Check .mat file
            if (_materialCache != null && _materialCache.TryGetValue(materialName.ToLower(), out MatFile matFile))
            {
                Console.WriteLine($"    ✓ Found .mat file: {materialName}");

                if (matFile.PropertyDict.TryGetValue("Layer", out object layerValue))
                {
                    string layerStr = layerValue?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(layerStr) && !layerStr.StartsWith("<"))
                    {
                        string texturePath = ResolveTexturePath(layerStr);
                        if (texturePath != null)
                        {
                            Console.WriteLine($"      → Texture from .mat (Layer): {Path.GetFileName(texturePath)}");
                            return texturePath;
                        }
                    }
                }

                Console.WriteLine($"      ℹ .mat file has no texture reference, trying fallback...");
            }

            // STRATEGY 1: Match by index
            if (materialIndex < bmod.TextChunks.Count)
            {
                var textChunk = bmod.TextChunks[materialIndex];
                if (!string.IsNullOrEmpty(textChunk.TexturePath))
                {
                    string resolvedPath = ResolveTexturePath(textChunk.TexturePath);
                    if (resolvedPath != null)
                    {
                        Console.WriteLine($"    Texture match (by index): {Path.GetFileName(resolvedPath)}");
                        return resolvedPath;
                    }
                }
            }

            // STRATEGY 2: Match by name
            string baseName = Path.GetFileNameWithoutExtension(materialName);

            foreach (var textChunk in bmod.TextChunks)
            {
                string texturePath = textChunk.TexturePath;
                string textureBaseName = Path.GetFileNameWithoutExtension(texturePath);

                if (textureBaseName.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    string resolvedPath = ResolveTexturePath(texturePath);
                    if (resolvedPath != null)
                    {
                        Console.WriteLine($"    Texture match (by name): {Path.GetFileName(resolvedPath)}");
                        return resolvedPath;
                    }
                }
            }

            // STRATEGY 3: Only one texture
            if (bmod.TextChunks.Count == 1)
            {
                string resolvedPath = ResolveTexturePath(bmod.TextChunks[0].TexturePath);
                if (resolvedPath != null)
                {
                    Console.WriteLine($"    Texture match (only one): {Path.GetFileName(resolvedPath)}");
                    return resolvedPath;
                }
            }

            // STRATEGY 4: Global cache
            if (_textureCache != null)
            {
                string[] extensions = { ".dds", ".tga", ".png", ".jpg", ".bmp" };
                foreach (var ext in extensions)
                {
                    string tryName = baseName + ext;
                    if (_textureCache.TryGetValue(tryName, out string cachedPath))
                    {
                        Console.WriteLine($"    Texture match (cache): {tryName}");
                        return cachedPath;
                    }
                }
            }

            // ✅ STRATEGY 5: Track material fallback
            if (baseName.Contains("track", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"    ℹ Track material detected, trying fallback...");

                string[] trackNames = { "track", "trackt54", "tracks", "chain" };

                foreach (var trackName in trackNames)
                {
                    foreach (var ext in new[] { ".dds", ".tga" })
                    {
                        string tryName = trackName + ext;
                        if (_textureCache != null && _textureCache.TryGetValue(tryName, out string cachedPath))
                        {
                            Console.WriteLine($"    → Track texture fallback: {tryName}");
                            return cachedPath;
                        }
                    }
                }
            }

            Console.WriteLine($"    ⚠ No texture found for material: {materialName}");
            return null;
        }

        private static string SanitizeMaterialName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "default";

            string baseName = Path.GetFileNameWithoutExtension(name);
            return baseName.Replace(" ", "_").Replace(".", "_");
        }

        private static string ResolveTexturePath(string textureName)
        {
            if (_textureCache == null)
                return null;

            string baseName = Path.GetFileNameWithoutExtension(textureName);
            string ddsName = baseName + ".dds";

            if (_textureCache.TryGetValue(ddsName, out string ddsPath))
                return ddsPath;

            if (_textureCache.TryGetValue(textureName, out string cachedPath))
                return cachedPath;

            string[] extensions = { ".tga", ".png", ".jpg", ".bmp" };
            foreach (var ext in extensions)
            {
                string altName = baseName + ext;
                if (_textureCache.TryGetValue(altName, out string altPath))
                    return altPath;
            }

            return null;
        }

        private static void ExportTextures(BmodFile bmod, string objPath)
        {
            string outputDir = Path.GetDirectoryName(objPath) ?? ".";

            Console.WriteLine("\nExporting textures...");

            var exportedTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int successCount = 0;
            int failCount = 0;

            // ✅ ONLY use TEXT chunks (actual textures, not material names)
            foreach (var text in bmod.TextChunks)
            {
                string textureName = text.TexturePath;

                if (string.IsNullOrEmpty(textureName))
                    continue;

                // Get base filename (without path)
                string textureFileName = Path.GetFileName(textureName);

                // Skip duplicates
                if (exportedTextures.Contains(textureFileName))
                    continue;

                exportedTextures.Add(textureFileName);

                // Find texture in cache
                string texturePath = ResolveTexturePath(textureName);

                if (texturePath != null && File.Exists(texturePath))
                {
                    // Copy or convert texture
                    string result = TextureConverter.CopyOrConvertTexture(
                        texturePath,
                        outputDir,
                        convertDDS: true  // DDS → TGA
                    );

                    if (result != null)
                        successCount++;
                    else
                        failCount++;
                }
                else
                {
                    Console.WriteLine($"  ⚠ Texture not found: {textureFileName}");
                    failCount++;
                }
            }

            // Summary
            Console.WriteLine();
            if (successCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Textures exported: {successCount} successful");
                Console.ResetColor();
            }

            if (failCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠ {failCount} textures failed or not found");
                Console.ResetColor();
            }

            if (successCount == 0 && failCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("ℹ No textures to export");
                Console.ResetColor();
            }
        }


        // Add this at the end of MatFileReader.cs, AFTER the MatProperty class:

        public struct Color4
        {
            public float R, G, B, A;

            public Color4(float r, float g, float b, float a)
            {
                // ✅ Normalize from [0-255] to [0-1] if needed
                R = (r > 1.0f) ? (r / 255.0f) : r;
                G = (g > 1.0f) ? (g / 255.0f) : g;
                B = (b > 1.0f) ? (b / 255.0f) : b;
                A = (a > 1.0f) ? (a / 255.0f) : a;
            }

            public override string ToString()
            {
                return $"RGBA({R:F3}, {G:F3}, {B:F3}, {A:F3})";
            }

            // Convert to OBJ/MTL format (0-1 range)
            public string ToMtlString()
            {
                return $"{R:F6} {G:F6} {B:F6}";
            }
        }

    }
}
