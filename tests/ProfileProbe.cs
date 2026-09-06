using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using GameBoostPro;

internal static class ProfileProbe
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    [STAThread]
    private static void Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GameBoostPro-Profiles-" + Guid.NewGuid().ToString("N"));
        AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", directory);
        try
        {
            AppConfig config = new AppConfig();
            string appPath = @"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe";
            string panelPath = @"C:\Program Files\NVIDIA Corporation\Control Panel Client\nvcplui.exe";
            string storeId = "NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel";
            GraphicsDestinations desktop = GraphicsDestinations.Resolve(new[] { appPath }, new[] { panelPath }, new[] { storeId }, delegate { return true; });
            Check(desktop.NvidiaApp.Target == appPath && desktop.ControlPanel.Target == panelPath && !desktop.ControlPanel.IsStoreApp,
                "Installed NVIDIA App and full-path desktop panel resolve separately");
            GraphicsDestinations store = GraphicsDestinations.Resolve(new[] { appPath }, new[] { panelPath }, new[] { storeId }, delegate(string p) { return p == appPath; });
            Check(store.NvidiaApp != null && store.ControlPanel.IsStoreApp && store.ControlPanel.Target == storeId,
                "Missing standalone panel falls back to a discovered Store AUMID");
            GraphicsDestinations absent = GraphicsDestinations.Resolve(new[] { "NVIDIA App.exe" }, new[] { "nvcplui.exe" }, new[] { "Unknown!App", storeId + " --argument" }, delegate { return true; });
            Check(absent.NvidiaApp == null && absent.ControlPanel == null, "No bare executables or unverified Store identifiers");
            GraphicsDestinations installed = System.Threading.Tasks.Task.Factory.StartNew<GraphicsDestinations>(GraphicsDestinations.Discover).Result;
            Console.WriteLine("nvidia_app_discovered=" + (installed.NvidiaApp != null) + "; control_panel_discovered=" + (installed.ControlPanel != null));
            PlatformProfile pc = PlatformDetector.Evaluate("Custom", "Desktop", false, false);
            PlatformProfile acer = PlatformDetector.Evaluate("Acer", "Nitro", true, true);
            Check(config.DefaultPreset == "Balanced" && config.Language == "TH", "Safe first-run defaults");
            string first = BoostProfiles.PathKey(@"C:\Games\One", true);
            string second = BoostProfiles.PathKey(@"C:\Games\Two", true);
            config.GamePresets[first] = "Light";
            config.GamePresets[second] = "Performance";
            AppConfig light = BoostProfiles.Snapshot(config, first, pc);
            Check(light.EnableWindowsGameMode && !light.DisableBackgroundCapture && !light.PreferHighPerformanceGpu &&
                !light.UseAboveNormalPriority && !light.UseHighQos && !light.UseDynamicPriorityBoost &&
                light.PowerPlanMode == PowerPlanPolicy.KeepCurrent, "Light exact settings");
            AppConfig balanced = BoostProfiles.Snapshot(config, "", pc);
            Check(balanced.EnableWindowsGameMode && balanced.DisableBackgroundCapture && balanced.PreferHighPerformanceGpu &&
                !balanced.UseAboveNormalPriority && !balanced.UseHighQos && !balanced.UseDynamicPriorityBoost &&
                balanced.PowerPlanMode == PowerPlanPolicy.KeepCurrent, "Balanced exact settings");
            AppConfig performance = BoostProfiles.Snapshot(config, second, pc);
            Check(performance.UseAboveNormalPriority && performance.UseHighQos && performance.UseDynamicPriorityBoost &&
                performance.PowerPlanMode == PowerPlanPolicy.Smart, "Desktop Performance exact settings");
            config.PowerPlanMode = PowerPlanPolicy.Ultimate;
            Check(BoostProfiles.Snapshot(config, second, acer).PowerPlanMode == PowerPlanPolicy.KeepCurrent,
                "Acer preserves current plan even with legacy Ultimate setting");
            config.UseHighQos = config.DisableBackgroundCapture = config.EnableWindowsGameMode = false;
            AppConfig constrained = BoostProfiles.Snapshot(config, second, pc);
            Check(!constrained.UseHighQos && !constrained.DisableBackgroundCapture && !constrained.EnableWindowsGameMode,
                "Advanced opt-outs are respected");
            BoostProfiles.SetAll(config, "Light");
            Check(config.GamePresets.Count == 0 && BoostProfiles.Get(config, second) == "Light", "Master resets all overrides");
            Check(performance.DefaultPreset == "Performance" && performance.UseHighQos && performance.DisableBackgroundCapture,
                "Existing session snapshot remains unchanged");
            config.GamePresets[first] = "Performance";
            Check(BoostProfiles.Get(config, second) == "Light", "Individual edit does not alter other games");
            config.LibraryGameDirectory = @"C:\Games\One";
            config.LibraryGameName = "One";
            Check(BoostProfiles.ResolveKey(config, @"C:\Games\One\bin\game.exe", null) == first, "Nested executable matches installation");
            Check(BoostProfiles.ResolveKey(config, @"C:\Games\OneOther\game.exe", null) != first, "Directory boundary avoids prefix collision");
            config.GamePresets[BoostProfiles.PathKey(@"C:\Games\One\bin\game.exe", false)] = "Balanced";
            Check(BoostProfiles.ResolveKey(config, @"C:\Games\One\bin\game.exe", null).StartsWith("exe:"), "Exact executable override wins");
            Check(BoostProfiles.ResolveKey(config, @"D:\Games\Unknown\game.exe", null) != first, "Detected game never uses another selected profile");
            Check(BoostProfiles.PathKey("steam://rungameid/730", false) == "", "Launcher URI is not a process identity");
            config.ManualGames.Add(new GameInstall { Source = "MANUAL", DisplayName = "Custom", LaunchTarget = @"C:\Custom\custom.exe" });
            config.ManualGames.Add(new GameInstall { Source = "MANUAL", DisplayName = "Voice", LaunchTarget = @"C:\Voice\discord.exe" });
            GameDetector.ConfigureManualGames(config.ManualGames);
            Check(GameDetector.IsConfiguredManualPath(@"c:\CUSTOM\custom.exe"), "Persisted manual game is detectable regardless of selection");
            Check(!GameDetector.IsConfiguredManualPath(@"D:\Other\custom.exe"), "Manual detection requires exact path");
            Check(!GameDetector.IsConfiguredManualPath(@"C:\Voice\discord.exe"), "Voice software stays protected");
            config.Language = "EN";
            Storage.SaveConfig(config);
            AppConfig loaded = Storage.LoadConfig();
            Check(loaded.Version == 6 && loaded.Language == "EN" && loaded.DefaultPreset == "Light" &&
                BoostProfiles.Get(loaded, first.ToUpperInvariant()) == "Performance" && loaded.ManualGames.Count == 2,
                "Profiles, language, and manual library survive restart");
            loaded.Language = "bad"; loaded.DefaultPreset = "bad"; loaded.GamePresets = null; loaded.ManualGames = null;
            BoostProfiles.NormalizeConfig(loaded);
            Check(loaded.Language == "TH" && loaded.DefaultPreset == "Balanced" && loaded.GamePresets.Count == 0 &&
                loaded.ManualGames.Count == 0, "Malformed optional fields normalize safely");
            loaded.GamePath = @"C:\Old\standalone.exe";
            BoostProfiles.NormalizeConfig(loaded);
            BoostProfiles.NormalizeConfig(loaded);
            Check(loaded.ManualGames.Count == 1 && loaded.ManualGames[0].LaunchTarget == loaded.GamePath,
                "Legacy selected executable migrates once into persistent library");
            Check(Contrast(ProfileColors.Master, ProfileColors.MasterSurface) >= 4.5, "Master text contrast");
            Check(Contrast(ProfileColors.Override, ProfileColors.OverrideSurface) >= 4.5, "Override text contrast");
            Check(Contrast(Palette.Coral, ProfileColors.MasterSurface) >= 4.5 &&
                Contrast(Palette.Coral, ProfileColors.OverrideSurface) >= 4.5, "Pending text contrast");
            Check(Contrast(ProfileColors.Track, ProfileColors.MasterSurface) >= 3 &&
                Contrast(ProfileColors.Track, ProfileColors.OverrideSurface) >= 3, "Slider track contrast");
            using (PresetSlider slider = new PresetSlider())
            {
                int events = 0;
                slider.ValueChanged += delegate { events++; };
                slider.Value = 1; slider.Value = 99;
                Check(slider.Value == 2 && events == 1, "Slider clamps values without duplicate events");
                MethodInfo key = typeof(PresetSlider).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
                key.Invoke(slider, new object[] { new KeyEventArgs(Keys.Home) });
                Check(slider.Value == 0, "Keyboard Home selects Light");
                key.Invoke(slider, new object[] { new KeyEventArgs(Keys.Right) });
                Check(slider.Value == 1, "Keyboard Right selects Balanced");
                UiText.Language = "EN";
                Check(slider.AccessibilityObject.Value == "Balanced", "Accessible slider value follows language");
                slider.Enabled = false;
                key.Invoke(slider, new object[] { new KeyEventArgs(Keys.Right) });
                Check(slider.Value == 1, "Disabled slider ignores keyboard");
            }
            using (NativeCpuReader cpu = new NativeCpuReader())
            {
                Check(Single.IsNaN(cpu.Sample(100, 200, 300)), "First CPU sample is unknown, not zero");
                Check(Math.Abs(cpu.Sample(150, 300, 400) - 75) < 0.01, "CPU idle time is included in kernel time");
                Check(Single.IsNaN(cpu.Sample(150, 300, 400)), "Zero elapsed CPU sample is unknown");
                Check(Single.IsNaN(cpu.Sample(0, 0, 0)), "CPU counters reset safely");
            }
            bool blocked = false;
            try { SystemTuner.Enable("", false, 0, pc, config); }
            catch (InvalidOperationException ex) { blocked = ex.Message.Contains("isolated test host"); }
            Check(blocked, "Tests cannot change real Windows settings");
            Console.WriteLine("profile_behavior_checks=" + checks);
        }
        finally
        {
            AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", null);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static double Contrast(Color first, Color second)
    {
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
    private static double Luminance(Color color)
    {
        return Linear(color.R) * 0.2126 + Linear(color.G) * 0.7152 + Linear(color.B) * 0.0722;
    }
    private static double Linear(byte component)
    {
        double v = component / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
