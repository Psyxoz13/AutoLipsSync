using System;
using System.Collections.Generic;
using UnityEngine;

public static class SignalProcessing
{
    public static float ComputeRMS(float[] data)
    {
        double s = 0.0;
        for (int i = 0; i < data.Length; i++)
            s += data[i] * data[i];
        return (float)Math.Sqrt(s / data.Length);
    }

    public static float[] HammingWindow(int n)
    {
        float[] w = new float[n];
        for (int i = 0; i < n; i++)
            w[i] = 0.54f - 0.46f * Mathf.Cos(2f * Mathf.PI * i / (n - 1));
        return w;
    }

    public static int NextPowerOfTwo(int v)
    {
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        v++;
        return v;
    }

    public static void FFT(float[] real, float[] imag, int n, bool invert = false)
    {
        int j = 0;
        for (int i = 1; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
            {
                float tr = real[i];
                real[i] = real[j];
                real[j] = tr;
                float ti = imag[i];
                imag[i] = imag[j];
                imag[j] = ti;
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = 2.0 * Math.PI / len * (invert ? -1 : 1);
            float wlenR = (float)Math.Cos(ang);
            float wlenI = (float)Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                float wR = 1f, wI = 0f;
                for (int k = 0; k < len / 2; k++)
                {
                    int u = i + k;
                    int v = i + k + len / 2;
                    float ur = real[u], ui = imag[u];
                    float vr = real[v] * wR - imag[v] * wI;
                    float vi = real[v] * wI + imag[v] * wR;
                    real[u] = ur + vr;
                    imag[u] = ui + vi;
                    real[v] = ur - vr;
                    imag[v] = ui - vi;
                    float nextWR = wR * wlenR - wI * wlenI;
                    float nextWI = wR * wlenI + wI * wlenR;
                    wR = nextWR;
                    wI = nextWI;
                }
            }
        }

        if (invert)
        {
            for (int i = 0; i < n; i++)
            {
                real[i] /= n;
                imag[i] /= n;
            }
        }
    }

    public static float[] LPCFromAutocorrelation(float[] signal, int order)
    {
        int n = signal.Length;
        float[] r = new float[order + 1];
        for (int i = 0; i <= order; i++)
        {
            double sum = 0.0;
            for (int j = 0; j < n - i; j++)
                sum += signal[j] * signal[j + i];
            r[i] = (float)sum;
        }

        float[] a = new float[order + 1];
        float[] e = new float[order + 1];
        a[0] = 1f;
        if (Mathf.Abs(r[0]) < 1e-9f)
        {
            for (int i = 1; i <= order; i++)
                a[i] = 0f;
            return a;
        }
        e[0] = r[0];

        for (int i = 1; i <= order; i++)
        {
            double acc = 0.0;
            for (int j = 1; j < i; j++)
                acc += a[j] * r[i - j];
            float k = (float)((r[i] - acc) / e[i - 1]);
            float[] anew = new float[i + 1];
            anew[0] = 1f;
            for (int j = 1; j < i; j++)
                anew[j] = a[j] - k * a[i - j];
            anew[i] = k;
            for (int j = 0; j <= i; j++)
                a[j] = anew[j];
            e[i] = e[i - 1] * (1f - k * k);
            if (e[i] <= 0f)
                e[i] = 1e-9f;
        }

        return a;
    }

