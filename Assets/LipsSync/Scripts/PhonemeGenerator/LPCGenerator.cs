using System;
using UnityEngine;

public class LPCGenerator : PhonemeTimelineGeneratorBase
{
    public LPCGenerator(GeneratorSettings settings) : base(settings) { }

    protected override AnalysisResult AnalyzeFrame(float[] windowedFrame, float[] real, float[] imag, int frameSize, int sampleRate, int lpcOrder)
    {
        AnalysisResult res = new AnalysisResult { Phoneme = ' ' };

        var lpcCoeffs = SignalProcessing.LPCFromAutocorrelation(windowedFrame, lpcOrder);
        var lpcFormants = SignalProcessing.ExtractFormantsFromLPCByFreqResponse(lpcCoeffs, sampleRate, _settings.MinFormantFreq, Mathf.Min(_settings.MaxFormantFreq, sampleRate / 2f), Math.Max(1024, frameSize * 2), 3);

        if (lpcFormants == null || lpcFormants.Count == 0)
        {
            res.HasFormants = false;
            return res;
        }

        float f1 = lpcFormants.Count > 0 ? lpcFormants[0] : 0f;
        float f2 = lpcFormants.Count > 1 ? lpcFormants[1] : f1 * 1.5f;
        res.HasFormants = true;
        res.F1 = f1;
        res.F2 = f2;
        res.Energy = SignalProcessing.SpectralEnergyFromFFT(real, imag, frameSize);
        return res;
    }
}
