using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;

public class LipsSyncTest : MonoBehaviour
{
    public AudioClip Clip;
    public LipsSyncTimelineAsset TimelineAsset;
    public MoodController.MoodType MoodType;

    [Space]
    public AutoLipsSync AutoLipsSync;
    public MoodController MoodController;
    public AudioSource AudioSource;
    public PlayableDirector Director;

    [Space]
    public bool PlayOnStart;
    public float Delay = 3f;

    private void Start()
    {
        if (PlayOnStart)
        {
            DOVirtual.DelayedCall(Delay, Play)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    [ContextMenu("Play")]
    public void Play()
    {
        AudioSource.clip = Clip;
        AudioSource.Play();
        MoodController.SetMood(MoodType);
        AutoLipsSync.PlayTimeline(TimelineAsset);

        if (Director && Director.isActiveAndEnabled && Director.playableAsset)
            Director.Play();
    }
}
