using System;
using System.Collections;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UniVRM10;

[RequireComponent(typeof(Vrm10Instance))]
public class AutoEyesBlink : MonoBehaviour
{
    [Header("Timing (seconds)")]
    [SerializeField] private float _minInterval = 2.5f;
    [SerializeField] private float _maxInterval = 5f;

    [Header("Blink shape")]
    [SerializeField][Range(0f, 1f)] private float _weightTarget = 1f;

    [Header("Duration (seconds)")]
    [SerializeField][Min(0.02f)] private float _baseDuration = 0.12f;
    [SerializeField][Range(0f, 0.8f)] private float _durationJitter = 0.25f;

    [Header("Behavior probabilities")]
    [SerializeField][Range(0f, 1f)] private float _doubleBlinkProbability = 0.12f;
    [SerializeField][Range(0f, 1f)] private float _microBlinkProbability = 0.08f;
    [SerializeField][Range(0f, 1f)] private float _longBlinkProbability = 0.02f;

    [Header("Long blink extra")]
    [SerializeField][Min(0f)] private float _longBlinkExtra = 0.18f;

    private Vrm10Instance _instance;
    private Vrm10RuntimeExpression _expression;
    private ExpressionKey _blinkKey;
    private Coroutine _blinkRoutine;
    private bool _stopRequested;

    private void Awake()
    {
        _instance = GetComponent<Vrm10Instance>();
    }

    private void OnEnable()
    {
        _stopRequested = false;
        if (_blinkRoutine == null)
            _blinkRoutine = StartCoroutine(BlinkLoop());
    }

    private void OnDisable()
    {
        _stopRequested = true;
        if (_blinkRoutine != null)
        {
            StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
        }
        ResetImmediate();
    }

    private IEnumerator BlinkLoop()
    {
        yield return WaitForRuntime();

        while (true)
        {
            if (_stopRequested)
                yield break;

            float next = SampleInterval();
            yield return new WaitForSeconds(next);

            if (_stopRequested)
                yield break;

            bool isMicro = UnityEngine.Random.value < _microBlinkProbability;
            bool isLong = !isMicro && (UnityEngine.Random.value < _longBlinkProbability);
            float duration = SampleDuration(isMicro, isLong);

            PlayBlink(duration);

            if (!isMicro && UnityEngine.Random.value < _doubleBlinkProbability)
            {
                float gap = Mathf.Clamp(duration * 0.6f, 0.05f, 0.25f);
                yield return new WaitForSeconds(gap);
                if (_stopRequested)
                    yield break;
                float secondDur = SampleDuration(false, false);
                PlayBlink(secondDur);
            }
        }
    }

    private IEnumerator WaitForRuntime()
    {
        int frameCount = 0;
        while (_instance == null || _instance.Runtime == null || _instance.Runtime.Expression == null)
        {
            frameCount++;
            if (frameCount > 300)
                yield break;
            yield return null;
        }

        _expression = _instance.Runtime.Expression;

        if (TryCreateBlinkKeyFromPreset(out var presetKey))
            _blinkKey = presetKey;
        else
            _blinkKey = ExpressionKey.CreateCustom("Blink");

        yield break;
    }

    private bool TryCreateBlinkKeyFromPreset(out ExpressionKey key)
    {
        key = default;
        if (Enum.TryParse(typeof(ExpressionPreset), "blink", true, out var parsed) && parsed is ExpressionPreset p)
        {
            key = ExpressionKey.CreateFromPreset(p);
            return true;
        }

        if (Enum.TryParse(typeof(ExpressionPreset), "eyeBlink", true, out parsed) && parsed is ExpressionPreset p2)
        {
            key = ExpressionKey.CreateFromPreset(p2);
            return true;
        }

        return false;
    }

    private float SampleInterval()
    {
        return UnityEngine.Random.Range(_minInterval, _maxInterval);
    }

    private float SampleDuration(bool micro, bool longBlink)
    {
        if (micro)
            return Mathf.Max(0.03f, _baseDuration * 0.45f);

        float jitter = (_durationJitter * (UnityEngine.Random.value * 2f - 1f));
        float dur = Mathf.Max(0.03f, _baseDuration + (_baseDuration * jitter));

        if (longBlink)
            dur += _longBlinkExtra;

        return dur;
    }

    private void PlayBlink(float duration)
    {
        if (_expression == null)
            return;

        DOTween.Kill(_blinkKey);

        float closeRatio = 0.35f;
        float closeTime = Mathf.Max(0.01f, duration * closeRatio);
        float openTime = Mathf.Max(0.01f, duration - closeTime);

        var get = new DOGetter<float>(() => _expression.GetWeight(_blinkKey));
        var set = new DOSetter<float>(x => _expression.SetWeight(_blinkKey, x));

        DOTween.To(get, set, _weightTarget, closeTime)
            .SetEase(Ease.OutSine)
            .SetRecyclable(true)
            .SetLink(gameObject)
            .SetId(_blinkKey);

        DOTween.To(get, set, 0f, openTime)
            .SetDelay(closeTime)
            .SetEase(Ease.InSine)
            .SetRecyclable(true)
            .SetLink(gameObject)
            .SetId(_blinkKey);
    }

    private void ResetImmediate()
    {
        if (_expression == null)
            return;

        _expression.SetWeight(_blinkKey, 0f);
        DOTween.Kill(_blinkKey);
    }
}
