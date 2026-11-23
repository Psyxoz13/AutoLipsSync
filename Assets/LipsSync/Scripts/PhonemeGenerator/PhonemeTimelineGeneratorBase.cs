using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class PhonemeTimelineGeneratorBase
{

    protected readonly GeneratorSettings _settings;

    protected static readonly Dictionary<char, Vector2> _vowelPrototypes = new()
    {
        { 'A', new Vector2(800f, 1200f) },
        { 'I', new Vector2(300f, 2200f) },
        { 'U', new Vector2(350f, 800f)  },
        { 'E', new Vector2(500f, 2000f) },
        { 'O', new Vector2(500f, 900f)  }
    };

    protected PhonemeTimelineGeneratorBase(GeneratorSettings settings)
    {
        _settings = settings;
    }

    protected abstract AnalysisResult AnalyzeFrame(float[] windowedFrame, float[] real, float[] imag, int frameSize, int sampleRate, int lpcOrder);

    public LipsSyncTimelineEntry[] GenerateTimeline(AudioClip clip)
    {
        if (clip == null || clip.samples == 0)
            return Array.Empty<LipsSyncTimelineEntry>();

        int sr = clip.frequency;
        int channels = clip.channels;
        int totalSamples = clip.samples * channels;

        float[] raw = new float[totalSamples];
        clip.GetData(raw, 0);

        int monoSamples = clip.samples;
        float[] mono = new float[monoSamples];
        if (channels == 1)
            Array.Copy(raw, mono, monoSamples);
        else
        {
            for (int i = 0; i < monoSamples; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += raw[i * channels + c];
                mono[i] = sum / channels;
            }
        }

        int frameSize = SignalProcessing.NextPowerOfTwo(Math.Max(16, Mathf.RoundToInt(_settings.FrameDuration * sr)));
        float hopSec = _settings.FrameDuration * (1f - _settings.Overlap);
        hopSec = Mathf.Max(hopSec, 1.0f / sr);
        int hop = Mathf.Max(1, Mathf.RoundToInt(hopSec * sr));

        float[] window = SignalProcessing.HammingWindow(frameSize);
        float minFreq = _settings.MinFormantFreq;
        float maxFreq = Mathf.Min(_settings.MaxFormantFreq, sr / 2f);
        int lpcOrder = Math.Min(16, Math.Max(8, frameSize / 2));

        List<LipsSyncTimelineEntry> timeline = new List<LipsSyncTimelineEntry>();

        for (int pos = 0; pos + frameSize <= monoSamples; pos += hop)
        {
            float[] frame = new float[frameSize];
            Array.Copy(mono, pos, frame, 0, frameSize);

            float rms = SignalProcessing.ComputeRMS(frame);
            if (rms < _settings.SilenceThreshold)
            {
                timeline.Add(new LipsSyncTimelineEntry(' ', hop / (float)sr));
                continue;
            }

            for (int i = 0; i < frameSize; i++)
                frame[i] *= window[i];

            float[] real = new float[frameSize];
            float[] imag = new float[frameSize];
            Array.Copy(frame, real, frameSize);
            SignalProcessing.FFT(real, imag, frameSize);

            var result = AnalyzeFrame(frame, real, imag, frameSize, sr, lpcOrder);

            if (result.Phoneme != ' ')
            {
                timeline.Add(new LipsSyncTimelineEntry(result.Phoneme, hop / (float)sr));
                continue;
            }

            if (!result.HasFormants)
            {
                timeline.Add(new LipsSyncTimelineEntry(' ', hop / (float)sr));
                continue;
            }

            float f1 = result.F1;
            float f2 = result.F2;

            char chosenVowel = '-';
            float bestDistV = float.MaxValue;
            foreach (var kv in _vowelPrototypes)
            {
                float dx = f1 - kv.Value.x;
                float dy = f2 - kv.Value.y;
                float d = dx * dx + dy * dy;
                if (d < bestDistV)
                {
                    bestDistV = d;
                    chosenVowel = kv.Key;
                }
            }

            float energySum = result.Energy;
            char finalPhoneme = (energySum < _settings.EnergyThreshold || bestDistV > _settings.VowelDistanceThreshold) ? '-' : chosenVowel;
            timeline.Add(new LipsSyncTimelineEntry(finalPhoneme, hop / (float)sr));
        }

        List<LipsSyncTimelineEntry> merged = new List<LipsSyncTimelineEntry>();
        foreach (var e in timeline)
        {
            if (merged.Count == 0)
                merged.Add(e);
            else
            {
                var last = merged[merged.Count - 1];
                if (last.Phoneme == e.Phoneme)
                    merged[merged.Count - 1] = new LipsSyncTimelineEntry(last.Phoneme, last.Duration + e.Duration);
                else
                    merged.Add(e);
            }
        }

        return merged.ToArray();
    }

    public struct AnalysisResult
    {
        public bool HasFormants;
        public float F1;
        public float F2;
        public char Phoneme;
        public float Energy;
    }

    public struct GeneratorSettings
    {
        public float FrameDuration;
        public float Overlap;
        public float SilenceThreshold;
        public float MinFormantFreq;
        public float MaxFormantFreq;
        public float EnergyThreshold;
        public float VowelDistanceThreshold;
    }

}