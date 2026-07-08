using System.Diagnostics;
using System.Threading.Tasks;

namespace AudioPlayerApi;

public static class AudioFileLoader
{
    public static float[] LoadAudioFile(string filePath, int targetSampleRate = 48000, int targetChannels = 1)
    {
        if (!File.Exists(filePath))
        {
            ServerConsole.AddLog($"[AudioFileLoader] File not found: {filePath}");
            return Array.Empty<float>();
        }

        // I think this line can kill the server, but i idk how to check ffmpeg. On server start mb
        Ffmpeg.InitializeFfmpegAsync().GetAwaiter().GetResult();

        try
        {
            return ConvertToRawPcm(filePath, targetSampleRate, targetChannels);
        }
        catch (Exception ex)
        {
            ServerConsole.AddLog($"[AudioFileLoader] Error loading audio file {filePath}: {ex}");
            return Array.Empty<float>();
        }
    }
    
    
    // this method I writed with gpt because I am dumb and can't understand simple things
    private static float[] ConvertToRawPcm(string filePath, int sampleRate, int channels)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Ffmpeg.FfmpegPath,
            Arguments = $"-hide_banner -nostats -loglevel error -i \"{filePath}\" -vn -ac {channels} -ar {sampleRate} -f f32le pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start())
            {
                ServerConsole.AddLog("[AudioFileLoader] Failed to start FFmpeg process");
                return Array.Empty<float>();
            }

            byte[] pcmBytes;
            using (var ms = new MemoryStream())
            {
                process.StandardOutput.BaseStream.CopyTo(ms);
                if (!process.WaitForExit(10000)) 
                {
                    try { process.Kill(); } catch { /* ignore */ }
                    ServerConsole.AddLog("[AudioFileLoader] FFmpeg process timeout");
                    return Array.Empty<float>();
                }

                pcmBytes = ms.ToArray();
            }

            if (process.ExitCode != 0)
            {
                ServerConsole.AddLog($"[AudioFileLoader] FFmpeg exited with code {process.ExitCode}");
                return Array.Empty<float>();
            }

            int sampleCount = pcmBytes.Length / 4;
            var samples = new float[sampleCount];
            Buffer.BlockCopy(pcmBytes, 0, samples, 0, pcmBytes.Length);

            ServerConsole.AddLog($"[AudioFileLoader] Loaded {filePath}: {sampleCount} samples, {sampleCount / (float)(sampleRate * channels):F2}s duration");
            return samples;
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
            throw;
        }
    }
}