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

        Task.Run(async () => await Ffmpeg.InitializeFfmpegAsync()).GetAwaiter().GetResult();

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
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = Ffmpeg.FfmpegPath,
            Arguments = $"-hide_banner -loglevel error -i \"{filePath}\" -vn -ac {channels} -ar {sampleRate} -f f32le pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(psi))
        {
            if (process == null)
            {
                ServerConsole.AddLog("[AudioFileLoader] Failed to start FFmpeg process");
                return Array.Empty<float>();
            }
        
            var errorTask = process.StandardError.ReadToEndAsync();
            
            using (MemoryStream ms = new MemoryStream())
            {
                process.StandardOutput.BaseStream.CopyTo(ms);
                process.WaitForExit();
                
                string errors = errorTask.Result;
                if (!string.IsNullOrEmpty(errors))
                {
                    ServerConsole.AddLog($"[AudioFileLoader] FFmpeg warnings/errors: {errors}");
                }

                if (process.ExitCode != 0)
                {
                    ServerConsole.AddLog($"[AudioFileLoader] FFmpeg exited with code {process.ExitCode}");
                    return Array.Empty<float>();
                }

                byte[] pcmBytes = ms.ToArray();
                int sampleCount = pcmBytes.Length / 4;
                float[] samples = new float[sampleCount];
                Buffer.BlockCopy(pcmBytes, 0, samples, 0, pcmBytes.Length);

                ServerConsole.AddLog($"[AudioFileLoader] Loaded {filePath}: {sampleCount} samples, {sampleCount / (float)(sampleRate * channels):F2}s duration");
            
                return samples;
            }
        }
    }
}