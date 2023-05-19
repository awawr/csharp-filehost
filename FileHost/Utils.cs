using System.IO.Compression;
using System.Reflection;

namespace FileHost
{
    internal class Utils
    {
        public static string GetHTMLMessage(string message) =>
            $"<html><body style=\"background-color:black;color:white;font-family:monospace\"><p>{message}</p></body></html>";

        public static byte[] GetEmbeddedResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var ms = new MemoryStream())
            {
                using (var stream = assembly.GetManifestResourceStream(name))
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        public static byte[] Compress(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            {
                using (var mso = new MemoryStream())
                {
                    using (var gs = new GZipStream(mso, CompressionMode.Compress))
                    {
                        msi.CopyTo(gs);
                    }
                    return mso.ToArray();
                }
            }
        }

        public static byte[] Decompress(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            {
                using (var mso = new MemoryStream())
                {
                    using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                    {
                        gs.CopyTo(mso);
                    }
                    return mso.ToArray();
                }
            }
        }
    }
}
