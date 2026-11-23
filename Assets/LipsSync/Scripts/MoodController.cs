using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UniVRM10;

[RequireComponent(typeof(Vrm10Instance))]
public class MoodController : MonoBehaviour
{
    public enum MoodType
    {
        Neutral,
        Happy,
        Sad,
        Angry,
        Relaxed,
        Surprised
    }

    [Serializable]
    public class MoodDefinition
    {
        public MoodType Mood;
        [Range(0f, 1f)] public float Weight = 1f;
        [Min(0f)] public float TransitionDuration = 0.25f;
        public Ease TransitionEase = Ease.OutSine;
    }

    [SerializeField] private MoodType _currentMood = MoodType.Neutral;

    [Header("Mood Definitions")]
    [SerializeField] private MoodDefinition[] _moods;

    [Header("Defaults")]
    [SerializeField][Range(0f, 1f)] private float _neutralWeight = 0f;

    private readonly List<Tween> _activeTweens = new List<Tween>(16);
    private readonly List<ExpressionKey> _allPresetKeys = new List<ExpressionKey>(16);

    private Vrm10RuntimeExpression _expression;
    private Vrm10Instance _instance;

    private bool _ready;

    private void Awake()
    {
        _instance = GetComponent<Vrm10Instance>();

        CacheAllPresetKeys();
        StartCoroutine(WaitForRuntime());
    }

    private void OnEnable()
    {
        SetMood(_currentMood);
    }

    public void SetMood(MoodType mood, float overrideDuration = -1f)
    {
        if (!_ready)
            return;

        var def = FindMoodDef(mood);
        if (def == null)
            return;

        float duration = overrideDuration >= 0f ? overrideDuration : def.TransitionDuration;
        Ease ease = def.TransitionEase;
        var targetPreset = MapMoodToPreset(mood);
        var targetKey = ExpressionKey.CreateFromPreset(targetPreset);

        KillActiveTweens();

        for (int i = 0; i < _allPresetKeys.Count; i++)
        {
            var key = _allPresetKeys[i];
            float target = key.Equals(targetKey) ? def.Weight : _neutralWeight;
            var getter = new DOGetter<float>(() => _expression.GetWeight(key));
            var setter = new DOSetter<float>(x => _expression.SetWeight(key, x));
            var tween = DOTween.To(getter, setter, target, duration)
                .SetEase(ease)
                .SetRecyclable(true)
                .SetLink(gameObject)
                .SetId(key);
            _activeTweens.Add(tween);
        }

        _currentMood = mood;
    }

    public void SetMoodImmediate(MoodType mood)
    {
        if (!_ready)
            return;

        var def = FindMoodDef(mood);
        if (def == null)
            return;

        KillActiveTweens();

        var targetPreset = MapMoodToPreset(mood);
        var targetKey = ExpressionKey.CreateFromPreset(targetPreset);

        for (int i = 0; i < _allPresetKeys.Count; i++)
        {
            var key = _allPresetKeys[i];
            _expression.SetWeight(key, key.Equals(targetKey) ? def.Weight : _neutralWeight);
        }
    }

    public void ResetAllImmediate()
    {
        if (!_ready)
            return;

        KillActiveTweens();

        for (int i = 0; i < _allPresetKeys.Count; i++)
            _expression.SetWeight(_allPresetKeys[i], _neutralWeight);
    }

    private void KillActiveTweens()
    {
        for (int i = _activeTweens.Count - 1; i >= 0; --i)
        {
            var t = _activeTweens[i];
            if (t.IsActive())
                t.Kill();
        }
        _activeTweens.Clear();
    }

    private IEnumerator WaitForRuntime()
    {
        int frameGuard = 0;
        while (_instance == null || _instance.Runtime == null || _instance.Runtime.Expression == null)
        {
            frameGuard++;
            if (frameGuard > 300)
                yield break;
            yield return null;
        }

        _expression = _instance.Runtime.Expression;
        _ready = true;
    }

    private MoodDefinition FindMoodDef(MoodType mood)
    {
        if (_moods == null || _moods.Length == 0)
            return null;
        for (int i = 0; i < _moods.Length; i++)
            if (_moods[i].Mood == mood)
                return _moods[i];
        return null;
    }

    private void CacheAllPresetKeys()
    {
        _allPresetKeys.Clear();
        var values = Enum.GetValues(typeof(ExpressionPreset));
        foreach (var v in values)
        {
            if (v == null)
                continue;
            var preset = (ExpressionPreset)v;
            if (preset == ExpressionPreset.custom)
                continue;
            _allPresetKeys.Add(ExpressionKey.CreateFromPreset(preset));
        }
    }

    private ExpressionPreset MapMoodToPreset(MoodType mood)
    {
        switch (mood)
        {
            case MoodType.Happy:
                return ExpressionPreset.happy;
            case MoodType.Angry:
                return ExpressionPreset.angry;
            case MoodType.Sad:
                return ExpressionPreset.sad;
            case MoodType.Relaxed:
                return ExpressionPreset.relaxed;
            case MoodType.Surprised:
                return ExpressionPreset.surprised;
            case MoodType.Neutral:
            default:
                return ExpressionPreset.neutral;
        }
    }

    private void OnDisable()
    {
        KillActiveTweens();
        ResetAllImmediate();
    }
}
