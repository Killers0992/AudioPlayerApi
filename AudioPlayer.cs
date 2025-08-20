using Utils.Networking;
using VoiceChat.Playbacks;

/// <summary>
/// Represents an audio player that can manage and play multiple audio clips.
/// </summary>
public class AudioPlayer : MonoBehaviour
{
    /// <summary>
    /// A dictionary of all AudioPlayer instances indexed by their names.
    /// </summary>
    public static Dictionary<string, AudioPlayer> AudioPlayerByName = new Dictionary<string, AudioPlayer>();

    /// <summary>
    /// A dictionary of all AudioPlayer instances indexed by their ids.
    /// </summary>
    public static Dictionary<byte, AudioPlayer> AudioPlayerById = new Dictionary<byte, AudioPlayer>();

    /// <summary>
    /// Creates a new AudioPlayer instance with the specified name.
    /// </summary>
    /// <param name="name">The unique name for the AudioPlayer instance.</param>
    /// <returns>A new <see cref="AudioPlayer"/> instance if the name is unique; otherwise, null.</returns>
    public static AudioPlayer Create(string name, string autoPlayClip = null, Action<AudioPlayer> onAutoPlay = null, bool destroyWhenAllClipsPlayed = false, bool sendSoundGlobally = true, List<ReferenceHub> owners = null, byte controllerId = 255, Action<AudioPlayer> onIntialCreation = null, Func<ReferenceHub, bool> condition = null)
    {
        if (AudioPlayerByName.ContainsKey(name))
        {
            ServerConsole.AddLog($"[AudioPlayer] Player with name {name} already exists!");
            return null;
        }

        GameObject go = new GameObject(name);
        go.hideFlags = HideFlags.DontUnloadUnusedAsset;

        AudioPlayer player = go.AddComponent<AudioPlayer>();

        byte targetId = controllerId;

        if (targetId == 255)
        {
            HashSet<byte> usedIds = new HashSet<byte>();
        
            foreach (var instance in SpeakerToyPlaybackBase.AllInstances)
            {
                usedIds.Add(instance.ControllerId);
            }
            
            foreach (var existingPlayer in AudioPlayerById.Values)
            {
                usedIds.Add(existingPlayer.ControllerID);
            }
            
            for (byte x = 0; x < byte.MaxValue; x++)
            {
                if (usedIds.Contains(x))
                    continue;

                targetId = x;
                break;
            }
        
            if (targetId == 255)
            {
                ServerConsole.AddLog($"[AudioPlayer] No available controller IDs!");
                Destroy(go);
                return null;
            }
        
            AudioPlayerById.Add(targetId, player);
        }
        else
        {
            bool idInUse = false;
            
            foreach (var instance in SpeakerToyPlaybackBase.AllInstances)
            {
                if (instance.ControllerId == controllerId)
                {
                    idInUse = true;
                    break;
                }
            }
            
            if (!idInUse && AudioPlayerById.ContainsKey(controllerId))
            {
                idInUse = true;
            }
        
            if (idInUse)
            {
                ServerConsole.AddLog($"[AudioPlayer] Controller ID {controllerId} is already in use!");
                Destroy(go);
                return null;
            }
        
            AudioPlayerById.Add(controllerId, player);
        }


        player.ControllerID = targetId;
        player.Name = name;

        if (!string.IsNullOrEmpty(autoPlayClip) && AudioClipStorage.AudioClips.ContainsKey(autoPlayClip))
        {
            onAutoPlay?.Invoke(player);
            player.AddClip(autoPlayClip);
        }

        player.DestroyWhenAllClipsPlayed = destroyWhenAllClipsPlayed;
        player.Condition = condition;
        player.SendSoundGlobally = sendSoundGlobally;

        if (owners != null)
            player.Owners = owners;

        onIntialCreation?.Invoke(player);
        AudioPlayerByName.Add(name, player);

        return player;
    }

