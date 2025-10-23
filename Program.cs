using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BmodReader
{
    class Program
    {
        // Texture cache
        private static Dictionary<string, string> _textureCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _textureCacheInitialized = false;

        // Material cache
        private static Dictionary<string, string> _materialCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _materialCacheInitialized = false;

        // BMOD file directory (for relative texture search)
        private static string _bmodDirectory = ".";

        static void Main(string[] args)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         BMOD Parser v1.0. Designed for the game, Plat00n. ║");
            Console.WriteLine("║         By: Krisztian Kispeti. K's Interactive.           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Show working directory
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Working directory: {Directory.GetCurrentDirectory()}");
            Console.ResetColor();
            Console.WriteLine();

            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: File not found: {filePath}");
                Console.ResetColor();
                return;
            }

            // Store BMOD file directory for texture searching
            _bmodDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (string.IsNullOrEmpty(_bmodDirectory))
                _bmodDirectory = ".";

            try
            {
                // Load BMOD file
                Console.WriteLine($"Loading: {Path.GetFileName(filePath)}");
                Console.WriteLine($"Size: {new FileInfo(filePath).Length:N0} bytes");
                Console.WriteLine();

                var bmod = BmodFile.Load(filePath);

                // Print all sections
                PrintHeader(bmod);
                PrintChunkSummary(bmod);
                PrintGeometryInfo(bmod);
                PrintAnimationInfo(bmod);
                PrintMaterialInfo(bmod);
                PrintTextureInfo(bmod);
                PrintBoundingBoxInfo(bmod);
                PrintDummyInfo(bmod);
                PrintObstacleInfo(bmod);
                PrintEffectInfo(bmod);
                PrintAssetInfo(bmod);
                PrintMetadata(bmod);

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                    ANALYSIS COMPLETE                      ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                // Export options
                PromptExport(bmod, filePath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                         ERROR                             ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Stack Trace:");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  bmodparser <file.bmod>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  bmodparser model.bmod");
            Console.WriteLine("  bmodparser Gfx\\Models\\Vehicles\\tank.bmod");
            Console.WriteLine();
        }

        // ========================================================================
        // CACHE INITIALIZATION
        // ========================================================================

        static void InitializeTextureCache()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Indexing textures...");
            Console.ResetColor();

            // Build search paths - CSAK A LEGFONTOSABBAK! (Priority order)
            List<string> searchRoots = new List<string>
    {
        _bmodDirectory,     // 1. HIGHEST PRIORITY: Same directory as BMOD file!
    };

            // Add parent directory ONLY
            try
            {
                DirectoryInfo bmodDir = new DirectoryInfo(_bmodDirectory);

                if (bmodDir.Parent != null)
                {
                    searchRoots.Add(bmodDir.Parent.FullName);  // 2. Parent directory
                }
            }
            catch { }

            // Fallback: search entire Gfx tree
            searchRoots.Add("Gfx");

            int fileCount = 0;

            // Search in PRIORITY ORDER - first found wins!
            foreach (var root in searchRoots.Distinct())
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    continue;

                try
                {
                    var textureFiles = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                        .Where(f =>
                        {
                            var ext = Path.GetExtension(f).ToLower();
                            return ext == ".dds" || ext == ".tga" || ext == ".png" ||
                                   ext == ".jpg" || ext == ".bmp";
                        });

                    foreach (var file in textureFiles)
                    {
                        string fileName = Path.GetFileName(file);

                        // ONLY add if NOT already in cache (maintains priority)
                        if (!_textureCache.ContainsKey(fileName))
                        {
                            _textureCache[fileName] = file;
                            fileCount++;
                        }
                    }
                }
                catch
                {
                    // Skip directories we can't access
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" ✓ ({fileCount} textures found)");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void InitializeMaterialCache()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Indexing materials...");
            Console.ResetColor();

            string[] searchRoots = { "Shaders", "Materials", ".", "..", @"..\..\..\.." };
            int fileCount = 0;

            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root))
                    continue;

                try
                {
                    var matFiles = Directory.GetFiles(root, "*.mat", SearchOption.AllDirectories);

                    foreach (var file in matFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        if (!_materialCache.ContainsKey(fileName))
                        {
                            _materialCache[fileName] = file;
                            fileCount++;
                        }
                    }
                }
                catch { }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" ✓ ({fileCount} materials found)");
            Console.ResetColor();
        }

        static string ResolveTexturePath(string textureName)
        {
            // FIRST: Try to find .dds version (preferred!)
            string baseName = Path.GetFileNameWithoutExtension(textureName);
            string ddsName = baseName + ".dds";

            if (_textureCache.TryGetValue(ddsName, out string ddsPath))
            {
                return ddsPath;
            }

            // SECOND: Try exact match
            if (_textureCache.TryGetValue(textureName, out string cachedPath))
            {
                return cachedPath;
            }

            // THIRD: Try other extensions
            string[] extensions = { ".tga", ".png", ".jpg", ".bmp" };

            foreach (var ext in extensions)
            {
                string altName = baseName + ext;

                if (_textureCache.TryGetValue(altName, out string altPath))
                {
                    return altPath;
                }
            }

            return null;
        }

        static string ResolveMaterialPath(string materialName)
        {
            if (_materialCache.TryGetValue(materialName, out string cachedPath))
                return cachedPath;

            return null;
        }

        // ========================================================================
        // PRINT FUNCTIONS
        // ========================================================================

        static void PrintHeader(BmodFile bmod)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  FILE HEADER");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            Console.WriteLine($"  Magic:        {bmod.Header.Magic}");
            Console.WriteLine($"  Version:      {bmod.Header.VersionString}");
            Console.WriteLine($"  File Size:    {bmod.Header.FileSize:N0} bytes");
            Console.WriteLine();
        }

        static void PrintChunkSummary(BmodFile bmod)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"  CHUNKS ({bmod.Chunks.Count})");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            var grouped = bmod.Chunks.GroupBy(c => c.ChunkId)
                                     .OrderByDescending(g => g.Sum(c => c.ChunkSize));

            foreach (var group in grouped)
            {
                int count = group.Count();
                long totalSize = group.Sum(c => c.ChunkSize);
                string countStr = count > 1 ? $" ({count}×)" : "";

                Console.WriteLine($"  {group.Key,-8} - {totalSize,8:N0} bytes{countStr}");
            }
            Console.WriteLine();
        }

        static void PrintGeometryInfo(BmodFile bmod)
        {
            bool hasGeometry = false;

            // MESH chunks
            if (bmod.MeshChunks.Count > 0)
            {
                hasGeometry = true;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("  MESH GEOMETRY");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();

                int meshIndex = 1;
                foreach (var mesh in bmod.MeshChunks)
                {
                    Console.WriteLine($"  Mesh #{meshIndex}:");
                    Console.WriteLine($"    Declared Vertices: {mesh.VertexCount:N0}");
                    Console.WriteLine($"    Declared Faces:    {mesh.FaceCount:N0}");

                    if (mesh.VertChunk != null)
                    {
                        Console.WriteLine($"    ├─ VERT chunk:");
                        Console.WriteLine($"    │  └─ Loaded vertices: {mesh.VertChunk.Vertices.Count:N0}");

                        if (mesh.VertChunk.Vertices.Count > 0)
                        {
                            var v = mesh.VertChunk.Vertices[0];
                            Console.WriteLine($"    │     First vertex:");
                            Console.WriteLine($"    │       Position: {v.Position}");
                            Console.WriteLine($"    │       Normal:   {v.Normal}");
                            Console.WriteLine($"    │       UV:       {v.UV}");
                        }

                        if (mesh.VertChunk.EntryCount > 0)
                        {
                            Console.WriteLine($"    │     Entries:    {mesh.VertChunk.EntryCount}");
                        }
                    }

                    if (mesh.IstrChunk != null)
                    {
                        Console.WriteLine($"    ├─ ISTR chunk:");
                        Console.WriteLine($"    │  └─ Indices:  {mesh.IstrChunk.IndexCount:N0}");
                        Console.WriteLine($"    │     Triangles: {mesh.IstrChunk.IndexCount / 3:N0}");
                    }

                    if (mesh.FaceChunk != null)
                    {
                        Console.WriteLine($"    └─ FACE chunk:");
                        Console.WriteLine($"       └─ Face count: {mesh.FaceChunk.FaceCount}");
                        Console.WriteLine($"          LODP levels: {mesh.FaceChunk.LodpChunks.Count}");

                        foreach (var lodp in mesh.FaceChunk.LodpChunks)
                        {
                            Console.WriteLine($"            └─ Keyframes: {lodp.KeyframeCount}");
                        }
                    }

                    Console.WriteLine();
                    meshIndex++;
                }
            }

            // OBMO chunks
            if (bmod.ObmoChunks.Count > 0)
            {
                hasGeometry = true;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("  OBMO GEOMETRY (Morphed 3D Objects)");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();

                int obmoIndex = 1;
                foreach (var obmo in bmod.ObmoChunks)
                {
                    Console.WriteLine($"  OBMO #{obmoIndex}:");
                    Console.WriteLine($"    Name: {obmo.Name ?? "(unnamed)"}");

                    if (obmo.VertChunk != null)
                    {
                        Console.WriteLine($"    Vertices: {obmo.VertChunk.Vertices.Count:N0}");
                    }

                    if (obmo.IstrChunk != null)
                    {
                        Console.WriteLine($"    Indices:  {obmo.IstrChunk.IndexCount:N0}");
                        Console.WriteLine($"    Triangles: {obmo.IstrChunk.IndexCount / 3:N0}");
                    }

                    Console.WriteLine();
                    obmoIndex++;
                }
            }

            if (!hasGeometry)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  (No geometry data found - MESH or OBMO chunks not present)");
                Console.WriteLine();
                Console.ResetColor();
            }
        }

        static void PrintAnimationInfo(BmodFile bmod)
        {
            if (bmod.BoneChunks.Count == 0)
                return;

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  BONE ANIMATION");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            foreach (var boneChunk in bmod.BoneChunks)
            {
                Console.WriteLine($"  Bone Count:     {boneChunk.BoneCount}");
                Console.WriteLine($"  Keyframe Count: {boneChunk.KeyframeCount}");
                Console.WriteLine();

                foreach (var anim in boneChunk.Animations)
                {
                    Console.WriteLine($"  ├─ Bone: {anim.Name}");
                    Console.WriteLine($"  │  ├─ Bone ID:   {anim.BoneId}");
                    Console.WriteLine($"  │  ├─ Parent ID: {anim.ParentId} {(anim.ParentId == -1 ? "(root)" : "")}");
                    Console.WriteLine($"  │  └─ Keyframes: {anim.Keyframes.Count}");

                    if (anim.Keyframes.Count > 0)
                    {
                        var first = anim.Keyframes[0];
                        var last = anim.Keyframes[anim.Keyframes.Count - 1];
                        Console.WriteLine($"  │     First time: {first.Time:F3}s");
                        Console.WriteLine($"  │     Last time:  {last.Time:F3}s");
                        Console.WriteLine($"  │     Duration:   {(last.Time - first.Time):F3}s");
                    }
                }
                Console.WriteLine();
            }
        }

        static void PrintMaterialInfo(BmodFile bmod)
        {
            if (bmod.MateChunks.Count == 0)
                return;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  MATERIALS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            // Pre-initialize cache
            if (!_materialCacheInitialized)
            {
                InitializeMaterialCache();
                _materialCacheInitialized = true;
                Console.WriteLine();
            }

            // ✅ JAVÍTÁS: FOREACH HOZZÁADVA!
            foreach (var mate in bmod.MateChunks)
            {
                Console.WriteLine($"    Material entries: {mate.Entries.Count}");

                foreach (var entry in mate.Entries)
                {
                    Console.WriteLine($"      Type: {entry.TypeName}");

                    if (entry.TypeName == "MATERIAL")
                    {
                        foreach (var prop in entry.Properties)
                        {
                            if (prop.Name == "TEXTURE")
                            {
                                Console.WriteLine($"        Texture: {prop.TextureValue}");

                                // Cache texture
                                if (!string.IsNullOrEmpty(prop.TextureValue))
                                {
                                    string textureName = Path.GetFileNameWithoutExtension(prop.TextureValue);
                                    if (!string.IsNullOrEmpty(textureName) && !_textureCache.ContainsKey(textureName))
                                    {
                                        _textureCache[textureName] = prop.TextureValue;
                                    }
                                }
                            }
                        }
                    }
                }
            } // ✅ foreach vége
        }

        static void PrintTextureInfo(BmodFile bmod)
        {
            if (bmod.TextChunks.Count == 0)
                return;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  TEXTURES");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            // Pre-initialize cache BEFORE printing textures
            if (!_textureCacheInitialized)
            {
                InitializeTextureCache();
                _textureCacheInitialized = true;
                Console.WriteLine();
            }

            foreach (var text in bmod.TextChunks)
            {
                Console.Write($"  • {text.TexturePath}");

                // Try to resolve actual texture file
                string texturePath = ResolveTexturePath(text.TexturePath);

                if (texturePath != null)
                {
                    var fileInfo = new FileInfo(texturePath);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($" ✓ ({fileInfo.Length:N0} bytes)");
                    Console.ResetColor();

                    // Show if converted from .tga to .dds
                    if (Path.GetExtension(texturePath).ToLower() != Path.GetExtension(text.TexturePath).ToLower())
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"    → Resolved to: {Path.GetFileName(texturePath)}");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(" ✗ (not found)");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }

        static void PrintBoundingBoxInfo(BmodFile bmod)
        {
            bool hasBounds = false;

            if (bmod.MboxChunk != null)
            {
                hasBounds = true;
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("  BOUNDING BOX (MBOX)");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();

                Console.WriteLine($"  Min: {bmod.MboxChunk.Min}");
                Console.WriteLine($"  Max: {bmod.MboxChunk.Max}");

                var size = new Vector3(
                    bmod.MboxChunk.Max.X - bmod.MboxChunk.Min.X,
                    bmod.MboxChunk.Max.Y - bmod.MboxChunk.Min.Y,
                    bmod.MboxChunk.Max.Z - bmod.MboxChunk.Min.Z
                );
                Console.WriteLine($"  Size: {size}");
                Console.WriteLine();
            }

            if (bmod.OboxChunk != null)
            {
                hasBounds = true;
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("  OBJECT BOUNDING BOX (OBOX)");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();

                Console.WriteLine($"  Center: {bmod.OboxChunk.Center}");
                Console.WriteLine($"  Normal: {bmod.OboxChunk.Normal}");
                Console.WriteLine();
            }
        }

        static void PrintDummyInfo(BmodFile bmod)
        {
            var dumyChunks = bmod.GetChunks<DumyChunk>();
            if (dumyChunks.Count == 0)
                return;

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  DUMMY OBJECTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            foreach (var dumy in dumyChunks)
            {
                Console.WriteLine($"  Object #{dumy.ObjectNumber}:");
                Console.WriteLine($"  ├─ Name: {dumy.Name ?? "(unnamed)"}");
                Console.WriteLine($"  └─ Data: {dumy.UnknownData?.Length ?? 0} bytes");
                Console.WriteLine();
            }
        }

        static void PrintObstacleInfo(BmodFile bmod)
        {
            var obstChunks = bmod.GetChunks<ObstChunk>();
            if (obstChunks.Count == 0)
                return;

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  OBSTACLES");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            foreach (var obst in obstChunks)
            {
                Console.WriteLine($"  Object #{obst.ObjectNumber}:");
                Console.WriteLine($"  ├─ Name: {obst.Name ?? "(unnamed)"}");
                Console.WriteLine($"  └─ Data: {obst.UnknownData?.Length ?? 0} bytes");
                Console.WriteLine();
            }
        }

        static void PrintEffectInfo(BmodFile bmod)
        {
            var blstChunks = bmod.GetChunks<BlstChunk>();
            var clouChunks = bmod.GetChunks<ClouChunk>();
            var omniChunks = bmod.GetChunks<OmniChunk>();
            var flarChunks = bmod.GetChunks<FlarChunk>();

            bool hasEffects = blstChunks.Count > 0 || clouChunks.Count > 0 ||
                            omniChunks.Count > 0 || flarChunks.Count > 0;

            if (!hasEffects)
                return;

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  EFFECTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            if (blstChunks.Count > 0)
                Console.WriteLine($"  BLST (Blast): {blstChunks.Count}");
            if (clouChunks.Count > 0)
                Console.WriteLine($"  CLOU (Cloud): {clouChunks.Count}");
            if (omniChunks.Count > 0)
                Console.WriteLine($"  OMNI (Light): {omniChunks.Count}");
            if (flarChunks.Count > 0)
                Console.WriteLine($"  FLAR (Flare): {flarChunks.Count}");

            Console.WriteLine();
        }

        static void PrintAssetInfo(BmodFile bmod)
        {
            var aselChunks = bmod.GetChunks<AselChunk>();
            var asecChunks = bmod.GetChunks<AsecChunk>();

            bool hasAssets = aselChunks.Count > 0 || asecChunks.Count > 0;

            if (!hasAssets)
                return;

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  ASSETS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            foreach (var asel in aselChunks)
            {
                Console.WriteLine($"  ASEL (Asset List):");
                Console.WriteLine($"  └─ String Count: {asel.StringCount}");

                foreach (var str in asel.Strings)
                {
                    Console.WriteLine($"     ├─ [{str.StringId}] {str.String}");
                }
                Console.WriteLine();
            }

            if (asecChunks.Count > 0)
            {
                Console.WriteLine($"  ASEC (Asset Section): {asecChunks.Count}");
                Console.WriteLine();
            }
        }

        static void PrintMetadata(BmodFile bmod)
        {
            var timeChunk = bmod.GetChunk<TimeChunk>();

            if (timeChunk == null)
                return;

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  METADATA");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.ResetColor();

            Console.WriteLine($"  Created: {timeChunk.Timestamp}");
            Console.WriteLine();
        }

        static void PromptExport(BmodFile bmod, string originalPath)
        {
            // Check if we have exportable data
            bool hasGeometry = bmod.MeshChunks.Count > 0 || bmod.ObmoChunks.Count > 0;
            bool hasAnimation = bmod.BoneChunks.Count > 0;

            if (!hasGeometry && !hasAnimation)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("(No exportable geometry or animation data found)");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Export Options:");
            Console.ResetColor();

            if (hasGeometry)
            {
                Console.WriteLine("  [1] Export geometry to OBJ");
            }

            if (hasAnimation)
            {
                Console.WriteLine("  [2] Export animation to JSON");
                Console.WriteLine("  [3] Export animation to BVH");
            }

            Console.WriteLine("  [0] Skip export");
            Console.WriteLine();
            Console.Write("Select option: ");

            var key = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine();

            string baseName = Path.GetFileNameWithoutExtension(originalPath);
            string outputDir = Path.GetDirectoryName(originalPath);
            if (string.IsNullOrEmpty(outputDir))
                outputDir = ".";

            switch (key.KeyChar)
            {
                case '1':
                    if (hasGeometry)
                    {
                        string objPath = Path.Combine(outputDir, baseName + ".obj");
                        Console.WriteLine($"Exporting to: {objPath}");
                        try
                        {
                            // Share texture cache with ObjExporter
                            ObjExporter.SetTextureCache(_textureCache);
                            ObjExporter.Export(bmod, objPath);

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Export successful!");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"✗ Export failed: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                    break;

                case '2':
                    if (hasAnimation)
                    {
                        string jsonPath = Path.Combine(outputDir, baseName + "_anim.json");
                        Console.WriteLine($"Exporting to: {jsonPath}");
                        try
                        {
                            AnimationExporter.ExportToJSON(bmod, jsonPath);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Export successful!");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"✗ Export failed: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                    break;

                case '3':
                    if (hasAnimation)
                    {
                        string bvhPath = Path.Combine(outputDir, baseName + "_anim.bvh");
                        Console.WriteLine($"Exporting to: {bvhPath}");
                        try
                        {
                            AnimationExporter.ExportToBVH(bmod, bvhPath);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Export successful!");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"✗ Export failed: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                    break;

                case '0':
                default:
                    Console.WriteLine("Export skipped.");
                    break;
            }
        }
    }
}
