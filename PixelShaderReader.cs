using System;
using System.IO;
using System.Text;

namespace BmodReader
{
    public class PixelShader
    {
        public string Expression;

        public static PixelShader Load(string filePath)
        {
            var ps = new PixelShader();
            var bytes = File.ReadAllBytes(filePath);
            ps.Expression = Encoding.ASCII.GetString(bytes).Trim();
            return ps;
        }

        public override string ToString()
        {
            return $"Pixel Shader: {Expression}";
        }
    }
}
