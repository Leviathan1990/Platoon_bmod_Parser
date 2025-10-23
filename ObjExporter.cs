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

        // Initialize with the global texture cache from Program.cs
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

            int vertexOffset = 1; // OBJ indices start at 1
            int meshIndex = 0;

            // Export all MESH chunks
            foreach (var mesh in bmod.MeshChunks)
            {
                if (mesh.VertChunk == null || mesh.IstrChunk == null)
                    continue;

                // ✅ JAVÍTVA: Try to find material for this mesh
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
                                  $"{(1.0f - vertex.UV.Y).ToString(culture)}");  // Flip V coordinate!
                }

                // Write faces (triangles)
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
                                  $"{(1.0f - vertex.UV.Y).ToString(culture)}");  // Flip V!
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

            // Export MTL file
            string mtlPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", mtlFileName);
            ExportMTL(bmod, mtlPath);
            Console.WriteLine($"✓ Exported MTL: {mtlPath}");
        }

        // ✅ JAVÍTVA: ExportMTL metódus
        private static void ExportMTL(BmodFile bmod, string mtlPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Material library exported from BMOD");
            sb.AppendLine($"# Materials: {bmod.MateChunks.Count}");
            sb.AppendLine();

            // Export materials from MATE chunks
            var processedMaterials = new HashSet<string>();

            foreach (var mate in bmod.MateChunks)
            {
                // ✅ JAVÍTVA: Get texture from new structure
                string textureName = GetTextureFromMate(mate);
                if (string.IsNullOrEmpty(textureName))
                    continue;

                string materialName = SanitizeMaterialName(textureName);

                // Skip duplicates
                if (processedMaterials.Contains(materialName))
                    continue;

                processedMaterials.Add(materialName);

                sb.AppendLine($"newmtl {materialName}");
                sb.AppendLine("Ka 1.0 1.0 1.0");  // Ambient
                sb.AppendLine("Kd 0.8 0.8 0.8");  // Diffuse
                sb.AppendLine("Ks 0.2 0.2 0.2");  // Specular
                sb.AppendLine("Ns 10.0");          // Shininess
                sb.AppendLine("d 1.0");            // Opacity
                sb.AppendLine("illum 2");          // Illumination model

                // ✅ JAVÍTVA: Find corresponding TEXT chunk
                var textChunk = bmod.TextChunks.FirstOrDefault(t =>
                    t.TexturePath.Contains(Path.GetFileNameWithoutExtension(textureName)));

                if (textChunk != null)
                {
                    string texturePath = ResolveTexturePath(textChunk.TexturePath);
                    if (texturePath != null)
                    {
                        string textureFileName = Path.GetFileName(texturePath);
                        string textureExt = Path.GetExtension(textureFileName).ToLower();

                        if (textureExt == ".dds")
                        {
                            string convertedName = Path.ChangeExtension(textureFileName, ".png");
                            sb.AppendLine($"# map_Kd {convertedName}  # Convert DDS to PNG");
                            sb.AppendLine($"# Original: {texturePath}");
                        }
                        else
                        {
                            sb.AppendLine($"map_Kd {textureFileName}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"# map_Kd {textChunk.TexturePath}  # Texture not found");
                    }
                }
                else
                {
                    sb.AppendLine($"# Material file: {textureName}");
                }

                sb.AppendLine();
            }

            // Default material if none exist
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

        // ✅ ÚJ HELPER METÓDUS
        /// <summary>
        /// Extract texture name from MATE chunk (corrected structure)
        /// </summary>
        private static string GetTextureFromMate(MateChunk mate)
        {
            // Find MATERIAL entry
            var materialEntry = mate.Entries
                .FirstOrDefault(e => e.TypeName == "MATERIAL");

            if (materialEntry != null)
            {
                // Find TEXTURE property
                var textureProp = materialEntry.Properties
                    .FirstOrDefault(p => p.Name == "TEXTURE");

                if (textureProp != null)
                {
                    return textureProp.TextureValue;
                }
            }

            return null;
        }

        private static string SanitizeMaterialName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "default";

            // Remove extension and invalid characters
            string baseName = Path.GetFileNameWithoutExtension(name);
            return baseName.Replace(" ", "_").Replace(".", "_");
        }

        private static string ResolveTexturePath(string textureName)
        {
            if (_textureCache == null)
                return null;

            // Try DDS first (priority)
            string baseName = Path.GetFileNameWithoutExtension(textureName);
            string ddsName = baseName + ".dds";

            if (_textureCache.TryGetValue(ddsName, out string ddsPath))
                return ddsPath;

            // Try exact match
            if (_textureCache.TryGetValue(textureName, out string cachedPath))
                return cachedPath;

            // Try other extensions
            string[] extensions = { ".tga", ".png", ".jpg", ".bmp" };
            foreach (var ext in extensions)
            {
                string altName = baseName + ext;
                if (_textureCache.TryGetValue(altName, out string altPath))
                    return altPath;
            }

            return null;
        }
    }
}
