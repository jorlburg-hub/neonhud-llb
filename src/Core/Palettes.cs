namespace NeonHud.Core
{
    public enum ThemePreset { Mono, Neon, Custom }

    public static class Palettes
    {
        public static void Apply(HudTheme t, ThemePreset preset)
        {
            if (preset == ThemePreset.Neon) Neon(t);
            else if (preset == ThemePreset.Mono) Mono(t);
        }

        public static void Mono(HudTheme t)
        {
            t.Background = H("#0A0A0DE6");
            t.Primary = H("#E4E4EA");
            t.Text = H("#F5F5F7");
            t.Accent = H("#FFFFFF");
            t.Secondary = H("#8B5CF6");
            t.Warn = H("#C147FF");
            t.Hit = H("#A855F7");
            t.Reduced = H("#C4B5FD");
            t.Ghost = H("#7A7A86");
            t.TeamColours[0] = H("#F5F5F7");
            t.TeamColours[1] = H("#8B5CF6");
            t.TeamColours[2] = H("#9A9AA6");
            t.TeamColours[3] = H("#C4B5FD");
            t.TeamColours[4] = H("#F5F5F7");
        }

        public static void Neon(HudTheme t)
        {
            t.Background = H("#07070DE0");
            t.Primary = H("#00F0FF");
            t.Text = H("#E8FBFF");
            t.Accent = H("#FCEE0A");
            t.Secondary = H("#FF2A6D");
            t.Warn = H("#FF003C");
            t.Hit = H("#FCEE0A");
            t.Reduced = H("#B026FF");
            t.Ghost = H("#FFFFFF");
            t.TeamColours[0] = H("#FF3B4E");
            t.TeamColours[1] = H("#2FA8FF");
            t.TeamColours[2] = H("#FCEE0A");
            t.TeamColours[3] = H("#39FF5A");
            t.TeamColours[4] = H("#E8FBFF");
        }

        private static Col H(string hex) { return Col.FromHex(hex, Col.White); }
    }
}
