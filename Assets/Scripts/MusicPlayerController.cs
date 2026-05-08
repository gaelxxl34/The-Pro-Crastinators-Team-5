using UnityEngine;

public class MusicPlayerController : MonoBehaviour
{
    [Header("Audio Setup")]
    [Tooltip("The AudioSource that will play music. If left empty, will look for one on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("The 4 music tracks. Drag your audio clips into these slots in the Inspector.")]
    public AudioClip[] tracks = new AudioClip[4];

    private int currentTrackIndex = -1;

    void Awake()
    {
        // If no AudioSource was assigned, grab the one on this GameObject
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Configure the AudioSource for music playback
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 0 = 2D audio, plays at same volume regardless of position
    }

    /// <summary>
    /// Called by the Track buttons. Pass 0, 1, 2, or 3 in the Inspector.
    /// </summary>
   public void PlayTrack(int trackIndex)
{
    Debug.Log($"PlayTrack called with index: {trackIndex}");  // ← add this line at the very top
    
        // Safety checks
        if (trackIndex < 0 || trackIndex >= tracks.Length)
        {
            Debug.LogWarning($"MusicPlayer: invalid track index {trackIndex}");
            return;
        }
        if (tracks[trackIndex] == null)
        {
            Debug.LogWarning($"MusicPlayer: track {trackIndex} has no audio clip assigned");
            return;
        }

        // If the same track is already playing, do nothing
        if (currentTrackIndex == trackIndex && audioSource.isPlaying)
            return;

        audioSource.Stop();
        audioSource.clip = tracks[trackIndex];
        audioSource.Play();
        currentTrackIndex = trackIndex;
    }

    /// <summary>
    /// Called by the Stop button.
    /// </summary>
    public void StopMusic()
    {
        audioSource.Stop();
        currentTrackIndex = -1;
    }
}

