using System;
using System.IO;
using FreeImageAPI;

namespace BmodReader
{
    /// <summary>
    /// Texture conversion utilities (DDS → TGA, PNG, etc.)
    /// </summary>
    public static class TextureConverter
    {
        /// <summary>
        /// Convert DDS to TGA format
        /// </summary>
        public static bool ConvertDDStoTGA(string ddsPath, string tgaPath)
        {
            try
            {
                if (!File.Exists(ddsPath))
                {
                    Console.WriteLine($"      ⚠ Source DDS not found: {ddsPath}");
                    return false;
                }

                // Load DDS
                var dib = FreeImage.LoadEx(ddsPath);

                if (dib.IsNull)
                {
                    Console.WriteLine($"      ✗ Failed to load DDS: {Path.GetFileName(ddsPath)}");
                    return false;
                }

                try
                {
                    // Convert to 24-bit RGB if needed (TGA prefers 24/32-bit)
                    if (FreeImage.GetBPP(dib) != 24 && FreeImage.GetBPP(dib) != 32)
                    {
                        var converted = FreeImage.ConvertTo24Bits(dib);
                        FreeImage.UnloadEx(ref dib);
                        dib = converted;
                    }

                    // Save as TGA
                    bool success = FreeImage.SaveEx(dib, tgaPath, FREE_IMAGE_FORMAT.FIF_TARGA);

                    if (success)
                    {
                        Console.WriteLine($"      ✓ Converted: {Path.GetFileName(ddsPath)} → {Path.GetFileName(tgaPath)}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"      ✗ Failed to save TGA: {Path.GetFileName(tgaPath)}");
                        return false;
                    }
                }
                finally
                {
                    // Always unload
                    if (!dib.IsNull)
                        FreeImage.UnloadEx(ref dib);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ✗ Conversion error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Copy or convert texture to output directory
        /// </summary>
        public static string CopyOrConvertTexture(string sourcePath, string outputDir, bool convertDDS = true)
        {
            try
            {
                string fileName = Path.GetFileName(sourcePath);
                string ext = Path.GetExtension(sourcePath).ToLower();

                if (ext == ".dds" && convertDDS)
                {
                    // Convert DDS → TGA
                    string tgaFileName = Path.ChangeExtension(fileName, ".tga");
                    string tgaPath = Path.Combine(outputDir, tgaFileName);

                    if (ConvertDDStoTGA(sourcePath, tgaPath))
                    {
                        return tgaFileName; // Return TGA filename
                    }
                    else
                    {
                        // Fallback: copy DDS as-is
                        string ddsPath = Path.Combine(outputDir, fileName);
                        File.Copy(sourcePath, ddsPath, overwrite: true);
                        Console.WriteLine($"      ℹ Copied DDS (conversion failed): {fileName}");
                        return fileName;
                    }
                }
                else
                {
                    // Copy as-is (TGA, PNG, JPG, etc.)
                    string destPath = Path.Combine(outputDir, fileName);
                    File.Copy(sourcePath, destPath, overwrite: true);
                    Console.WriteLine($"      ✓ Copied texture: {fileName}");
                    return fileName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ✗ Failed to copy texture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get output texture filename (DDS → TGA, others keep extension)
        /// </summary>
        public static string GetOutputTextureName(string sourceTexture, bool convertDDS = true)
        {
            string ext = Path.GetExtension(sourceTexture).ToLower();

            if (ext == ".dds" && convertDDS)
            {
                return Path.ChangeExtension(sourceTexture, ".tga");
            }

            return sourceTexture;
        }
    }
}
