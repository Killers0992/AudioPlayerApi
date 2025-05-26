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

        var pool = new AudioPlayerPool(poolName, size, ref _nextControllerId);
        _pools.Add(poolName, pool);
        return pool;
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