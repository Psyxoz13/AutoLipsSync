using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVRM10;

public class AutoLipsSync : MonoBehaviour
{
    [SerializeField] private Vrm10Instance _instance;
    [SerializeField] private LipsSyncTimelineAsset _timelineAsset;

    [SerializeField][Range(0f, 1f)] private float _weightTarget = .7f;
    [SerializeField][Range(0f, 1f)] private float _attackRatio = .25f;
    [SerializeField] private bool _autoplayOnEnable = true;
    [SerializeField] private bool _loop = false;

    [Header("Smoothness")]
    [SerializeField][Range(0f, 0.9f)] private float _crossfade = .17f;
    [SerializeField] private float _minAttack = .08f;
    [SerializeField] private float _minRelease = .08f;
    [SerializeField] private float _maxAttack = .12f;
    [SerializeField] private float _maxRelease = .12f;

    [Serializable]
    public struct PhonemeClip
    {
        public char Phoneme;
        public VRM10Expression Clip;
    }

    [SerializeField]
    private List<PhonemeClip> _phonemeClips = new List<PhonemeClip>()
    {
        new PhonemeClip() { Phoneme = 'A', Clip = null },
        new PhonemeClip() { Phoneme = 'I', Clip = null },
        new PhonemeClip() { Phoneme = 'U', Clip = null },
        new PhonemeClip() { Phoneme = 'E', Clip = null },
        new PhonemeClip() { Phoneme = 'O', Clip = null },
    };

    private const float WeightEpsilon = 0.0005f;

    private readonly Dictionary<char, ExpressionKey> _keysMap = new Dictionary<char, ExpressionKey>(8);
    private readonly Dictionary<ExpressionKey, float> _currentWeights = new Dictionary<ExpressionKey, float>(128);

    private readonly HashSet<ExpressionKey> _usedKeys = new HashSet<ExpressionKey>(16);

    private readonly Queue<Envelope> _scheduled = new Queue<Envelope>(128);
    private readonly List<Envelope> _active = new List<Envelope>(128);
    private readonly Stack<Envelope> _envelopePool = new Stack<Envelope>(128);

    private Vrm10RuntimeExpression _expression;

    private bool _isPlaying = false;

    private void Awake()
    {
        if (_instance == null)
            return;

        _expression = _instance.Runtime?.Expression;

        var modelExpression = _instance?.Vrm?.Expression;
        if (modelExpression == null || _expression == null)
            return;

        var keys = _expression.ExpressionKeys;
        for (int i = 0; i < _phonemeClips.Count; ++i)
        {
            var item = _phonemeClips[i];
            if (item.Clip == null)
                continue;

            AddClipToModel(modelExpression, item);

            for (int k = 0; k < keys.Count; ++k)
            {
                if (string.Equals(keys[k].Name, item.Clip.name, StringComparison.OrdinalIgnoreCase))
                {
                    _keysMap[item.Phoneme] = keys[k];
                    break;
                }
            }
        }
    }

    private void AddClipToModel(VRM10ObjectExpression modelExpression, PhonemeClip item)
    {
        var pressets = (ExpressionPreset[])Enum.GetValues(typeof(ExpressionPreset));
        bool isContains = false;

        foreach (var presset in pressets)
        {
            if (modelExpression.Clips.Contains((presset, item.Clip)))
            {
                isContains = true;
                break;
            }
        }

        if (!isContains)
        {
            modelExpression.AddClip(ExpressionPreset.custom, item.Clip);
        }
    }

    private void OnEnable()
    {
        if (_autoplayOnEnable)
            PlayTimeline(_timelineAsset, _loop);
    }

    public void PlayTimeline(LipsSyncTimelineAsset timelineAsset, bool shouldLoop = false)
    {
        if (timelineAsset == null)
            return;

        StopTimeline();

        _timelineAsset = timelineAsset;
        _loop = shouldLoop;

        _usedKeys.Clear();
        _currentWeights.Clear();

        while (_scheduled.Count > 0)
        {
            ReleaseEnvelope(_scheduled.Dequeue());
        }

        for (int i = 0; i < _active.Count; ++i)
        {
            ReleaseEnvelope(_active[i]);
        }

        _active.Clear();

        _expression = _instance?.Runtime?.Expression ?? _expression;

        var entries = timelineAsset.TimelineEntries;
        if (entries == null || entries.Length == 0)
        {
            _isPlaying = false;
            return;
        }

        float cursor = 0f;
        float startTimeBase = Time.realtimeSinceStartup;

        for (int i = 0; i < entries.Length; ++i)
        {
            var e = entries[i];
            char phoneme = e.Phoneme;
            if (char.IsWhiteSpace(phoneme))
            {
                cursor += e.Duration;
                continue;
            }

            bool nextIsReal = (i + 1 < entries.Length) && !char.IsWhiteSpace(entries[i + 1].Phoneme);
            float overlapSec = nextIsReal ? Mathf.Clamp(_crossfade * e.Duration, 0f, Mathf.Max(0f, e.Duration)) : 0f;
            float relativeStart = Mathf.Max(0f, cursor - overlapSec);
            float absoluteStart = startTimeBase + relativeStart;

            float attack = Mathf.Clamp(Mathf.Max(_minAttack, e.Duration * _attackRatio), 0f, _maxAttack);
            float release = Mathf.Clamp(Mathf.Max(_minRelease, e.Duration - attack), 0f, _maxRelease);
            float sustain = Mathf.Max(0f, e.Duration - attack - release);

            if (!_keysMap.TryGetValue(phoneme, out var key))
            {
                key = ExpressionKey.CreateCustom(phoneme.ToString());
                _keysMap[phoneme] = key;
            }

            _usedKeys.Add(key);

            var env = AcquireEnvelope();
            env.Setup(key, absoluteStart, attack, sustain, release);

            _scheduled.Enqueue(env);
            cursor += e.Duration;
        }

        _isPlaying = true;
    }

