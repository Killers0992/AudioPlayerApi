/// <summary>
/// Handles the playback of audio clips, including looping, volume control, and PCM data handling.
/// </summary>
public class AudioClipPlayback : IDisposable
{
    /// <summary>
    /// Size of audio packets to be processed in samples.
    /// </summary>
    public const int PacketSize = 480;

    /// <summary>
    /// Size of the buffer ahead of playback.
    /// </summary>
    public const int AheadBuffer = 4096;

    /// <summary>
    /// The sampling rate used for audio playback.
    /// </summary>
    public const int SamplingRate = 48000;

    /// <summary>
    /// The number of audio channels.
    /// </summary>
    public const int Channels = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioClipPlayback"/> class.
    /// </summary>
    /// <param name="id">The unique ID of the playback instance.</param>
    /// <param name="clip">The name of the audio clip to play.</param>
    /// <param name="volume">The playback volume (default is 1.0).</param>
    /// <param name="loop">Indicates whether the clip should loop (default is false).</param>
    /// <param name="destroyOnEnd">Indicates whether to destroy the clip after playback ends (default is true).</param>
    public AudioClipPlayback(int id, string clip, float volume = 1f, bool loop = false, bool destroyOnEnd = true)
    {
        Id = id;
        Clip = clip;
        Volume = volume;
        Loop = loop;
        DestroyOnEnd = destroyOnEnd;
    }

    /// <summary>
    /// Gets the unique identifier of this playback instance.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Indicates whether this playback instance represents a live stream
    /// instead of a standard audio clip.
    /// </summary>
    public bool IsStream { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="StreamPlayback"/> source that provides
    /// the live audio stream data for this playback instance.
    /// </summary>
    public StreamPlayback StreamSource { get; set; }

    /// <summary>
    /// Gets the name of the audio clip being played.
    /// </summary>
    public string Clip { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the clip should loop during playback.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Gets a value indicating whether the clip should be destroyed after playback ends.
    /// </summary>
    public bool DestroyOnEnd { get; }

    /// <summary>
    /// Gets the PCM samples of the audio clip.
    /// </summary>
    public float[] Samples
    {
        get
        {
            if (string.IsNullOrEmpty(Clip))
                return Array.Empty<float>();

            if (!AudioClipStorage.AudioClips.TryGetValue(Clip, out AudioClipData data))
                return Array.Empty<float>();

            return data.Samples;
        }
    }

    /// <summary>
    /// Gets the total duration of the audio clip.
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            double duration = Samples.Length / (SamplingRate * Channels);

            return TimeSpan.FromSeconds(duration);
        }
    }

    /// <summary>
    /// Gets the current playback position as a time value.
    /// </summary>
    public TimeSpan CurrentTime
    {
        get
        {
            double duration = ReadPosition / (SamplingRate * Channels);

            return TimeSpan.FromSeconds(duration);
        }
    }

    /// <summary>
    /// Gets the current playback progress as a percentage.
    /// </summary>
    public float Progress
    {
        get
        {
            return Mathf.Clamp01((float)ReadPosition / Samples.Length);
        }
    }

    /// <summary>
    /// Gets or sets the current read position in the audio clip.
    /// </summary>
    public int ReadPosition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the playback is paused.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// Gets or sets the playback volume.
    /// </summary>
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// Gets the next PCM sample to be played.
    /// </summary>
    public float[] NextSample { get; private set; }

    /// <summary>
    /// Prepares the next sample chunk for playback.
    /// </summary>
    /// <returns>True if the sample was successfully prepared; otherwise, false.</returns>
    public bool PrepareSample()
    {
        if (IsStream && StreamSource != null)
        {
            if (StreamSource.IsInitializing)
                return true;

            NextSample = new float[PacketSize];
            int got = StreamSource.Read(NextSample, 0, NextSample.Length);

            if (got <= 0)
                return false;

            if (got < NextSample.Length)
                Array.Clear(NextSample, got, NextSample.Length - got);

            return true;
        }

        bool destroy = false;

        NextSample = ReadPcmChunk(ref destroy);

        return !destroy;
    }

    /// <summary>
    /// Reads a chunk of PCM data from the audio clip.
    /// </summary>
    /// <param name="destroy">Indicates whether the clip should be destroyed after playback ends.</param>
    /// <returns>An array of PCM data.</returns>
    public float[] ReadPcmChunk(ref bool destroy)
    {
        if (IsPaused)
            return null;

        if (Samples.Length == 0)
            return null;

        if (ReadPosition >= Samples.Length)
        {
            if (Loop)
                ReadPosition = 0;
            else
            {
                destroy = true;
                return null;
            }
        }

        int samplesToSend = Math.Min(PacketSize, Samples.Length - ReadPosition);
        float[] pcmChunk = new float[samplesToSend];

        Array.Copy(Samples, ReadPosition, pcmChunk, 0, samplesToSend);

        ReadPosition += samplesToSend;

        return PadPCMFloat(pcmChunk, PacketSize);
    }

    /// <summary>
    /// Pads a PCM buffer to the target length with zeros.
    /// </summary>
    /// <param name="pcmBuffer">The PCM buffer to pad.</param>
    /// <param name="targetLength">The target length of the buffer.</param>
    /// <returns>A padded PCM buffer.</returns>
    public static float[] PadPCMFloat(float[] pcmBuffer, int targetLength)
    {
        if (pcmBuffer.Length >= targetLength)
            return pcmBuffer;

        float[] paddedBuffer = new float[targetLength];
        Array.Copy(pcmBuffer, paddedBuffer, pcmBuffer.Length);

        return paddedBuffer;
    }

    static float[] _mixedData = new float[PacketSize];

    /// <summary>
    /// Mixes multiple audio playbacks into a single PCM buffer.
    /// </summary>
    /// <param name="playbacks">The array of audio playbacks to mix.</param>
    /// <param name="clipsToDestroy">The list of clip IDs to destroy after mixing.</param>
    /// <returns>A mixed PCM buffer.</returns>
    public static float[] MixPlaybacks(AudioClipPlayback[] playbacks, ref List<int> clipsToDestroy)
    {
        bool invalid = true;

        foreach (AudioClipPlayback playback in playbacks)
        {
            if (!playback.PrepareSample())
                clipsToDestroy.Add(playback.Id);
        }

        for (int i = 0; i < PacketSize; i++)
        {
            float mixedSample = 0;

            for (int j = 0; j < playbacks.Length; j++)
            {
                if (playbacks[j].NextSample == null)
                    continue;

                float sample = playbacks[j].NextSample[i] * playbacks[j].Volume;
                mixedSample += sample;
                invalid = false;
            }

            if (mixedSample > 1.0f)
                mixedSample = 1.0f;

            if (mixedSample < -1.0f)
                mixedSample = -1.0f;

            _mixedData[i] = mixedSample;
        }

        if (invalid)
            return null;

        return _mixedData;
    }

    public void Dispose()
    {
        if (StreamSource != null)
            StreamSource.Stop();
    }
}