using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class Ffmpeg
{
    private static readonly string FfmpegDir = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
    private static readonly string WindowsUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private static readonly string LinuxUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz";
    
    public static string FfmpegPath =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(FfmpegDir, "ffmpeg.exe")
            : Path.Combine(FfmpegDir, "ffmpeg-master-latest-linux64-gpl", "bin", "ffmpeg");

    /// <summary>
    /// Ensures that FFmpeg is downloaded and ready to use.
    /// </summary>
    public static async Task InitializeFfmpegAsync()
    {
        Directory.CreateDirectory(FfmpegDir);

        if (File.Exists(FfmpegPath))
            return;

        string url = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsUrl : LinuxUrl;
        string archivePath = Path.Combine(FfmpegDir, Path.GetFileName(url));

        ServerConsole.AddLog($"[AudioPlayer] [FFmpeg] Downloading from {url} to {archivePath}");

        using (HttpClient client = new HttpClient())
        using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            long? contentLength = response.Content.Headers.ContentLength;

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                int lastLogged = -1;

                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0)
                        break;

                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (contentLength.HasValue)
                    {
                        int percent = (int)((totalRead * 100L) / contentLength.Value);

                        if (percent != lastLogged)
                        {
                            ServerConsole.AddLog($"[AudioPlayer] [FFmpeg] Download progress: {percent}%");
                            lastLogged = percent;
                        }
                    }
                    else
                    {
                        if (totalRead % (5 * 1024 * 1024) < buffer.Length)
                            ServerConsole.AddLog($"[AudioPlayer] [FFmpeg] Downloaded {totalRead / (1024 * 1024)} MB...");
                    }
                }
            }
        }

        ServerConsole.AddLog($"[AudioPlayer] [FFmpeg] Extracting to {FfmpegDir}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, FfmpegDir);

            var exe = Directory.GetFiles(FfmpegDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe != null)
                File.Copy(exe, FfmpegPath, true);

            File.Delete(archivePath);
            File.Delete(FfmpegDir);
        }
        else
        {
            try
            {
                ExtractTarXz(archivePath, FfmpegDir);
            }
            catch (Exception ex)
            {
                ServerConsole.AddLog($"[AudioPlayer] [FFmpeg] Error extracting tar.xz: {ex}");
                return;
            }

            File.Delete(archivePath);
        }

        ServerConsole.AddLog("[AudioPlayer] [FFmpeg] Extraction complete.");
    }

    public static void ExtractTarXz(string archivePath, string destination)
    {
        using (Stream xzStream = File.OpenRead(archivePath))
        using (var decompressedStream = new XZStream(xzStream))
        using (var reader = TarReader.Open(decompressedStream))
        {
            reader.WriteAllToDirectory(destination, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true,
                PreserveFileTime = true
            });
        }
    }
}