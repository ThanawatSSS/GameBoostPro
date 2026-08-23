using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Game Boost Pro")]
[assembly: System.Reflection.AssemblyDescription("Smart game detection and reversible Windows gaming optimization")]
[assembly: System.Reflection.AssemblyProduct("Game Boost Pro")]
[assembly: System.Reflection.AssemblyCompany("Local PC Tools")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

namespace GameBoostPro
{
    internal static class Palette
    {
        public static readonly Color Back = Color.FromArgb(19, 22, 18);
        public static readonly Color Surface = Color.FromArgb(29, 34, 28);
        public static readonly Color SurfaceHigh = Color.FromArgb(38, 44, 36);
        public static readonly Color Line = Color.FromArgb(62, 69, 57);
        public static readonly Color Text = Color.FromArgb(238, 242, 231);
        public static readonly Color Muted = Color.FromArgb(159, 169, 149);
        public static readonly Color Lime = Color.FromArgb(199, 243, 107);
        public static readonly Color Amber = Color.FromArgb(244, 183, 77);
        public static readonly Color Coral = Color.FromArgb(242, 112, 89);
    }

    internal static class SafetyPolicy
    {
        private static readonly HashSet<string> ProtectedProcesses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Discord",
            "TeamSpeak",
            "ts3client_win32",
            "ts3client_win64",
            "NitroSense",
            "AcerPurifiedVoiceApp"
        };

        public static bool IsProtectedProcess(string processName)
        {
            if (String.IsNullOrWhiteSpace(processName)) return false;
            return ProtectedProcesses.Contains(processName) ||
                processName.StartsWith("Acer", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal enum PlatformKind
    {
        AcerNitroSenseLaptop,
        DesktopPc,
        UnsupportedLaptop
    }

    internal sealed class PlatformProfile
    {
        public PlatformKind Kind { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public bool IsLaptop { get; set; }
        public bool HasNitroSense { get; set; }
        public bool IsSupported { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
    }

    internal static class PlatformDetector
    {
        private static readonly HashSet<int> PortableChassisTypes = new HashSet<int>
        {
            8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32
        };

        public static PlatformProfile Detect()
        {
            string manufacturer = ReadBiosValue("SystemManufacturer");
            string model = ReadBiosValue("SystemProductName");
            bool isLaptop = DetectPortableChassis();
            bool hasNitroSense = DetectNitroSense();
            return Evaluate(manufacturer, model, isLaptop, hasNitroSense);
        }

        public static PlatformProfile Evaluate(string manufacturer, string model, bool isLaptop, bool hasNitroSense)
        {
            manufacturer = String.IsNullOrWhiteSpace(manufacturer) ? "Unknown" : manufacturer.Trim();
            model = String.IsNullOrWhiteSpace(model) ? "Unknown model" : model.Trim();
            bool acer = manufacturer.IndexOf("Acer", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isLaptop)
            {
                return new PlatformProfile
                {
                    Kind = PlatformKind.DesktopPc,
                    Manufacturer = manufacturer,
                    Model = model,
                    IsLaptop = false,
                    HasNitroSense = hasNitroSense,
                    IsSupported = true,
                    Title = "DESKTOP PC / NATIVE",
                    Detail = "Game Boost ควบคุม Power Plan และ Windows gaming stack"
                };
            }

            if (acer && hasNitroSense)
            {
                return new PlatformProfile
                {
                    Kind = PlatformKind.AcerNitroSenseLaptop,
                    Manufacturer = manufacturer,
                    Model = model,
                    IsLaptop = true,
                    HasNitroSense = true,
                    IsSupported = true,
                    Title = "ACER + NITROSENSE",
                    Detail = "Game Boost คุม Power / NitroSense คุมพัดลมและฮาร์ดแวร์"
                };
            }

            return new PlatformProfile
            {
                Kind = PlatformKind.UnsupportedLaptop,
                Manufacturer = manufacturer,
                Model = model,
                IsLaptop = true,
                HasNitroSense = hasNitroSense,
                IsSupported = false,
                Title = "LAPTOP NOT SUPPORTED",
                Detail = manufacturer + " " + model + " / รุ่นนี้ยังไม่เปิด Boost"
            };
        }

        private static string ReadBiosValue(string name)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                    return key == null ? "" : Convert.ToString(key.GetValue(name));
            }
            catch { return ""; }
        }

        private static bool DetectPortableChassis()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                {
                    foreach (ManagementObject enclosure in searcher.Get())
                    {
                        ushort[] types = enclosure["ChassisTypes"] as ushort[];
                        if (types == null) continue;
                        foreach (ushort type in types)
                            if (PortableChassisTypes.Contains(type)) return true;
                    }
                    return false;
                }
            }
            catch
            {
                return (SystemInformation.PowerStatus.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0;
            }
        }

        private static bool DetectNitroSense()
        {
            try
            {
                if (Process.GetProcessesByName("NitroSense").Length > 0) return true;
            }
            catch { }
            try
            {
                using (RegistryKey agent = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\AASSvc"))
                using (RegistryKey monitor = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ASMSvc"))
                    if (agent != null && monitor != null) return true;
            }
            catch { }
            foreach (string root in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            })
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(root))
                    {
                        if (key == null) continue;
                        foreach (string child in key.GetSubKeyNames())
                        {
                            using (RegistryKey app = key.OpenSubKey(child))
                            {
                                string name = app == null ? "" : Convert.ToString(app.GetValue("DisplayName"));
                                if (name.IndexOf("NitroSense", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                            }
                        }
                    }
                }
                catch { }
            }
            return false;
        }
    }

    internal class AppConfig
    {
        public int Version { get; set; }
        public string GamePath { get; set; }
        public bool AutoMode { get; set; }
        public bool LaunchOnBoost { get; set; }
        public string LibraryGameName { get; set; }
        public string LibraryGameDirectory { get; set; }
        public string LibraryLaunchTarget { get; set; }

        public AppConfig()
        {
            Version = 3;
            AutoMode = true;
        }
    }

    internal class RegistrySnapshot
    {
        public string SubKey { get; set; }
        public string Name { get; set; }
        public bool Exists { get; set; }
        public int Kind { get; set; }
        public string Value { get; set; }
    }

    internal class BoostState
    {
        public string EnabledAt { get; set; }
        public string PreviousPowerGuid { get; set; }
        public string PreviousPowerName { get; set; }
        public string TargetPowerGuid { get; set; }
        public string PlatformTitle { get; set; }
        public bool AutoTriggered { get; set; }
        public string GamePath { get; set; }
        public int GameProcessId { get; set; }
        public string PreviousPriority { get; set; }
        public bool ProcessTuningApplied { get; set; }
        public bool PreviousPriorityBoostDisabled { get; set; }
        public bool HadPowerThrottleState { get; set; }
        public uint PreviousThrottleControl { get; set; }
        public uint PreviousThrottleState { get; set; }
        public List<RegistrySnapshot> Registry { get; set; }
    }

    internal class PowerPlan
    {
        public string Guid { get; set; }
        public string Name { get; set; }
    }

    internal class DetectedGame
    {
        public string DisplayName { get; set; }
        public string ExePath { get; set; }
        public string Source { get; set; }
        public Process Process { get; set; }
    }

    internal class GameInstall
    {
        public string DisplayName { get; set; }
        public string DirectoryPath { get; set; }
        public string Source { get; set; }
        public string LaunchTarget { get; set; }
    }

    internal static class GameDetector
    {
        private static readonly Dictionary<string, string> KnownProcesses =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "cs2", "Counter-Strike 2" },
            { "tslgame", "PUBG: BATTLEGROUNDS" },
            { "valorant-win64-shipping", "VALORANT" },
            { "r5apex", "Apex Legends" },
            { "fortniteclient-win64-shipping", "Fortnite" },
            { "cod", "Call of Duty" },
            { "gta5", "Grand Theft Auto V" },
            { "rainbowsix", "Rainbow Six Siege" },
            { "rainbowsix_vulkan", "Rainbow Six Siege" },
            { "overwatch", "Overwatch 2" },
            { "dota2", "Dota 2" },
            { "league of legends", "League of Legends" },
            { "marvel-win64-shipping", "Marvel Rivals" },
            { "eldenring", "ELDEN RING" },
            { "helldivers2", "HELLDIVERS 2" },
            { "destiny2", "Destiny 2" },
            { "cyberpunk2077", "Cyberpunk 2077" },
            { "rocketleague", "Rocket League" },
            { "deadlock", "Deadlock" },
            { "discovery", "THE FINALS" },
            { "deltaforceclient-win64-shipping", "Delta Force" }
        };

        private static readonly HashSet<string> ExcludedProcesses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "steam", "epicgameslauncher", "riotclientservices", "riotclientux",
            "easyanticheat", "easyanticheat_eos", "beservice", "battleye",
            "unitycrashhandler64", "crashreportclient", "unrealcefsubprocess",
            "launcher", "updater", "uninstall"
        };

        private static readonly List<GameInstall> Catalog = new List<GameInstall>();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static bool catalogLoaded;

        public static int InstalledCount
        {
            get { EnsureCatalog(); return Catalog.Count; }
        }

        public static string InstalledSummary
        {
            get
            {
                EnsureCatalog();
                List<string> names = new List<string>();
                foreach (GameInstall item in Catalog)
                    if (!names.Contains(item.DisplayName)) names.Add(item.DisplayName);
                return String.Join(" / ", names.ToArray());
            }
        }

        public static List<GameInstall> GetCatalog()
        {
            EnsureCatalog();
            return new List<GameInstall>(Catalog);
        }

        public static void RefreshCatalog()
        {
            Catalog.Clear();
            DiscoverSteam();
            DiscoverEpic();
            DiscoverRiot();
            catalogLoaded = true;
        }

        public static DetectedGame FindRunningGame(string manualPath)
        {
            EnsureCatalog();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id || process.HasExited) continue;
                    string processName = process.ProcessName;
                    if (SafetyPolicy.IsProtectedProcess(processName)) continue;
                    string knownName;
                    if (KnownProcesses.TryGetValue(processName, out knownName))
                    {
                        return new DetectedGame
                        {
                            DisplayName = knownName,
                            ExePath = TryGetProcessPath(process),
                            Source = "KNOWN",
                            Process = process
                        };
                    }

                    if (ExcludedProcesses.Contains(processName)) continue;
                    string exePath = TryGetProcessPath(process);
                    if (String.IsNullOrWhiteSpace(exePath)) continue;

                    if (!String.IsNullOrWhiteSpace(manualPath) &&
                        String.Equals(exePath, manualPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return new DetectedGame
                        {
                            DisplayName = Path.GetFileNameWithoutExtension(manualPath),
                            ExePath = exePath,
                            Source = "MANUAL",
                            Process = process
                        };
                    }

                    foreach (GameInstall install in Catalog)
                    {
                        if (IsUnderDirectory(exePath, install.DirectoryPath))
                        {
                            return new DetectedGame
                            {
                                DisplayName = install.DisplayName,
                                ExePath = exePath,
                                Source = install.Source,
                                Process = process
                            };
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private static void EnsureCatalog()
        {
            if (!catalogLoaded) RefreshCatalog();
        }

        private static string TryGetProcessPath(Process process)
        {
            try { return process.MainModule.FileName; }
            catch { return ""; }
        }

        private static bool IsUnderDirectory(string filePath, string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) return false;
            try
            {
                string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string file = Path.GetFullPath(filePath);
                return file.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void AddInstall(string name, string path, string source, string launchTarget)
        {
            if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            path = NormalizeDirectory(path);
            foreach (GameInstall item in Catalog)
                if (String.Equals(item.DirectoryPath, path, StringComparison.OrdinalIgnoreCase)) return;
            Catalog.Add(new GameInstall
            {
                DisplayName = name,
                DirectoryPath = path,
                Source = source,
                LaunchTarget = launchTarget ?? ""
            });
        }

        private static void DiscoverSteam()
        {
            List<string> steamRoots = new List<string>();
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                    if (key != null) AddUnique(steamRoots, Convert.ToString(key.GetValue("InstallPath")));
            }
            catch { }
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (key != null) AddUnique(steamRoots, Convert.ToString(key.GetValue("SteamPath")));
            }
            catch { }

            List<string> libraries = new List<string>(steamRoots);
            foreach (string root in steamRoots)
            {
                try
                {
                    string vdfPath = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(vdfPath)) continue;
                    foreach (Match match in Regex.Matches(File.ReadAllText(vdfPath), "\"path\"\\s+\"([^\"]+)\""))
                        AddUnique(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch { }
            }

            foreach (string library in libraries)
            {
                string apps = Path.Combine(library, "steamapps");
                if (!Directory.Exists(apps)) continue;
                try
                {
                    foreach (string manifest in Directory.GetFiles(apps, "appmanifest_*.acf"))
                    {
                        string raw = File.ReadAllText(manifest);
                        Match name = Regex.Match(raw, "\"name\"\\s+\"([^\"]+)\"");
                        Match dir = Regex.Match(raw, "\"installdir\"\\s+\"([^\"]+)\"");
                        Match appId = Regex.Match(raw, "\"appid\"\\s+\"([^\"]+)\"");
                        if (name.Success && dir.Success &&
                            !name.Groups[1].Value.Equals("Steamworks Common Redistributables", StringComparison.OrdinalIgnoreCase))
                            AddInstall(name.Groups[1].Value, Path.Combine(apps, "common", dir.Groups[1].Value), "STEAM",
                                appId.Success ? "steam://rungameid/" + appId.Groups[1].Value : "");
                    }
                }
                catch { }
            }
        }

        private static void DiscoverEpic()
        {
            string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifests)) return;
            try
            {
                foreach (string file in Directory.GetFiles(manifests, "*.item"))
                {
                    Dictionary<string, object> item = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
                    string name = item.ContainsKey("DisplayName") ? Convert.ToString(item["DisplayName"]) : "Epic Game";
                    string path = item.ContainsKey("InstallLocation") ? Convert.ToString(item["InstallLocation"]) : "";
                    string launch = item.ContainsKey("LaunchExecutable") ? Convert.ToString(item["LaunchExecutable"]) : "";
                    string appName = item.ContainsKey("AppName") ? Convert.ToString(item["AppName"]) : "";
                    if (!String.IsNullOrWhiteSpace(launch))
                        AddInstall(name, path, "EPIC", String.IsNullOrWhiteSpace(appName) ? "" :
                            "com.epicgames.launcher://apps/" + appName + "?action=launch&silent=true");
                }
            }
            catch { }
        }

        private static void DiscoverRiot()
        {
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Riot Games", "RiotClientInstalls.json");
            if (!File.Exists(configPath)) return;
            try
            {
                Dictionary<string, object> root = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(configPath));
                Dictionary<string, object> clients = root["associated_client"] as Dictionary<string, object>;
                if (clients == null) return;
                foreach (string path in clients.Keys)
                {
                    if (path.IndexOf("VALORANT", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string normalized = path.Replace('/', Path.DirectorySeparatorChar);
                        string directory = File.Exists(normalized) ? Path.GetDirectoryName(normalized) : normalized;
                        AddInstall("VALORANT", directory, "RIOT", File.Exists(normalized) ? normalized : "");
                    }
                }
            }
            catch { }
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (String.IsNullOrWhiteSpace(value) || !Directory.Exists(value)) return;
            value = NormalizeDirectory(value);
            foreach (string existing in list)
                if (String.Equals(existing, value, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(value);
        }

        private static string NormalizeDirectory(string value)
        {
            try
            {
                return Path.GetFullPath(value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar))
                    .TrimEnd(Path.DirectorySeparatorChar);
            }
            catch { return value; }
        }
    }

    internal static class Storage
    {
        public static readonly string AppDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGameBoost");
        public static readonly string ConfigPath = Path.Combine(AppDir, "config-pro.json");
        public static readonly string StatePath = Path.Combine(AppDir, "state-pro.json");
        public static readonly string LegacyStatePath = Path.Combine(AppDir, "state.json");
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string raw = File.ReadAllText(ConfigPath);
                    AppConfig config = Json.Deserialize<AppConfig>(raw);
                    Dictionary<string, object> fields = Json.Deserialize<Dictionary<string, object>>(raw);
                    if (!fields.ContainsKey("Version") || config.Version < 3)
                    {
                        config.Version = 3;
                        config.AutoMode = true;
                        SaveConfig(config);
                    }
                    return config;
                }
            }
            catch { }
            return new AppConfig();
        }

        public static void SaveConfig(AppConfig config)
        {
            Directory.CreateDirectory(AppDir);
            WriteAtomic(ConfigPath, Json.Serialize(config));
        }

        public static BoostState LoadState()
        {
            if (!File.Exists(StatePath)) return null;
            return Json.Deserialize<BoostState>(File.ReadAllText(StatePath));
        }

        public static void SaveState(BoostState state)
        {
            Directory.CreateDirectory(AppDir);
            WriteAtomic(StatePath, Json.Serialize(state));
        }

        public static bool HasState()
        {
            return File.Exists(StatePath) || File.Exists(LegacyStatePath);
        }

        private static void WriteAtomic(string path, string content)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content);
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                try { File.Replace(temporary, path, backup, true); }
                finally { try { if (File.Exists(backup)) File.Delete(backup); } catch { } }
            }
            else
            {
                File.Move(temporary, path);
            }
        }
    }

    internal static class SystemTuner
    {
        private const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        private static readonly Tuple<string, string, object, RegistryValueKind>[] Tweaks =
        {
            Tuple.Create(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", (object)1, RegistryValueKind.DWord),
            Tuple.Create(@"Software\Microsoft\GameBar", "AllowAutoGameMode", (object)1, RegistryValueKind.DWord),
            Tuple.Create(@"System\GameConfigStore", "GameDVR_Enabled", (object)0, RegistryValueKind.DWord),
            Tuple.Create(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", (object)0, RegistryValueKind.DWord)
        };

        public static bool IsAdmin()
        {
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static PowerPlan GetActivePowerPlan()
        {
            string output = RunPowerCfg("/getactivescheme");
            Match guid = Regex.Match(output, @"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
            if (!guid.Success) throw new InvalidOperationException("อ่านแผนพลังงานปัจจุบันไม่สำเร็จ");
            Match name = Regex.Match(output, @"\(([^)]+)\)\s*$");
            return new PowerPlan { Guid = guid.Value, Name = name.Success ? name.Groups[1].Value : "แผนปัจจุบัน" };
        }

        public static string GetBestPerformanceScheme()
        {
            string schemes = RunPowerCfg("/list");
            if (schemes.IndexOf(UltimateGuid, StringComparison.OrdinalIgnoreCase) >= 0) return UltimateGuid;
            if (schemes.IndexOf(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase) >= 0) return HighPerformanceGuid;

            string output = RunPowerCfg("/duplicatescheme " + HighPerformanceGuid);
            Match duplicated = Regex.Match(output, @"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
            if (!duplicated.Success)
                throw new InvalidOperationException("สร้าง High Performance power plan ไม่สำเร็จ");
            return duplicated.Value;
        }

        public static BoostState Enable(string gamePath, bool autoTriggered, int processId, PlatformProfile platform)
        {
            if (Storage.HasState()) throw new InvalidOperationException("Game Mode เปิดอยู่แล้ว");
            if (platform == null || !platform.IsSupported)
                throw new InvalidOperationException("แพลตฟอร์มนี้ยังไม่รองรับ Boost เพื่อป้องกันการชนกับซอฟต์แวร์ OEM");
            if (!IsAdmin())
                throw new InvalidOperationException("Best Mode ต้องใช้สิทธิ์ Administrator กรุณาเปิดโปรแกรมใหม่และกดยืนยัน UAC");

            PowerPlan current = GetActivePowerPlan();
            string targetPowerGuid = GetBestPerformanceScheme();
            BoostState state = new BoostState
            {
                EnabledAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                PreviousPowerGuid = current.Guid,
                PreviousPowerName = current.Name,
                TargetPowerGuid = targetPowerGuid,
                PlatformTitle = platform.Title,
                AutoTriggered = autoTriggered,
                GamePath = gamePath ?? "",
                GameProcessId = processId,
                Registry = new List<RegistrySnapshot>()
            };

            foreach (Tuple<string, string, object, RegistryValueKind> tweak in Tweaks)
                state.Registry.Add(Capture(tweak.Item1, tweak.Item2));

            if (!String.IsNullOrWhiteSpace(gamePath))
                state.Registry.Add(Capture(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath));

            Storage.SaveState(state);
            try
            {
                RunPowerCfg("/S " + targetPowerGuid);
                PowerPlan active = GetActivePowerPlan();
                if (!String.Equals(active.Guid, targetPowerGuid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Windows ไม่ยอมเปลี่ยน Power Plan เป็นโหมดประสิทธิภาพสูง");

                foreach (Tuple<string, string, object, RegistryValueKind> tweak in Tweaks)
                    SetRegistry(tweak.Item1, tweak.Item2, tweak.Item3, tweak.Item4);

                if (!String.IsNullOrWhiteSpace(gamePath))
                    SetRegistry(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath,
                        "GpuPreference=2;", RegistryValueKind.String);

                ApplyGamePriority(state);
                return state;
            }
            catch
            {
                try { Disable(); } catch { }
                throw;
            }
        }

        public static void Disable()
        {
            if (File.Exists(Storage.StatePath))
            {
                BoostState state = Storage.LoadState();
                if (state == null) throw new InvalidOperationException("ข้อมูลคืนค่าเสียหาย กรุณาอย่าลบไฟล์สถานะ");

                if (state.GameProcessId > 0 && state.ProcessTuningApplied)
                {
                    try
                    {
                        Process p = Process.GetProcessById(state.GameProcessId);
                        if (!String.IsNullOrWhiteSpace(state.PreviousPriority))
                            p.PriorityClass = (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), state.PreviousPriority);
                        SetProcessPriorityBoost(p.Handle, state.PreviousPriorityBoostDisabled);
                        POWER_THROTTLING_STATE previous = new POWER_THROTTLING_STATE();
                        previous.Version = 1;
                        previous.ControlMask = state.HadPowerThrottleState ? state.PreviousThrottleControl : 0;
                        previous.StateMask = state.HadPowerThrottleState ? state.PreviousThrottleState : 0;
                        SetProcessInformation(p.Handle, ProcessPowerThrottling, ref previous, Marshal.SizeOf(previous));
                    }
                    catch { }
                }

                RunPowerCfg("/S " + state.PreviousPowerGuid);
                foreach (RegistrySnapshot item in state.Registry) Restore(item);
                File.Delete(Storage.StatePath);
                return;
            }

            if (File.Exists(Storage.LegacyStatePath))
            {
                RestoreLegacyState();
                return;
            }

            throw new InvalidOperationException("ไม่พบข้อมูลเดิมสำหรับคืนค่า");
        }

        public static bool ApplyGamePriority(BoostState state)
        {
            if (state == null) return false;
            Process game = null;
            if (state.GameProcessId > 0)
            {
                try { game = Process.GetProcessById(state.GameProcessId); }
                catch { game = null; }
            }
            if (game == null) game = FindGameProcess(state.GamePath);
            if (game == null) return false;

            try
            {
                if (!state.ProcessTuningApplied || state.GameProcessId != game.Id)
                {
                    state.GameProcessId = game.Id;
                    state.PreviousPriority = game.PriorityClass.ToString();
                    bool boostDisabled;
                    if (GetProcessPriorityBoost(game.Handle, out boostDisabled))
                        state.PreviousPriorityBoostDisabled = boostDisabled;

                    POWER_THROTTLING_STATE previous = new POWER_THROTTLING_STATE();
                    previous.Version = 1;
                    if (GetProcessInformation(game.Handle, ProcessPowerThrottling, ref previous, Marshal.SizeOf(previous)))
                    {
                        state.HadPowerThrottleState = true;
                        state.PreviousThrottleControl = previous.ControlMask;
                        state.PreviousThrottleState = previous.StateMask;
                    }

                    game.PriorityClass = ProcessPriorityClass.AboveNormal;
                    SetProcessPriorityBoost(game.Handle, false);
                    POWER_THROTTLING_STATE highQos = new POWER_THROTTLING_STATE();
                    highQos.Version = 1;
                    highQos.ControlMask = PowerThrottlingExecutionSpeed | PowerThrottlingIgnoreTimerResolution;
                    highQos.StateMask = 0;
                    SetProcessInformation(game.Handle, ProcessPowerThrottling, ref highQos, Marshal.SizeOf(highQos));
                    state.ProcessTuningApplied = true;
                    Storage.SaveState(state);
                }
                return true;
            }
            catch { return false; }
        }

        public static void AttachGamePath(BoostState state, string gamePath)
        {
            if (state == null || String.IsNullOrWhiteSpace(gamePath)) return;
            if (String.Equals(state.GamePath, gamePath, StringComparison.OrdinalIgnoreCase)) return;

            bool captured = false;
            foreach (RegistrySnapshot item in state.Registry)
            {
                if (String.Equals(item.SubKey, @"Software\Microsoft\DirectX\UserGpuPreferences", StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(item.Name, gamePath, StringComparison.OrdinalIgnoreCase))
                {
                    captured = true;
                    break;
                }
            }
            if (!captured)
                state.Registry.Add(Capture(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath));

            state.GamePath = gamePath;
            SetRegistry(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath,
                "GpuPreference=2;", RegistryValueKind.String);
            Storage.SaveState(state);
        }

        public static Process FindGameProcess(string gamePath)
        {
            if (String.IsNullOrWhiteSpace(gamePath)) return null;
            string processName = Path.GetFileNameWithoutExtension(gamePath);
            foreach (Process p in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (String.Equals(p.MainModule.FileName, gamePath, StringComparison.OrdinalIgnoreCase)) return p;
                }
                catch
                {
                    return p;
                }
            }
            return null;
        }

        private const int ProcessPowerThrottling = 4;
        private const uint PowerThrottlingExecutionSpeed = 0x1;
        private const uint PowerThrottlingIgnoreTimerResolution = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        private struct POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(IntPtr hProcess, int processInformationClass,
            ref POWER_THROTTLING_STATE processInformation, int processInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessInformation(IntPtr hProcess, int processInformationClass,
            ref POWER_THROTTLING_STATE processInformation, int processInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessPriorityBoost(IntPtr hProcess, out bool disablePriorityBoost);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessPriorityBoost(IntPtr hProcess, bool disablePriorityBoost);

        private static RegistrySnapshot Capture(string subKey, string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey, false))
            {
                if (key == null || key.GetValue(name, null) == null)
                    return new RegistrySnapshot { SubKey = subKey, Name = name, Exists = false };

                object value = key.GetValue(name);
                return new RegistrySnapshot
                {
                    SubKey = subKey,
                    Name = name,
                    Exists = true,
                    Kind = (int)key.GetValueKind(name),
                    Value = Convert.ToString(value, CultureInfo.InvariantCulture)
                };
            }
        }

        private static void SetRegistry(string subKey, string name, object value, RegistryValueKind kind)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey))
                key.SetValue(name, value, kind);
        }

        private static void Restore(RegistrySnapshot snapshot)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(snapshot.SubKey))
            {
                if (!snapshot.Exists)
                {
                    key.DeleteValue(snapshot.Name, false);
                    return;
                }

                RegistryValueKind kind = (RegistryValueKind)snapshot.Kind;
                object value = snapshot.Value;
                if (kind == RegistryValueKind.DWord) value = Int32.Parse(snapshot.Value, CultureInfo.InvariantCulture);
                else if (kind == RegistryValueKind.QWord) value = Int64.Parse(snapshot.Value, CultureInfo.InvariantCulture);
                key.SetValue(snapshot.Name, value, kind);
            }
        }

        private static string RunPowerCfg(string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo("powercfg.exe", arguments);
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.CreateNoWindow = true;
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("ตั้งค่าแผนพลังงานไม่สำเร็จ " + error.Trim());
                return output + "\n" + error;
            }
        }

        private static void RestoreLegacyState()
        {
            JavaScriptSerializer json = new JavaScriptSerializer();
            Dictionary<string, object> root = json.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(Storage.LegacyStatePath));

            Dictionary<string, object> plan = root["PowerPlan"] as Dictionary<string, object>;
            if (plan != null && plan.ContainsKey("Guid")) RunPowerCfg("/S " + plan["Guid"]);

            object[] registry = root["Registry"] as object[];
            if (registry != null)
            {
                foreach (object raw in registry)
                {
                    Dictionary<string, object> old = raw as Dictionary<string, object>;
                    if (old == null) continue;
                    string fullPath = Convert.ToString(old["Path"]);
                    string subKey = fullPath.Replace("HKCU:\\", "");
                    string name = Convert.ToString(old["Name"]);
                    bool exists = Convert.ToBoolean(old["Exists"]);
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey))
                    {
                        if (exists) key.SetValue(name, Convert.ToInt32(old["Value"]), RegistryValueKind.DWord);
                        else key.DeleteValue(name, false);
                    }
                }
            }
            File.Delete(Storage.LegacyStatePath);
        }
    }

    internal sealed class BoostDial : Control
    {
        private bool active;
        private bool busy;
        private bool hover;
        private int phase;
        private readonly Timer animation;

        public event EventHandler BoostClick;

        public bool Active
        {
            get { return active; }
            set { active = value; Invalidate(); }
        }

        public bool Busy
        {
            get { return busy; }
            set
            {
                busy = value;
                animation.Enabled = value;
                Invalidate();
            }
        }

        public BoostDial()
        {
            Size = new Size(320, 320);
            TabStop = true;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            animation = new Timer();
            animation.Interval = 55;
            animation.Tick += delegate { phase = (phase + 1) % 24; Invalidate(); };
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (!busy && BoostClick != null) BoostClick(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!busy && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) && BoostClick != null)
            {
                BoostClick(this, EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF ring = new RectangleF(28, 28, Width - 56, Height - 56);
            Color accent = active ? Palette.Coral : Palette.Lime;

            for (int i = 0; i < 24; i++)
            {
                int distance = (i - phase + 24) % 24;
                Color color = busy && distance < 6 ? Palette.Amber :
                    Color.FromArgb(active || hover ? 235 : 135, accent);
                using (Pen pen = new Pen(color, i % 3 == 0 ? 8f : 5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, ring, -90 + i * 15 + 2, 10);
                }
            }

            RectangleF core = new RectangleF(57, 57, Width - 114, Height - 114);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(70, Color.Black)))
                g.FillEllipse(shadow, new RectangleF(core.X + 4, core.Y + 8, core.Width, core.Height));
            using (SolidBrush fill = new SolidBrush(hover ? Palette.SurfaceHigh : Palette.Surface))
                g.FillEllipse(fill, core);
            using (Pen border = new Pen(Color.FromArgb(95, accent), 2f))
                g.DrawEllipse(border, core);

            string eyebrow = busy ? "กำลังปรับระบบ" : (active ? "GAME MODE  ON" : "READY");
            string action = active ? "RESTORE" : "BOOST";
            string hint = active ? "กลับสู่โหมดปกติ" : "กดเพื่อเร่งเครื่อง";
            DrawCentered(g, eyebrow, new Font("Segoe UI", 9, FontStyle.Bold), Palette.Muted, 119);
            DrawCentered(g, action, new Font("Segoe UI Semibold", 25, FontStyle.Bold), accent, 145);
            DrawCentered(g, hint, new Font("Segoe UI", 9), Palette.Text, 187);

            if (Focused)
            {
                using (Pen focus = new Pen(Palette.Amber, 1f))
                {
                    focus.DashStyle = DashStyle.Dot;
                    g.DrawEllipse(focus, new RectangleF(core.X - 6, core.Y - 6, core.Width + 12, core.Height + 12));
                }
            }
        }

        private static void DrawCentered(Graphics g, string text, Font font, Color color, float y)
        {
            using (font)
            using (SolidBrush brush = new SolidBrush(color))
            {
                SizeF size = g.MeasureString(text, font);
                g.DrawString(text, font, brush, (320 - size.Width) / 2f, y);
            }
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool value;
        public event EventHandler ValueChanged;
        public bool Value
        {
            get { return value; }
            set { if (this.value == value) return; this.value = value; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        }

        public ToggleSwitch()
        {
            Size = new Size(48, 26);
            Cursor = Cursors.Hand;
            TabStop = true;
            DoubleBuffered = true;
        }

        protected override void OnClick(EventArgs e) { Value = !Value; base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { Value = !Value; e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color track = value ? Palette.Lime : Palette.Line;
            using (SolidBrush brush = new SolidBrush(track))
            {
                e.Graphics.FillEllipse(brush, 0, 0, 26, 26);
                e.Graphics.FillEllipse(brush, 22, 0, 26, 26);
                e.Graphics.FillRectangle(brush, 13, 0, 22, 26);
            }
            int x = value ? 25 : 3;
            using (SolidBrush knob = new SolidBrush(value ? Palette.Back : Palette.Text))
                e.Graphics.FillEllipse(knob, x, 3, 20, 20);
        }
    }

    internal sealed class MetricBar : Control
    {
        private float value;
        public string Caption { get; set; }
        public float Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }

        public MetricBar()
        {
            Size = new Size(180, 48);
            Caption = "";
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (Font label = new Font("Segoe UI", 8))
            using (Font number = new Font("Segoe UI Semibold", 10, FontStyle.Bold))
            using (SolidBrush muted = new SolidBrush(Palette.Muted))
            using (SolidBrush text = new SolidBrush(Palette.Text))
            {
                e.Graphics.DrawString(Caption, label, muted, 0, 1);
                string numberText = Math.Round(value).ToString(CultureInfo.InvariantCulture) + "%";
                SizeF size = e.Graphics.MeasureString(numberText, number);
                e.Graphics.DrawString(numberText, number, text, Width - size.Width, 0);
            }
            using (SolidBrush track = new SolidBrush(Palette.Line)) e.Graphics.FillRectangle(track, 0, 30, Width, 4);
            using (SolidBrush fill = new SolidBrush(value > 85 ? Palette.Coral : Palette.Lime))
                e.Graphics.FillRectangle(fill, 0, 30, (int)(Width * value / 100f), 4);
        }
    }

    internal sealed class GameLibraryForm : Form
    {
        private readonly ListView games;
        private readonly Button selectButton;
        public GameInstall SelectedGame { get; private set; }

        public GameLibraryForm(List<GameInstall> catalog)
        {
            Text = "Game Library";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 430);
            MinimumSize = new Size(720, 430);
            MaximumSize = new Size(720, 430);
            MaximizeBox = false;
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 9);

            Label title = new Label
            {
                Text = "GAME LIBRARY",
                Location = new Point(24, 20),
                Size = new Size(260, 28),
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = Palette.Text
            };
            Controls.Add(title);
            Controls.Add(new Label
            {
                Text = "พบจาก Steam / Epic / Riot ทั้งหมด " + catalog.Count + " รายการ",
                Location = new Point(25, 52),
                Size = new Size(430, 22),
                ForeColor = Palette.Muted
            });

            games = new ListView
            {
                Location = new Point(24, 88),
                Size = new Size(672, 270),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BackColor = Palette.Surface,
                ForeColor = Palette.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            games.Columns.Add("GAME", 260);
            games.Columns.Add("SOURCE", 90);
            games.Columns.Add("INSTALL LOCATION", 300);
            catalog.Sort(delegate(GameInstall left, GameInstall right)
            {
                return StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName, right.DisplayName);
            });
            foreach (GameInstall game in catalog)
            {
                ListViewItem item = new ListViewItem(game.DisplayName);
                item.SubItems.Add(game.Source);
                item.SubItems.Add(game.DirectoryPath);
                item.Tag = game;
                games.Items.Add(item);
            }
            games.SelectedIndexChanged += delegate { selectButton.Enabled = games.SelectedItems.Count == 1; };
            games.DoubleClick += delegate { AcceptSelection(); };
            Controls.Add(games);

            Button cancel = CreateButton("ยกเลิก", 468, 378, 108, Palette.SurfaceHigh, Palette.Text);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
            selectButton = CreateButton("เลือกเกม", 586, 378, 110, Palette.Lime, Palette.Back);
            selectButton.Enabled = false;
            selectButton.Click += delegate { AcceptSelection(); };
            Controls.Add(selectButton);
        }

        private void AcceptSelection()
        {
            if (games.SelectedItems.Count != 1) return;
            SelectedGame = games.SelectedItems[0].Tag as GameInstall;
            DialogResult = SelectedGame == null ? DialogResult.Cancel : DialogResult.OK;
            Close();
        }

        private static Button CreateButton(string text, int x, int y, int width, Color back, Color fore)
        {
            Button button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 34),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }

    internal sealed class GpuUsageReader : IDisposable
    {
        private readonly List<PerformanceCounter> counters = new List<PerformanceCounter>();

        public GpuUsageReader()
        {
            try
            {
                PerformanceCounterCategory category = new PerformanceCounterCategory("GPU Engine");
                foreach (string instance in category.GetInstanceNames())
                {
                    if (instance.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    PerformanceCounter counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                    try { counter.NextValue(); counters.Add(counter); }
                    catch { counter.Dispose(); }
                }
            }
            catch { }
        }

        public float NextValue()
        {
            float total = 0;
            foreach (PerformanceCounter counter in counters)
            {
                try { total += counter.NextValue(); } catch { }
            }
            return Math.Max(0, Math.Min(100, total));
        }

        public void Dispose()
        {
            foreach (PerformanceCounter counter in counters) counter.Dispose();
            counters.Clear();
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly AppConfig config;
        private readonly PlatformProfile platform;
        private readonly BoostDial dial;
        private readonly Label stateText;
        private readonly Label gameName;
        private readonly Label gamePath;
        private readonly Label powerStatus;
        private readonly Label modeStatus;
        private readonly Label captureStatus;
        private readonly Label activityText;
        private readonly ToggleSwitch autoSwitch;
        private readonly CheckBox launchCheck;
        private readonly Button browseButton;
        private readonly Button libraryButton;
        private readonly Button launchButton;
        private readonly Button adminButton;
        private readonly MetricBar cpuBar;
        private readonly MetricBar ramBar;
        private readonly MetricBar gpuBar;
        private readonly Timer monitor;
        private readonly PerformanceCounter cpuCounter;
        private readonly GpuUsageReader gpuReader;
        private readonly NotifyIcon tray;
        private bool working;
        private bool allowClose;
        private int missingGameTicks;
        private DetectedGame detectedGame;

        public MainForm()
        {
            config = Storage.LoadConfig();
            platform = PlatformDetector.Detect();
            GameDetector.RefreshCatalog();
            Text = "Game Boost Pro";
            Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (appIcon != null) Icon = appIcon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 610);
            MinimumSize = new Size(900, 610);
            MaximumSize = new Size(900, 610);
            MaximizeBox = false;
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 10);

            Label brand = MakeLabel("GAME BOOST", 30, 23, 210, 30, 16, FontStyle.Bold, Palette.Text);
            Label pro = MakeLabel("PRO", 198, 26, 42, 22, 8, FontStyle.Bold, Palette.Back);
            pro.BackColor = Palette.Lime;
            pro.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(brand);
            Controls.Add(pro);
            pro.BringToFront();
            Controls.Add(MakeLabel("Performance control / reversible profile", 30, 52, 320, 20, 8, FontStyle.Regular, Palette.Muted));
            Controls.Add(MakeLabel(platform.Title, 390, 52, 315, 20, 8, FontStyle.Bold,
                platform.IsSupported ? Palette.Lime : Palette.Coral));

            adminButton = MakeButton(SystemTuner.IsAdmin() ? "ADMIN READY" : "ADMIN BOOST", 730, 25, 138, 30,
                SystemTuner.IsAdmin() ? Palette.SurfaceHigh : Palette.Amber,
                SystemTuner.IsAdmin() ? Palette.Lime : Palette.Back);
            adminButton.Enabled = !SystemTuner.IsAdmin();
            adminButton.Click += RestartElevated;
            Controls.Add(adminButton);

            Panel line = new Panel { BackColor = Palette.Line, Location = new Point(30, 86), Size = new Size(838, 1) };
            Controls.Add(line);

            dial = new BoostDial { Location = new Point(65, 118) };
            dial.BoostClick += delegate { ToggleBoost(false); };
            Controls.Add(dial);

            stateText = MakeLabel("", 62, 447, 326, 46, 10, FontStyle.Regular, Palette.Muted);
            stateText.TextAlign = ContentAlignment.TopCenter;
            Controls.Add(stateText);

            Panel divider = new Panel { BackColor = Palette.Line, Location = new Point(425, 116), Size = new Size(1, 375) };
            Controls.Add(divider);

            Controls.Add(MakeLabel("SMART GAME DETECTOR", 466, 117, 260, 28, 13, FontStyle.Bold, Palette.Text));
            Controls.Add(MakeLabel("ตรวจจับจากโปรเซสและคลัง Steam / Epic / Riot", 466, 146, 380, 22, 8, FontStyle.Regular, Palette.Muted));

            gameName = MakeLabel("พร้อมตรวจจับเกมอัตโนมัติ", 466, 185, 374, 24, 11, FontStyle.Bold, Palette.Text);
            gamePath = MakeLabel("CS2 / PUBG / VALORANT / และเกมจากคลังที่พบ", 466, 211, 374, 38, 8, FontStyle.Regular, Palette.Muted);
            gamePath.AutoEllipsis = true;
            Controls.Add(gameName);
            Controls.Add(gamePath);

            browseButton = MakeButton("เพิ่มเกม...", 466, 258, 100, 36, Palette.SurfaceHigh, Palette.Text);
            libraryButton = MakeButton("GAME LIBRARY", 575, 258, 124, 36, Palette.SurfaceHigh, Palette.Text);
            launchButton = MakeButton("เปิดเกม", 708, 258, 136, 36, Palette.Lime, Palette.Back);
            browseButton.Click += BrowseGame;
            libraryButton.Click += OpenGameLibrary;
            launchButton.Click += delegate { LaunchGame(); };
            Controls.Add(browseButton);
            Controls.Add(libraryButton);
            Controls.Add(launchButton);

            Controls.Add(MakeLabel("DETECT + BOOST อัตโนมัติ", 466, 320, 275, 24, 9, FontStyle.Bold, Palette.Text));
            Controls.Add(MakeLabel("พบเกมแล้วเร่งให้ทันที และคืนค่าเมื่อเกมปิด", 466, 345, 310, 21, 8, FontStyle.Regular, Palette.Muted));
            autoSwitch = new ToggleSwitch { Location = new Point(792, 323) };
            autoSwitch.ValueChanged += delegate
            {
                config.AutoMode = autoSwitch.Value;
                Storage.SaveConfig(config);
            };
            Controls.Add(autoSwitch);

            launchCheck = new CheckBox();
            launchCheck.Text = "กด BOOST แล้วเปิดเกมที่เลือก";
            launchCheck.Location = new Point(466, 382);
            launchCheck.Size = new Size(290, 28);
            launchCheck.ForeColor = Palette.Muted;
            launchCheck.FlatStyle = FlatStyle.Flat;
            launchCheck.CheckedChanged += delegate
            {
                config.LaunchOnBoost = launchCheck.Checked;
                Storage.SaveConfig(config);
            };
            Controls.Add(launchCheck);

            Controls.Add(MakeLabel("PROTECTED  Discord / TeamSpeak 3 / NitroSense + Acer", 466, 410, 390, 18, 7, FontStyle.Bold, Palette.Amber));

            Controls.Add(MakeLabel("สิ่งที่กำลังควบคุม", 466, 428, 220, 24, 9, FontStyle.Bold, Palette.Text));
            powerStatus = MakeStatusLabel(466, 457);
            modeStatus = MakeStatusLabel(610, 457);
            captureStatus = MakeStatusLabel(748, 457);
            Controls.Add(powerStatus);
            Controls.Add(modeStatus);
            Controls.Add(captureStatus);

            Panel footer = new Panel { BackColor = Palette.Surface, Location = new Point(0, 512), Size = new Size(900, 98) };
            Controls.Add(footer);
            cpuBar = new MetricBar { Caption = "CPU", Location = new Point(30, 22) };
            ramBar = new MetricBar { Caption = "MEMORY", Location = new Point(236, 22) };
            gpuBar = new MetricBar { Caption = "GPU 3D", Location = new Point(442, 22) };
            footer.Controls.Add(cpuBar);
            footer.Controls.Add(ramBar);
            footer.Controls.Add(gpuBar);
            activityText = MakeLabel("พร้อมทำงาน", 650, 15, 218, 58, 8, FontStyle.Regular, Palette.Muted);
            activityText.TextAlign = ContentAlignment.MiddleLeft;
            footer.Controls.Add(activityText);

            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            try { cpuCounter.NextValue(); } catch { }
            gpuReader = new GpuUsageReader();

            monitor = new Timer();
            monitor.Interval = 1500;
            monitor.Tick += MonitorTick;
            monitor.Start();

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("เปิด Game Boost Pro", null, delegate { ShowFromTray(); });
            trayMenu.Items.Add("สลับ Game Mode", null, delegate { ToggleBoost(false); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("ออกจากโปรแกรม", null, ExitApplication);
            tray = new NotifyIcon();
            tray.Icon = appIcon != null ? (Icon)appIcon.Clone() : SystemIcons.Application;
            tray.Text = "Game Boost Pro";
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { ShowFromTray(); };
            tray.Visible = true;

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    tray.ShowBalloonTip(1200, "Game Boost Pro", "ยังทำงานอยู่ที่มุมจอ", ToolTipIcon.Info);
                }
            };
            FormClosing += OnFormClosing;
            FormClosed += delegate { gpuReader.Dispose(); cpuCounter.Dispose(); };

            autoSwitch.Value = config.AutoMode;
            launchCheck.Checked = config.LaunchOnBoost;
            if (!platform.IsSupported)
            {
                config.AutoMode = false;
                autoSwitch.Value = false;
                autoSwitch.Enabled = false;
                dial.Enabled = false;
                Storage.SaveConfig(config);
            }
            RefreshGameProfile();
            RefreshState(platform.IsSupported
                ? platform.Detail
                : platform.Detail + " / รองรับเฉพาะ Acer + NitroSense และ Desktop PC");
        }

        private void BrowseGame(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "เลือกไฟล์เกม";
                dialog.Filter = "ไฟล์เกม (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                dialog.InitialDirectory = !String.IsNullOrWhiteSpace(config.GamePath) && File.Exists(config.GamePath)
                    ? Path.GetDirectoryName(config.GamePath)
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    config.GamePath = dialog.FileName;
                    config.LibraryGameName = "";
                    config.LibraryGameDirectory = "";
                    config.LibraryLaunchTarget = "";
                    Storage.SaveConfig(config);
                    RefreshGameProfile();
                    RefreshState("บันทึกโปรไฟล์ " + Path.GetFileNameWithoutExtension(config.GamePath) + " แล้ว");
                }
            }
        }

        private void OpenGameLibrary(object sender, EventArgs e)
        {
            GameDetector.RefreshCatalog();
            using (GameLibraryForm dialog = new GameLibraryForm(GameDetector.GetCatalog()))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedGame == null) return;
                config.LibraryGameName = dialog.SelectedGame.DisplayName;
                config.LibraryGameDirectory = dialog.SelectedGame.DirectoryPath;
                config.LibraryLaunchTarget = dialog.SelectedGame.LaunchTarget;
                config.GamePath = "";
                Storage.SaveConfig(config);
                RefreshGameProfile();
                RefreshState("เลือก " + config.LibraryGameName + " จาก " + dialog.SelectedGame.Source + " แล้ว");
            }
        }

        private void LaunchGame()
        {
            string target = !String.IsNullOrWhiteSpace(config.LibraryGameName)
                ? config.LibraryLaunchTarget : config.GamePath;
            if (String.IsNullOrWhiteSpace(target) ||
                (target.IndexOf("://", StringComparison.OrdinalIgnoreCase) < 0 && !File.Exists(target)))
            {
                ShowError("เกมนี้ไม่มีคำสั่งเปิดอัตโนมัติ กรุณาเปิดจาก Launcher ตามปกติ");
                return;
            }
            try
            {
                ProcessStartInfo info;
                if (SystemTuner.IsAdmin())
                    info = new ProcessStartInfo("explorer.exe", "\"" + target + "\"");
                else
                {
                    info = new ProcessStartInfo(target);
                    info.UseShellExecute = true;
                    if (File.Exists(target)) info.WorkingDirectory = Path.GetDirectoryName(target);
                }
                Process.Start(info);
                RefreshState("กำลังเปิด " + GetConfiguredGameName());
            }
            catch (Exception ex) { ShowError("เปิดเกมไม่สำเร็จ: " + ex.Message); }
        }

        private void ToggleBoost(bool autoTriggered)
        {
            if (working) return;
            if (!platform.IsSupported)
            {
                ShowError(platform.Detail + "\n\nรุ่นนี้รองรับเฉพาะ Acer Laptop ที่มี NitroSense และ Desktop PC");
                return;
            }
            working = true;
            dial.Busy = true;
            SetControlsEnabled(false);

            try
            {
                if (Storage.HasState())
                {
                    SystemTuner.Disable();
                    RefreshState(autoTriggered ? "เกมปิดแล้ว คืนค่าเครื่องเรียบร้อย" : "คืนค่าเครื่องกลับเป็นปกติแล้ว");
                }
                else
                {
                    DetectedGame candidate = detectedGame ?? GameDetector.FindRunningGame(config.GamePath);
                    string targetPath = candidate != null && !String.IsNullOrWhiteSpace(candidate.ExePath)
                        ? candidate.ExePath : config.GamePath;
                    int processId = candidate != null && candidate.Process != null ? candidate.Process.Id : 0;
                    SystemTuner.Enable(targetPath, autoTriggered, processId, platform);
                    if (config.LaunchOnBoost && !autoTriggered) LaunchGame();
                    RefreshState(autoTriggered && candidate != null
                        ? "ตรวจพบ " + candidate.DisplayName + " / เปิด Boost 6 ระบบแล้ว"
                        : "Game Mode พร้อมลุย / รอจับโปรเซสเกม");
                }
            }
            catch (Exception ex)
            {
                RefreshState("ตรวจพบปัญหา กรุณาดูข้อความแจ้งเตือน");
                ShowError(ex.Message);
            }
            finally
            {
                working = false;
                dial.Busy = false;
                SetControlsEnabled(true);
                UpdateStateVisuals();
            }
        }

        private void MonitorTick(object sender, EventArgs e)
        {
            UpdateMetrics();
            if (working) return;

            detectedGame = GameDetector.FindRunningGame(config.GamePath);
            bool running = detectedGame != null;
            BoostState state = null;
            try { state = Storage.LoadState(); } catch { }

            if (running)
            {
                missingGameTicks = 0;
                ShowDetectedGame(detectedGame);
                if (config.AutoMode && !Storage.HasState())
                {
                    ToggleBoost(true);
                    return;
                }
                if (state != null)
                {
                    if (!String.IsNullOrWhiteSpace(detectedGame.ExePath))
                        SystemTuner.AttachGamePath(state, detectedGame.ExePath);
                    if (!state.ProcessTuningApplied || state.GameProcessId != detectedGame.Process.Id)
                    {
                        state.GameProcessId = detectedGame.Process.Id;
                        Storage.SaveState(state);
                    }
                    SystemTuner.ApplyGamePriority(state);
                }
            }
            else if (state != null && state.AutoTriggered)
            {
                missingGameTicks++;
                if (missingGameTicks >= 2)
                {
                    missingGameTicks = 0;
                    ToggleBoost(true);
                    return;
                }
            }
            else
            {
                RefreshGameProfile();
            }

            launchButton.Text = running ? "กำลังเล่น" : "เปิดเกม";
            launchButton.Enabled = !running && !working && CanLaunchConfiguredGame();
        }

        private void UpdateMetrics()
        {
            try { cpuBar.Value = cpuCounter.NextValue(); } catch { }
            try { gpuBar.Value = gpuReader.NextValue(); } catch { }
            MEMORYSTATUSEX memory = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memory)) ramBar.Value = memory.dwMemoryLoad;
        }

        private void RefreshGameProfile()
        {
            bool manualValid = !String.IsNullOrWhiteSpace(config.GamePath) && File.Exists(config.GamePath);
            bool libraryValid = !String.IsNullOrWhiteSpace(config.LibraryGameName) &&
                !String.IsNullOrWhiteSpace(config.LibraryGameDirectory) && Directory.Exists(config.LibraryGameDirectory);
            gameName.ForeColor = Palette.Text;
            gameName.Text = libraryValid ? "พร้อมเล่น: " + config.LibraryGameName : "พร้อมตรวจจับเกมอัตโนมัติ";
            gamePath.Text = "พบเกมในคลัง " + GameDetector.InstalledCount + " รายการ" +
                (manualValid ? " / สำรอง: " + Path.GetFileNameWithoutExtension(config.GamePath) :
                libraryValid ? " / " + config.LibraryGameDirectory : "");
            launchButton.Enabled = CanLaunchConfiguredGame();
        }

        private string GetConfiguredGameName()
        {
            if (!String.IsNullOrWhiteSpace(config.LibraryGameName)) return config.LibraryGameName;
            return String.IsNullOrWhiteSpace(config.GamePath)
                ? "เกม" : Path.GetFileNameWithoutExtension(config.GamePath);
        }

        private bool CanLaunchConfiguredGame()
        {
            if (!String.IsNullOrWhiteSpace(config.LibraryGameName))
                return !String.IsNullOrWhiteSpace(config.LibraryLaunchTarget);
            return !String.IsNullOrWhiteSpace(config.GamePath) && File.Exists(config.GamePath);
        }

        private void ShowDetectedGame(DetectedGame game)
        {
            gameName.Text = "ตรวจพบ: " + game.DisplayName;
            gameName.ForeColor = Palette.Lime;
            string detail = game.Source + " / " + Path.GetFileName(game.ExePath ?? "กำลังทำงาน");
            gamePath.Text = detail;
            activityText.Text = Storage.HasState()
                ? game.DisplayName + " กำลังใช้ HighQoS + AboveNormal"
                : "พบ " + game.DisplayName + " / กำลังเตรียม Boost";
        }

        private void RefreshState(string activity)
        {
            activityText.Text = activity;
            UpdateStateVisuals();
        }

        private void UpdateStateVisuals()
        {
            bool active = Storage.HasState();
            dial.Active = active;
            stateText.Text = active
                ? "เครื่องอยู่ในโหมดเล่นเกม\nกดอีกครั้งเพื่อคืนค่าทุกอย่าง"
                : "เครื่องอยู่ในโหมดปกติ\nค่าทุกอย่างพร้อมถูกจดจำก่อน Boost";
            SetStatus(powerStatus, active, active ? "POWER MAX" : "POWER NORMAL");
            SetStatus(modeStatus, active, active ? "HIGHQOS ON" : "HIGHQOS READY");
            SetStatus(captureStatus, active, active ? "CAPTURE OFF" : "CAPTURE NORMAL");
            tray.Text = active ? "Game Boost Pro - Game Mode ON" : "Game Boost Pro - Normal";
        }

        private void SetControlsEnabled(bool enabled)
        {
            browseButton.Enabled = enabled;
            libraryButton.Enabled = enabled;
            autoSwitch.Enabled = enabled && platform.IsSupported;
            launchCheck.Enabled = enabled;
            adminButton.Enabled = enabled && !SystemTuner.IsAdmin();
            launchButton.Enabled = enabled && CanLaunchConfiguredGame();
        }

        private void RestartElevated(object sender, EventArgs e)
        {
            if (Storage.HasState())
            {
                ShowError("กรุณากด RESTORE ก่อนเปลี่ยนเป็น Admin Boost");
                return;
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(Application.ExecutablePath);
                info.UseShellExecute = true;
                info.Verb = "runas";
                Process.Start(info);
                allowClose = true;
                tray.Visible = false;
                Close();
            }
            catch
            {
                RefreshState("ยกเลิก Admin Boost / ยังใช้โหมดปกติได้");
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApplication(object sender, EventArgs e)
        {
            if (Storage.HasState())
            {
                DialogResult result = MessageBox.Show(this,
                    "Game Mode ยังเปิดอยู่ ต้องการคืนค่าเครื่องก่อนออกหรือไม่?",
                    "Game Boost Pro", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes)
                {
                    try { SystemTuner.Disable(); }
                    catch (Exception ex) { ShowError(ex.Message); return; }
                }
            }
            allowClose = true;
            tray.Visible = false;
            Close();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (allowClose) return;
            if (!config.AutoMode && !Storage.HasState())
            {
                tray.Visible = false;
                return;
            }
            e.Cancel = true;
            Hide();
            tray.ShowBalloonTip(1200, "Game Boost Pro", "โปรแกรมยังทำงานอยู่ กดไอคอนเพื่อเปิดอีกครั้ง", ToolTipIcon.Info);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Game Boost Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static Label MakeLabel(string text, int x, int y, int width, int height, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                BackColor = Color.Transparent
            };
        }

        private static Button MakeButton(string text, int x, int y, int width, int height, Color back, Color fore)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.BackColor = back;
            button.ForeColor = fore;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private static Label MakeStatusLabel(int x, int y)
        {
            Label label = MakeLabel("", x, y, 126, 26, 7, FontStyle.Bold, Palette.Muted);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.BackColor = Palette.SurfaceHigh;
            return label;
        }

        private static void SetStatus(Label label, bool active, string text)
        {
            label.Text = text;
            label.ForeColor = active ? Palette.Lime : Palette.Muted;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    }

    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            bool created;
            using (System.Threading.Mutex instance = new System.Threading.Mutex(true, @"Local\Codex.GameBoostPro", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Game Boost Pro เปิดอยู่แล้วที่มุมจอ", "Game Boost Pro",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MainForm form = new MainForm();
                Application.Run(form);
            }
        }
    }
}
