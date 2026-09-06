using System;
using System.Collections.Generic;
using System.IO;

namespace GameBoostPro
{
    internal static class BoostProfiles
    {
        public const string Light = "Light";
        public const string Balanced = "Balanced";
        public const string Performance = "Performance";

        public static string Normalize(string value)
        {
            if (String.Equals(value, Light, StringComparison.OrdinalIgnoreCase)) return Light;
            if (String.Equals(value, Performance, StringComparison.OrdinalIgnoreCase)) return Performance;
            return Balanced;
        }

        public static void NormalizeConfig(AppConfig config)
        {
            config.Language = String.Equals(config.Language, "EN", StringComparison.OrdinalIgnoreCase) ? "EN" : "TH";
            config.DefaultPreset = Normalize(config.DefaultPreset);
            Dictionary<string, string> clean = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (config.GamePresets != null)
                foreach (KeyValuePair<string, string> entry in config.GamePresets)
                    if (!String.IsNullOrWhiteSpace(entry.Key) && entry.Key.Length <= 32768 && clean.Count < 2000)
                        clean[entry.Key] = Normalize(entry.Value);
            config.GamePresets = clean;
            if (config.ManualGames == null) config.ManualGames = new List<GameInstall>();
            string legacyKey = PathKey(config.GamePath, false);
            if (legacyKey.Length > 0 && !config.ManualGames.Exists(delegate(GameInstall game) { return Key(game) == legacyKey; }))
                config.ManualGames.Add(new GameInstall { Source = "MANUAL", DisplayName = Path.GetFileNameWithoutExtension(config.GamePath),
                    DirectoryPath = Path.GetDirectoryName(config.GamePath), LaunchTarget = config.GamePath, LaunchArguments = "" });
        }

        // Executables are exact matches; launcher installations use a directory boundary.
        public static string PathKey(string path, bool directory)
        {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return "";
            try { return (directory ? "dir:" : "exe:") + Path.GetFullPath(path).TrimEnd('\\', '/').ToLowerInvariant(); }
            catch { return ""; }
        }

        public static string Key(GameInstall game)
        {
            if (game == null) return "";
            return String.Equals(game.Source, "MANUAL", StringComparison.OrdinalIgnoreCase)
                ? PathKey(game.LaunchTarget, false) : PathKey(game.DirectoryPath, true);
        }

        public static string SelectedKey(AppConfig config)
        {
            return !String.IsNullOrWhiteSpace(config.GamePath) ? PathKey(config.GamePath, false)
                : PathKey(config.LibraryGameDirectory, true);
        }

        public static string ResolveKey(AppConfig config, string runningPath, IList<GameInstall> catalog)
        {
            if (String.IsNullOrWhiteSpace(runningPath)) return SelectedKey(config);
            string exact = PathKey(runningPath, false);
            if (exact.Length == 0) return "";
            if (config.GamePresets.ContainsKey(exact)) return exact;
            string normalized = exact.Substring(4);
            string best = "";
            foreach (string key in config.GamePresets.Keys)
                if (key.StartsWith("dir:", StringComparison.OrdinalIgnoreCase) &&
                    normalized.StartsWith(key.Substring(4) + "\\", StringComparison.OrdinalIgnoreCase) && key.Length > best.Length)
                    best = key;
            if (best.Length > 0) return best;
            if (catalog != null)
                foreach (GameInstall game in catalog)
                {
                    string key = Key(game);
                    if (key.StartsWith("dir:", StringComparison.Ordinal) &&
                        normalized.StartsWith(key.Substring(4) + "\\", StringComparison.OrdinalIgnoreCase) && key.Length > best.Length)
                        best = key;
                }
            return best.Length > 0 ? best : exact;
        }

        public static string Get(AppConfig config, string key)
        {
            string value;
            return !String.IsNullOrEmpty(key) && config.GamePresets.TryGetValue(key, out value)
                ? Normalize(value) : Normalize(config.DefaultPreset);
        }

        public static void SetAll(AppConfig config, string preset)
        {
            config.DefaultPreset = Normalize(preset);
            config.GamePresets.Clear();
        }

        public static AppConfig Snapshot(AppConfig source, string key, PlatformProfile platform)
        {
            string preset = Get(source, key);
            bool balanced = preset != Light;
            bool performance = preset == Performance;
            // Copy only engine inputs. Never pass mutable UI configuration to a transition worker.
            return new AppConfig
            {
                DefaultPreset = preset,
                EnableWindowsGameMode = source.EnableWindowsGameMode,
                DisableBackgroundCapture = balanced && source.DisableBackgroundCapture,
                PreferHighPerformanceGpu = balanced && source.PreferHighPerformanceGpu,
                UseAboveNormalPriority = performance && source.UseAboveNormalPriority,
                UseHighQos = performance && source.UseHighQos,
                UseDynamicPriorityBoost = performance && source.UseDynamicPriorityBoost,
                PowerPlanMode = !performance || (platform != null && platform.IsLaptop)
                    ? PowerPlanPolicy.KeepCurrent : PowerPlanPolicy.Normalize(source.PowerPlanMode)
            };
        }
    }
}
