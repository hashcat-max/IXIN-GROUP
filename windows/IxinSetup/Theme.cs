using System.Drawing;
using System.Windows.Forms;

namespace IxinSetup
{
    /// <summary>Couleurs et contrôles aux couleurs d'IXIN (thème sombre, accent émeraude).</summary>
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(11, 12, 16);
        public static readonly Color Card = Color.FromArgb(17, 19, 24);
        public static readonly Color Border = Color.FromArgb(35, 39, 47);
        public static readonly Color Text = Color.FromArgb(233, 236, 239);
        public static readonly Color Muted = Color.FromArgb(141, 146, 153);
        public static readonly Color Accent = Color.FromArgb(25, 243, 164);
        public static readonly Color Blue = Color.FromArgb(28, 107, 255);
        public static readonly Color Warning = Color.FromArgb(245, 166, 35);
        public static readonly Color Danger = Color.FromArgb(255, 92, 92);

        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font Small = new Font("Segoe UI", 9f);
        public static readonly Font Bold = new Font("Segoe UI Semibold", 10f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 16f);
        public static readonly Font Subtitle = new Font("Segoe UI Semibold", 12f);
        public static readonly Font Mono = new Font("Consolas", 11f);

        public static Label Label(string text, Font font = null, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Font = font ?? Body,
                ForeColor = color ?? Text,
                AutoSize = true,
                MaximumSize = new Size(660, 0),
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.Transparent,
            };
        }

        public static Button Button(string text, bool primary = false)
        {
            var b = new Button
            {
                Text = text,
                Font = primary ? Bold : Body,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 6),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Card,
                ForeColor = primary ? Color.FromArgb(5, 5, 5) : Text,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0),
            };
            b.FlatAppearance.BorderColor = primary ? Accent : Border;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(70, 248, 186) : Color.FromArgb(26, 30, 38);
            return b;
        }

        public static FlowLayoutPanel Column()
        {
            return new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 18, 28, 10),
                BackColor = Background,
            };
        }

        public static string Symbol(CheckLevel level)
        {
            switch (level)
            {
                case CheckLevel.Ok: return "✔";
                case CheckLevel.Info: return "ℹ";
                case CheckLevel.Warning: return "⚠";
                default: return "✖";
            }
        }

        public static Color ColorOf(CheckLevel level)
        {
            switch (level)
            {
                case CheckLevel.Ok: return Accent;
                case CheckLevel.Info: return Blue;
                case CheckLevel.Warning: return Warning;
                default: return Danger;
            }
        }
    }
}
