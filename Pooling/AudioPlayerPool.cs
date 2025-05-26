/// <summary>
/// Represents a pool of AudioPlayer instances.
/// </summary>
public class AudioPlayerPool : IDisposable
{
    private readonly string _poolName;
    private readonly int _maxSize;
    private readonly Queue<PooledAudioPlayer> _available = new Queue<PooledAudioPlayer>();
    private readonly HashSet<PooledAudioPlayer> _inUse = new HashSet<PooledAudioPlayer>();
    
    private readonly List<byte> _reservedControllerIds = new List<byte>(); 
    private readonly Queue<byte> _availableControllerIds = new Queue<byte>();
    
    private byte _startControllerIdValue;
    private bool _disposed = false;

    internal AudioPlayerPool(string poolName, int size, ref byte nextGlobalControllerIdRef)
    {
        _poolName = poolName;
        _maxSize = size;
        _startControllerIdValue = nextGlobalControllerIdRef; // Record the starting ID for this pool
        
        if (nextGlobalControllerIdRef + size > 256 || size <= 0)
        {
            throw new InvalidOperationException($"Cannot create pool '{poolName}' with size {size}. Controller ID range exceeded or invalid size. Starting global ID: {nextGlobalControllerIdRef}, byte.MaxValue: {byte.MaxValue}. Required IDs: {size}.");
        }
        
        for (int i = 0; i < size; i++)
        {
            byte currentIdToReserve = nextGlobalControllerIdRef;
            _reservedControllerIds.Add(currentIdToReserve);
            _availableControllerIds.Enqueue(currentIdToReserve);
            nextGlobalControllerIdRef++;
        }
    }

    /// <summary>
    /// Rents an AudioPlayer from the pool.
    /// </summary>
    public PooledAudioPlayer Rent(string playerName = null, Action<AudioPlayer> onRent = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AudioPlayerPool));

        PooledAudioPlayer pooledPlayer;

        if (_available.Count > 0)
        {
            pooledPlayer = _available.Dequeue();
            pooledPlayer.Activate(playerName);
        }
        
        else if ((_inUse.Count + _available.Count) < _maxSize && _availableControllerIds.Count > 0)
        {
            byte controllerId = _availableControllerIds.Dequeue();
            
            string internalName = $"{_poolName}_PooledPlayer_ID{controllerId}";
            
            var player = CreatePooledPlayer(internalName, controllerId);
            
            pooledPlayer = new PooledAudioPlayer(player, this, playerName);
        }
        else
        {
            if ((_inUse.Count + _available.Count) >= _maxSize)
                ServerConsole.AddLog($"[AudioPlayerPool] Pool '{_poolName}' is at its maximum capacity ({_maxSize}). Available: {_available.Count}, InUse: {_inUse.Count}.");
            else if (_availableControllerIds.Count == 0)
                 ServerConsole.AddLog($"[AudioPlayerPool] Pool '{_poolName}' has no available controller IDs to create a new player, though total player count ({_inUse.Count + _available.Count}) is less than maxSize ({_maxSize}).");
            return null;
        }

        _inUse.Add(pooledPlayer);
        onRent?.Invoke(pooledPlayer.Player);
        return pooledPlayer;
    }

    /// <summary>
    /// Returns a player to the pool for reuse.
    /// </summary>
    internal void Return(PooledAudioPlayer pooledPlayer)
    {
        if (_disposed || !_inUse.Contains(pooledPlayer))
            return;

        _inUse.Remove(pooledPlayer);
        pooledPlayer.Deactivate();
        _available.Enqueue(pooledPlayer);
    }

    /// <summary>
    /// Returns and destroys a player, freeing its resources and controller ID for potential new player creation.
    /// The player instance is not returned to the available queue.
    /// </summary>
    internal void ReturnDel(PooledAudioPlayer pooledPlayer)
    {
        if (_disposed || !_inUse.Contains(pooledPlayer))
            return;

        _inUse.Remove(pooledPlayer);
        
        var player = pooledPlayer.Player;
        if (player != null)
        {
            byte controllerId = player.ControllerID;
            // string playerName = player.Name;
            // AudioPlayer.AudioPlayerById.Remove(controllerId);
            // AudioPlayer.AudioPlayerByName.Remove(playerName); 
            
            UnityEngine.Object.Destroy(player.gameObject);
            
            _availableControllerIds.Enqueue(controllerId);
        }
    }

    private AudioPlayer CreatePooledPlayer(string internalName, byte controllerId)
    {
        if (AudioPlayer.AudioPlayerById.ContainsKey(controllerId))
        {
            ServerConsole.AddLog($"[AudioPlayerPool:{_poolName}] Warning: Controller ID {controllerId} is already in global use (AudioPlayerById) when creating new pooled player '{internalName}'.");
        }
        if (AudioPlayer.AudioPlayerByName.ContainsKey(internalName))
        {
             ServerConsole.AddLog($"[AudioPlayerPool:{_poolName}] Warning: Name '{internalName}' is already in global use (AudioPlayerByName) when creating new pooled player with ID {controllerId}.");
        }

        GameObject go = new GameObject(internalName);
        go.hideFlags = HideFlags.DontUnloadUnusedAsset;
        
        AudioPlayer player = go.AddComponent<AudioPlayer>();
        
        player.ControllerID = controllerId; 
        player.Name = internalName;       
        
        AudioPlayer.AudioPlayerById[controllerId] = player;
        AudioPlayer.AudioPlayerByName[internalName] = player;
        
        return player;
    }

    /// <summary>
    /// Gets the current status of the pool.
    /// </summary>
    public (int availableInQueue, int inUse, int totalManaged, int availableControllerIds) GetStatus()
    {
        return (_available.Count, _inUse.Count, _maxSize, _availableControllerIds.Count);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        foreach (var pooledPlayer in _inUse)
        {
            if (pooledPlayer.Player != null)
            {
                UnityEngine.Object.Destroy(pooledPlayer.Player.gameObject);
            }
        }
        _inUse.Clear();
        
        foreach (var pooledPlayer in _available)
        {
            if (pooledPlayer.Player != null)
            {
                UnityEngine.Object.Destroy(pooledPlayer.Player.gameObject);
            }
        }
        _available.Clear();
        
        _availableControllerIds.Clear();
        _reservedControllerIds.Clear();
    }
}