using System.IO;
using System.Reflection;

/// <summary>
/// Manages the storage and loading of audio clips for playback.
/// </summary>
public class AudioClipStorage
{
    /// <summary>
    /// Dictionary containing all loaded audio clips, indexed by their names.
    /// </summary>
    public static Dictionary<string, AudioClipData> AudioClips { get; } = new Dictionary<string, AudioClipData>();

    /// <summary>
    /// Loads an audio clip from raw byte data and registers it under the given name.
    /// </summary>
    /// <param name="rawData">
    /// The raw byte array containing the audio data in Ogg Vorbis format.
    /// </param>
    /// <param name="name">
    /// The unique name to assign to the loaded audio clip.
    /// </param>
    /// <returns>
    /// <c>true</c> if the clip was successfully loaded and registered;  
    /// <c>false</c> if the raw data is invalid or a clip with the same name already exists.
    /// </returns>
    /// <remarks>
    /// - Logs errors if the raw data is null/empty or if a duplicate name is used.  
    /// - Uses <see cref="VorbisReader"/> to decode the Ogg Vorbis stream.  
    /// - The decoded clip is stored in the <c>AudioClips</c> dictionary as an <see cref="AudioClipData"/>.
    /// </remarks>
    public static bool LoadClip(byte[] rawData, string name)
    {
        // Ensure raw data is not null or empty.
        if (rawData == null || rawData.Length == 0)
        {
            ServerConsole.AddLog("[AudioPlayer] Failed loading clip because raw data is null or empty!");
            return false;
        }

        // Ensure no clip with the same name is already loaded.
        if (AudioClips.ContainsKey(name))
        {
            ServerConsole.AddLog($"[AudioPlayer] Failed loading clip because clip with {name} is already loaded!");
            return false;
        }

        float[] samples = null;
        int sampleRate = 0;
        int channels = 0;

        using (MemoryStream ms = new MemoryStream(rawData))
        {
            using (VorbisReader reader = new VorbisReader(ms))
            {
                sampleRate = reader.SampleRate;
                channels = reader.Channels;

                samples = new float[reader.TotalSamples * channels];
                reader.ReadSamples(samples);
            }
        }

        // Create a new AudioClipData instance with default values.
        AudioClips.Add(name, new AudioClipData(name, sampleRate, channels, samples));
        return true;
    }

    /// <summary>
    /// Loads an audio clip from the specified file path and stores it in the collection.
    /// </summary>
    /// <param name="path">The file path of the audio clip to load.</param>
    /// <param name="name">The name to assign to the audio clip. Defaults to the file name without extension if null or empty.</param>
    /// <returns>True if the clip was successfully loaded; otherwise, false.</returns>
    public static bool LoadClip(string path, string name = null)
    {
        // Ensure the file exists at the given path.
        if (!File.Exists(path))
        {
            ServerConsole.AddLog($"[AudioPlayer] Failed loading clip from {path} because file not exists!");
            return false;
        }

        // Use the file name without extension as the clip name if no name is provided.
        if (string.IsNullOrEmpty(name))
            name = Path.GetFileNameWithoutExtension(path);

        // Ensure no clip with the same name is already loaded.
        if (AudioClips.ContainsKey(name))
        {
            ServerConsole.AddLog($"[AudioPlayer] Failed loading clip from {path} because clip with {name} is already loaded!");
            return false;
        }

        string extension = Path.GetExtension(path);

        float[] samples = null;
        int sampleRate = 0;
        int channels = 0;

        // Handle supported file formats.
        switch (extension)
        {
            case ".ogg":
                using (VorbisReader reader = new VorbisReader(path))
                {
                    sampleRate = reader.SampleRate;
                    channels = reader.Channels;

                    samples = new float[reader.TotalSamples * channels];

                    reader.ReadSamples(samples, 0, samples.Length);
                }
                break;
            default:
                ServerConsole.AddLog($"[AudioPlayer] Failed loading clip from {path} because clip is not supported! ( extension {extension} )");
                return false;
        }

        // Add the loaded clip data to the collection.
        AudioClips.Add(name, new AudioClipData(name, sampleRate, channels, samples));
        return true;
    }

    /// <summary>
    /// Destroys loaded clips.
    /// </summary>
    /// <param name="name">Then name of clip.</param>
    /// <returns>If clip was successfully destroyed.</returns>
    public static bool DestroyClip(string name)
    {
        if (!AudioClips.ContainsKey(name))
        {
            ServerConsole.AddLog($"[AudioPlayer] Clip with name {name} is not loaded!");
            return false;
        }

        return AudioClips.Remove(name);
    }
}