    public void StopTimeline()
    {
        _isPlaying = false;

        while (_scheduled.Count > 0)
        {
            ReleaseEnvelope(_scheduled.Dequeue());
        }

        for (int i = 0; i < _active.Count; ++i)
        {
            ReleaseEnvelope(_active[i]);
        }

        _active.Clear();

        if (_expression != null)
        {
            foreach (var key in _usedKeys)
            {
                _expression.SetWeight(key, 0f);
            }
        }

        _usedKeys.Clear();
        _currentWeights.Clear();
    }

    private void Update()
    {
        if (!_isPlaying)
            return;

        float now = Time.realtimeSinceStartup;

        while (_scheduled.Count > 0)
        {
            var head = _scheduled.Peek();
            if (head.StartTime <= now)
            {
                head = _scheduled.Dequeue();

                for (int j = _active.Count - 1; j >= 0; --j)
                {
                    if (_active[j].Key.Equals(head.Key))
                    {
                        ReleaseEnvelope(_active[j]);
                        _active.RemoveAt(j);
                    }
                }
                _active.Add(head);

                continue;
            }
            break;
        }

        for (int i = _active.Count - 1; i >= 0; --i)
        {
            var env = _active[i];
            var key = env.Key;

            if (!_currentWeights.TryGetValue(key, out var curVal))
            {
                curVal = _expression != null ? _expression.GetWeight(key) : 0f;
                _currentWeights[key] = curVal;
            }

            if (!env.Started)
            {
                env.StartValue = curVal;
                env.Started = true;
            }

            float t = now - env.StartTime;
            float attack = env.Attack;
            float sustain = env.Sustain;
            float release = env.Release;
            float total = attack + sustain + release;

            float value;
            if (t < 0f)
            {
                continue;
            }
            else if (t < attack)
            {
                float p = attack > 0f ? (t / attack) : 1f;
                value = Mathf.Lerp(env.StartValue, _weightTarget, p);
            }
            else if (t < attack + sustain)
            {
                value = _weightTarget;
            }
            else if (t < total)
            {
                float p = release > 0f ? ((t - attack - sustain) / release) : 1f;
                value = Mathf.Lerp(_weightTarget, 0f, p);
            }
            else
            {
                value = 0f;
                if (_expression != null)
                {
                    _expression.SetWeight(key, 0f);
                }
                _currentWeights.Remove(key);
                ReleaseEnvelope(env);

                int last = _active.Count - 1;

                _active[i] = _active[last];
                _active.RemoveAt(last);

                continue;
            }

            if (Mathf.Abs(curVal - value) > WeightEpsilon)
            {
                if (_expression != null)
                {
                    _expression.SetWeight(key, value);
                }
                _currentWeights[key] = value;
            }
        }

        if (_active.Count == 0 && _scheduled.Count == 0 && _loop)
        {
            PlayTimeline(_timelineAsset, _loop);
        }
    }

    private Envelope AcquireEnvelope()
    {
        return _envelopePool.Count > 0 ? _envelopePool.Pop() : new Envelope();
    }

    private void ReleaseEnvelope(Envelope e)
    {
        e.Reset();

        _envelopePool.Push(e);
    }

    private void OnDisable()
    {
        StopTimeline();
    }

    private void OnDestroy()
    {
        StopTimeline();
    }

    private class Envelope
    {
        public ExpressionKey Key;

        public float StartTime;
        public float Attack;
        public float Sustain;
        public float Release;
        public float StartValue;

        public bool Started;

        public void Setup(ExpressionKey key, float startTime, float attack, float sustain, float release)
        {
            Key = key;
            StartTime = startTime;
            Attack = attack;
            Sustain = sustain;
            Release = release;
            StartValue = 0f;
            Started = false;
        }

        public void Reset()
        {
            Key = default;
            StartTime = 0f;
            Attack = 0f;
            Sustain = 0f;
            Release = 0f;
            StartValue = 0f;
            Started = false;
        }
    }
}
