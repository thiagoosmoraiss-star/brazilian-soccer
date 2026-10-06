using System.Security.Cryptography;
using System.Text;

namespace Game.Save
{
    /// <summary>SHA-256 (hex) over a save body's raw bytes (TECHNICAL_SPEC §14: "Validação: Checksum...").</summary>
    public static class SaveChecksum
    {
        public static string Compute(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
