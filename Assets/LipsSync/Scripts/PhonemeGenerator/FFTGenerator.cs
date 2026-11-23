using UnityEngine;

public class FFTGenerator : PhonemeTimelineGeneratorBase
{
    public FFTGenerator(GeneratorSettings settings) : base(settings) { }

    protected override AnalysisResult AnalyzeFrame(float[] windowedFrame, float[] real, float[] imag, int frameSize, int sampleRate, int lpcOrder)
    {
        AnalysisResult res = new AnalysisResult { Phoneme = ' ' };

        int minBin = Mathf.Clamp(Mathf.FloorToInt(_settings.MinFormantFreq * frameSize / (float)sampleRate), 0, frameSize / 2);
        int maxBin = Mathf.Clamp(Mathf.CeilToInt(_settings.MaxFormantFreq * frameSize / (float)sampleRate), 0, frameSize / 2);

        int peak1 = -1, peak2 = -1;
        float mag1 = 0f, mag2 = 0f;
        for (int b = minBin; b <= maxBin; b++)
        {
            float mag = Mathf.Sqrt(real[b] * real[b] + imag[b] * imag[b]);
            if (mag > mag1)
            {
                mag2 = mag1;
                peak2 = peak1;
                mag1 = mag;
                peak1 = b;
            }
            else if (mag > mag2)
            {
                mag2 = mag;
                peak2 = b;
            }
        }

        if (peak1 < 0)
        {
            res.HasFormants = false;
            return res;
        }

        float f1 = sampleRate * peak1 / (float)frameSize;
        float f2 = peak2 >= 0 ? sampleRate * peak2 / (float)frameSize : f1 * 1.5f;
        if (f1 > f2)
            (f1, f2) = (f2, f1);

        res.HasFormants = true;
        res.F1 = f1;
        res.F2 = f2;
        res.Energy = mag1 + mag2;
        return res;
    }
}