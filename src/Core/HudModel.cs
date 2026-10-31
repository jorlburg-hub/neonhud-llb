using System;

namespace NeonHud.Core
{
    public sealed class HudTheme
    {
        public Col Background = Col.FromHex("#07070DE0", Col.Black);
        public Col Primary = Col.FromHex("#00F0FF", Col.White);
        public Col Secondary = Col.FromHex("#FF2A6D", Col.White);
        public Col Accent = Col.FromHex("#FCEE0A", Col.White);
        public Col Warn = Col.FromHex("#FF003C", Col.White);
        public Col Text = Col.FromHex("#E8FBFF", Col.White);
        public Col Hit = Col.FromHex("#FCEE0A", Col.White);
        public Col Ghost = Col.FromHex("#FFFFFF", Col.White);

        public float Opacity = 1f;
        public string Title = "NEON//LEAGUE";
        public bool Compact = true;

        public bool Scanlines = true;
        public bool Chromatic = true;
        public bool Glitch = true;
        public bool Spectrum = true;
        public bool DataLog = true;
        public bool ReactiveColours = true;
        public bool JapaneseAccents = true;

        public float Tier1 = 40f, Tier2 = 100f, Tier3 = 200f;
        public float SpeedBarMax = 600f;
        public bool TimerCountsDown = false;
        public float TimerWarnSeconds = 30f;

        public bool ShowRelease = true;
        public float ReleaseWindow = 0.8f;
        public Col Reduced = Col.FromHex("#B026FF", Col.White);

        public bool PlayerCards = true;
        public bool PlayerBand = true;
        public Col[] TeamColours =
        {
            Col.FromHex("#FF3B4E", Col.White),
            Col.FromHex("#2FA8FF", Col.White),
            Col.FromHex("#FCEE0A", Col.White),
            Col.FromHex("#39FF5A", Col.White),
            Col.FromHex("#E8FBFF", Col.White),
        };
    }

    public sealed class HudState
    {
        public const int Bands = 16;

        public float Time;

        public bool HasSpeed;
        public float Speed;
        public string SpeedText = "--";
        public float PeakSpeed;
        public string PeakLabel = "PEAK 0";
        public string DeltaLabel = "STABLE";
        public float SpeedChangedAt = -100f;
        private int _lastSpeed = -1;

        public bool HasTimer;
        public float TimerSeconds;
        public string TimerText = "--:--";
        public string TimerMode = "NO LINK";
        private int _lastTimer = int.MinValue;

        public string Song = "NO SIGNAL";
        public float SongChangedAt;
        public readonly float[] Spectrum = new float[Bands];
        public readonly float[] Peaks = new float[Bands];
        public float LevelDb = -80f;
        public string LevelLabel = "-INF dB";
        private int _lastLevel = int.MinValue;

        public string SourceLabel = "SRC:AUDIO";
        public string FpsLabel = "FPS --";
        public string SceneLabel = "SCN --";
        public string ClockLabel = "LOCAL --:--";
        public string ModeLabel = "MODE --";

        public void SetSpeed(float v, float now)
        {
            int iv = (int)Math.Round(M.Clamp(v, 0f, 9999999f));
            HasSpeed = true;
            if (iv == _lastSpeed) return;

            if (_lastSpeed >= 0)
            {
                int delta = iv - _lastSpeed;
                if (Math.Abs(delta) >= Math.Max(2f, _lastSpeed * 0.08f))
                {
                    SpeedChangedAt = now;
                    DeltaLabel = (delta > 0 ? "Δ +" : "Δ ") + delta;
                }
            }
            _lastSpeed = iv;
            Speed = iv;
            SpeedText = iv.ToString();
            if (iv > PeakSpeed)
            {
                PeakSpeed = iv;
                PeakLabel = "PEAK " + iv;
            }
        }

        public void ClearSpeed()
        {
            if (!HasSpeed && _lastSpeed < 0) return;
            HasSpeed = false;
            _lastSpeed = -1;
            Speed = 0f;
            SpeedText = "--";
        }

        public void SetTimer(float seconds, bool countdown, string mode)
        {
            HasTimer = true;
            TimerSeconds = seconds;
            if (!ReferenceEquals(mode, TimerMode)) { TimerMode = mode; ModeLabel = "MODE " + mode; }
            int total = countdown ? (int)Math.Ceiling(seconds) : (int)Math.Floor(seconds);
            if (total < 0) total = 0;
            if (total > 99 * 60 + 59) total = 99 * 60 + 59;
            if (total == _lastTimer) return;
            _lastTimer = total;
            TimerText = (total / 60) + ":" + (total % 60).ToString("00");
        }

        public void ClearTimer()
        {
            if (!HasTimer) return;
            HasTimer = false;
            _lastTimer = int.MinValue;
            TimerText = "--:--";
            TimerMode = "NO LINK";
            ModeLabel = "MODE NO LINK";
        }

        public void SetSong(string title, float now)
        {
            if (string.IsNullOrEmpty(title)) title = "NO SIGNAL";
            if (title == Song) return;
            Song = title;
            SongChangedAt = now;
        }

        public void SetLevel(float db)
        {
            LevelDb = db;
            int tenths = (int)Math.Round(db * 10f);
            if (tenths == _lastLevel) return;
            _lastLevel = tenths;
            LevelLabel = db <= -79.5f ? "-INF dB" : (tenths / 10f).ToString("0.0") + " dB";
        }

        public readonly PlayerCard[] Players = { new PlayerCard(), new PlayerCard(), new PlayerCard(), new PlayerCard() };

        public bool HasRelease;
        public bool ReleaseActive;
        public bool ReleaseReduced;
        public float ReleaseProgress;
        public int ReleaseFramesLeft = -1;
        public float ReleaseEndedAt = -100f;
        public string ReleaseReadout = "--";
        public string ReleaseUnit = "";
        private int _lastReadout = int.MinValue;

        public void SetRelease(bool active, float progress, int framesLeft, bool reduced, float now)
        {
            HasRelease = true;
            if (ReleaseActive && !active) ReleaseEndedAt = now;
            ReleaseActive = active;
            ReleaseReduced = active && reduced;
            ReleaseProgress = active ? M.Clamp01(progress) : 0f;
            ReleaseFramesLeft = active ? framesLeft : -1;

            int key;
            if (!active) key = int.MinValue + 1;
            else if (framesLeft >= 0) key = Math.Min(framesLeft, 999);
            else key = -1000 - (int)(ReleaseProgress * 100f);
            if (key == _lastReadout) return;
            _lastReadout = key;
            if (!active) { ReleaseReadout = "--"; ReleaseUnit = ""; }
            else if (framesLeft >= 0) { ReleaseReadout = Math.Min(framesLeft, 999).ToString(); ReleaseUnit = "F"; }
            else { ReleaseReadout = ((int)(ReleaseProgress * 100f)).ToString(); ReleaseUnit = "%"; }
        }

        public void ClearRelease()
        {
            if (!HasRelease && !ReleaseActive) return;
            HasRelease = false;
            ReleaseActive = false;
            ReleaseReduced = false;
            ReleaseProgress = 0f;
            ReleaseFramesLeft = -1;
            _lastReadout = int.MinValue;
            ReleaseReadout = "--";
            ReleaseUnit = "";
        }

        public void ResetMatch()
        {
            PeakSpeed = 0f;
            PeakLabel = "PEAK 0";
            DeltaLabel = "STABLE";
            ClearSpeed();
            ClearRelease();
            ReleaseEndedAt = -100f;
            foreach (PlayerCard p in Players) p.Reset();
        }
    }
}
