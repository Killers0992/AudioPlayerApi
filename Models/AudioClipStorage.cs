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
                    reader.ReadSamples(samples);
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
}