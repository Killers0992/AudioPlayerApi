/// <summary>
/// Wrapper for a pooled AudioPlayer instance.
/// </summary>
public class PooledAudioPlayer
{
    private readonly AudioPlayerPool _pool;
    private readonly string _internalName;
    private Vector3 _hiddenPosition = new Vector3(999, 999, 999);
    private bool _isReturning = false;
    
    public AudioPlayer Player { get; private set; }
    public string UserName { get; private set; }
    public bool IsActive { get; private set; }

    internal PooledAudioPlayer(AudioPlayer player, AudioPlayerPool pool, string userName)
    {
        Player = player;
        _pool = pool;
        _internalName = player.Name;
        UserName = userName;
        IsActive = true;
    }

    internal void Activate(string userName)
    {
        IsActive = true;
        _isReturning = false;
        UserName = userName ?? $"PooledPlayer_{Player.ControllerID}";
        
        Player.RemoveAllClips();
        Player.DestroyWhenAllClipsPlayed = false;
        Player.SendSoundGlobally = true;
        Player.Owners.Clear();
        Player.Condition = null;
        
        foreach (var speaker in Player.SpeakersByName.Values)
        {
            if (speaker != null && speaker.transform.position == _hiddenPosition)
            {
                speaker.Position = Vector3.zero;
            }
        }
    }

    internal void Deactivate()
    {
        IsActive = false;
        _isReturning = false;
        
        Player.RemoveAllClips();
        
        foreach (var speaker in Player.SpeakersByName.Values)
        {
            if (speaker != null)
            {
                speaker.Position = _hiddenPosition;
                speaker.Volume = 0f;
            }
        }
        
        Player.Owners.Clear();
        Player.Condition = null;
    }

    /// <summary>
    /// Returns this player to the pool.
    /// </summary>
    public void Return()
    {
        if (_isReturning) return;
        _pool.Return(this);
    }

    /// <summary>
    /// Returns and destroys this player.
    /// </summary>
    public void ReturnDel()
    {
        if (_isReturning) return;
        _pool.ReturnDel(this);
    }

    /// <summary>
    /// Returns this player to the pool when all clips have finished playing.
    /// </summary>
    public void ReturnWhenAllClipsPlayed()
    {
        if (_isReturning) return;
        _isReturning = true;
        
        if (Player.ClipsById.Count == 0)
        {
            Return();
            return;
        }
        
        Player.StartCoroutine(MonitorClipsAndReturn(false));
    }

    /// <summary>
    /// Returns and destroys this player when all clips have finished playing.
    /// </summary>
    public void ReturnDelWhenAllClipsPlayed()
    {
        if (_isReturning) return;
        _isReturning = true;
        
        if (Player.ClipsById.Count == 0)
        {
            ReturnDel();
            return;
        }
        
        Player.StartCoroutine(MonitorClipsAndReturn(true));
    }

    private System.Collections.IEnumerator MonitorClipsAndReturn(bool destroy)
    {
        try
        {
            while (Player.ClipsById.Count > 0)
            {
                bool allClipsWillEnd = true;
                foreach (var clip in Player.ClipsById.Values)
                {
                    if (clip.Loop && clip.DestroyOnEnd == false)
                    {
                        allClipsWillEnd = false;
                        break;
                    }
                }

                if (!allClipsWillEnd)
                {
                    foreach (var clip in Player.ClipsById.Values.ToList())
                    {
                        if (clip.Loop)
                        {
                            clip.Loop = false;
                        }
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }

            _isReturning = false;

            if (destroy)
            {
                ReturnDel();
            }
            else
            {
                Return();
            }
        }
        finally
        {
            _isReturning = false;
        }
    }
    
    public AudioClipPlayback AddClip(string clipName, float volume = 1f, bool loop = false, bool destroyOnEnd = true)
        => Player.AddClip(clipName, volume, loop, destroyOnEnd);

    public Speaker AddSpeaker(string name, Vector3 position, float volume = 1f, bool isSpatial = true, float minDistance = 5f, float maxDistance = 5f)
        => Player.AddSpeaker(name, position, volume, isSpatial, minDistance, maxDistance);

    public Speaker GetOrAddSpeaker(string name, Vector3 position, float volume = 1f, bool isSpatial = true, float minDistance = 5f, float maxDistance = 5f)
        => Player.GetOrAddSpeaker(name, position, volume, isSpatial, minDistance, maxDistance);

    public bool RemoveSpeaker(string name)
        => Player.RemoveSpeaker(name);

    public bool SetSpeakerPosition(string name, Vector3 position)
        => Player.SetSpeakerPosition(name, position);

    public bool TryGetSpeaker(string name, out Speaker speaker)
        => Player.TryGetSpeaker(name, out speaker);

    public bool RemoveClipById(int clipId)
        => Player.RemoveClipById(clipId);

    public bool RemoveClipByName(string clipName)
        => Player.RemoveClipByName(clipName);

    public void RemoveAllClips()
        => Player.RemoveAllClips();

    public bool TryGetClip(int clipId, out AudioClipPlayback clip)
        => Player.TryGetClip(clipId, out clip);
}