    public static List<float> ExtractFormantsFromLPCByFreqResponse(float[] lpc, int sampleRate, float minFreq, float maxFreq, int points = 4096, int maxFormants = 3)
    {
        int order = lpc.Length - 1;
        int half = points / 2;
        float[] mags = new float[half + 1];
        for (int k = 0; k <= half; k++)
        {
            float w = 2f * Mathf.PI * k / points;
            float real = 0f;
            float imag = 0f;
            for (int n = 0; n <= order; n++)
            {
                real += lpc[n] * Mathf.Cos(w * n);
                imag -= lpc[n] * Mathf.Sin(w * n);
            }
            float denom = real * real + imag * imag;
            mags[k] = denom > 0f ? 1f / Mathf.Sqrt(denom) : 0f;
        }

        int minBin = Mathf.Clamp(Mathf.FloorToInt(minFreq * points / sampleRate), 0, half);
        int maxBin = Mathf.Clamp(Mathf.CeilToInt(maxFreq * points / sampleRate), 0, half);

        List<Tuple<int, float>> peaks = new List<Tuple<int, float>>();
        for (int b = Mathf.Max(minBin + 1, 1); b < Mathf.Min(maxBin - 1, half - 1); b++)
        {
            if (mags[b] > mags[b - 1] && mags[b] > mags[b + 1])
                peaks.Add(new Tuple<int, float>(b, mags[b]));
        }

        peaks.Sort((x, y) => y.Item2.CompareTo(x.Item2));
        List<float> formants = new List<float>();
        int count = Math.Min(maxFormants, peaks.Count);
        for (int i = 0; i < count; i++)
        {
            float freq = peaks[i].Item1 * sampleRate / (float)points;
            formants.Add(freq);
        }

        formants.Sort();
        return formants;
    }

    public static float SpectralEnergyFromFFT(float[] real, float[] imag, int size)
    {
        double sum = 0.0;
        int half = size / 2;
        for (int i = 0; i <= half; i++)
        {
            double mag = real[i] * real[i] + imag[i] * imag[i];
            sum += mag;
        }
        return (float)sum;
    }

    public static float[] MFCCFromFrame(float[] frame, int sampleRate, int numCoeffs = 13, int numFilters = 26)
    {
        int n = frame.Length;
        int N = NextPowerOfTwo(Math.Max(512, n));
        float[] real = new float[N];
        float[] imag = new float[N];
        for (int i = 0; i < n; i++)
            real[i] = frame[i];
        FFT(real, imag, N);
        int half = N / 2;
        float[] power = new float[half + 1];
        for (int i = 0; i <= half; i++)
            power[i] = real[i] * real[i] + imag[i] * imag[i];

        float fMin = 0f;
        float fMax = sampleRate / 2f;
        float melMin = HzToMel(fMin);
        float melMax = HzToMel(fMax);
        float[] melPoints = new float[numFilters + 2];
        for (int i = 0; i < melPoints.Length; i++)
            melPoints[i] = melMin + (melMax - melMin) * i / (melPoints.Length - 1);
        float[] hzPoints = new float[melPoints.Length];
        for (int i = 0; i < hzPoints.Length; i++)
            hzPoints[i] = MelToHz(melPoints[i]);
        int[] bin = new int[hzPoints.Length];
        for (int i = 0; i < hzPoints.Length; i++)
            bin[i] = Mathf.Clamp(Mathf.FloorToInt((hzPoints[i] / sampleRate) * N), 0, half);

        float[][] filters = new float[numFilters][];
        for (int m = 1; m <= numFilters; m++)
        {
            int f_m_minus = bin[m - 1];
            int f_m = bin[m];
            int f_m_plus = bin[m + 1];
            filters[m - 1] = new float[half + 1];
            for (int k = f_m_minus; k <= f_m; k++)
                filters[m - 1][k] = (k - f_m_minus) / (float)Math.Max(1, f_m - f_m_minus);
            for (int k = f_m; k <= f_m_plus; k++)
                filters[m - 1][k] = (f_m_plus - k) / (float)Math.Max(1, f_m_plus - f_m);
        }

        float[] filterEnergies = new float[numFilters];
        for (int m = 0; m < numFilters; m++)
        {
            double sum = 0.0;
            for (int k = 0; k <= half; k++)
                sum += power[k] * filters[m][k];
            filterEnergies[m] = (float)Math.Log(Math.Max(1e-9, sum));
        }

        float[] mfcc = new float[numCoeffs];
        for (int k = 0; k < numCoeffs; k++)
        {
            double sum = 0.0;
            for (int m = 0; m < numFilters; m++)
                sum += filterEnergies[m] * Math.Cos(Math.PI * k * (2 * m + 1) / (2.0 * numFilters));
            mfcc[k] = (float)sum;
        }

        return mfcc;
    }

    public static float HzToMel(float hz) => 2595f * Mathf.Log10(1f + hz / 700f);
    public static float MelToHz(float mel) => 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);
}
