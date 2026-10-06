using DG.Tweening;
using UnityEngine;


[RequireComponent(typeof(AudioSource))]
public class AudioPitchRandomizer : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] bool randomizePitch = true;
    [SerializeField][Range(0f, 1f)] float volume = 0.1f;
    [SerializeField][Range(0.8f, 1.2f)] float pitch = 0.8f;
    [SerializeField][Range(0f, 0.2f)] float pitchVariance = 0.2f;
    [SerializeField] bool fadeOutOnStop = true;
    [SerializeField] float fadeOutTime = 0.25f;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
    }

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

    public void PlayLoopSound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;

        audioSource.volume = volume;
        audioSource.pitch = randomizePitch
            ? pitch + Random.Range(-pitchVariance, pitchVariance)
            : pitch;

        audioSource.clip = clip;
        audioSource.Play();
    }

    public void PlayLoopSound(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null) return;

        audioSource.volume = volume;
        audioSource.pitch = randomizePitch
            ? pitch + Random.Range(-pitchVariance, pitchVariance)
            : pitch;

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void PlayPopSound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;

        audioSource.pitch = randomizePitch
            ? pitch + Random.Range(-pitchVariance, pitchVariance)
            : pitch;

        audioSource.PlayOneShot(clip);
    }

    public void PlayPopSound(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null) return;

        audioSource.pitch = randomizePitch
            ? pitch + Random.Range(-pitchVariance, pitchVariance)
            : pitch;

        audioSource.PlayOneShot(clip, volume);
    }

    public void StopSound()
    {
        if (audioSource == null) return;

        if (fadeOutOnStop)
        {
            audioSource.DOFade(0, 0.5f).OnComplete(()=>
            {
                audioSource.Stop();
                audioSource.clip = null;
            });
        }
        else
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
}