    /// <summary>
    /// Creates a new AudioPlayer instance with the specified name or gets existing one.
    /// </summary>
    /// <param name="name">The unique name for the AudioPlayer instance.</param>
    /// <returns>A new <see cref="AudioPlayer"/> instance if the name is unique; otherwise, null.</returns>
    public static AudioPlayer CreateOrGet(string name, string autoPlayClip = null, Action<AudioPlayer> onAutoPlay = null, bool destroyWhenAllClipsPlayed = false, bool sendSoundGlobally = true, List<ReferenceHub> owners = null, byte controllerId = 255, Action<AudioPlayer> onIntialCreation = null, Func<ReferenceHub, bool> condition = null)
    {
        if (TryGet(name, out AudioPlayer player))
        {
            if (!string.IsNullOrEmpty(autoPlayClip) && AudioClipStorage.AudioClips.ContainsKey(autoPlayClip))
            {
                onAutoPlay?.Invoke(player);
                player.AddClip(autoPlayClip);
            }

            return player;
        }

        return Create(name, autoPlayClip, onAutoPlay, destroyWhenAllClipsPlayed, sendSoundGlobally, owners, controllerId, onIntialCreation, condition);
    }

    /// <summary>
    /// Attempts to retrieve an audio player by its name.
    /// </summary>
    /// <param name="name">The name of the audio player.</param>
    /// <param name="player">The retrieved audio player if found.</param>
    /// <returns>True if the audio player is found; otherwise, false.</returns>
    public static bool TryGet(string name, out AudioPlayer player) => AudioPlayerByName.TryGetValue(name, out player);

    private double _lastSendTime;

    /// <summary>
    /// Internal buffer for mixed PCM audio data.
    /// </summary>
    private float[] _mixedPcm = new float[AudioClipPlayback.PacketSize];

    /// <summary>
    /// Internal buffer for encoded PCM audio data.
    /// </summary>
    private byte[] _encodedPcm = new byte[AudioClipPlayback.PacketSize];

    /// <summary>
    /// Opus encoder for compressing audio data.
    /// </summary>
    private OpusEncoder encoder = new OpusEncoder(OpusApplicationType.Audio);

    /// <summary>
    /// List of IDs of audio clips to be destroyed.
    /// </summary>
    private List<int> clipsToDestroy = new List<int>();

    /// <summary>
    /// A dictionary of active audio clips indexed by their IDs.
    /// </summary>
    public Dictionary<int, AudioClipPlayback> ClipsById = new Dictionary<int, AudioClipPlayback>();

    /// <summary>
    /// A dictionary of active speakers indexed by their names.
    /// </summary>
    public Dictionary<string, Speaker> SpeakersByName = new Dictionary<string, Speaker>();

    /// <summary>
    /// Gets the name of the AudioPlayer instance.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// Destroys this audioplayer when all clips played.
    /// </summary>
    public bool DestroyWhenAllClipsPlayed { get; set; }

    /// <summary>
    /// Sends sounds globally to everyone connected to server.
    /// </summary>
    public bool SendSoundGlobally { get; set; } = true;

    /// <summary>
    /// Gets owners of this audioplayer which will receive this sound.
    /// </summary>
    public List<ReferenceHub> Owners = new List<ReferenceHub>();

    /// <summary>
    /// Gets used condition who will be able to hear sounds.
    /// </summary>
    public Func<ReferenceHub, bool> Condition { get; set; }

    /// <summary>
    /// Gets or sets the ID of the controller associated with this AudioPlayer.
    /// </summary>
    public byte ControllerID { get; set; } = 0;

    /// <summary>
    /// Gets the next available ID for a new audio clip.
    /// </summary>
    public int GetNextId
    {
        get
        {
            for (int x = 0; x < int.MaxValue; x++)
            {
                if (ClipsById.ContainsKey(x))
                    continue;

                return x;
            }

            return 0;
        }
    }

    /// <summary>
    /// Adds a new audio clip to the AudioPlayer.
    /// </summary>
    /// <param name="clipName">The name of the audio clip.</param>
    /// <param name="volume">The volume of the clip. Default is 1f.</param>
    /// <param name="loop">Whether the clip should loop. Default is false.</param>
    /// <param name="destroyOnEnd">Whether the clip should be destroyed after playback ends. Default is true.</param>
    /// <returns>A new <see cref="AudioClipPlayback"/> instance.</returns>
    public AudioClipPlayback AddClip(string clipName, float volume = 1f, bool loop = false, bool destroyOnEnd = true)
    {
        int newId = GetNextId;

        AudioClipPlayback clip = new AudioClipPlayback(newId, clipName, volume, loop, destroyOnEnd);
        ClipsById.Add(newId, clip);

        return clip;
    }

