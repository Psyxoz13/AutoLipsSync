public class MFCCGenerator : PhonemeTimelineGeneratorBase
{
    public MFCCGenerator(GeneratorSettings settings) : base(settings) { }

    protected override AnalysisResult AnalyzeFrame(float[] windowedFrame, float[] real, float[] imag, int frameSize, int sampleRate, int lpcOrder)
    {
        AnalysisResult res = new AnalysisResult { Phoneme = ' ' };

        float[] mfcc = SignalProcessing.MFCCFromFrame(windowedFrame, sampleRate, 13, 26);

        float centroid = 0f;
        double magSum = 0.0;
        int half = frameSize / 2;
        for (int k = 1; k <= half; k++)
        {
            float mag = real[k] * real[k] + imag[k] * imag[k];
            centroid += k * mag;
            magSum += mag;
        }
        if (magSum > 0)
            centroid = centroid / (float)magSum * sampleRate / frameSize;
        else
            centroid = 0f;

        char chosen = '-';
        float bestDist = float.MaxValue;
        foreach (var kv in _vowelPrototypes)
        {
            float protoCentroid = (kv.Value.x + kv.Value.y) * 0.5f;
            float d = (centroid - protoCentroid) * (centroid - protoCentroid);
            if (d < bestDist)
            { bestDist = d; chosen = kv.Key; }
        }

        float energy = SignalProcessing.SpectralEnergyFromFFT(real, imag, frameSize);
        char phoneme = (energy < _settings.EnergyThreshold || bestDist > _settings.VowelDistanceThreshold) ? '-' : chosen;

        res.Phoneme = phoneme;
        res.HasFormants = false;
        res.Energy = energy;
        return res;
    }
}

