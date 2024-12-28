public class Speaker : MonoBehaviour
{
    /// <summary>
    /// Creates a new speaker instance in the scene.
    /// </summary>
    /// <param name="controllerId">The network controller ID associated with the speaker.</param>
    /// <param name="position">The position to place the speaker in world coordinates.</param>
    /// <param name="volume">The initial volume of the speaker. Default is 1f.</param>
    /// <param name="isSpatial">Whether the audio is spatialized. Default is true.</param>
    /// <param name="minDistance">The minimum distance for full-volume audio. Default is 5f.</param>
    /// <param name="maxDistance">The maximum audible distance for the audio. Default is 5f.</param>
    /// <returns>A new <see cref="Speaker"/> instance if successful; otherwise, null.</returns>
    public static Speaker Create(byte controllerId, Vector3 position, float volume = 1f, bool isSpatial = true, float minDistance = 5f, float maxDistance = 5f)
    {
        SpeakerToy target = null;
        foreach (GameObject pref in NetworkClient.prefabs.Values)
        {
            if (!pref.TryGetComponent(out target))
                continue;

            break;
        }

        // This should never happen but safety.
        if (target == null)
            return null;

        SpeakerToy newInstance = Instantiate(target, position, Quaternion.identity);

        newInstance.NetworkControllerId = controllerId;

        newInstance.NetworkVolume = volume;
        newInstance.IsSpatial = isSpatial;
        newInstance.MinDistance = minDistance;
        newInstance.MaxDistance = maxDistance;

        Speaker speaker = newInstance.gameObject.AddComponent<Speaker>();
        speaker.Base = newInstance;

        NetworkServer.Spawn(newInstance.gameObject);

        return speaker;
    }

    /// <summary>
    /// Base SpeakerToy instance that this Speaker is wrapping around.
    /// </summary>
    public SpeakerToy Base;

    /// <summary>
    /// Owner of speaker.
    /// </summary>
    public AudioPlayer Owner;

    /// <summary>
    /// Gets name of speaker.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets speaker position.
    /// </summary>
    public Vector3 Position
    {
        get => transform.position;
        set => transform.position = value;
    }

    /// <summary>
    /// Gets or sets the volume of the speaker.
    /// </summary>
    public float Volume
    {
        get => Base.NetworkVolume;
        set => Base.NetworkVolume = value;
    }

    /// <summary>
    /// Gets or sets whether the speaker uses spatial audio.
    /// </summary>
    public bool IsSpatial
    {
        get => Base.NetworkIsSpatial;
        set => Base.NetworkIsSpatial = value;
    }

    /// <summary>
    /// Gets or sets the maximum distance at which the audio is audible.
    /// </summary>
    public float MaxDistance
    {
        get => Base.NetworkMaxDistance;
        set => Base.NetworkMaxDistance = value;
    }

    /// <summary>
    /// Gets or sets the minimum distance at which the audio is at full volume.
    /// </summary>
    public float MinDistance
    {
        get => Base.NetworkMinDistance;
        set => Base.NetworkMinDistance = value;
    }

    /// <summary>
    /// Destroys speaker.
    /// </summary>
    public void Destroy() => UnityEngine.Object.Destroy(gameObject);

    void OnDestroy() => Owner?.RemoveSpeaker(Name);
}