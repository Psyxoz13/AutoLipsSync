using System;
using UnityEngine;

public class LipsSyncTimelineAsset : ScriptableObject
{
    public LipsSyncTimelineEntry[] TimelineEntries;
}

[Serializable]
public struct LipsSyncTimelineEntry
{
    public char Phoneme;
    public float Duration;

    public LipsSyncTimelineEntry(char phoneme, float duration)
    {
        Phoneme = phoneme;
        Duration = duration;
    }
}