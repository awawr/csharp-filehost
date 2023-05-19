using System.Net;
using System.Text;

namespace FileHost
{
    internal class Program
    {
        private static readonly double s_sizeLimit = 5e+7;
        private static readonly int s_daysExpire = 7;
        private static readonly string s_storePath = $"{AppDomain.CurrentDomain.BaseDirectory}shelf";
        private static readonly string[] s_prefixes = {
            "http://shelf.awawr.com/", "https://shelf.awawr.com/"
        };

        private static readonly HttpListener s_listener = new HttpListener();
        private static readonly Random s_random = new Random();
        private static readonly byte[] s_mainPage = Utils.GetEmbeddedResource("FileHost.Resources.shelf.html");
        private static readonly string[] s_defExtensions = { ".txt", ".png", ".jpg", ".jpeg", ".gif" };

        private static void Main()
        {
            foreach (string prefix in s_prefixes)
            {
                s_listener.Prefixes.Add(prefix);
            }
            s_listener.Start();
            new Thread(ExpireThread).Start();
            s_listener.BeginGetContext(new AsyncCallback(ListenerCallback), null);
            Thread.Sleep(-1);
        }

        private static void ListenerCallback(IAsyncResult result)
        {
            HttpListenerContext context = s_listener.EndGetContext(result);
            s_listener.BeginGetContext(new AsyncCallback(ListenerCallback), null);

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            if (request.HttpMethod == "HEAD")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }
            byte[]? respData = null;
            try
            {
                switch (request.HttpMethod)
                {
                    case "GET":
                        {
                            string path = request.Url.AbsolutePath.TrimStart('/');
                            if (path == string.Empty)
                            {
                                respData = s_mainPage;
                                break;
                            }
                            string file = $"{s_storePath}\\{path.Replace("/", "\\")}";
                            string extension = Path.GetExtension(file);
                            if (File.Exists(file) && extension != ".log")
                            {
                                if (extension == ".mp4")
                                    response.ContentType = "video/mp4";
                                else if (extension == ".pdf")
                                    response.ContentType = "application/pdf";
                                else if (!s_defExtensions.Contains(extension))
                                    response.ContentType = "application/octet-stream";
                                respData = Utils.Decompress(File.ReadAllBytes(file));
                            }
                            break;
                        }
                    case "POST":
                        {
                            respData = SaveFile(request);
                            break;
                        }
                }
                if (respData == null)
                    respData = Encoding.UTF8.GetBytes(Utils.GetHTMLMessage("404 Not Found"));
            }
            catch (Exception e)
            {
                respData = Encoding.UTF8.GetBytes(Utils.GetHTMLMessage("400 Bad Request"));
                Console.WriteLine(e.ToString());
            }
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentLength64 = respData.LongLength;
            response.OutputStream.Write(respData, 0, respData.Length);
            response.Close();
        }

        private static byte[] SaveFile(HttpListenerRequest request)
        {
            int contentLength = (int)request.ContentLength64;
            if (contentLength > s_sizeLimit)
                return Encoding.UTF8.GetBytes(Utils.GetHTMLMessage("File upload too large. Limit: 50 MB."));

            byte[] buffer = new byte[contentLength];
            using (var ms = new MemoryStream())
            {
                int i = 0;
                do
                {
                    i = request.InputStream.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, i);
                } while (i > 0);
                ms.Flush();
                buffer = ms.ToArray();
            }

            var bufferList = new List<byte>(buffer);
            string[] lines = Encoding.UTF8.GetString(buffer).Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            bufferList.RemoveRange(0, string.Join(Environment.NewLine, lines.Take(4)).Length + 2);
            int lastline = lines[lines.Length - 2].Length + 4;
            bufferList.RemoveRange(bufferList.Count - lastline, lastline);
            buffer = bufferList.ToArray();

            string filename = new string(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_", 6).Select(s => s[s_random.Next(s.Length)]).ToArray());
            string extension = lines[1].Split('.')[1].TrimEnd('\"');
            string trueFilename = $"{filename}.{extension}";
            try
            {
                File.WriteAllBytes($"{s_storePath}\\{trueFilename}", Utils.Compress(bufferList.ToArray()));
                File.WriteAllText($"{s_storePath}\\{trueFilename}.log", $"{request.RemoteEndPoint.Address}|{DateTime.Now}");
                return Encoding.UTF8.GetBytes(Utils.GetHTMLMessage($"<a href=\"https://shelf.awawr.com/{trueFilename}\" style=\"color:white\">https://shelf.awawr.com/{trueFilename}"));
            }
            catch
            {
                return Encoding.UTF8.GetBytes(Utils.GetHTMLMessage($"File upload failed. Storage may be full. Try again later."));
            }
        }

        private static void ExpireThread()
        {
            while (true)
            {
                foreach (string file in Directory.EnumerateFiles(s_storePath, "*.log"))
                {
                    if ((DateTime.Now - DateTime.Parse(File.ReadAllText(file).Split('|')[1])).Days >= s_daysExpire)
                    {
                        File.Delete(Path.ChangeExtension(file, null));
                        File.Delete(file);
                    }
                    Thread.Sleep(1);
                }
                Thread.Sleep(300000);
            }
        }
    }
}
