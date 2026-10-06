using System.IO;
using System.IO.Compression;

namespace Game.Save.IO
{
    /// <summary>JSON + gzip (TECHNICAL_SPEC §14): the body is compressed, the header never is.</summary>
    internal static class Gzip
    {
        public static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