    /// <summary>
    /// Adds a new live audio stream and registers it in the playback system.
    /// </summary>
    /// <param name="url">
    /// The URL of the live audio stream to play.
    /// </param>
    /// <param name="name">
    /// The name to associate with the stream. Defaults to "RadioStream" if not specified.
    /// </param>
    /// <returns>
    /// A <see cref="StreamPlayback"/> instance representing the created live stream.
    /// </returns>
    public StreamPlayback AddLiveStream(string url, float volume = 1f, string name = "RadioStream")
    {
        var stream = new StreamPlayback(url, name);
        int newId = GetNextId;

        var wrapper = new AudioClipPlayback(newId, name, volume, true, false)
        {
            IsStream = true,
            StreamSource = stream
        };

        ClipsById.Add(newId, wrapper);
        return stream;
    }

    /// <summary>
    /// Removes clip by their identifier.
    /// </summary>
    /// <param name="clipId">The clip identifier.</param>
    /// <returns>If successfuly removed.</returns>
    public bool RemoveClipById(int clipId)
    {
        if (!ClipsById.TryGetValue(clipId, out AudioClipPlayback clip))
            return false;

        clip.Dispose();

        return ClipsById.Remove(clipId);
    }

    /// <summary>
    /// Removes clip by their name. ( remember that if theres multiple playing clips with same name all will be removed )
    /// </summary>
    /// <param name="clipName">The clip name.</param>
    /// <returns>If any removed.</returns>
    public bool RemoveClipByName(string clipName)
    {
        List<int> idsToDestroy = new List<int>();

        foreach(AudioClipPlayback clip in ClipsById.Values)
        {
            if (clip.Clip == clipName)
                idsToDestroy.Add(clip.Id);
        }

        if (idsToDestroy.Count == 0)
            return false;

        foreach(int id in idsToDestroy)
        {
            if (!ClipsById.TryGetValue(id, out AudioClipPlayback clip))
                continue;

            clip.Dispose();
            ClipsById.Remove(id);
        }

        return true;
    }


    /// <summary>
    /// Removes all audio clips currently stored in the player.
    /// </summary>
    public void RemoveAllClips()
    {
        ClipsById.Clear();
    }

    /// <summary>
    /// Attempts to retrieve an audio clip by its unique identifier.
    /// </summary>
    /// <param name="clipId">The unique identifier of the audio clip.</param>
    /// <param name="clip">The retrieved audio clip playback object if found.</param>
    /// <returns>True if the audio clip is found; otherwise, false.</returns>
    public bool TryGetClip(int clipId, out AudioClipPlayback clip) => ClipsById.TryGetValue(clipId, out clip);

    /// <summary>
    /// Gets or adds a speaker with the specified parameters.
    /// </summary>
    public Speaker GetOrAddSpeaker(string name, float volume = 1f, bool isSpatial = true, float minDistance = 5f, float maxDistance = 5f) =>
        GetOrAddSpeaker(name, Vector3.zero, volume, isSpatial, minDistance, maxDistance);

    /// <summary>
    /// Gets or adds a speaker with the specified parameters.
    /// </summary>
    public Speaker GetOrAddSpeaker(string name, Vector3 position, float volume = 1f, bool isSpatial = true, float minDistance = 5f, float maxDistance = 5f)
    {
        if (SpeakersByName.TryGetValue(name, out Speaker speaker))
            return speaker;

        speaker = AddSpeaker(name, position, volume, isSpatial, minDistance, maxDistance);
        return speaker;
    }

    /// <summary>
    /// Adds a new speaker with the specified parameters.
    /// </summary>
    public Speaker AddSpeaker(string name, Vector3 position, float volume = 1f, bool isSpatial = true, float minDistance = 5f, float maxDistance = 5f)
    {
        if (SpeakersByName.ContainsKey(name))
        {
            ServerConsole.AddLog($"[AudioPlayer] Player {Name} already contains speaker with name {name}");
            return null;
        }

        Speaker speaker = Speaker.Create(ControllerID, position, volume, isSpatial, minDistance, maxDistance);

        speaker.Name = name;
        speaker.Owner = this;

        SpeakersByName.Add(name, speaker);

        return speaker;
    }

