using VoiceChat.Playbacks;

public static class AudioPlayerPoolManager
{
    private static Dictionary<string, AudioPlayerPool> _pools = new Dictionary<string, AudioPlayerPool>();
    private static byte _nextControllerId = 0;

    /// <summary>
    /// Creates or gets an AudioPlayer pool with the specified name and size.
    /// </summary>
    public static AudioPlayerPool GetPool(string poolName, int size)
    {
        if (_pools.TryGetValue(poolName, out AudioPlayerPool existingPool))
            return existingPool;
        
        _nextControllerId = GetNextGloballyAvailableControllerId(_nextControllerId);
        
        var pool = new AudioPlayerPool(poolName, size, ref _nextControllerId);
        _pools.Add(poolName, pool);
        return pool;
    }
    
    private static byte GetNextGloballyAvailableControllerId(byte startFrom)
    {
        HashSet<byte> usedIds = new HashSet<byte>();
        
        foreach (var instance in SpeakerToyPlaybackBase.AllInstances)
        {
            usedIds.Add(instance.ControllerId);
        }
        
        foreach (var id in AudioPlayer.AudioPlayerById.Keys)
        {
            usedIds.Add(id);
        }
        
        for (byte i = startFrom; i < byte.MaxValue; i++)
        {
            if (!usedIds.Contains(i))
                return i;
        }
        
        for (byte i = 0; i < startFrom; i++)
        {
            if (!usedIds.Contains(i))
                return i;
        }
        
        throw new InvalidOperationException("No globally available controller IDs found.");
    }

    /// <summary>
    /// Removes a pool by name.
    /// </summary>
    public static bool RemovePool(string poolName)
    {
        if (_pools.TryGetValue(poolName, out AudioPlayerPool pool))
        {
            pool.Dispose();
            return _pools.Remove(poolName);
        }
        return false;
    }

    /// <summary>
    /// Gets all active pools.
    /// </summary>
    public static IReadOnlyDictionary<string, AudioPlayerPool> GetAllPools() => _pools;
}