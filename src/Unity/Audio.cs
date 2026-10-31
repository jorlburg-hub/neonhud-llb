using System;
using NeonHud.Core;
using UnityEngine;

namespace NeonHud
{
    internal sealed class SpectrumAnalyzer
    {
        private const int FftSize = 1024;
        private readonly float[] _fft = new float[FftSize];
        private readonly float[] _wave = new float[256];
        private readonly int[] _edges;
        private readonly float[] _hold = new float[HudState.Bands];

        public SpectrumAnalyzer()
        {
            int n = HudState.Bands;
            _edges = new int[n + 1];
            const double lo = 2, hi = 700;
            for (int i = 0; i <= n; i++)
            {
                _edges[i] = (int)Math.Round(lo * Math.Pow(hi / lo, i / (double)n));
                if (i > 0 && _edges[i] <= _edges[i - 1]) _edges[i] = _edges[i - 1] + 1;
            }
        }

        public void Update(HudState s, float dt, float gainDb)
        {
            AudioListener.GetSpectrumData(_fft, 0, FFTWindow.BlackmanHarris);

            for (int i = 0; i < HudState.Bands; i++)
            {
                float mx = 0f;
                for (int b = _edges[i]; b < _edges[i + 1] && b < FftSize; b++)
                    if (_fft[b] > mx) mx = _fft[b];

                float db = 20f * (float)Math.Log10(mx + 1e-9f) + i * 1.2f + gainDb;
                float target = M01((db + 72f) / 60f);

                float cur = s.Spectrum[i];
                cur = target > cur ? cur + (target - cur) * 0.6f : Math.Max(target, cur - dt * 1.8f);
                s.Spectrum[i] = cur;

                if (cur >= s.Peaks[i]) { s.Peaks[i] = cur; _hold[i] = 0.45f; }
                else if (_hold[i] > 0f) _hold[i] -= dt;
                else s.Peaks[i] = Math.Max(cur, s.Peaks[i] - dt * 0.55f);
            }

            AudioListener.GetOutputData(_wave, 0);
            float sum = 0f;
            for (int i = 0; i < _wave.Length; i++) sum += _wave[i] * _wave[i];
            float rms = (float)Math.Sqrt(sum / _wave.Length);
            s.SetLevel(Math.Max(-80f, 20f * (float)Math.Log10(rms + 1e-9f)));
        }

        public static void Clear(HudState s)
        {
            for (int i = 0; i < HudState.Bands; i++) { s.Spectrum[i] = 0f; s.Peaks[i] = 0f; }
            s.SetLevel(-80f);
        }

        private static float M01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    }
}
