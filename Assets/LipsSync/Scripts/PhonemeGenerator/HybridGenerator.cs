using System;
using UnityEngine;

public class HybridGenerator : PhonemeTimelineGeneratorBase
{
    public HybridGenerator(GeneratorSettings settings) : base(settings) { }

    protected override AnalysisResult AnalyzeFrame(float[] windowedFrame, float[] real, float[] imag, int frameSize, int sampleRate, int lpcOrder)
    {
        AnalysisResult res = new AnalysisResult { Phoneme = ' ' };

        var lpcCoeffs = SignalProcessing.LPCFromAutocorrelation(windowedFrame, lpcOrder);
        var lpcFormants = SignalProcessing.ExtractFormantsFromLPCByFreqResponse(lpcCoeffs, sampleRate, _settings.MinFormantFreq, Mathf.Min(_settings.MaxFormantFreq, sampleRate / 2f), Math.Max(1024, frameSize * 2), 3);

        if (lpcFormants != null && lpcFormants.Count > 0)
        {
            float f1 = lpcFormants.Count > 0 ? lpcFormants[0] : 0f;
            float f2 = lpcFormants.Count > 1 ? lpcFormants[1] : f1 * 1.5f;
            res.HasFormants = true;
            res.F1 = f1;
            res.F2 = f2;
            res.Energy = SignalProcessing.SpectralEnergyFromFFT(real, imag, frameSize);
            return res;
        }

        int minBin = Mathf.Clamp(Mathf.FloorToInt(_settings.MinFormantFreq * frameSize / (float)sampleRate), 0, frameSize / 2);
        int maxBin = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(_settings.MaxFormantFreq, sampleRate / 2f) * frameSize / (float)sampleRate), 0, frameSize / 2);

        int peak1 = -1, peak2 = -1;
        float mag1 = 0f, mag2 = 0f;
        for (int b = minBin; b <= maxBin; b++)
        {
            float mag = Mathf.Sqrt(real[b] * real[b] + imag[b] * imag[b]);
            if (mag > mag1)
            { mag2 = mag1; peak2 = peak1; mag1 = mag; peak1 = b; }
            else if (mag > mag2)
            { mag2 = mag; peak2 = b; }
        }

        if (peak1 < 0)
        { res.HasFormants = false; return res; }

        float ff1 = sampleRate * peak1 / (float)frameSize;
        float ff2 = peak2 >= 0 ? sampleRate * peak2 / (float)frameSize : ff1 * 1.5f;
        if (ff1 > ff2)
            (ff1, ff2) = (ff2, ff1);

        res.HasFormants = true;
        res.F1 = ff1;
        res.F2 = ff2;
        res.Energy = mag1 + mag2;
        return res;
    }
}