    /// <summary>
    /// Overloaded methods for adding speakers with fewer parameters.
    /// </summary>
    public Speaker AddSpeaker(string name, float volume = 1f, bool isSpatial = true, float minDistance = 5f, float maxDistance = 5f) =>
        this.AddSpeaker(name, Vector3.zero, volume, isSpatial, minDistance, maxDistance);

    /// <summary>
    /// Removes a speaker by its name.
    /// </summary>
    public bool RemoveSpeaker(string name)
    {
        if (!SpeakersByName.TryGetValue(name, out Speaker speaker))
            return false;

        NetworkServer.Destroy(speaker.gameObject);
        SpeakersByName.Remove(name);
        return true;
    }

    /// <summary>
    /// Sets the position of a specified speaker in the 3D space.
    /// </summary>
    /// <param name="name">The name of the speaker.</param>
    /// <param name="position">The new position of the speaker in 3D space.</param>
    /// <returns>True if the speaker was successfully updated; otherwise, false if the speaker was not found.</returns>
    public bool SetSpeakerPosition(string name, Vector3 position)
    {
        if (!SpeakersByName.TryGetValue(name, out Speaker speaker))
        {
            ServerConsole.AddLog($"[AudioPlayer] Speaker with name {name} not found!");
            return false;
        }

        speaker.Position = position;
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a speaker by its name.
    /// </summary>
    /// <param name="name">The name of the speaker.</param>
    /// <param name="speaker">The retrieved speaker object if found.</param>
    /// <returns>True if the speaker is found; otherwise, false.</returns>
    public bool TryGetSpeaker(string name, out Speaker speaker) => SpeakersByName.TryGetValue(name, out speaker);

    /// <summary>
    /// Destroys audioplayer.
    /// </summary>
    public void Destroy() => UnityEngine.Object.Destroy(gameObject);

    void Update()
    {
        double packetInterval = (double)AudioClipPlayback.PacketSize / AudioClipPlayback.SamplingRate;
        double now = Time.unscaledTimeAsDouble;

        if (_lastSendTime == 0)
            _lastSendTime = now;

        while (now - _lastSendTime >= packetInterval)
        {
            SendAudioData();
            _lastSendTime += packetInterval;
        }
    }

    /// <summary>
    /// Sends mixed audio data to the network.
    /// </summary>
    void SendAudioData()
    {
        if (ClipsById.Count == 0)
        {
            if (DestroyWhenAllClipsPlayed)
                Destroy(this.gameObject);
            return;
        }

        _mixedPcm = AudioClipPlayback.MixPlaybacks(ClipsById.Values.ToArray(), ref clipsToDestroy);

        bool anyRemoved = false;
        foreach (int clipId in clipsToDestroy)
        {
            if (ClipsById.TryGetValue(clipId, out AudioClipPlayback clip))
                clip.Dispose();

            ClipsById.Remove(clipId);
            anyRemoved = true;
        }

        if (anyRemoved)
            clipsToDestroy.Clear();

        //This can only happen when theres clips which are paused.
        if (_mixedPcm == null)
            return;

        int encodedLength = encoder.Encode(_mixedPcm, _encodedPcm);

        if (encodedLength <= 0)
        {
            ServerConsole.AddLog($"[AudioPlayer] Failed to encode audio!");
            return;
        }

        AudioMessage msg = new AudioMessage
        {
            ControllerId = ControllerID,
            Data = _encodedPcm,
            DataLength = encodedLength,
        };

        if (Condition != null)
        {
            msg.SendToHubsConditionally(Condition);
            return;
        }

        if (SendSoundGlobally)
        {
            NetworkServer.SendToReady(msg);
        }
        else if (Owners.Count != 0)
        {
            foreach (ReferenceHub owner in Owners)
            {
                owner.connectionToClient.Send(msg);
            }
        }
    }

    /// <summary>
    /// Called when the component is destroyed.
    /// </summary>
    void OnDestroy()
    {
        if (IsInvoking(nameof(SendAudioData)))
            CancelInvoke(nameof(SendAudioData));

        foreach(var clip in ClipsById.Values)
        {
            clip.Dispose();
        }

        ClipsById.Clear();

        foreach (var speaker in SpeakersByName.Values)
        {
            speaker.Destroy();
        }

        SpeakersByName = null;

        AudioPlayerById.Remove(ControllerID);

        AudioPlayerByName.Remove(Name);

        encoder?.Dispose();
        encoder = null;
    }
}