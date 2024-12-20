/// <summary>
/// Represents audio clip data, including its metadata and raw PCM samples.
/// </summary>
public class AudioClipData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AudioClipData"/> class.
    /// </summary>
    /// <param name="name">The name of the audio clip.</param>
    /// <param name="sampleRate">The sample rate of the audio clip.</param>
    /// <param name="channels">The number of channels in the audio clip.</param>
    /// <param name="samples">The raw PCM samples of the audio clip.</param>
    public AudioClipData(string name, int sampleRate, int channels, float[] samples)
    {
        Name = name;
        SampleRate = sampleRate;
        Channels = channels;
        Samples = samples;
    }

    /// <summary>
    /// Gets the name of the audio clip.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the sample rate of the audio clip.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of audio channels in the clip.
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets the raw PCM samples of the audio clip.
    /// </summary>
    public float[] Samples { get; }
}