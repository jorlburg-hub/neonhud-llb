using System;

namespace NeonHud.Core
{
    public sealed class DemoFeed
    {
        private float _speed = 22f;
        private float _nextHit, _hitAt = -100f, _stunLen;
        private int _hits;
        private readonly Random _rng = new Random(7);

        public void Step(HudState s, float now)
        {
            if (now >= _nextHit)
            {
                _speed = _speed > 700f ? 22f : _speed * 1.32f + 6f;
                _hitAt = now;
                _stunLen = 0.25f + Math.Min(1.1f, _speed / 500f);
                _hits++;
                _nextHit = now + _stunLen + 0.7f + (float)_rng.NextDouble() * 0.6f;
            }
            s.SetSpeed(_speed, now);

            float inStun = now - _hitAt;
            if (inStun < _stunLen)
            {
                float left = _stunLen - inStun;
                s.SetRelease(true, inStun / _stunLen, (int)Math.Ceiling(left * 60f), _hits % 4 == 0, now);
            }
            else s.SetRelease(false, 0f, -1, false, now);

            DemoPlayers(s, now, _speed);

            float remaining = 300f - (now % 300f);
            s.SetTimer(remaining, true, "DEMO");
            s.SetSong("OUTSKIRTS  //  DEMO SIGNAL  //  NEONHUD", now);
            s.SourceLabel = "SRC:DEMO";
            Spectrum(s, now);
        }

        private static readonly string[] Chars = { "SWITCH", "CANDYMAN", "LATCH", "DUST & ASHES" };
        private static readonly string[] Names = { "GLITCH", "P2", "NETRUNNER", "P4" };

        public static void DemoPlayers(HudState s, float now, float speed)
        {
            float threat = Math.Min(1.2f, speed / 600f);
            for (int i = 0; i < 4; i++)
            {
                PlayerCard p = s.Players[i];
                p.Present = true;
                p.SetIdentity(i, i, Chars[i], Names[i]);
                float cycle = (now * 0.08f + i * 0.27f) % 1f;
                float hp = 1f - (float)Math.Floor(cycle * 5f) * 0.19f;
                p.SetHp(hp, 100, false, threat, threat >= hp, now);
                p.Energy01 = (now * 0.15f + i * 0.3f) % 1.15f;
                p.EnergyFull = p.Energy01 >= 1f;
                p.SetScore(false, 3 - (int)((now * 0.02f + i * 0.4f) % 3f), -1);
            }
        }

        public static void Spectrum(HudState s, float now)
        {
            for (int i = 0; i < HudState.Bands; i++)
            {
                float baseLevel = 0.85f - i * 0.035f;
                float wobble = 0.5f + 0.5f * (float)Math.Sin(now * (2.1f + i * 0.37f) + i * 1.7f);
                float beat = (float)Math.Pow(0.5 + 0.5 * Math.Sin(now * 7.85), 6.0) * (i < 4 ? 0.35f : 0.1f);
                float v = M.Clamp01(baseLevel * (0.35f + 0.55f * wobble) + beat);
                s.Spectrum[i] = v;
                s.Peaks[i] = M.Clamp01(v + 0.06f + 0.04f * (float)Math.Sin(now + i));
            }
            s.SetLevel(-14f + 6f * (float)Math.Sin(now * 1.3));
        }
    }
}
