using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class GuiVisualProbe
{
    private static bool englishCapture;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2) return 2;
        string testDirectory = Path.Combine(Path.GetTempPath(),
            "GameBoostPro-Visual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        Directory.CreateDirectory(args[1]);
        AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", testDirectory);
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Assembly assembly = Assembly.LoadFile(args[0]);
            CaptureMainForm(assembly, args[1]);
            CaptureGameLibrary(assembly, args[1]);
            CaptureAdvancedForm(assembly, args[1]);
            CaptureGraphicsAdvisor(assembly, args[1]);
            CaptureFrameLab(assembly, args[1]);
            assembly.GetType("GameBoostPro.UiText", true).GetField("Language", BindingFlags.Static | BindingFlags.Public).SetValue(null, "EN");
            englishCapture = true;
            CaptureGameLibrary(assembly, args[1]);
            CaptureAdvancedForm(assembly, args[1]);
            CaptureGraphicsAdvisor(assembly, args[1]);
            CaptureFrameLab(assembly, args[1]);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", null);
            try { Directory.Delete(testDirectory, true); }
            catch { }
        }
    }

    private static void CaptureMainForm(Assembly assembly, string outputDirectory)
    {
        Type gameType = assembly.GetType("GameBoostPro.GameInstall", true);
        Type detectorType = assembly.GetType("GameBoostPro.GameDetector", true);
        System.Collections.IList catalog = (System.Collections.IList)detectorType.GetField("Catalog", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        catalog.Clear();
        foreach (string name in new[] { "Counter-Strike 2", "PUBG: BATTLEGROUNDS", "VALORANT" })
        {
            object game = Activator.CreateInstance(gameType, true);
            Set(game, "DisplayName", name);
            Set(game, "DirectoryPath", @"C:\Games\Fixture" + catalog.Count);
            Set(game, "Source", name == "VALORANT" ? "RIOT" : "STEAM");
            Set(game, "LaunchTarget", "steam://rungameid/730");
            Set(game, "LaunchArguments", "");
            catalog.Add(game);
        }
        detectorType.GetField("catalogLoaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        Type formType = assembly.GetType("GameBoostPro.MainForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType, true))
        {
            BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            object config = formType.GetField("config", fields).GetValue(form);
            config.GetType().GetProperty("AutoMode").SetValue(config, false, null);
            Type detector = assembly.GetType("GameBoostPro.PlatformDetector", true);
            MethodInfo evaluate = detector.GetMethod("Evaluate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object platform = evaluate.Invoke(null, new object[] { "Acer", "Nitro ANV15-51", true, true });
            formType.GetField("platform", fields).SetValue(form, platform);
            Label platformLabel = (Label)formType.GetField("platformLabel", fields).GetValue(form);
            platformLabel.Text = "ACER + NITROSENSE";
            platformLabel.ForeColor = Color.FromArgb(92, 207, 219);
            object autoSwitch = formType.GetField("autoSwitch", fields).GetValue(form);
            autoSwitch.GetType().GetProperty("Value").SetValue(autoSwitch, false, null);
            formType.GetField("isAdmin", fields).SetValue(form, true);
            ListBox list = (ListBox)formType.GetField("gameList", fields).GetValue(form);
            list.SelectedIndex = 0;
            System.Collections.IDictionary initialOverrides = (System.Collections.IDictionary)config.GetType().GetProperty("GamePresets").GetValue(config, null);
            initialOverrides[@"dir:c:\games\fixture1"] = "Performance";
            formType.GetMethod("UpdateStateVisuals", fields).Invoke(form, null);
            ValidateUsability(form, fields);
            Capture(form, Path.Combine(outputDirectory, "main.png"));
            Capture(form, Path.Combine(outputDirectory, "main-frame-entry.png"), delegate
            {
                formType.GetMethod("OpenFrameLab", fields).Invoke(form, new object[] { null, EventArgs.Empty });
                Form frames = (Form)formType.GetField("frameLab", fields).GetValue(form);
                if (!frames.Visible || !form.Enabled) throw new Exception("Frame Lab must open without blocking dashboard Boost / Restore");
                frames.Close();
            });
            list.SelectedIndex = 1;
            Capture(form, Path.Combine(outputDirectory, "main-override.png"));
            list.SelectedIndex = 0;
            ValidateProfileUi(assembly, form, fields, outputDirectory);
            CheckLayout(form);
            ValidateManualRestorePause(assembly, form, fields);

            form.ClientSize = new Size(680, 718);
            Capture(form, Path.Combine(outputDirectory, "main-narrow.png"), delegate
            {
                if (form.ClientSize.Width != 680) throw new Exception("Compact window must not be clamped");
                Control content = (Control)formType.GetField("dashboardContent", fields).GetValue(form);
                if (content.Width > form.ClientSize.Width) throw new Exception("Compact content overflows horizontally");
                Control boost = (Control)formType.GetField("dial", fields).GetValue(form);
                if (boost.PointToScreen(Point.Empty).Y >= list.PointToScreen(Point.Empty).Y)
                    throw new Exception("Compact layout must place Boost before the game list");
            });
            CheckLayout(form);
            Capture(form, Path.Combine(outputDirectory, "main-narrow-bottom.png"), delegate
            {
                ((ScrollableControl)form.Controls[0]).ScrollControlIntoView((Control)formType.GetField("telemetryCheck", fields).GetValue(form));
            });
            form.ClientSize = new Size(1100, 780);
            Capture(form, Path.Combine(outputDirectory, "main-wide.png"));
            CheckLayout(form);

            form.ClientSize = new Size(900, 718);
            ScaleUi(form, 1.5f);
            Capture(form, Path.Combine(outputDirectory, "main-scale150.png"));
            CheckLayout(form);
            ScaleUi(form, 4f / 3);
            Capture(form, Path.Combine(outputDirectory, "main-scale200.png"));
            CheckLayout(form);
            Capture(form, Path.Combine(outputDirectory, "main-scale200-bottom.png"), delegate
            {
                Control footerControl = (Control)formType.GetField("telemetryCheck", fields).GetValue(form);
                footerControl.Focus();
                ((ScrollableControl)form.Controls[0]).ScrollControlIntoView(footerControl);
                if (((ScrollableControl)form.Controls[0]).AutoScrollPosition.Y >= -100)
                    throw new Exception("Scaled footer must be reachable by scrolling");
            });

            formType.GetField("isAdmin", fields).SetValue(form, false);
            formType.GetMethod("UpdateStateVisuals", fields).Invoke(form, null);
            Control dial = (Control)formType.GetField("dial", fields).GetValue(form);
            if (dial.Enabled) throw new Exception("Non-admin Boost must be disabled");
            formType.GetField("isAdmin", fields).SetValue(form, true);
            formType.GetMethod("UpdateStateVisuals", fields).Invoke(form, null);
            if (!dial.Enabled) throw new Exception("Supported admin Boost must be enabled");
            Control admin = (Control)formType.GetField("adminStatus", fields).GetValue(form);
            if (admin is Button || admin.TabStop) throw new Exception("Admin indicator must not be a button or keyboard stop");

            CheckBox telemetry = (CheckBox)formType.GetField("telemetryCheck", fields).GetValue(form);
            telemetry.Checked = false;
            if ((bool)config.GetType().GetProperty("ShowTelemetry").GetValue(config, null))
                throw new Exception("Telemetry switch must update config");
            Control cpu = (Control)formType.GetField("cpuBar", fields).GetValue(form);
            if (!Single.IsNaN((float)cpu.GetType().GetProperty("Value").GetValue(cpu, null)))
                throw new Exception("Disabled telemetry must clear stale data");
        }
    }

    private static void ValidateUsability(Form form, BindingFlags flags)
    {
        System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();
        Type type = form.GetType();
        Control admin = (Control)type.GetField("adminStatus", flags).GetValue(form);
        if (admin is Button || admin.TabStop) failures.Add("Elevated Admin must be a non-interactive status label");
        Control games = (Control)type.GetField("gameList", flags).GetValue(form);
        form.ClientSize = new Size(1040, 720);
        form.PerformLayout();
        int normalHeight = games.Height;
        form.ClientSize = new Size(1040, 960);
        form.PerformLayout();
        if (games.Height < normalHeight + 100) failures.Add("Game workspace must grow with window height");
        form.ClientSize = new Size(680, 718);
        form.PerformLayout();
        if (form.ClientSize.Width != 680) failures.Add("Compact width must not be clamped to the old desktop minimum");
        if (type.GetField("frameLabButton", flags) == null) failures.Add("Frame Lab needs a direct dashboard entry");
        form.ClientSize = new Size(1040, 720);
        if (failures.Count > 0) throw new Exception(String.Join("\n", failures.ToArray()));
    }

    private static void ValidateProfileUi(Assembly assembly, Form form, BindingFlags flags, string directory)
    {
        Type type = form.GetType();
        object config = type.GetField("config", flags).GetValue(form);
        Control master = (Control)type.GetField("masterSlider", flags).GetValue(form);
        Control selected = (Control)type.GetField("gameSlider", flags).GetValue(form);
        Type slider = selected.GetType();
        System.Collections.IDictionary overrides = (System.Collections.IDictionary)config.GetType().GetProperty("GamePresets").GetValue(config, null);
        overrides.Clear();
        type.GetMethod("RefreshPresetControls", flags).Invoke(form, null);
        CheckBox individual = (CheckBox)type.GetField("overrideCheck", flags).GetValue(form);
        if (individual.Checked || selected.Enabled) throw new Exception("Master inheritance must be unchecked and read-only");
        individual.Checked = true;
        if (!selected.Enabled) throw new Exception("Override checkbox enables the game slider");
        slider.GetProperty("Value").SetValue(selected, 2, null);
        if (overrides.Count != 1) throw new Exception("Per-game slider must save one override");
        MethodInfo key = slider.GetMethod("OnKeyDown", flags);
        key.Invoke(master, new object[] { new KeyEventArgs(Keys.Home) });
        if (overrides.Count != 1 || (int)slider.GetProperty("Value").GetValue(selected, null) != 2)
            throw new Exception("Changing Master must preserve Override profiles");
        individual.Checked = false;
        if (overrides.Count != 0 || selected.Enabled || (int)slider.GetProperty("Value").GetValue(selected, null) != 0)
            throw new Exception("Unchecking Override restores live Master inheritance");
        key.Invoke(master, new object[] { new KeyEventArgs(Keys.End) });
        individual.Checked = true;
        slider.GetProperty("Value").SetValue(selected, 0, null);
        key.Invoke(master, new object[] { new KeyEventArgs(Keys.End) });
        if (overrides.Count != 1 || (int)slider.GetProperty("Value").GetValue(selected, null) != 0)
            throw new Exception("Reselecting Master must not erase Overrides");
        type.GetMethod("ApplyMasterToAll", flags).Invoke(form, null);
        if (overrides.Count != 0 || individual.Checked || selected.Enabled || (int)slider.GetProperty("Value").GetValue(selected, null) != 2)
            throw new Exception("Explicit apply-to-all resets every game to Master");
        Button language = (Button)type.GetField("languageButton", flags).GetValue(form);
        Capture(form, Path.Combine(directory, "main-en.png"), delegate
        {
            language.PerformClick();
            CheckEnglishText(form);
        });
        if (Convert.ToString(config.GetType().GetProperty("Language").GetValue(config, null)) != "EN")
            throw new Exception("Language switch must persist EN");
        Type stateType = assembly.GetType("GameBoostPro.BoostState", true);
        object state = Activator.CreateInstance(stateType, true);
        Set(state, "Preset", "Performance");
        Set(state, "PowerPlanMode", "KeepCurrent");
        Set(state, "TargetPowerName", "Nezha");
        Set(state, "PreviousPowerGuid", "e9a42b02-d5df-448d-aa00-03f14749eb61");
        Set(state, "TargetPowerGuid", "e9a42b02-d5df-448d-aa00-03f14749eb61");
        Set(state, "ProcessTuningStatus", "Applied");
        Set(state, "UseAboveNormalPriority", true);
        Type storage = assembly.GetType("GameBoostPro.Storage", true);
        BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        storage.GetMethod("SaveState", statics).Invoke(null, new object[] { state });
        Capture(form, Path.Combine(directory, "main-active.png"), delegate
        {
            language.PerformClick();
            slider.GetProperty("Value").SetValue(master, 0, null);
            type.GetMethod("UpdateStateVisuals", flags).Invoke(form, null);
        });
        object saved = storage.GetMethod("LoadState", statics).Invoke(null, null);
        if (Convert.ToString(stateType.GetProperty("Preset").GetValue(saved, null)) != "Performance" ||
            !(bool)stateType.GetProperty("UseAboveNormalPriority").GetValue(saved, null))
            throw new Exception("Changing profiles must not mutate active recovery state");
        if (!((Control)type.GetField("advancedButton", flags).GetValue(form)).Enabled)
            throw new Exception("Advanced next-session settings must remain available during Boost");
        storage.GetMethod("DeleteState", statics).Invoke(null, null);
        type.GetMethod("UpdateStateVisuals", flags).Invoke(form, null);
        Control boost = (Control)type.GetField("dial", flags).GetValue(form);
        boost.GetType().GetProperty("Busy").SetValue(boost, true, null);
        Capture(form, Path.Combine(directory, "main-busy.png"));
        boost.GetType().GetProperty("Busy").SetValue(boost, false, null);
        TextBox search = (TextBox)type.GetField("gameSearch", flags).GetValue(form);
        search.Text = "no matching title";
        Capture(form, Path.Combine(directory, "main-search-empty.png"));
        search.Text = "";
        slider.GetProperty("Value").SetValue(master, 1, null);
    }

    private static void CheckEnglishText(Control parent)
    {
        foreach (char c in parent.Text)
            if (c >= '\u0e00' && c <= '\u0e7f') throw new Exception("Untranslated English UI: " + parent.Text);
        foreach (Control child in parent.Controls) CheckEnglishText(child);
    }

    private static void CheckLayout(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            if (!(parent is ScrollableControl && ((ScrollableControl)parent).AutoScroll))
            {
                Rectangle allowed = parent.ClientRectangle;
                allowed.Inflate(2, 2);
                if (!allowed.Contains(child.Bounds))
                    throw new Exception("Control outside parent: " + child.GetType().Name + " " + child.Text);
            }
            CheckLayout(child);
        }
        if (parent is TableLayoutPanel)
            for (int i = 0; i < parent.Controls.Count; i++)
                for (int j = i + 1; j < parent.Controls.Count; j++)
                    if (parent.Controls[i].Visible && parent.Controls[j].Visible &&
                        parent.Controls[i].Bounds.IntersectsWith(parent.Controls[j].Bounds))
                        throw new Exception("Overlapping layout controls");
    }

    private static void ValidateManualRestorePause(Assembly assembly, Form form, BindingFlags flags)
    {
        Type formType = form.GetType();
        object config = formType.GetField("config", flags).GetValue(form);
        config.GetType().GetProperty("AutoMode").SetValue(config, true, null);
        formType.GetField("autoBoostPausedUntilExit", flags).SetValue(form, true);
        object snapshot = Activator.CreateInstance(assembly.GetType("GameBoostPro.MonitorSnapshot", true), true);
        object game = Activator.CreateInstance(assembly.GetType("GameBoostPro.DetectedGame", true), true);
        Set(game, "DisplayName", "Fixture game");
        Set(game, "Source", "TEST");
        Set(game, "ExePath", "");
        Set(game, "Process", System.Diagnostics.Process.GetCurrentProcess());
        Set(snapshot, "Game", game);
        MethodInfo apply = formType.GetMethod("ApplyMonitorSnapshot", flags);
        apply.Invoke(form, new object[] { snapshot });
        if ((bool)formType.GetField("working", flags).GetValue(form))
            throw new Exception("Manual restore must not re-boost the same running game");
        Set(snapshot, "Game", null);
        apply.Invoke(form, new object[] { snapshot });
        if (!(bool)formType.GetField("autoBoostPausedUntilExit", flags).GetValue(form))
            throw new Exception("One missing scan must not clear the pause");
        apply.Invoke(form, new object[] { snapshot });
        if ((bool)formType.GetField("autoBoostPausedUntilExit", flags).GetValue(form))
            throw new Exception("Confirmed game exit must clear the pause");
        config.GetType().GetProperty("AutoMode").SetValue(config, false, null);
    }

    private static void ScaleUi(Form form, float scale)
    {
        System.Collections.Generic.List<Control> controls = new System.Collections.Generic.List<Control>();
        CollectExplicitFonts(form, controls);
        form.Scale(new SizeF(scale, scale));
        foreach (Control control in controls) control.Font = new Font(control.Font.FontFamily,
            control.Font.SizeInPoints * scale, control.Font.Style);
    }

    private static void CollectExplicitFonts(Control parent, System.Collections.Generic.List<Control> controls)
    {
        if (System.ComponentModel.TypeDescriptor.GetProperties(parent)["Font"].ShouldSerializeValue(parent))
            controls.Add(parent);
        foreach (Control child in parent.Controls) CollectExplicitFonts(child, controls);
    }

    private static void CaptureGameLibrary(Assembly assembly, string outputDirectory)
    {
        Type gameType = assembly.GetType("GameBoostPro.GameInstall", true);
        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(gameType);
        object games = Activator.CreateInstance(listType);
        foreach (string name in new[] { "Counter-Strike 2", "PUBG: BATTLEGROUNDS", "VALORANT" })
        {
            object game = Activator.CreateInstance(gameType, true);
            Set(game, "DisplayName", name);
            Set(game, "DirectoryPath", @"C:\Games\" + name);
            Set(game, "Source", name == "VALORANT" ? "RIOT" : "STEAM");
            Set(game, "LaunchTarget", "");
            listType.GetMethod("Add").Invoke(games, new object[] { game });
        }
        Type formType = assembly.GetType("GameBoostPro.GameLibraryForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { games, "Counter-Strike 2" }, null))
        {
            Capture(form, Path.Combine(outputDirectory, "library.png"));
            form.ClientSize = new Size(650, 520);
            Capture(form, Path.Combine(outputDirectory, "library-narrow.png"));
        }
    }

    private static void CaptureAdvancedForm(Assembly assembly, string outputDirectory)
    {
        Type configType = assembly.GetType("GameBoostPro.AppConfig", true);
        object config = Activator.CreateInstance(configType, true);
        Type formType = assembly.GetType("GameBoostPro.AdvancedSettingsForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { config }, null))
        {
            Capture(form, Path.Combine(outputDirectory, "advanced.png"));
            form.ClientSize = new Size(650, 520);
            Capture(form, Path.Combine(outputDirectory, "advanced-narrow.png"));
        }
    }

    private static void CaptureGraphicsAdvisor(Assembly assembly, string outputDirectory)
    {
        Type formType = assembly.GetType("GameBoostPro.GraphicsAdvisorForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { "VALORANT", "", @"C:\Riot Games\VALORANT", Activator.CreateInstance(assembly.GetType("GameBoostPro.AppConfig", true), true) }, null))
        {
            Type capabilitiesType = assembly.GetType("GameBoostPro.GraphicsCapabilities", true);
            object capabilities = Activator.CreateInstance(capabilitiesType, true);
            Set(capabilities, "GpuName", "NVIDIA GeForce RTX 4050 Laptop GPU");
            Set(capabilities, "IsNvidia", true);
            Set(capabilities, "IsRtx", true);
            Set(capabilities, "RtxSeries", 40);
            Set(capabilities, "SupportsDlssSuperResolution", true);
            Set(capabilities, "SupportsFrameGeneration", true);
            Set(capabilities, "SupportsMultiFrameGeneration", false);
            Set(capabilities, "SupportsSmoothMotion", true);

            Type snapshotType = assembly.GetType("GameBoostPro.GraphicsAdvisorSnapshot", true);
            object snapshot = Activator.CreateInstance(snapshotType, true);
            Set(snapshot, "Capabilities", capabilities);
            Set(snapshot, "DriverVersion", "32.0.15.1664");
            Set(snapshot, "DisplayRoute", "Inactive");
            Set(snapshot, "NisEligibility", "RouteBlocked");
            Set(snapshot, "HasHybridGraphics", true);
            Set(snapshot, "HasNvidiaApp", true);
            Set(snapshot, "NvidiaAppVersion", "11.0.9.251");
            Set(snapshot, "GameName", "VALORANT");
            Set(snapshot, "GamePath", "");
            Set(snapshot, "HasDlssLibraryHint", false);
            Set(snapshot, "HasFrameGenerationLibraryHint", false);
            Set(snapshot, "IsCompetitiveGame", true);
            MethodInfo apply = formType.GetMethod("ApplySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Capture(form, Path.Combine(outputDirectory, "graphics-advisor.png"), delegate
            {
                apply.Invoke(form, new object[] { snapshot });
                Type destinations = assembly.GetType("GameBoostPro.GraphicsDestinations", true);
                object found = destinations.GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { new[] { @"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe" }, new string[0], new string[0], new Func<string, bool>(delegate { return true; }) });
                formType.GetMethod("ApplyDestinations", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new[] { found });
            });
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            CheckBox prefer = (CheckBox)formType.GetField("preferGpu", flags).GetValue(form);
            object settings = formType.GetField("settings", flags).GetValue(form);
            bool before = (bool)settings.GetType().GetProperty("PreferHighPerformanceGpu").GetValue(settings, null);
            prefer.Checked = !before;
            if ((bool)settings.GetType().GetProperty("PreferHighPerformanceGpu").GetValue(settings, null) != before)
                throw new Exception("Graphics changes must not save before explicit Save");
            formType.GetMethod("SaveSettings", flags).Invoke(form, null);
            if ((bool)settings.GetType().GetProperty("PreferHighPerformanceGpu").GetValue(settings, null) == before)
                throw new Exception("Graphics Save must persist a real Boost setting");
            form.ClientSize = new Size(650, 550);
            Capture(form, Path.Combine(outputDirectory, "graphics-narrow.png"));
            Control tabs = FindControl(form, "WorkspaceTabs");
            tabs.GetType().GetMethod("SelectPage").Invoke(tabs, new object[] { 1 });
            Capture(form, Path.Combine(outputDirectory, "graphics-compatibility.png"));
        }
    }

    private static void CaptureFrameLab(Assembly assembly, string outputDirectory)
    {
        Type formType = assembly.GetType("GameBoostPro.FrameBenchmarkForm", true);
        using (System.Diagnostics.Process current = System.Diagnostics.Process.GetCurrentProcess())
        using (Form form = (Form)Activator.CreateInstance(formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { "VALORANT", current.Id, current.ProcessName,
                current.StartTime.ToUniversalTime().Ticks }, null))
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            formType.GetField("captureToolReady", flags).SetValue(form, true);
            formType.GetMethod("RenderHistory", flags).Invoke(form, null);
            Capture(form, Path.Combine(outputDirectory, "frame-lab.png"));
            object baseline = formType.GetField("baselinePane", flags).GetValue(form);
            object boosted = formType.GetField("boostedPane", flags).GetValue(form);
            Button baselineCapture = (Button)baseline.GetType().GetProperty("Capture").GetValue(baseline, null);
            Button boostedCapture = (Button)boosted.GetType().GetProperty("Capture").GetValue(boosted, null);
            if (!baselineCapture.Enabled || boostedCapture.Enabled) throw new Exception("Normal mode must offer Baseline only");
            Type storage = assembly.GetType("GameBoostPro.Storage", true);
            Type stateType = assembly.GetType("GameBoostPro.BoostState", true);
            object state = Activator.CreateInstance(stateType, true);
            BindingFlags statics = BindingFlags.Public | BindingFlags.Static;
            storage.GetMethod("SaveState", statics).Invoke(null, new[] { state });
            formType.GetMethod("RenderHistory", flags).Invoke(form, null);
            if (baselineCapture.Enabled || !boostedCapture.Enabled) throw new Exception("Boost mode must offer Boosted only");
            Capture(form, Path.Combine(outputDirectory, "frame-boosted.png"));
            storage.GetMethod("DeleteState", statics).Invoke(null, null);
            formType.GetMethod("SetTarget").Invoke(form, new object[] { "", 0, "", 0L });
            if (baselineCapture.Enabled || boostedCapture.Enabled) throw new Exception("No game must disable both capture actions");
            form.ClientSize = new Size(650, 540);
            Capture(form, Path.Combine(outputDirectory, "frame-empty.png"));
        }
    }

    private static Control FindControl(Control parent, string typeName)
    {
        if (parent.GetType().Name == typeName) return parent;
        foreach (Control child in parent.Controls)
        {
            Control found = FindControl(child, typeName);
            if (found != null) return found;
        }
        return null;
    }

    private static void Set(object target, string property, object value)
    {
        target.GetType().GetProperty(property).SetValue(target, value, null);
    }

    private static void Capture(Form form, string outputPath)
    {
        Capture(form, outputPath, null);
    }

    private static void Capture(Form form, string outputPath, Action afterShow)
    {
        form.Show();
        Application.DoEvents();
        if (afterShow != null) afterShow();
        Application.DoEvents();
        form.PerformLayout();
        CheckLayout(form);
        if (englishCapture)
        {
            CheckEnglishText(form);
            outputPath = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + "-en.png");
        }
        if (form.GetType().Name == "MainForm")
        {
            CheckLayout(form);
            foreach (string field in new[] { "masterLabel", "libraryHeading", "stateText", "autoLabel" })
            {
                Control c = (Control)form.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Size textSize = TextRenderer.MeasureText(c.Text, c.Font);
                if (textSize.Height > c.Height || textSize.Width > c.Width)
                    throw new Exception("Critical label clipped: " + field);
            }
        }
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(outputPath, ImageFormat.Png);
        }
        form.Hide();
    }
}
