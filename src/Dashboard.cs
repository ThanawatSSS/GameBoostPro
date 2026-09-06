using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace GameBoostPro
{
    internal static class UiText
    {
        public static string Language = "TH";
        public static string T(string th, string en) { return Language == "EN" ? en : th; }
        public static string Preset(string preset)
        {
            return preset == BoostProfiles.Light ? T("เบา", "Light") : preset == BoostProfiles.Performance
                ? T("ประสิทธิภาพ", "Performance") : T("สมดุล", "Balanced");
        }
        public static Font Body(float size, FontStyle style) { return new Font("Leelawadee UI", size, style); }
    }

    internal static class ProfileColors
    {
        public static readonly Color Master = Palette.Cyan;
        public static readonly Color Override = Palette.Amber;
        public static readonly Color MasterSurface = Color.FromArgb(22, 39, 43);
        public static readonly Color OverrideSurface = Color.FromArgb(43, 35, 23);
        public static readonly Color Track = Color.FromArgb(108, 120, 114);
    }

    internal sealed class PresetSlider : Control
    {
        private int value = 1;
        private bool dragging;
        private int dragStartValue;
        private readonly Pen track = new Pen(ProfileColors.Track, 4);
        private readonly Pen selectedTrack = new Pen(Palette.Lime, 4);
        private readonly SolidBrush knob = new SolidBrush(Palette.Lime);
        public event EventHandler ValueChanged;
        public event EventHandler ValueReselected;
        public Color AccentColor = Palette.Lime;
        public int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(0, Math.Min(2, value));
                if (this.value == next) return;
                this.value = next;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
        public string Preset
        {
            get { return Value == 0 ? BoostProfiles.Light : Value == 2 ? BoostProfiles.Performance : BoostProfiles.Balanced; }
            set { Value = value == BoostProfiles.Light ? 0 : value == BoostProfiles.Performance ? 2 : 1; }
        }
        public PresetSlider()
        {
            DoubleBuffered = true;
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(430, 70);
            Font = UiText.Body(10, FontStyle.Regular);
            AccessibleRole = AccessibleRole.Slider;
            BackColor = Palette.Back;
        }
        private float Inset { get { return Width / 6f; } }
        private void Pick(int x) { Value = (int)Math.Round((x - Inset) * 2 / Math.Max(1, Width - 2 * Inset)); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            Focus(); dragStartValue = Value; dragging = true; Capture = true; Pick(e.X);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) Pick(e.X); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging && Value == dragStartValue && ValueReselected != null) ValueReselected(this, EventArgs.Empty);
            dragging = false; Capture = false; base.OnMouseUp(e);
        }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Home || key == Keys.End || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled)
            {
                int previous = Value;
                if (e.KeyCode == Keys.Left) Value--;
                else if (e.KeyCode == Keys.Right) Value++;
                else if (e.KeyCode == Keys.Home) Value = 0;
                else if (e.KeyCode == Keys.End) Value = 2;
                else { base.OnKeyDown(e); return; }
                if (previous == Value && ValueReselected != null) ValueReselected(this, EventArgs.Empty);
                e.Handled = e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Font.SizeInPoints / 10f;
            float y = Height - 18 * scale;
            float step = (Width - 2 * Inset) / 2;
            track.Width = selectedTrack.Width = 3 * scale;
            selectedTrack.Color = AccentColor;
            g.DrawLine(track, Inset, y, Width - Inset, y);
            g.DrawLine(selectedTrack, Inset, y, Inset + Value * step, y);
            for (int i = 0; i < 3; i++)
            {
                float x = Inset + i * step;
                knob.Color = i <= Value ? AccentColor : ProfileColors.Track;
                float r = (i == Value ? 7 : 3) * scale;
                g.FillEllipse(knob, x - r, y - r, r * 2, r * 2);
                string label = UiText.Preset(i == 0 ? BoostProfiles.Light : i == 2 ? BoostProfiles.Performance : BoostProfiles.Balanced);
                int w = (int)(Width / 3f);
                Rectangle rect = new Rectangle(i * w, 0, i == 2 ? Width - 2 * w : w, (int)(y - 9 * scale));
                TextRenderer.DrawText(g, label, Font, rect, i == Value ? AccentColor : Palette.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -2, -2), Palette.Text, BackColor);
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new SliderAccessibility(this); }
        private sealed class SliderAccessibility : ControlAccessibleObject
        {
            private readonly PresetSlider slider;
            public SliderAccessibility(PresetSlider owner) : base(owner) { slider = owner; }
            public override string Value { get { return UiText.Preset(slider.Preset); } }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { track.Dispose(); selectedTrack.Dispose(); knob.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class GameList : ListBox
    {
        public AppConfig Config;
        public GameList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 76;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            BackColor = Palette.Surface;
            ForeColor = Palette.Text;
            Font = UiText.Body(10, FontStyle.Regular);
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ItemHeight = Font.Height * 4; }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            GameInstall game = (GameInstall)Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool individual = Config != null && Config.GamePresets.ContainsKey(BoostProfiles.Key(game));
            Color accent = individual ? ProfileColors.Override : ProfileColors.Master;
            Color surface = individual ? ProfileColors.OverrideSurface : ProfileColors.MasterSurface;
            using (SolidBrush brush = new SolidBrush(selected ? surface : BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
            float scale = Font.SizeInPoints / 10f;
            int pad = (int)(12 * scale);
            Rectangle title = new Rectangle(e.Bounds.X + pad, e.Bounds.Y + (int)(7 * scale), e.Bounds.Width - 2 * pad, Font.Height + 4);
            TextRenderer.DrawText(e.Graphics, game.DisplayName, Font, title, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            string level = Config == null ? "" : UiText.Preset(BoostProfiles.Get(Config, BoostProfiles.Key(game)));
            title.Y += Font.Height + (int)(4 * scale);
            TextRenderer.DrawText(e.Graphics, (individual ? "OVERRIDE" : "MASTER") + " / " + level, Font, title, accent,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            title.Y += Font.Height + (int)(2 * scale);
            TextRenderer.DrawText(e.Graphics, game.Source ?? "", Font, title, Palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (selected)
                using (SolidBrush brush = new SolidBrush(accent)) e.Graphics.FillRectangle(brush, e.Bounds.X, e.Bounds.Y + pad, 3 * scale, e.Bounds.Height - 2 * pad);
            e.DrawFocusRectangle();
        }
    }

    internal sealed class DashboardButton : Button
    {
        private bool hover;
        public string Glyph;
        private Font glyphFont;
        private readonly SolidBrush background = new SolidBrush(Palette.SurfaceHigh);
        private readonly Pen border = new Pen(Palette.Line);
        protected override void OnFontChanged(EventArgs e)
        {
            if (glyphFont != null) glyphFont.Dispose();
            glyphFont = new Font("Segoe MDL2 Assets", Font.SizeInPoints + 1);
            base.OnFontChanged(e);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            background.Color = Enabled && hover ? Palette.SurfaceHigh : BackColor;
            e.Graphics.FillRectangle(background, ClientRectangle);
            if (FlatAppearance.BorderSize > 0)
            {
                border.Color = Enabled ? ForeColor : Palette.Muted;
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }
            Rectangle textRect = ClientRectangle;
            if (!String.IsNullOrEmpty(Glyph) && glyphFont != null)
            {
                int iconWidth = (int)(Font.Height * 1.8f);
                TextRenderer.DrawText(e.Graphics, Glyph, glyphFont, new Rectangle(4, 0, iconWidth, Height), Enabled ? ForeColor : Palette.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                textRect.X += iconWidth; textRect.Width -= iconWidth;
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Enabled ? ForeColor : Palette.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), Palette.Text, BackColor);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { background.Dispose(); border.Dispose(); if (glyphFont != null) glyphFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal sealed partial class MainForm
    {
        private PresetSlider masterSlider;
        private PresetSlider gameSlider;
        private GameList gameList;
        private TextBox gameSearch;
        private Label masterLabel, masterCount, libraryHeading, presetHeading, profileTiming, autoLabel;
        private Button languageButton, applyAllButton, frameLabButton, elevationButton;
        private Panel dashboardViewport;
        private TableLayoutPanel dashboardContent;
        private Action reflowDashboard;
        private CheckBox overrideCheck;
        private TableLayoutPanel profileSurface;
        private Label powerCaption, modeCaption, captureCaption, libraryEmpty;
        private bool refreshingProfiles;
        private List<GameInstall> dashboardGames = new List<GameInstall>();

        private Label DashboardLabel(string text, float size, Color color)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, AutoEllipsis = true, Margin = Padding.Empty,
                Font = UiText.Body(size, FontStyle.Regular), ForeColor = color, TextAlign = ContentAlignment.MiddleLeft };
        }
        private Button DashboardButton(string text, int width, Color back, Color fore)
        {
            Button button = MakeButton(text, 0, 0, width, 38, back, fore);
            button.Font = UiText.Body(10, FontStyle.Regular);
            button.Anchor = AnchorStyles.Right;
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }
        private void BuildDashboard()
        {
            UiText.Language = config.Language;
            Text = "Game Boost Pro " + BuildVersion.Display;
            StartPosition = FormStartPosition.CenterScreen;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1040, 720);
            MinimumSize = new Size(640, 500);
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = UiText.Body(10, FontStyle.Regular);
            DoubleBuffered = true;

            Panel viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            dashboardViewport = viewport;
            TableLayoutPanel content = CreateGrid(1, 5);
            content.Dock = DockStyle.Top;
            dashboardContent = content;
            content.Height = 720;
            content.Padding = new Padding(24, 8, 24, 0);
            foreach (int height in new[] { 70, 108, 392, 78, 40 }) content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            viewport.Controls.Add(content);
            Controls.Add(viewport);

            TableLayoutPanel header = CreateGrid(5, 1);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            TableLayoutPanel brand = CreateGrid(1, 2);
            brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label brandName = DashboardLabel("GAME BOOST PRO", 18, Palette.Text);
            brandName.Font = new Font("Bahnschrift", 18, FontStyle.Bold);
            brand.Controls.Add(brandName, 0, 0);
            platformLabel = DashboardLabel(platform.Title, 9, Palette.Muted);
            brand.Controls.Add(platformLabel, 0, 1);
            header.Controls.Add(brand, 0, 0);
            gpuAdvisorButton = DashboardButton("", 142, Palette.SurfaceHigh, Palette.Text);
            gpuAdvisorButton.Click += OpenGraphicsAdvisor;
            advancedButton = DashboardButton("", 142, Palette.SurfaceHigh, Palette.Text);
            ((DashboardButton)advancedButton).Glyph = "\uE713";
            advancedButton.Click += OpenAdvancedSettings;
            languageButton = DashboardButton("TH / EN", 74, Palette.Back, Palette.Lime);
            languageButton.Click += delegate
            {
                if (frameLab != null && frameLab.IsCapturing) return;
                bool reopenFrames = frameLab != null && frameLab.Visible;
                if (frameLab != null) frameLab.Close();
                config.Language = config.Language == "TH" ? "EN" : "TH";
                UiText.Language = config.Language;
                activityNotice = "";
                activityNoticeUntil = DateTime.MinValue;
                Storage.SaveConfig(config);
                ApplyDashboardLanguage();
                RefreshGameProfile();
                UpdateStateVisuals();
                if (reopenFrames) OpenFrameLab(this, EventArgs.Empty);
            };
            frameLabButton = DashboardButton("", 126, Palette.SurfaceHigh, Palette.Amber);
            frameLabButton.Click += OpenFrameLab;
            ((DashboardButton)frameLabButton).Glyph = "\uE9D9";
            ((DashboardButton)gpuAdvisorButton).Glyph = "\uE7F4";
            header.Controls.Add(gpuAdvisorButton, 1, 0);
            header.Controls.Add(frameLabButton, 2, 0);
            header.Controls.Add(advancedButton, 3, 0);
            header.Controls.Add(languageButton, 4, 0);
            content.Controls.Add(header, 0, 0);

            TableLayoutPanel master = CreateGrid(3, 1);
            master.BackColor = ProfileColors.MasterSurface;
            master.Margin = new Padding(0, 10, 0, 14);
            master.Padding = new Padding(18, 5, 18, 5);
            master.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            master.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            master.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            TableLayoutPanel masterIdentity = CreateGrid(1, 2);
            masterIdentity.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            masterIdentity.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            masterLabel = DashboardLabel("", 12, ProfileColors.Master);
            masterCount = DashboardLabel("", 9, Palette.Text);
            masterIdentity.Controls.Add(masterLabel, 0, 0);
            masterIdentity.Controls.Add(masterCount, 0, 1);
            master.Controls.Add(masterIdentity, 0, 0);
            masterSlider = new PresetSlider { Dock = DockStyle.Fill, BackColor = ProfileColors.MasterSurface,
                AccentColor = ProfileColors.Master, Preset = config.DefaultPreset };
            EventHandler applyMaster = delegate
            {
                if (refreshingProfiles) return;
                config.DefaultPreset = masterSlider.Preset;
                Storage.SaveConfig(config);
                RefreshPresetControls();
                ShowNotice(UiText.T("บันทึก Master แล้ว / เก็บ Override ไว้", "Master saved / Overrides preserved") + NextSessionNotice());
            };
            masterSlider.ValueChanged += applyMaster;
            masterSlider.ValueReselected += applyMaster;
            master.Controls.Add(masterSlider, 1, 0);
            applyAllButton = DashboardButton("", 162, ProfileColors.MasterSurface, ProfileColors.Master);
            applyAllButton.FlatAppearance.BorderSize = 1;
            applyAllButton.Click += delegate
            {
                if (config.GamePresets.Count > 0 && MessageBox.Show(this,
                    UiText.T("ใช้ Master กับทุกเกมและล้าง Override ", "Apply Master to all games and clear ") +
                        config.GamePresets.Count + UiText.T(" โปรไฟล์?", " overrides?"), "Game Boost Pro",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                ApplyMasterToAll();
            };
            master.Controls.Add(applyAllButton, 2, 0);
            content.Controls.Add(master, 0, 1);

            TableLayoutPanel workspace = CreateGrid(2, 1);
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            TableLayoutPanel library = CreateGrid(1, 4);
            library.BackColor = Palette.Surface;
            library.Padding = new Padding(12, 0, 12, 8);
            library.Margin = new Padding(0, 0, 20, 0);
            foreach (int height in new[] { 38, 38 }) library.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            library.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            library.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            libraryHeading = DashboardLabel("", 11, Palette.Text);
            library.Controls.Add(libraryHeading, 0, 0);
            gameSearch = new TextBox { Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Palette.SurfaceHigh, ForeColor = Palette.Text, Font = UiText.Body(10, FontStyle.Regular) };
            gameSearch.TextChanged += delegate { PopulateGameList(); };
            gameSearch.HandleCreated += delegate { SetSearchCue(); };
            library.Controls.Add(gameSearch, 0, 1);
            Panel gamesArea = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            gameList = new GameList { Dock = DockStyle.Fill, Config = config };
            gameList.SelectedIndexChanged += delegate
            {
                if (refreshingProfiles || gameList.SelectedItem == null) return;
                SelectDashboardGame((GameInstall)gameList.SelectedItem);
            };
            gamesArea.Controls.Add(gameList);
            libraryEmpty = DashboardLabel("", 10, Palette.Muted);
            libraryEmpty.TextAlign = ContentAlignment.MiddleCenter;
            gamesArea.Controls.Add(libraryEmpty);
            library.Controls.Add(gamesArea, 0, 2);
            TableLayoutPanel libraryTools = CreateGrid(2, 1);
            libraryTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            libraryTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            libraryButton = DashboardButton("", 138, Palette.SurfaceHigh, Palette.Text);
            libraryButton.Dock = DockStyle.Fill;
            libraryButton.Margin = new Padding(0, 2, 8, 0);
            libraryButton.Click += OpenGameLibrary;
            browseButton = DashboardButton("+", 36, Palette.SurfaceHigh, Palette.Text);
            browseButton.Margin = Padding.Empty;
            browseButton.Click += BrowseGame;
            libraryTools.Controls.Add(libraryButton, 0, 0);
            libraryTools.Controls.Add(browseButton, 1, 0);
            library.Controls.Add(libraryTools, 0, 3);
            workspace.Controls.Add(library, 0, 0);

            TableLayoutPanel session = CreateGrid(1, 5);
            foreach (int height in new[] { 68, 62, 100, 86, 76 }) session.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            TableLayoutPanel action = CreateGrid(2, 1);
            action.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            action.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 186));
            stateText = DashboardLabel("", 15, Palette.Text);
            stateText.Margin = new Padding(0, 0, 12, 0);
            action.Controls.Add(stateText, 0, 0);
            dial = new BoostDial { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 8) };
            dial.BoostClick += delegate { ToggleBoost(false); };
            action.Controls.Add(dial, 1, 0);
            session.Controls.Add(action, 0, 0);

            TableLayoutPanel selected = CreateGrid(2, 1);
            selected.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selected.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            TableLayoutPanel selectedInfo = CreateGrid(1, 2);
            selectedInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            selectedInfo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gameName = DashboardLabel("", 12, Palette.Text);
            gamePath = DashboardLabel("", 9, Palette.Muted);
            selectedInfo.Controls.Add(gameName, 0, 0);
            selectedInfo.Controls.Add(gamePath, 0, 1);
            selected.Controls.Add(selectedInfo, 0, 0);
            launchButton = DashboardButton("", 92, Palette.SurfaceHigh, Palette.Text);
            launchButton.Click += delegate { LaunchGame(); };
            selected.Controls.Add(launchButton, 1, 0);
            session.Controls.Add(selected, 0, 1);

            TableLayoutPanel profile = CreateGrid(1, 2);
            profileSurface = profile;
            profile.Padding = new Padding(10, 0, 10, 0);
            profile.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            profile.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            TableLayoutPanel profileTitle = CreateGrid(3, 1);
            profileTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
            profileTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            profileTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 242));
            presetHeading = DashboardLabel("", 11, ProfileColors.Master);
            profileTiming = DashboardLabel("", 9, Palette.Coral);
            overrideCheck = new CheckBox { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Standard, ForeColor = ProfileColors.Override };
            overrideCheck.CheckedChanged += delegate
            {
                if (refreshingProfiles) return;
                string key = BoostProfiles.SelectedKey(config);
                if (key.Length == 0) return;
                if (overrideCheck.Checked) config.GamePresets[key] = BoostProfiles.Get(config, key);
                else config.GamePresets.Remove(key);
                Storage.SaveConfig(config);
                RefreshPresetControls();
            };
            profileTitle.Controls.Add(presetHeading, 0, 0);
            profileTitle.Controls.Add(profileTiming, 1, 0);
            profileTitle.Controls.Add(overrideCheck, 2, 0);
            profile.Controls.Add(profileTitle, 0, 0);
            gameSlider = new PresetSlider { Dock = DockStyle.Fill };
            EventHandler applyGame = delegate
            {
                string key = BoostProfiles.SelectedKey(config);
                if (refreshingProfiles || key.Length == 0 || !overrideCheck.Checked) return;
                config.GamePresets[key] = gameSlider.Preset;
                Storage.SaveConfig(config);
                RefreshPresetControls();
                ShowNotice(UiText.T("บันทึกโปรไฟล์เกมแล้ว", "Game profile saved") + NextSessionNotice());
            };
            gameSlider.ValueChanged += applyGame;
            gameSlider.ValueReselected += applyGame;
            profile.Controls.Add(gameSlider, 0, 1);
            session.Controls.Add(profile, 0, 2);

            TableLayoutPanel effects = CreateGrid(2, 3);
            effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
            effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
            for (int i = 0; i < 3; i++) effects.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
            powerCaption = DashboardLabel("Power plan", 10, Palette.Muted);
            modeCaption = DashboardLabel("CPU scheduling", 10, Palette.Muted);
            captureCaption = DashboardLabel("Background capture", 10, Palette.Muted);
            powerStatus = DashboardLabel("", 10, Palette.Text);
            modeStatus = DashboardLabel("", 10, Palette.Text);
            captureStatus = DashboardLabel("", 10, Palette.Text);
            effects.Controls.Add(powerCaption, 0, 0); effects.Controls.Add(powerStatus, 1, 0);
            effects.Controls.Add(modeCaption, 0, 1); effects.Controls.Add(modeStatus, 1, 1);
            effects.Controls.Add(captureCaption, 0, 2); effects.Controls.Add(captureStatus, 1, 2);
            session.Controls.Add(effects, 0, 3);

            TableLayoutPanel automation = CreateGrid(3, 2);
            automation.Padding = new Padding(0, 8, 0, 0);
            automation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            automation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            automation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
            automation.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            automation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            autoLabel = DashboardLabel("", 10, Palette.Text);
            autoSwitch = new ToggleSwitch { Anchor = AnchorStyles.Right, Margin = Padding.Empty };
            autoSwitch.ValueChanged += delegate
            {
                config.AutoMode = autoSwitch.Value;
                autoBoostPausedUntilExit = false;
                Storage.SaveConfig(config);
            };
            automation.Controls.Add(autoLabel, 0, 0); automation.Controls.Add(autoSwitch, 1, 0);
            launchCheck = new CheckBox { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, ForeColor = Palette.Muted };
            launchCheck.CheckedChanged += delegate { config.LaunchOnBoost = launchCheck.Checked; Storage.SaveConfig(config); };
            automation.Controls.Add(launchCheck, 0, 1); automation.SetColumnSpan(launchCheck, 3);
            session.Controls.Add(automation, 0, 4);
            workspace.Controls.Add(session, 1, 0);
            content.Controls.Add(workspace, 0, 2);

            TableLayoutPanel metrics = CreateGrid(4, 1);
            metrics.Padding = new Padding(0, 22, 0, 0);
            for (int i = 0; i < 3; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            cpuBar = new MetricBar { Caption = "CPU", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 28, 0) };
            ramBar = new MetricBar { Caption = "RAM", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 28, 0) };
            gpuBar = new MetricBar { Caption = "GPU 3D", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 28, 0) };
            metrics.Controls.Add(cpuBar, 0, 0); metrics.Controls.Add(ramBar, 1, 0); metrics.Controls.Add(gpuBar, 2, 0);
            telemetryCheck = new CheckBox { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
                ForeColor = Palette.Muted, Checked = config.ShowTelemetry };
            telemetryCheck.CheckedChanged += delegate
            {
                config.ShowTelemetry = telemetryCheck.Checked;
                Storage.SaveConfig(config);
                if (!config.ShowTelemetry) ClearMetrics();
            };
            metrics.Controls.Add(telemetryCheck, 3, 0);
            content.Controls.Add(metrics, 0, 3);

            TableLayoutPanel footer = CreateGrid(2, 1);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            activityText = DashboardLabel("", 9, Palette.Muted);
            adminStatus = DashboardLabel("", 9, Palette.Cyan);
            adminStatus.TextAlign = ContentAlignment.MiddleRight;
            adminStatus.TabStop = false;
            elevationButton = DashboardButton("", 140, Palette.SurfaceHigh, Palette.Amber);
            elevationButton.Dock = DockStyle.Fill;
            elevationButton.Click += RestartAsAdmin;
            Panel permissions = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            permissions.Controls.Add(adminStatus);
            permissions.Controls.Add(elevationButton);
            footer.Controls.Add(activityText, 0, 0); footer.Controls.Add(permissions, 1, 0);
            content.Controls.Add(footer, 0, 4);
            ApplyDashboardLanguage();
            ResumeGridLayout(content);
            bool layingOut = false;
            bool? lastCompact = null;
            float lastScale = 0;
            reflowDashboard = delegate
            {
                if (layingOut || IsDisposed) return;
                layingOut = true;
                try
                {
                    float scale;
                    using (Graphics graphics = CreateGraphics())
                        scale = Math.Max(Font.SizeInPoints / 10f, graphics.DpiX / 96f);
                    bool compact = viewport.ClientSize.Width / scale < 900;
                    content.SuspendLayout();
                    if (lastCompact != compact || Math.Abs(lastScale - scale) > .01f)
                    {
                        lastCompact = compact; lastScale = scale;
                        int pad = (int)(20 * scale);
                        content.Padding = new Padding(pad, (int)(8 * scale), pad, 0);
                        SetGridTracks(header, compact ? new float[] { -1, -1, -1, 82 * scale } :
                            new float[] { -1, 134 * scale, 134 * scale, 150 * scale, 82 * scale },
                            compact ? new float[] { 60 * scale, 48 * scale } : new float[] { -1 });
                        header.SetColumnSpan(brand, compact ? 4 : 1);
                        header.SetCellPosition(brand, new TableLayoutPanelCellPosition(0, 0));
                        Control[] commands = { gpuAdvisorButton, frameLabButton, advancedButton, languageButton };
                        for (int i = 0; i < commands.Length; i++)
                        {
                            header.SetCellPosition(commands[i], new TableLayoutPanelCellPosition(compact ? i : i + 1, compact ? 1 : 0));
                            commands[i].Dock = DockStyle.Fill;
                            commands[i].Margin = new Padding((int)(6 * scale), (int)(12 * scale), 0, (int)(12 * scale));
                            if (compact) commands[i].Margin = new Padding((int)(6 * scale), (int)(5 * scale), 0, (int)(5 * scale));
                        }
                        SetGridTracks(master, compact ? new float[] { -1, 180 * scale } :
                            new float[] { 158 * scale, -1, 180 * scale },
                            compact ? new float[] { 56 * scale, 64 * scale } : new float[] { -1 });
                        master.SetCellPosition(masterIdentity, new TableLayoutPanelCellPosition(0, 0));
                        master.SetCellPosition(applyAllButton, new TableLayoutPanelCellPosition(compact ? 1 : 2, 0));
                        master.SetCellPosition(masterSlider, new TableLayoutPanelCellPosition(compact ? 0 : 1, compact ? 1 : 0));
                        master.SetColumnSpan(masterSlider, compact ? 2 : 1);
                        SetGridTracks(workspace, compact ? new float[] { -1 } : new float[] { 238 * scale, -1 },
                            compact ? new float[] { -1, 254 * scale } : new float[] { -1 });
                        workspace.SetCellPosition(library, new TableLayoutPanelCellPosition(0, compact ? 1 : 0));
                        workspace.SetCellPosition(session, new TableLayoutPanelCellPosition(compact ? 0 : 1, 0));
                        library.Margin = compact ? new Padding(0, (int)(14 * scale), 0, 0) : new Padding(0, 0, (int)(20 * scale), 0);
                        // A second source row keeps Override and next-session state readable at every width.
                        SetGridTracks(profileTitle, new float[] { -1, 248 * scale }, new float[] { 32 * scale, 24 * scale });
                        profileTitle.SetCellPosition(presetHeading, new TableLayoutPanelCellPosition(0, 0));
                        profileTitle.SetCellPosition(overrideCheck, new TableLayoutPanelCellPosition(1, 0));
                        profileTitle.SetCellPosition(profileTiming, new TableLayoutPanelCellPosition(0, 1));
                        profileTitle.SetColumnSpan(profileTiming, 2);
                        profile.RowStyles[0].Height = 56 * scale;
                        SetGridTracks(session, new float[] { -1 }, new float[] { 68 * scale, 62 * scale, 118 * scale, -1, 76 * scale });
                        SetGridTracks(metrics, compact ? new float[] { -1, -1, -1 } :
                            new float[] { -1, -1, -1, 136 * scale }, compact ? new float[] { 54 * scale, 36 * scale } : new float[] { -1 });
                        metrics.SetCellPosition(telemetryCheck, new TableLayoutPanelCellPosition(compact ? 0 : 3, compact ? 1 : 0));
                        metrics.SetColumnSpan(telemetryCheck, compact ? 3 : 1);
                        SetGridTracks(content, new float[] { -1 }, new float[] {
                            (compact ? 108 : 70) * scale, (compact ? 154 : 108) * scale,
                            -1, (compact ? 114 : 78) * scale, 40 * scale });
                    }
                    int minimumHeight = (int)((compact ? 1110 : 716) * scale);
                    content.Height = Math.Max(minimumHeight, viewport.ClientSize.Height);
                    content.ResumeLayout(true);
                }
                finally { layingOut = false; }
            };
            viewport.SizeChanged += delegate { reflowDashboard(); };
            FontChanged += delegate { reflowDashboard(); };
            ResumeLayout(true);
            reflowDashboard();
            RefreshDashboardGames();
        }

        private static void SetGridTracks(TableLayoutPanel grid, float[] columns, float[] rows)
        {
            grid.SuspendLayout();
            grid.ColumnCount = columns.Length; grid.RowCount = rows.Length;
            grid.ColumnStyles.Clear(); grid.RowStyles.Clear();
            foreach (float size in columns) grid.ColumnStyles.Add(new ColumnStyle(size < 0 ? SizeType.Percent : SizeType.Absolute, size < 0 ? 100 : size));
            foreach (float size in rows) grid.RowStyles.Add(new RowStyle(size < 0 ? SizeType.Percent : SizeType.Absolute, size < 0 ? 100 : size));
            grid.ResumeLayout(false);
        }

        private string NextSessionNotice()
        {
            return Storage.HasState() || working ? UiText.T(" / ใช้รอบถัดไป", " / next session") : "";
        }
        private void ApplyDashboardLanguage()
        {
            masterLabel.Text = UiText.T("Master / ทุกเกม", "Master / All games");
            libraryHeading.Text = UiText.T("เกมของคุณ", "Your games");
            gpuAdvisorButton.Text = UiText.T("กราฟิก", "Graphics");
            frameLabButton.Text = UiText.T("วัดเฟรม", "Frame Lab");
            advancedButton.Text = UiText.T("ตั้งค่าขั้นสูง", "Advanced");
            libraryButton.Text = UiText.T("จัดการคลังเกม", "Manage library");
            launchButton.Text = UiText.T("เปิดเกม", "Launch");
            autoLabel.Text = UiText.T("Boost อัตโนมัติเมื่อตรวจพบเกม", "Auto Boost on game detection");
            launchCheck.Text = UiText.T("เปิดเกมที่เลือกหลัง Boost", "Launch selected game after Boost");
            telemetryCheck.Text = UiText.T("อ่านค่าเครื่อง", "Live metrics");
            overrideCheck.Text = UiText.T("ตั้งค่าเฉพาะเกม (Override)", "Override for this game");
            applyAllButton.Text = UiText.T("ใช้ Master กับทุกเกม", "Apply Master to all");
            languageButton.Text = config.Language == "TH" ? "TH / en" : "th / EN";
            languageButton.AccessibleName = "Language: " + config.Language;
            if (tray != null)
            {
                tray.ContextMenuStrip.Items[0].Text = UiText.T("เปิด Game Boost Pro", "Open Game Boost Pro");
                tray.ContextMenuStrip.Items[1].Text = UiText.T("สลับ Game Mode", "Toggle Game Mode");
                tray.ContextMenuStrip.Items[3].Text = UiText.T("ออกจากโปรแกรม", "Exit");
            }
            gameSearch.AccessibleName = UiText.T("ค้นหาเกม", "Search games");
            if (gameSearch.IsHandleCreated) SetSearchCue();
            masterSlider.AccessibleName = UiText.T("ระดับ Boost ทุกเกม", "Master Boost level");
            gameSlider.AccessibleName = UiText.T("ระดับ Boost เกมที่เลือก", "Selected game Boost level");
            tips.SetToolTip(masterSlider, UiText.T("เปลี่ยนเฉพาะเกมที่ใช้ Master โดยไม่ล้าง Override", "Update games using Master; preserve Overrides"));
            tips.SetToolTip(overrideCheck, UiText.T("ติ๊กเพื่อกำหนดระดับเฉพาะเกม / เอาติ๊กออกเพื่อกลับไปใช้ Master", "Check for an individual level; uncheck to use Master"));
            tips.SetToolTip(applyAllButton, UiText.T("ใช้ Master กับทุกเกมและล้าง Override หลังยืนยัน", "Use Master for every game and clear Overrides after confirmation"));
            tips.SetToolTip(gameSearch, gameSearch.AccessibleName);
            tips.SetToolTip(browseButton, UiText.T("เพิ่มไฟล์เกม .exe", "Add game executable"));
            tips.SetToolTip(autoSwitch, UiText.T("เปิดเมื่อพบเกม และคืนค่าเมื่อเกมปิด", "Boost on detection; restore after the game exits"));
            tips.SetToolTip(telemetryCheck, UiText.T("พักการอ่าน CPU / RAM / GPU โดยยังตรวจจับเกม", "Pause CPU / RAM / GPU reads; game detection stays active"));
            tips.SetToolTip(cpuBar, UiText.T("การใช้ CPU ทั้งเครื่อง ไม่ใช่อุณหภูมิ", "System CPU usage, not temperature"));
            tips.SetToolTip(ramBar, UiText.T("การใช้ RAM ทั้งเครื่อง", "System RAM usage"));
            tips.SetToolTip(gpuBar, UiText.T("3D engine ที่ใช้งานสูงสุด ไม่ใช่อุณหภูมิ", "Busiest 3D engine, not temperature"));
            tips.SetToolTip(advancedButton, UiText.T("กำหนดค่าที่อนุญาตให้โปรไฟล์ใช้", "Choose which settings profiles may apply"));
            masterSlider.Invalidate(); gameSlider.Invalidate(); dial.Invalidate(); gameList.Invalidate();
            PopulateGameList();
            RefreshPresetControls();
        }

        private void SetSearchCue()
        {
            SendMessage(gameSearch.Handle, 0x1501, new IntPtr(1), UiText.T("ค้นหาเกม", "Search games"));
        }
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr parameter, string text);

        private void RefreshDashboardGames()
        {
            GameDetector.ConfigureManualGames(config.ManualGames);
            dashboardGames = GameDetector.IsCatalogLoaded ? GameDetector.GetCatalog() : new List<GameInstall>();
            foreach (GameInstall manual in config.ManualGames)
                if (manual != null && !dashboardGames.Exists(delegate(GameInstall g) { return BoostProfiles.Key(g) == BoostProfiles.Key(manual); }))
                    dashboardGames.Add(manual);
            if (BoostProfiles.SelectedKey(config).Length > 0 &&
                !dashboardGames.Exists(delegate(GameInstall g) { return BoostProfiles.Key(g) == BoostProfiles.SelectedKey(config); }))
                dashboardGames.Insert(0, new GameInstall { DisplayName = GetConfiguredGameName(),
                    DirectoryPath = config.LibraryGameDirectory, LaunchTarget = !String.IsNullOrWhiteSpace(config.GamePath)
                        ? config.GamePath : config.LibraryLaunchTarget, LaunchArguments = config.LibraryLaunchArguments,
                    Source = !String.IsNullOrWhiteSpace(config.GamePath) ? "MANUAL" : "LAUNCHER" });
            PopulateGameList();
        }

        private void PopulateGameList()
        {
            refreshingProfiles = true;
            gameList.BeginUpdate();
            try
            {
                string query = gameSearch.Text.Trim();
                string selected = BoostProfiles.SelectedKey(config);
                gameList.Items.Clear();
                foreach (GameInstall game in dashboardGames)
                {
                    if ((game.DisplayName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int index = gameList.Items.Add(game);
                    if (BoostProfiles.Key(game) == selected) gameList.SelectedIndex = index;
                }
                libraryEmpty.Text = query.Length > 0 ? UiText.T("ไม่พบเกมที่ค้นหา", "No matching games") :
                    GameDetector.IsCatalogLoaded ? UiText.T("ยังไม่มีเกมในคลัง", "No games in library") : UiText.T("กำลังค้นหาเกม...", "Finding games...");
                libraryEmpty.Visible = gameList.Items.Count == 0;
                if (libraryEmpty.Visible) libraryEmpty.BringToFront();
            }
            finally { gameList.EndUpdate(); refreshingProfiles = false; }
        }

        private void SelectDashboardGame(GameInstall game)
        {
            config.LibraryGameName = game.DisplayName;
            config.LibraryGameDirectory = game.DirectoryPath;
            config.LibraryLaunchTarget = game.LaunchTarget;
            config.LibraryLaunchArguments = game.LaunchArguments;
            config.GamePath = String.Equals(game.Source, "MANUAL", StringComparison.OrdinalIgnoreCase) ? game.LaunchTarget : "";
            Storage.SaveConfig(config);
            RefreshGameProfile();
            RefreshPresetControls();
        }

        private void RememberManualGame()
        {
            if (String.IsNullOrWhiteSpace(config.GamePath)) return;
            GameInstall game = new GameInstall { DisplayName = Path.GetFileNameWithoutExtension(config.GamePath),
                DirectoryPath = Path.GetDirectoryName(config.GamePath), LaunchTarget = config.GamePath, LaunchArguments = "", Source = "MANUAL" };
            if (!config.ManualGames.Exists(delegate(GameInstall g) { return g != null && BoostProfiles.Key(g) == BoostProfiles.Key(game); }))
                config.ManualGames.Add(game);
        }

        private void RefreshPresetControls()
        {
            if (gameSlider == null) return;
            refreshingProfiles = true;
            string key = BoostProfiles.SelectedKey(config);
            bool individual = key.Length > 0 && config.GamePresets.ContainsKey(key);
            try
            {
                masterSlider.Preset = config.DefaultPreset;
                gameSlider.Preset = BoostProfiles.Get(config, key);
                gameSlider.Enabled = key.Length > 0 && individual;
                overrideCheck.Enabled = key.Length > 0;
                overrideCheck.Checked = individual;
                presetHeading.Text = key.Length == 0 ? UiText.T("ยังไม่เลือกเกม", "Select a game") :
                    individual ? UiText.T("ใช้ Override", "Override") : UiText.T("ใช้ Master", "Master");
                presetHeading.ForeColor = individual ? ProfileColors.Override : ProfileColors.Master;
                gameSlider.AccentColor = key.Length == 0 ? Palette.Muted : presetHeading.ForeColor;
                profileSurface.BackColor = individual ? ProfileColors.OverrideSurface : ProfileColors.MasterSurface;
                gameSlider.BackColor = profileSurface.BackColor;
                profileTiming.Text = Storage.HasState() || working ? UiText.T("รอรอบถัดไป", "Next session") : "";
                masterCount.Text = config.GamePresets.Count + UiText.T(" เกมใช้ Override", " game Overrides");
                gameSlider.Invalidate();
                gameList.Invalidate();
            }
            finally { refreshingProfiles = false; }
            RefreshEffectPreview();
        }

        private void ApplyMasterToAll()
        {
            BoostProfiles.SetAll(config, masterSlider.Preset);
            Storage.SaveConfig(config);
            RefreshPresetControls();
            ShowNotice(UiText.T("ทุกเกมกลับมาใช้ Master แล้ว", "All games now use Master") + NextSessionNotice());
        }

        private void RefreshEffectPreview()
        {
            BoostState state = null;
            try { state = Storage.LoadState(); } catch { }
            string key = BoostProfiles.SelectedKey(config);
            AppConfig options = BoostProfiles.Snapshot(config, key, platform);
            bool active = state != null;
            powerCaption.Text = active ? UiText.T("Power plan / ใช้อยู่", "Power plan / active") : UiText.T("Power plan / รอบถัดไป", "Power plan / next");
            powerStatus.Text = active ? state.TargetPowerName : options.PowerPlanMode == PowerPlanPolicy.KeepCurrent
                ? UiText.T("คงแผนเดิม", "Keep current") : PowerPlanPolicy.GetShortLabel(options.PowerPlanMode);
            List<string> scheduling = new List<string>();
            if (options.UseAboveNormalPriority) scheduling.Add("AboveNormal");
            if (options.UseHighQos) scheduling.Add("QoS");
            if (options.UseDynamicPriorityBoost) scheduling.Add("Dynamic boost");
            modeStatus.Text = active ? GetProcessStatusText(state) : scheduling.Count > 0
                ? String.Join(" / ", scheduling.ToArray()) : UiText.T("คงค่าเดิม", "Keep current");
            tips.SetToolTip(modeStatus, active ? state.ProcessTuningDetail : modeStatus.Text);
            bool capture = options.DisableBackgroundCapture;
            if (active) capture = state.Registry != null && state.Registry.Exists(delegate(RegistrySnapshot r) { return r.Name == "AppCaptureEnabled"; });
            captureStatus.Text = capture ? UiText.T("ปิดระหว่าง Boost", "Off during Boost") : UiText.T("คงค่าเดิม", "Keep current");
            powerStatus.ForeColor = Palette.Text;
            modeStatus.ForeColor = active && (state.ProcessTuningStatus == "Blocked" || state.ProcessTuningStatus == "NotRetained" ||
                state.ProcessTuningStatus == "Partial") ? Palette.Amber : active ? Palette.Cyan : Palette.Text;
            captureStatus.ForeColor = capture ? Palette.Amber : Palette.Text;
            tips.SetToolTip(gameSlider, UiText.T("GPU preference: ", "GPU preference: ") +
                (options.PreferHighPerformanceGpu ? "High performance" : "Keep current") +
                UiText.T(" / ไม่ปรับกราฟิกในเกม", " / does not change in-game graphics"));
            if (DateTime.UtcNow >= activityNoticeUntil && !Storage.HasRecoveryWarning && !working)
                activityText.Text = active ? (detectedGame == null ? "" : detectedGame.DisplayName + " / ") +
                    UiText.T("กำลังใช้ ", "Active: ") + (String.IsNullOrEmpty(state.Preset) ? "Legacy" : UiText.Preset(state.Preset))
                    : UiText.T("ไม่ปรับพัดลม  /  ยกเว้น Discord + TS3", "Fans unchanged  /  Discord + TS3 protected");
        }
    }
}
