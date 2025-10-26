using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace BmodReader
{
    public class AnimationExporter
    {
        public static void ExportToJSON(BmodFile bmod, string outputPath)
        {
            var culture = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine("  \"animations\": [");

            bool firstBone = true;
            foreach (var boneChunk in bmod.BoneChunks)
            {
                foreach (var anim in boneChunk.Animations)
                {
                    if (!firstBone) sb.AppendLine(",");
                    sb.AppendLine();
                    firstBone = false;

                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"name\": \"{anim.Name}\",");
                    sb.AppendLine($"      \"boneId\": {anim.BoneId},");
                    sb.AppendLine($"      \"parentId\": {anim.ParentId},");
                    sb.AppendLine("      \"keyframes\": [");

                    for (int i = 0; i < anim.Keyframes.Count; i++)
                    {
                        var kf = anim.Keyframes[i];
                        sb.AppendLine("        {");
                        sb.AppendLine($"          \"time\": {kf.Time.ToString(culture)},");
                        sb.AppendLine($"          \"position\": [{kf.Position.X.ToString(culture)}, " +
                                     $"{kf.Position.Y.ToString(culture)}, " +
                                     $"{kf.Position.Z.ToString(culture)}],");
                        sb.AppendLine($"          \"rotation\": [{kf.Rotation.X.ToString(culture)}, " +
                                     $"{kf.Rotation.Y.ToString(culture)}, " +
                                     $"{kf.Rotation.Z.ToString(culture)}, " +
                                     $"{kf.Rotation.W.ToString(culture)}],");
                        sb.AppendLine($"          \"scale\": [{kf.Scale.X.ToString(culture)}, " +
                                     $"{kf.Scale.Y.ToString(culture)}, " +
                                     $"{kf.Scale.Z.ToString(culture)}]");

                        if (i < anim.Keyframes.Count - 1)
                            sb.AppendLine("        },");
                        else
                            sb.AppendLine("        }");
                    }

                    sb.AppendLine("      ]");
                    sb.Append("    }");
                }
            }

            sb.AppendLine();
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(outputPath, sb.ToString());
        }

        public static void ExportToBVH(BmodFile bmod, string outputPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine("HIERARCHY");

            if (bmod.BoneChunks.Count == 0)
            {
                throw new InvalidOperationException("No animation data found in BMOD file.");
            }

            foreach (var boneChunk in bmod.BoneChunks)
            {
                // Build hierarchy
                var rootBones = boneChunk.Animations.FindAll(a => a.ParentId == -1);

                foreach (var root in rootBones)
                {
                    WriteBoneHierarchy(sb, root, boneChunk.Animations, 0);
                }

                // Write motion data
                sb.AppendLine("MOTION");
                sb.AppendLine($"Frames: {boneChunk.KeyframeCount}");
                sb.AppendLine("Frame Time: 0.033333"); // 30 FPS

                for (int frame = 0; frame < boneChunk.KeyframeCount; frame++)
                {
                    foreach (var anim in boneChunk.Animations)
                    {
                        if (frame < anim.Keyframes.Count)
                        {
                            var kf = anim.Keyframes[frame];
                            sb.Append($"{kf.Position.X:F6} {kf.Position.Y:F6} {kf.Position.Z:F6} ");

                            // Convert quaternion to euler angles
                            var euler = QuaternionToEuler(kf.Rotation);
                            sb.Append($"{euler.X:F6} {euler.Y:F6} {euler.Z:F6} ");
                        }
                    }
                    sb.AppendLine();
                }
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private static void WriteBoneHierarchy(StringBuilder sb, BoneAnimation bone,
                                               List<BoneAnimation> allBones, int indent)
        {
            string indentStr = new string(' ', indent * 2);

            bool isRoot = bone.ParentId == -1;
            sb.AppendLine($"{indentStr}{(isRoot ? "ROOT" : "JOINT")} {bone.Name}");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}  OFFSET 0.0 0.0 0.0");
            sb.AppendLine($"{indentStr}  CHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation");

            // Find children
            var children = allBones.FindAll(a => a.ParentId == (int)bone.BoneId);
            foreach (var child in children)
            {
                WriteBoneHierarchy(sb, child, allBones, indent + 1);
            }

            if (children.Count == 0)
            {
                sb.AppendLine($"{indentStr}  End Site");
                sb.AppendLine($"{indentStr}  {{");
                sb.AppendLine($"{indentStr}    OFFSET 0.0 0.0 0.0");
                sb.AppendLine($"{indentStr}  }}");
            }

            sb.AppendLine($"{indentStr}}}");
        }

        private static Vector3 QuaternionToEuler(Quaternion q)
        {
            var euler = new Vector3();

            // Roll (x-axis rotation)
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            euler.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // Pitch (y-axis rotation)
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
                euler.Y = (float)Math.CopySign(Math.PI / 2, sinp);
            else
                euler.Y = (float)Math.Asin(sinp);

            // Yaw (z-axis rotation)
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            euler.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            // Convert to degrees
            euler.X *= (float)(180.0 / Math.PI);
            euler.Y *= (float)(180.0 / Math.PI);
            euler.Z *= (float)(180.0 / Math.PI);

            return euler;
        }
    }
}
