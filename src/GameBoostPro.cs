using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Game Boost Pro")]
[assembly: System.Reflection.AssemblyDescription("Smart game detection and reversible Windows gaming optimization")]
[assembly: System.Reflection.AssemblyProduct("Game Boost Pro")]
[assembly: System.Reflection.AssemblyCompany("Local PC Tools")]
[assembly: System.Reflection.AssemblyVersion("3.1.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.1.1.0")]

namespace GameBoostPro
{
    internal static class Palette
    {
        public static readonly Color Back = Color.FromArgb(17, 19, 18);
        public static readonly Color Surface = Color.FromArgb(27, 30, 29);
        public static readonly Color SurfaceHigh = Color.FromArgb(39, 43, 41);
        public static readonly Color Line = Color.FromArgb(67, 73, 70);
        public static readonly Color Text = Color.FromArgb(239, 242, 240);
        public static readonly Color Muted = Color.FromArgb(158, 169, 164);
        public static readonly Color Lime = Color.FromArgb(199, 243, 107);
        public static readonly Color Cyan = Color.FromArgb(92, 207, 219);
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
            Process[] processes = new Process[0];
            try
            {
                processes = Process.GetProcessesByName("NitroSense");
                if (processes.Length > 0) return true;
            }
            catch { }
            finally
            {
                foreach (Process process in processes) process.Dispose();
            }
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
        public string LibraryLaunchArguments { get; set; }
        public bool EnableWindowsGameMode { get; set; }
        public bool DisableBackgroundCapture { get; set; }
        public bool PreferHighPerformanceGpu { get; set; }
        public bool UseAboveNormalPriority { get; set; }
        public bool UseHighQos { get; set; }
        public bool UseDynamicPriorityBoost { get; set; }

        public AppConfig()
        {
            Version = 4;
            AutoMode = true;
            ResetAdvancedDefaults();
        }

        public void ResetAdvancedDefaults()
        {
            EnableWindowsGameMode = true;
            DisableBackgroundCapture = true;
            PreferHighPerformanceGpu = true;
            UseAboveNormalPriority = true;
            UseHighQos = true;
            UseDynamicPriorityBoost = true;
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
        public bool PreferHighPerformanceGpu { get; set; }
        public bool UseAboveNormalPriority { get; set; }
        public bool UseHighQos { get; set; }
        public bool UseDynamicPriorityBoost { get; set; }
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
        public string LaunchArguments { get; set; }
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
        private static readonly object CatalogLock = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static volatile bool catalogLoaded;
        private static DateTime lastDeepScanUtc = DateTime.MinValue;
        private static readonly object RunningGameCacheLock = new object();
        private static int cachedGameProcessId;
        private static string cachedGameProcessName = "";
        private static string cachedGameDisplayName = "";
        private static string cachedGameExePath = "";
        private static string cachedGameSource = "";
        private static readonly object ProcessAccessLock = new object();
        private static readonly Dictionary<int, Tuple<string, DateTime>> InaccessibleProcessUntilUtc =
            new Dictionary<int, Tuple<string, DateTime>>();
        private static readonly TimeSpan ProcessAccessRetryDelay = TimeSpan.FromSeconds(30);

        public static int InstalledCount
        {
            get { lock (CatalogLock) return Catalog.Count; }
        }

        public static bool IsCatalogLoaded
        {
            get { return catalogLoaded; }
        }

        public static string InstalledSummary
        {
            get
            {
                EnsureCatalog();
                List<string> names = new List<string>();
                lock (CatalogLock)
                    foreach (GameInstall item in Catalog)
                        if (!names.Contains(item.DisplayName)) names.Add(item.DisplayName);
                return String.Join(" / ", names.ToArray());
            }
        }

        public static List<GameInstall> GetCatalog()
        {
            EnsureCatalog();
            lock (CatalogLock) return new List<GameInstall>(Catalog);
        }

        public static void RefreshCatalog()
        {
            lock (CatalogLock)
            {
                Catalog.Clear();
                DiscoverSteam();
                DiscoverEpic();
                DiscoverRiot();
                catalogLoaded = true;
            }
        }

        public static DetectedGame FindRunningGame(string manualPath)
        {
            EnsureCatalog();
            DetectedGame cached = TryGetCachedRunningGame(manualPath);
            if (cached != null) return cached;

            Process[] processes = Process.GetProcesses();
            DetectedGame result = null;
            try
            {
                int currentProcessId;
                using (Process currentProcess = Process.GetCurrentProcess()) currentProcessId = currentProcess.Id;
                string manualName = String.IsNullOrWhiteSpace(manualPath)
                    ? "" : Path.GetFileNameWithoutExtension(manualPath);

                foreach (Process process in processes)
                {
                    try
                    {
                        if (process.Id == currentProcessId) continue;
                        string processName = process.ProcessName;
                        if (SafetyPolicy.IsProtectedProcess(processName)) continue;
                        string knownName;
                        if (KnownProcesses.TryGetValue(processName, out knownName))
                        {
                            result = CreateDetectedGame(knownName, TryGetProcessPath(process), "KNOWN", process);
                            break;
                        }
                        if (!String.IsNullOrWhiteSpace(manualName) &&
                            String.Equals(processName, manualName, StringComparison.OrdinalIgnoreCase))
                        {
                            string path = TryGetProcessPath(process);
                            if (String.IsNullOrWhiteSpace(path) ||
                                String.Equals(path, manualPath, StringComparison.OrdinalIgnoreCase))
                            {
                                result = CreateDetectedGame(manualName, path, "MANUAL", process);
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (result == null && DateTime.UtcNow - lastDeepScanUtc >= TimeSpan.FromSeconds(8))
                {
                    lastDeepScanUtc = DateTime.UtcNow;
                    List<GameInstall> catalog;
                    lock (CatalogLock) catalog = new List<GameInstall>(Catalog);

                    foreach (Process process in processes)
                    {
                        try
                        {
                            string processName = process.ProcessName;
                            if (SafetyPolicy.IsProtectedProcess(processName) || ExcludedProcesses.Contains(processName)) continue;
                            string exePath = TryGetProcessPath(process);
                            if (String.IsNullOrWhiteSpace(exePath)) continue;
                            foreach (GameInstall install in catalog)
                            {
                                if (!IsUnderDirectory(exePath, install.DirectoryPath)) continue;
                                result = CreateDetectedGame(install.DisplayName, exePath, install.Source, process);
                                break;
                            }
                            if (result != null) break;
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                foreach (Process process in processes)
                    if (result == null || result.Process == null || process.Id != result.Process.Id) process.Dispose();
            }
            if (result != null) CacheRunningGame(result);
            return result;
        }

        private static DetectedGame CreateDetectedGame(string name, string path, string source, Process process)
        {
            return new DetectedGame { DisplayName = name, ExePath = path, Source = source, Process = process };
        }

        private static DetectedGame TryGetCachedRunningGame(string manualPath)
        {
            int processId;
            string processName;
            string displayName;
            string exePath;
            string source;
            lock (RunningGameCacheLock)
            {
                processId = cachedGameProcessId;
                processName = cachedGameProcessName;
                displayName = cachedGameDisplayName;
                exePath = cachedGameExePath;
                source = cachedGameSource;
            }

            if (processId <= 0) return null;
            if (String.Equals(source, "MANUAL", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(exePath, manualPath, StringComparison.OrdinalIgnoreCase))
            {
                ClearRunningGameCache(processId);
                return null;
            }

            Process process = null;
            try
            {
                process = Process.GetProcessById(processId);
                if (process.HasExited || !String.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException();
                if (!String.IsNullOrWhiteSpace(exePath))
                {
                    string currentPath = TryGetProcessPath(process);
                    if (!String.Equals(currentPath, exePath, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException();
                }
                return CreateDetectedGame(displayName, exePath, source, process);
            }
            catch
            {
                if (process != null) process.Dispose();
                ClearRunningGameCache(processId);
                return null;
            }
        }

        private static void CacheRunningGame(DetectedGame game)
        {
            if (game == null || game.Process == null) return;
            try
            {
                lock (RunningGameCacheLock)
                {
                    cachedGameProcessId = game.Process.Id;
                    cachedGameProcessName = game.Process.ProcessName;
                    cachedGameDisplayName = game.DisplayName ?? "";
                    cachedGameExePath = game.ExePath ?? "";
                    cachedGameSource = game.Source ?? "";
                }
            }
            catch { ClearRunningGameCache(0); }
        }

        private static void ClearRunningGameCache(int expectedProcessId)
        {
            lock (RunningGameCacheLock)
            {
                if (expectedProcessId > 0 && cachedGameProcessId != expectedProcessId) return;
                cachedGameProcessId = 0;
                cachedGameProcessName = "";
                cachedGameDisplayName = "";
                cachedGameExePath = "";
                cachedGameSource = "";
            }
        }

        private static void EnsureCatalog()
        {
            lock (CatalogLock)
                if (!catalogLoaded) RefreshCatalog();
        }

        private static string TryGetProcessPath(Process process)
        {
            int processId;
            string processName;
            try
            {
                processId = process.Id;
                processName = process.ProcessName;
            }
            catch { return ""; }

            DateTime now = DateTime.UtcNow;
            lock (ProcessAccessLock)
            {
                Tuple<string, DateTime> retry;
                if (InaccessibleProcessUntilUtc.TryGetValue(processId, out retry))
                {
                    if (String.Equals(retry.Item1, processName, StringComparison.OrdinalIgnoreCase) &&
                        retry.Item2 > now) return "";
                    InaccessibleProcessUntilUtc.Remove(processId);
                }
            }

            try
            {
                string path = process.MainModule.FileName;
                lock (ProcessAccessLock) InaccessibleProcessUntilUtc.Remove(processId);
                return path;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 5)
                {
                    lock (ProcessAccessLock)
                    {
                        if (InaccessibleProcessUntilUtc.Count >= 256)
                        {
                            List<int> expired = new List<int>();
                            foreach (KeyValuePair<int, Tuple<string, DateTime>> item in InaccessibleProcessUntilUtc)
                                if (item.Value.Item2 <= now) expired.Add(item.Key);
                            foreach (int id in expired) InaccessibleProcessUntilUtc.Remove(id);
                            if (InaccessibleProcessUntilUtc.Count >= 256) InaccessibleProcessUntilUtc.Clear();
                        }
                        InaccessibleProcessUntilUtc[processId] =
                            Tuple.Create(processName, now.Add(ProcessAccessRetryDelay));
                    }
                }
                return "";
            }
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

        private static void AddInstall(string name, string path, string source, string launchTarget,
            string launchArguments = "")
        {
            if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            path = NormalizeDirectory(path);
            lock (CatalogLock)
            {
                foreach (GameInstall item in Catalog)
                    if (String.Equals(item.DirectoryPath, path, StringComparison.OrdinalIgnoreCase)) return;
                Catalog.Add(new GameInstall
                {
                    DisplayName = name,
                    DirectoryPath = path,
                    Source = source,
                    LaunchTarget = launchTarget ?? "",
                    LaunchArguments = launchArguments ?? ""
                });
            }
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
                        string client = Convert.ToString(clients[path]).Replace('/', Path.DirectorySeparatorChar);
                        AddInstall("VALORANT", directory, "RIOT", File.Exists(client) ? client : "",
                            "--launch-product=valorant --launch-patchline=live");
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
        public static readonly string AppDir = ResolveAppDirectory();
        public static readonly string ConfigPath = Path.Combine(AppDir, "config-pro.json");
        public static readonly string StatePath = Path.Combine(AppDir, "state-pro.json");
        public static readonly string LegacyStatePath = Path.Combine(AppDir, "state.json");
        private static readonly JavaScriptSerializer ConfigJson = new JavaScriptSerializer();
        private static readonly JavaScriptSerializer StateJson = new JavaScriptSerializer();
        private static readonly object StateLock = new object();
        private static bool statePresenceLoaded;
        private static bool currentStateExists;
        private static bool legacyStateExists;
        private static bool stateContentLoaded;
        private static BoostState cachedState;

        private static string ResolveAppDirectory()
        {
            string testDirectory = AppDomain.CurrentDomain.GetData("GameBoostPro.TestAppDirectory") as string;
            if (!String.IsNullOrWhiteSpace(testDirectory) && Path.IsPathRooted(testDirectory))
                return Path.GetFullPath(testDirectory);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGameBoost");
        }

        public static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string raw = File.ReadAllText(ConfigPath);
                    AppConfig config = ConfigJson.Deserialize<AppConfig>(raw);
                    Dictionary<string, object> fields = ConfigJson.Deserialize<Dictionary<string, object>>(raw);
                    if (!fields.ContainsKey("Version") || config.Version < 4)
                    {
                        config.Version = 4;
                        if (!fields.ContainsKey("AutoMode")) config.AutoMode = true;
                        config.ResetAdvancedDefaults();
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
            WriteAtomic(ConfigPath, ConfigJson.Serialize(config));
        }

        public static BoostState LoadState()
        {
            lock (StateLock)
            {
                EnsureStatePresence();
                if (!currentStateExists) return null;
                if (!stateContentLoaded)
                {
                    cachedState = StateJson.Deserialize<BoostState>(File.ReadAllText(StatePath));
                    stateContentLoaded = true;
                }
                return cachedState;
            }
        }

        public static BoostState LoadStateForRestore()
        {
            lock (StateLock)
            {
                if (!File.Exists(StatePath))
                {
                    currentStateExists = false;
                    statePresenceLoaded = true;
                    stateContentLoaded = true;
                    cachedState = null;
                    return null;
                }

                BoostState state = StateJson.Deserialize<BoostState>(File.ReadAllText(StatePath));
                currentStateExists = true;
                statePresenceLoaded = true;
                stateContentLoaded = true;
                cachedState = state;
                return state;
            }
        }

        public static void SaveState(BoostState state)
        {
            lock (StateLock)
            {
                try
                {
                    Directory.CreateDirectory(AppDir);
                    WriteAtomic(StatePath, StateJson.Serialize(state));
                    cachedState = state;
                    stateContentLoaded = true;
                    currentStateExists = true;
                    statePresenceLoaded = true;
                }
                catch
                {
                    cachedState = null;
                    stateContentLoaded = false;
                    statePresenceLoaded = false;
                    throw;
                }
            }
        }

        public static bool HasState()
        {
            lock (StateLock)
            {
                EnsureStatePresence();
                return currentStateExists || legacyStateExists;
            }
        }

        public static bool HasCurrentState()
        {
            lock (StateLock)
            {
                EnsureStatePresence();
                return currentStateExists;
            }
        }

        public static bool HasLegacyState()
        {
            lock (StateLock)
            {
                EnsureStatePresence();
                return legacyStateExists;
            }
        }

        public static void DeleteState()
        {
            lock (StateLock)
            {
                if (File.Exists(StatePath)) File.Delete(StatePath);
                cachedState = null;
                stateContentLoaded = true;
                currentStateExists = false;
                statePresenceLoaded = true;
            }
        }

        public static void DeleteLegacyState()
        {
            lock (StateLock)
            {
                if (File.Exists(LegacyStatePath)) File.Delete(LegacyStatePath);
                legacyStateExists = false;
                statePresenceLoaded = true;
            }
        }

        private static void EnsureStatePresence()
        {
            if (statePresenceLoaded) return;
            currentStateExists = File.Exists(StatePath);
            legacyStateExists = File.Exists(LegacyStatePath);
            statePresenceLoaded = true;
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

        private static readonly Tuple<string, string, object, RegistryValueKind>[] GameModeTweaks =
        {
            Tuple.Create(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", (object)1, RegistryValueKind.DWord),
            Tuple.Create(@"Software\Microsoft\GameBar", "AllowAutoGameMode", (object)1, RegistryValueKind.DWord)
        };

        private static readonly Tuple<string, string, object, RegistryValueKind>[] CaptureTweaks =
        {
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
            Match existing = Regex.Match(schemes,
                @"([0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}).*\((?:Game Boost Pro Ultimate|Ultimate Performance)\)",
                RegexOptions.IgnoreCase);
            if (existing.Success) return existing.Groups[1].Value;

            string output = RunPowerCfg("/duplicatescheme " + UltimateGuid);
            Match duplicated = Regex.Match(output, @"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
            if (!duplicated.Success)
                throw new InvalidOperationException("สร้าง Ultimate Performance power plan ไม่สำเร็จ");
            RunPowerCfg("/changename " + duplicated.Value + " \"Game Boost Pro Ultimate\"");
            return duplicated.Value;
        }

        public static BoostState Enable(string gamePath, bool autoTriggered, int processId,
            PlatformProfile platform, AppConfig options)
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
                PreferHighPerformanceGpu = options.PreferHighPerformanceGpu,
                UseAboveNormalPriority = options.UseAboveNormalPriority,
                UseHighQos = options.UseHighQos,
                UseDynamicPriorityBoost = options.UseDynamicPriorityBoost,
                Registry = new List<RegistrySnapshot>()
            };

            List<Tuple<string, string, object, RegistryValueKind>> enabledTweaks = GetEnabledTweaks(options);
            foreach (Tuple<string, string, object, RegistryValueKind> tweak in enabledTweaks)
                state.Registry.Add(Capture(tweak.Item1, tweak.Item2));

            if (options.PreferHighPerformanceGpu && !String.IsNullOrWhiteSpace(gamePath))
                state.Registry.Add(Capture(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath));

            Storage.SaveState(state);
            try
            {
                RunPowerCfg("/S " + targetPowerGuid);
                PowerPlan active = GetActivePowerPlan();
                if (!String.Equals(active.Guid, targetPowerGuid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Windows ไม่ยอมเปลี่ยน Power Plan เป็นโหมดประสิทธิภาพสูง");

                foreach (Tuple<string, string, object, RegistryValueKind> tweak in enabledTweaks)
                    SetRegistry(tweak.Item1, tweak.Item2, tweak.Item3, tweak.Item4);

                if (options.PreferHighPerformanceGpu && !String.IsNullOrWhiteSpace(gamePath))
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
            if (Storage.HasCurrentState())
            {
                BoostState state = Storage.LoadStateForRestore();
                if (state == null) throw new InvalidOperationException("ข้อมูลคืนค่าเสียหาย กรุณาอย่าลบไฟล์สถานะ");

                if (state.GameProcessId > 0 && state.ProcessTuningApplied)
                {
                    try
                    {
                        using (Process p = Process.GetProcessById(state.GameProcessId))
                        {
                            if (!String.IsNullOrWhiteSpace(state.PreviousPriority))
                                p.PriorityClass = (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), state.PreviousPriority);
                            SetProcessPriorityBoost(p.Handle, state.PreviousPriorityBoostDisabled);
                            POWER_THROTTLING_STATE previous = new POWER_THROTTLING_STATE();
                            previous.Version = 1;
                            previous.ControlMask = state.HadPowerThrottleState ? state.PreviousThrottleControl : 0;
                            previous.StateMask = state.HadPowerThrottleState ? state.PreviousThrottleState : 0;
                            SetProcessInformation(p.Handle, ProcessPowerThrottling, ref previous, Marshal.SizeOf(previous));
                        }
                    }
                    catch { }
                }

                RunPowerCfg("/S " + state.PreviousPowerGuid);
                foreach (RegistrySnapshot item in state.Registry) Restore(item);
                Storage.DeleteState();
                return;
            }

            if (Storage.HasLegacyState())
            {
                RestoreLegacyState();
                return;
            }

            throw new InvalidOperationException("ไม่พบข้อมูลเดิมสำหรับคืนค่า");
        }

        public static bool ApplyGamePriority(BoostState state)
        {
            return ApplyGamePriority(state, state == null ? 0 : state.GameProcessId);
        }

        public static bool ApplyGamePriority(BoostState state, int detectedProcessId)
        {
            if (state == null) return false;
            if (!state.UseAboveNormalPriority && !state.UseHighQos && !state.UseDynamicPriorityBoost) return true;
            Process game = null;
            int targetProcessId = detectedProcessId > 0 ? detectedProcessId : state.GameProcessId;
            if (targetProcessId > 0)
            {
                try { game = Process.GetProcessById(targetProcessId); }
                catch { game = null; }
            }
            if (game == null) game = FindGameProcess(state.GamePath);
            if (game == null) return false;

            try
            {
                if (!state.ProcessTuningApplied || state.GameProcessId != game.Id)
                {
                    int gameProcessId = game.Id;
                    string previousPriority = game.PriorityClass.ToString();
                    bool previousPriorityBoostDisabled = false;
                    GetProcessPriorityBoost(game.Handle, out previousPriorityBoostDisabled);

                    POWER_THROTTLING_STATE previous = new POWER_THROTTLING_STATE();
                    previous.Version = 1;
                    bool hadPowerThrottleState = GetProcessInformation(game.Handle, ProcessPowerThrottling,
                        ref previous, Marshal.SizeOf(previous));

                    if (state.UseAboveNormalPriority)
                        game.PriorityClass = ProcessPriorityClass.AboveNormal;
                    if (state.UseDynamicPriorityBoost)
                        SetProcessPriorityBoost(game.Handle, false);
                    if (state.UseHighQos)
                    {
                        POWER_THROTTLING_STATE highQos = new POWER_THROTTLING_STATE();
                        highQos.Version = 1;
                        highQos.ControlMask = PowerThrottlingExecutionSpeed | PowerThrottlingIgnoreTimerResolution;
                        highQos.StateMask = 0;
                        SetProcessInformation(game.Handle, ProcessPowerThrottling, ref highQos, Marshal.SizeOf(highQos));
                    }

                    state.GameProcessId = gameProcessId;
                    state.PreviousPriority = previousPriority;
                    state.PreviousPriorityBoostDisabled = previousPriorityBoostDisabled;
                    state.HadPowerThrottleState = hadPowerThrottleState;
                    state.PreviousThrottleControl = hadPowerThrottleState ? previous.ControlMask : 0;
                    state.PreviousThrottleState = hadPowerThrottleState ? previous.StateMask : 0;
                    state.ProcessTuningApplied = true;
                    Storage.SaveState(state);
                }
                return true;
            }
            catch { return false; }
            finally { if (game != null) game.Dispose(); }
        }

        public static void AttachGamePath(BoostState state, string gamePath)
        {
            if (state == null || String.IsNullOrWhiteSpace(gamePath)) return;
            if (String.Equals(state.GamePath, gamePath, StringComparison.OrdinalIgnoreCase)) return;
            string previousGamePath = state.GamePath;
            if (!state.PreferHighPerformanceGpu)
            {
                state.GamePath = gamePath;
                Storage.SaveState(state);
                return;
            }

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
            RegistrySnapshot addedSnapshot = null;
            if (!captured)
            {
                addedSnapshot = Capture(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath);
                state.Registry.Add(addedSnapshot);
            }

            state.GamePath = gamePath;
            try
            {
                Storage.SaveState(state);
                SetRegistry(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath,
                    "GpuPreference=2;", RegistryValueKind.String);
            }
            catch
            {
                try
                {
                    if (addedSnapshot != null) state.Registry.Remove(addedSnapshot);
                    state.GamePath = previousGamePath;
                    Storage.SaveState(state);
                }
                catch { }
                throw;
            }
        }

        private static List<Tuple<string, string, object, RegistryValueKind>> GetEnabledTweaks(AppConfig options)
        {
            List<Tuple<string, string, object, RegistryValueKind>> tweaks =
                new List<Tuple<string, string, object, RegistryValueKind>>();
            if (options.EnableWindowsGameMode) tweaks.AddRange(GameModeTweaks);
            if (options.DisableBackgroundCapture) tweaks.AddRange(CaptureTweaks);
            return tweaks;
        }

        public static Process FindGameProcess(string gamePath)
        {
            if (String.IsNullOrWhiteSpace(gamePath)) return null;
            string processName = Path.GetFileNameWithoutExtension(gamePath);
            Process[] processes = Process.GetProcessesByName(processName);
            Process result = null;
            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        if (String.Equals(process.MainModule.FileName, gamePath, StringComparison.OrdinalIgnoreCase))
                        {
                            result = process;
                            break;
                        }
                    }
                    catch
                    {
                        result = process;
                        break;
                    }
                }
            }
            finally
            {
                foreach (Process process in processes)
                    if (!Object.ReferenceEquals(process, result)) process.Dispose();
            }
            return result;
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
            Storage.DeleteLegacyState();
        }
    }

    internal sealed class BoostDial : Control
    {
        private bool active;
        private bool busy;
        private bool hover;
        private int phase;
        private readonly Timer animation;
        private readonly Pen thinRingPen;
        private readonly Pen thickRingPen;
        private readonly Pen borderPen;
        private readonly Pen focusPen;
        private readonly SolidBrush shadowBrush;
        private readonly SolidBrush coreBrush;
        private readonly SolidBrush eyebrowBrush;
        private readonly SolidBrush actionBrush;
        private readonly SolidBrush hintBrush;
        private readonly Font eyebrowFont;
        private readonly Font actionFont;
        private readonly Font hintFont;

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
            thinRingPen = CreateRingPen(5f);
            thickRingPen = CreateRingPen(8f);
            borderPen = new Pen(Palette.Lime, 2f);
            focusPen = new Pen(Palette.Amber, 1f) { DashStyle = DashStyle.Dot };
            shadowBrush = new SolidBrush(Color.FromArgb(70, Color.Black));
            coreBrush = new SolidBrush(Palette.Surface);
            eyebrowBrush = new SolidBrush(Palette.Muted);
            actionBrush = new SolidBrush(Palette.Lime);
            hintBrush = new SolidBrush(Palette.Text);
            eyebrowFont = new Font("Segoe UI", 9, FontStyle.Bold);
            actionFont = new Font("Segoe UI Semibold", 25, FontStyle.Bold);
            hintFont = new Font("Segoe UI", 9);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animation.Dispose();
                thinRingPen.Dispose();
                thickRingPen.Dispose();
                borderPen.Dispose();
                focusPen.Dispose();
                shadowBrush.Dispose();
                coreBrush.Dispose();
                eyebrowBrush.Dispose();
                actionBrush.Dispose();
                hintBrush.Dispose();
                eyebrowFont.Dispose();
                actionFont.Dispose();
                hintFont.Dispose();
            }
            base.Dispose(disposing);
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
                Pen pen = i % 3 == 0 ? thickRingPen : thinRingPen;
                pen.Color = color;
                g.DrawArc(pen, ring, -90 + i * 15 + 2, 10);
            }

            RectangleF core = new RectangleF(57, 57, Width - 114, Height - 114);
            g.FillEllipse(shadowBrush, new RectangleF(core.X + 4, core.Y + 8, core.Width, core.Height));
            coreBrush.Color = hover ? Palette.SurfaceHigh : Palette.Surface;
            g.FillEllipse(coreBrush, core);
            borderPen.Color = Color.FromArgb(95, accent);
            g.DrawEllipse(borderPen, core);

            string eyebrow = busy ? "กำลังปรับระบบ" : (active ? "GAME MODE  ON" : "STANDBY");
            string action = active ? "RESTORE" : "BOOST";
            string hint = active ? "กลับสู่โหมดปกติ" : "กดเพื่อเร่งเครื่อง";
            actionBrush.Color = accent;
            DrawCentered(g, eyebrow, eyebrowFont, eyebrowBrush, 119);
            DrawCentered(g, action, actionFont, actionBrush, 145);
            DrawCentered(g, hint, hintFont, hintBrush, 187);

            if (Focused)
                g.DrawEllipse(focusPen, new RectangleF(core.X - 6, core.Y - 6, core.Width + 12, core.Height + 12));
        }

        private static Pen CreateRingPen(float width)
        {
            return new Pen(Palette.Lime, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        }

        private void DrawCentered(Graphics g, string text, Font font, Brush brush, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (ClientSize.Width - size.Width) / 2f, y);
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
        private readonly Font labelFont;
        private readonly Font numberFont;
        private readonly SolidBrush mutedBrush;
        private readonly SolidBrush textBrush;
        private readonly SolidBrush trackBrush;
        private readonly SolidBrush fillBrush;
        public string Caption { get; set; }
        public float Value
        {
            get { return value; }
            set
            {
                float next = Math.Max(0, Math.Min(100, value));
                if (Math.Abs(next - this.value) < 0.25f) return;
                this.value = next;
                Invalidate();
            }
        }

        public MetricBar()
        {
            Size = new Size(180, 48);
            Caption = "";
            DoubleBuffered = true;
            labelFont = new Font("Segoe UI", 8);
            numberFont = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
            mutedBrush = new SolidBrush(Palette.Muted);
            textBrush = new SolidBrush(Palette.Text);
            trackBrush = new SolidBrush(Palette.Line);
            fillBrush = new SolidBrush(Palette.Lime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                labelFont.Dispose();
                numberFont.Dispose();
                mutedBrush.Dispose();
                textBrush.Dispose();
                trackBrush.Dispose();
                fillBrush.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawString(Caption, labelFont, mutedBrush, 0, 1);
            string numberText = Math.Round(value).ToString(CultureInfo.InvariantCulture) + "%";
            SizeF size = e.Graphics.MeasureString(numberText, numberFont);
            e.Graphics.DrawString(numberText, numberFont, textBrush, Width - size.Width, 0);
            e.Graphics.FillRectangle(trackBrush, 0, 30, Width, 4);
            fillBrush.Color = value > 85 ? Palette.Coral : Palette.Lime;
            e.Graphics.FillRectangle(fillBrush, 0, 30, (int)(Width * value / 100f), 4);
        }
    }

    internal sealed class GameLibraryForm : Form
    {
        private readonly List<GameInstall> catalog;
        private readonly ListView games;
        private readonly TextBox searchBox;
        private readonly ComboBox sourceFilter;
        private readonly Label countLabel;
        private readonly Button folderButton;
        private readonly Button launchButton;
        private readonly Button useButton;
        public GameInstall SelectedGame { get; private set; }
        public bool LaunchRequested { get; private set; }

        public GameLibraryForm(List<GameInstall> installedGames, string selectedName)
        {
            catalog = new List<GameInstall>(installedGames ?? new List<GameInstall>());
            Text = "Game Library";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(860, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 9);

            Label title = new Label
            {
                Text = "GAME LIBRARY",
                Location = new Point(24, 20),
                Size = new Size(320, 28),
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = Palette.Text
            };
            Controls.Add(title);
            Controls.Add(new Label
            {
                Text = "ค้นหา เลือกโปรไฟล์ หรือเปิดเกมได้จากหน้าจอนี้",
                Location = new Point(25, 52),
                Size = new Size(540, 22),
                ForeColor = Palette.Muted
            });

            Controls.Add(CreateLabel("SEARCH", 24, 84, 100, Palette.Muted));
            searchBox = new TextBox
            {
                Location = new Point(24, 105),
                Size = new Size(610, 32),
                BackColor = Palette.Surface,
                ForeColor = Palette.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            searchBox.TextChanged += delegate { RefreshItems(null); };
            Controls.Add(searchBox);

            Controls.Add(CreateLabel("SOURCE", 654, 84, 100, Palette.Muted));
            sourceFilter = new ComboBox
            {
                Location = new Point(654, 104),
                Size = new Size(180, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Palette.Surface,
                ForeColor = Palette.Text,
                FlatStyle = FlatStyle.Flat
            };
            sourceFilter.Items.AddRange(new object[] { "ALL", "STEAM", "EPIC", "RIOT", "MANUAL" });
            sourceFilter.SelectedIndexChanged += delegate { RefreshItems(null); };
            Controls.Add(sourceFilter);

            games = new ListView
            {
                Location = new Point(24, 154),
                Size = new Size(810, 300),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BackColor = Palette.Surface,
                ForeColor = Palette.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            games.Columns.Add("GAME", 250);
            games.Columns.Add("SOURCE", 85);
            games.Columns.Add("STATUS", 92);
            games.Columns.Add("INSTALL LOCATION", 360);
            catalog.Sort(delegate(GameInstall left, GameInstall right)
            {
                return StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName, right.DisplayName);
            });
            games.SelectedIndexChanged += delegate { UpdateActions(); };
            games.DoubleClick += delegate
            {
                if (CanLaunch(GetSelected())) UseSelection(true);
                else UseSelection(false);
            };
            Controls.Add(games);

            countLabel = CreateLabel("", 24, 464, 360, Palette.Muted);
            Controls.Add(countLabel);

            Button add = CreateButton("ADD EXE", 24, 500, 112, Palette.SurfaceHigh, Palette.Text);
            add.Click += AddManualGame;
            Controls.Add(add);
            folderButton = CreateButton("OPEN FOLDER", 146, 500, 130, Palette.SurfaceHigh, Palette.Text);
            folderButton.Enabled = false;
            folderButton.Click += OpenSelectedFolder;
            Controls.Add(folderButton);

            Button cancel = CreateButton("ยกเลิก", 487, 500, 100, Palette.SurfaceHigh, Palette.Text);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
            launchButton = CreateButton("PLAY NOW", 596, 500, 112, Palette.SurfaceHigh, Palette.Lime);
            launchButton.Enabled = false;
            launchButton.Click += delegate { UseSelection(true); };
            Controls.Add(launchButton);
            useButton = CreateButton("USE PROFILE", 718, 500, 116, Palette.Lime, Palette.Back);
            useButton.Enabled = false;
            useButton.Click += delegate { UseSelection(false); };
            Controls.Add(useButton);

            AcceptButton = useButton;
            CancelButton = cancel;
            sourceFilter.SelectedIndex = 0;
            RefreshItems(null);
            SelectByName(selectedName);
        }

        private void RefreshItems(GameInstall select)
        {
            string query = searchBox.Text.Trim();
            string source = sourceFilter.SelectedItem == null ? "ALL" : sourceFilter.SelectedItem.ToString();
            games.BeginUpdate();
            games.Items.Clear();
            foreach (GameInstall game in catalog)
            {
                if (source != "ALL" && !String.Equals(source, game.Source, StringComparison.OrdinalIgnoreCase)) continue;
                if (query.Length > 0 &&
                    game.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                    game.DirectoryPath.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                ListViewItem item = new ListViewItem(game.DisplayName);
                item.SubItems.Add(game.Source);
                item.SubItems.Add(CanLaunch(game) ? "READY" : "LAUNCHER");
                item.SubItems.Add(game.DirectoryPath);
                item.Tag = game;
                games.Items.Add(item);
                if (select != null && String.Equals(game.DirectoryPath, select.DirectoryPath,
                    StringComparison.OrdinalIgnoreCase)) item.Selected = true;
            }
            games.EndUpdate();
            countLabel.Text = games.Items.Count + " OF " + catalog.Count + " GAMES";
            UpdateActions();
        }

        private void SelectByName(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return;
            foreach (ListViewItem item in games.Items)
            {
                GameInstall game = item.Tag as GameInstall;
                if (game != null && String.Equals(game.DisplayName, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    return;
                }
            }
        }

        private void AddManualGame(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "เพิ่มไฟล์เกมใน Library";
                dialog.Filter = "ไฟล์เกม (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                GameInstall added = new GameInstall
                {
                    DisplayName = Path.GetFileNameWithoutExtension(dialog.FileName),
                    DirectoryPath = Path.GetDirectoryName(dialog.FileName),
                    Source = "MANUAL",
                    LaunchTarget = dialog.FileName,
                    LaunchArguments = ""
                };
                foreach (GameInstall existing in catalog)
                {
                    if (!String.Equals(existing.LaunchTarget, added.LaunchTarget, StringComparison.OrdinalIgnoreCase)) continue;
                    searchBox.Text = "";
                    sourceFilter.SelectedItem = "ALL";
                    RefreshItems(existing);
                    return;
                }
                catalog.Add(added);
                catalog.Sort(delegate(GameInstall left, GameInstall right)
                {
                    return StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName, right.DisplayName);
                });
                searchBox.Text = "";
                sourceFilter.SelectedItem = "ALL";
                RefreshItems(added);
            }
        }

        private void OpenSelectedFolder(object sender, EventArgs e)
        {
            GameInstall game = GetSelected();
            if (game == null || !Directory.Exists(game.DirectoryPath)) return;
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + game.DirectoryPath + "\"")); }
            catch { }
        }

        private void UseSelection(bool launch)
        {
            SelectedGame = GetSelected();
            if (SelectedGame == null || (launch && !CanLaunch(SelectedGame))) return;
            LaunchRequested = launch;
            DialogResult = DialogResult.OK;
            Close();
        }

        private GameInstall GetSelected()
        {
            return games.SelectedItems.Count == 1 ? games.SelectedItems[0].Tag as GameInstall : null;
        }

        private void UpdateActions()
        {
            GameInstall selected = GetSelected();
            useButton.Enabled = selected != null;
            launchButton.Enabled = CanLaunch(selected);
            folderButton.Enabled = selected != null && Directory.Exists(selected.DirectoryPath);
        }

        private static bool CanLaunch(GameInstall game)
        {
            if (game == null || String.IsNullOrWhiteSpace(game.LaunchTarget)) return false;
            return game.LaunchTarget.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                File.Exists(game.LaunchTarget);
        }

        private static Label CreateLabel(string text, int x, int y, int width, Color color)
        {
            return new Label
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, 20),
                ForeColor = color, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
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

    internal sealed class AdvancedSettingsForm : Form
    {
        private readonly AppConfig config;
        private readonly ToggleSwitch gameMode;
        private readonly ToggleSwitch capture;
        private readonly ToggleSwitch gpu;
        private readonly ToggleSwitch priority;
        private readonly ToggleSwitch highQos;
        private readonly ToggleSwitch priorityBoost;

        public AdvancedSettingsForm(AppConfig source)
        {
            config = source;
            Text = "Advanced Mode";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(650, 530);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 9);

            Controls.Add(CreateLabel("ADVANCED MODE", 26, 20, 300, 32, 16, FontStyle.Bold, Palette.Text));
            Controls.Add(CreateLabel("Best Mode เปิดทุกระบบ ค่า Custom จะถูกใช้ใน Boost รอบถัดไป", 27, 55, 530, 22,
                9, FontStyle.Regular, Palette.Muted));

            Panel power = new Panel { Location = new Point(26, 91), Size = new Size(598, 54), BackColor = Palette.SurfaceHigh };
            power.Controls.Add(CreateLabel("POWER PLAN", 14, 8, 130, 18, 8, FontStyle.Bold, Palette.Amber));
            power.Controls.Add(CreateLabel("ULTIMATE PERFORMANCE / AUTO-CREATE", 14, 27, 390, 20, 9,
                FontStyle.Bold, Palette.Lime));
            Controls.Add(power);

            int y = 160;
            gameMode = AddSetting("Windows Game Mode", "ให้ Windows จัดลำดับทรัพยากรสำหรับเกม", y, source.EnableWindowsGameMode);
            capture = AddSetting("Disable background capture", "หยุด Game DVR และการอัดหน้าจอเบื้องหลัง", y += 52, source.DisableBackgroundCapture);
            gpu = AddSetting("High-performance GPU", "กำหนด GPU ประสิทธิภาพสูงให้ไฟล์เกม", y += 52, source.PreferHighPerformanceGpu);
            priority = AddSetting("AboveNormal priority", "เพิ่มลำดับ CPU โดยไม่ใช้ High หรือ Realtime", y += 52, source.UseAboveNormalPriority);
            highQos = AddSetting("Disable power throttling", "กัน Windows ลดความเร็วเฉพาะโปรเซสเกม", y += 52, source.UseHighQos);
            priorityBoost = AddSetting("Dynamic priority boost", "เปิดกลไกตอบสนองระยะสั้นของ Windows", y += 52, source.UseDynamicPriorityBoost);

            Button reset = CreateButton("RESET BEST", 26, 480, 122, Palette.SurfaceHigh, Palette.Text);
            reset.Click += delegate
            {
                gameMode.Value = capture.Value = gpu.Value = priority.Value = highQos.Value = priorityBoost.Value = true;
            };
            Controls.Add(reset);
            Button cancel = CreateButton("ยกเลิก", 408, 480, 96, Palette.SurfaceHigh, Palette.Text);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
            Button save = CreateButton("SAVE", 514, 480, 110, Palette.Lime, Palette.Back);
            save.Click += delegate
            {
                config.EnableWindowsGameMode = gameMode.Value;
                config.DisableBackgroundCapture = capture.Value;
                config.PreferHighPerformanceGpu = gpu.Value;
                config.UseAboveNormalPriority = priority.Value;
                config.UseHighQos = highQos.Value;
                config.UseDynamicPriorityBoost = priorityBoost.Value;
                Storage.SaveConfig(config);
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(save);
        }

        private ToggleSwitch AddSetting(string title, string detail, int y, bool value)
        {
            Controls.Add(CreateLabel(title, 28, y, 300, 22, 10, FontStyle.Bold, Palette.Text));
            Controls.Add(CreateLabel(detail, 28, y + 23, 490, 20, 8, FontStyle.Regular, Palette.Muted));
            ToggleSwitch toggle = new ToggleSwitch { Location = new Point(566, y + 8), Value = value };
            Controls.Add(toggle);
            return toggle;
        }

        private static Label CreateLabel(string text, int x, int y, int width, int height, float size,
            FontStyle style, Color color)
        {
            return new Label
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, height),
                Font = new Font("Segoe UI", size, style), ForeColor = color, BackColor = Color.Transparent
            };
        }

        private static Button CreateButton(string text, int x, int y, int width, Color back, Color fore)
        {
            Button button = new Button
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, 34), BackColor = back,
                ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }

    internal sealed class GpuUsageReader : IDisposable
    {
        private readonly PerformanceCounterCategory category;
        private Dictionary<string, CounterSample> previous = new Dictionary<string, CounterSample>();

        public GpuUsageReader()
        {
            try { category = new PerformanceCounterCategory("GPU Engine"); }
            catch { category = null; }
        }

        public float NextValue()
        {
            if (category == null) return 0;
            Dictionary<string, CounterSample> current = new Dictionary<string, CounterSample>();
            float total = 0;
            try
            {
                InstanceDataCollection values = category.ReadCategory()["Utilization Percentage"];
                foreach (string name in values.Keys)
                {
                    if (name.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    CounterSample sample = values[name].Sample;
                    current[name] = sample;
                    CounterSample old;
                    if (previous.TryGetValue(name, out old))
                    {
                        float value = CounterSample.Calculate(old, sample);
                        if (!Single.IsNaN(value) && !Single.IsInfinity(value) && value > 0) total += value;
                    }
                }
                previous = current;
            }
            catch { return 0; }
            return Math.Max(0, Math.Min(100, total));
        }

        public void Dispose()
        {
            previous.Clear();
        }
    }

    internal sealed class MonitorSnapshot
    {
        public bool HasMetrics { get; set; }
        public float Cpu { get; set; }
        public float Memory { get; set; }
        public float Gpu { get; set; }
        public DetectedGame Game { get; set; }
        public BoostState State { get; set; }
    }

    internal sealed class BoostTransitionResult
    {
        public bool WasRestore { get; set; }
        public bool ShouldLaunch { get; set; }
        public string Activity { get; set; }
        public Exception Error { get; set; }
    }

    internal sealed class MainForm : Form
    {
        private const int DiscoveryMonitorIntervalMs = 1500;
        private const int ActiveMonitorIntervalMs = 3000;
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
        private readonly Button advancedButton;
        private readonly MetricBar cpuBar;
        private readonly MetricBar ramBar;
        private readonly MetricBar gpuBar;
        private readonly Timer monitor;
        private readonly PerformanceCounter cpuCounter;
        private readonly GpuUsageReader gpuReader;
        private readonly NotifyIcon tray;
        private readonly object systemLock = new object();
        private volatile bool working;
        private int monitorInFlight;
        private bool allowClose;
        private int missingGameTicks;
        private DetectedGame detectedGame;

        public MainForm()
        {
            config = Storage.LoadConfig();
            platform = PlatformDetector.Detect();
            Text = "Game Boost Pro";
            Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (appIcon != null) Icon = appIcon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 610);
            FormBorderStyle = FormBorderStyle.FixedSingle;
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
                platform.IsSupported ? Palette.Cyan : Palette.Coral));

            advancedButton = MakeButton("ADVANCED", 708, 23, 160, 34, Palette.SurfaceHigh, Palette.Lime);
            advancedButton.Click += OpenAdvancedSettings;
            Controls.Add(advancedButton);
            Label adminStatus = MakeLabel("ADMIN ACTIVE", 742, 59, 126, 17, 7, FontStyle.Bold, Palette.Muted);
            adminStatus.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(adminStatus);

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
            monitor.Interval = DiscoveryMonitorIntervalMs;
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
            FormClosed += delegate
            {
                monitor.Stop();
                monitor.Dispose();
                if (detectedGame != null && detectedGame.Process != null) detectedGame.Process.Dispose();
                gpuReader.Dispose();
                cpuCounter.Dispose();
                tray.Dispose();
            };

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
            RefreshAdvancedButton();
            RefreshGameProfile();
            RefreshState(platform.IsSupported
                ? platform.Detail
                : platform.Detail + " / รองรับเฉพาะ Acer + NitroSense และ Desktop PC");
            RefreshCatalogAsync();
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
                    config.LibraryLaunchArguments = "";
                    Storage.SaveConfig(config);
                    RefreshGameProfile();
                    RefreshState("บันทึกโปรไฟล์ " + Path.GetFileNameWithoutExtension(config.GamePath) + " แล้ว");
                }
            }
        }

        private void OpenGameLibrary(object sender, EventArgs e)
        {
            List<GameInstall> catalog = GameDetector.GetCatalog();
            if (!String.IsNullOrWhiteSpace(config.LibraryGameName) &&
                !String.IsNullOrWhiteSpace(config.LibraryLaunchTarget) &&
                String.IsNullOrWhiteSpace(config.LibraryLaunchArguments) && File.Exists(config.LibraryLaunchTarget))
            {
                bool exists = false;
                foreach (GameInstall game in catalog)
                    if (String.Equals(game.LaunchTarget, config.LibraryLaunchTarget, StringComparison.OrdinalIgnoreCase))
                        exists = true;
                if (!exists)
                    catalog.Add(new GameInstall
                    {
                        DisplayName = config.LibraryGameName,
                        DirectoryPath = config.LibraryGameDirectory,
                        Source = "MANUAL",
                        LaunchTarget = config.LibraryLaunchTarget,
                        LaunchArguments = ""
                    });
            }
            using (GameLibraryForm dialog = new GameLibraryForm(catalog, config.LibraryGameName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedGame == null) return;
                config.LibraryGameName = dialog.SelectedGame.DisplayName;
                config.LibraryGameDirectory = dialog.SelectedGame.DirectoryPath;
                config.LibraryLaunchTarget = dialog.SelectedGame.LaunchTarget;
                config.LibraryLaunchArguments = dialog.SelectedGame.LaunchArguments;
                config.GamePath = String.Equals(dialog.SelectedGame.Source, "MANUAL", StringComparison.OrdinalIgnoreCase)
                    ? dialog.SelectedGame.LaunchTarget : "";
                Storage.SaveConfig(config);
                RefreshGameProfile();
                RefreshState("เลือก " + config.LibraryGameName + " จาก " + dialog.SelectedGame.Source + " แล้ว");
                if (dialog.LaunchRequested) LaunchGame();
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
                if (SystemTuner.IsAdmin())
                    LaunchThroughDesktopShell(target, config.LibraryLaunchArguments);
                else
                {
                    ProcessStartInfo info = new ProcessStartInfo(target);
                    info.UseShellExecute = true;
                    info.Arguments = config.LibraryLaunchArguments ?? "";
                    if (File.Exists(target)) info.WorkingDirectory = Path.GetDirectoryName(target);
                    Process.Start(info);
                }
                RefreshState("กำลังเปิด " + GetConfiguredGameName());
            }
            catch (Exception ex) { ShowError("เปิดเกมไม่สำเร็จ: " + ex.Message); }
        }

        private static void LaunchThroughDesktopShell(string target, string arguments)
        {
            object shell = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) throw new InvalidOperationException("ไม่พบ Windows Desktop Shell");
                shell = Activator.CreateInstance(shellType);
                string directory = File.Exists(target) ? Path.GetDirectoryName(target) : "";
                shellType.InvokeMember("ShellExecute", System.Reflection.BindingFlags.InvokeMethod, null, shell,
                    new object[] { target, arguments ?? "", directory, "open", 1 });
            }
            finally
            {
                if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
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
            bool restore = Storage.HasState();
            DetectedGame candidate = detectedGame;
            string targetPath = candidate != null && !String.IsNullOrWhiteSpace(candidate.ExePath)
                ? candidate.ExePath : config.GamePath;
            int processId = candidate != null && candidate.Process != null ? candidate.Process.Id : 0;
            bool shouldLaunch = config.LaunchOnBoost && !autoTriggered && !restore;

            Task.Factory.StartNew(delegate
            {
                BoostTransitionResult result = new BoostTransitionResult
                {
                    WasRestore = restore,
                    ShouldLaunch = shouldLaunch
                };
                try
                {
                    lock (systemLock)
                    {
                        if (restore) SystemTuner.Disable();
                        else SystemTuner.Enable(targetPath, autoTriggered, processId, platform, config);
                    }
                    result.Activity = restore
                        ? (autoTriggered ? "เกมปิดแล้ว คืนค่าเครื่องเรียบร้อย" : "คืนค่าเครื่องกลับเป็นปกติแล้ว")
                        : (autoTriggered && candidate != null
                            ? "ตรวจพบ " + candidate.DisplayName + " / เปิด Best Mode แล้ว"
                            : "Best Mode พร้อมทำงาน / รอจับโปรเซสเกม");
                }
                catch (Exception ex) { result.Error = ex; }
                return result;
            }).ContinueWith(delegate(Task<BoostTransitionResult> task)
            {
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        BoostTransitionResult result = task.Status == TaskStatus.RanToCompletion
                            ? task.Result : new BoostTransitionResult { Error = task.Exception };
                        if (result.Error == null)
                        {
                            if (result.ShouldLaunch) LaunchGame();
                            RefreshState(result.Activity);
                        }
                        else
                        {
                            RefreshState("ตรวจพบปัญหา กรุณาดูข้อความแจ้งเตือน");
                            ShowError(result.Error.GetBaseException().Message);
                        }
                        working = false;
                        dial.Busy = false;
                        SetControlsEnabled(true);
                        UpdateStateVisuals();
                    }));
                }
                catch { working = false; }
            });
        }

        private void MonitorTick(object sender, EventArgs e)
        {
            if (System.Threading.Interlocked.Exchange(ref monitorInFlight, 1) != 0) return;
            bool collectMetrics = Visible && WindowState != FormWindowState.Minimized;
            Task.Factory.StartNew<MonitorSnapshot>(delegate { return BuildMonitorSnapshot(collectMetrics); }).ContinueWith(delegate(Task<MonitorSnapshot> task)
            {
                try
                {
                    if (IsDisposed || Disposing)
                    {
                        System.Threading.Interlocked.Exchange(ref monitorInFlight, 0);
                        return;
                    }
                    BeginInvoke(new Action(delegate
                    {
                        System.Threading.Interlocked.Exchange(ref monitorInFlight, 0);
                        if (task.Status == TaskStatus.RanToCompletion) ApplyMonitorSnapshot(task.Result);
                    }));
                }
                catch { System.Threading.Interlocked.Exchange(ref monitorInFlight, 0); }
            });
        }

        private MonitorSnapshot BuildMonitorSnapshot(bool collectMetrics)
        {
            System.Threading.Thread worker = System.Threading.Thread.CurrentThread;
            System.Threading.ThreadPriority previousPriority = worker.Priority;
            bool priorityLowered = false;
            try
            {
                if (previousPriority > System.Threading.ThreadPriority.BelowNormal)
                {
                    worker.Priority = System.Threading.ThreadPriority.BelowNormal;
                    priorityLowered = true;
                }
            }
            catch { }

            try { return CollectMonitorSnapshot(collectMetrics); }
            finally
            {
                if (priorityLowered)
                {
                    try { worker.Priority = previousPriority; }
                    catch { }
                }
            }
        }

        private MonitorSnapshot CollectMonitorSnapshot(bool collectMetrics)
        {
            MonitorSnapshot snapshot = new MonitorSnapshot();
            if (collectMetrics)
            {
                snapshot.HasMetrics = true;
                try { snapshot.Cpu = cpuCounter.NextValue(); } catch { }
                try { snapshot.Gpu = gpuReader.NextValue(); } catch { }
                MEMORYSTATUSEX memory = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memory)) snapshot.Memory = memory.dwMemoryLoad;
            }
            if (working) return snapshot;

            snapshot.Game = GameDetector.FindRunningGame(config.GamePath);
            lock (systemLock)
            {
                if (working) return snapshot;
                try { snapshot.State = Storage.LoadState(); }
                catch { return snapshot; }

                if (snapshot.Game != null && snapshot.State != null)
                {
                    try
                    {
                        BoostState latest = snapshot.State;
                        if (latest != null)
                        {
                            if (!String.IsNullOrWhiteSpace(snapshot.Game.ExePath))
                                SystemTuner.AttachGamePath(latest, snapshot.Game.ExePath);
                            if (!latest.ProcessTuningApplied || latest.GameProcessId != snapshot.Game.Process.Id)
                                SystemTuner.ApplyGamePriority(latest, snapshot.Game.Process.Id);
                            snapshot.State = latest;
                        }
                    }
                    catch { }
                }
            }
            return snapshot;
        }

        private void ApplyMonitorSnapshot(MonitorSnapshot snapshot)
        {
            if (snapshot.HasMetrics)
            {
                cpuBar.Value = snapshot.Cpu;
                ramBar.Value = snapshot.Memory;
                gpuBar.Value = snapshot.Gpu;
            }
            if (working) return;

            if (detectedGame != null && detectedGame.Process != null &&
                (snapshot.Game == null || !Object.ReferenceEquals(detectedGame.Process, snapshot.Game.Process)))
                detectedGame.Process.Dispose();
            detectedGame = snapshot.Game;
            bool running = detectedGame != null;
            BoostState state = snapshot.State;
            int desiredInterval = running && state != null
                ? ActiveMonitorIntervalMs : DiscoveryMonitorIntervalMs;
            if (monitor.Interval != desiredInterval) monitor.Interval = desiredInterval;

            if (running)
            {
                missingGameTicks = 0;
                ShowDetectedGame(detectedGame);
                if (config.AutoMode && !Storage.HasState())
                {
                    ToggleBoost(true);
                    return;
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
            string catalogStatus = GameDetector.IsCatalogLoaded
                ? "พบเกมในคลัง " + GameDetector.InstalledCount + " รายการ"
                : "กำลังอ่านคลัง Steam / Epic / Riot";
            gamePath.Text = catalogStatus +
                (manualValid ? " / สำรอง: " + Path.GetFileNameWithoutExtension(config.GamePath) :
                libraryValid ? " / " + config.LibraryGameDirectory : "");
            launchButton.Enabled = CanLaunchConfiguredGame();
        }

        private void RefreshCatalogAsync()
        {
            libraryButton.Enabled = false;
            Task.Factory.StartNew(delegate { GameDetector.RefreshCatalog(); }).ContinueWith(delegate(Task task)
            {
                try
                {
                    if (IsDisposed || Disposing) return;
                    BeginInvoke(new Action(delegate
                    {
                        libraryButton.Enabled = !working && GameDetector.IsCatalogLoaded;
                        RefreshGameProfile();
                    }));
                }
                catch { }
            });
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
                return !String.IsNullOrWhiteSpace(config.LibraryLaunchTarget) &&
                    (config.LibraryLaunchTarget.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    File.Exists(config.LibraryLaunchTarget));
            return !String.IsNullOrWhiteSpace(config.GamePath) && File.Exists(config.GamePath);
        }

        private void ShowDetectedGame(DetectedGame game)
        {
            gameName.Text = "ตรวจพบ: " + game.DisplayName;
            gameName.ForeColor = Palette.Cyan;
            string detail = game.Source + " / " + Path.GetFileName(game.ExePath ?? "กำลังทำงาน");
            gamePath.Text = detail;
            activityText.Text = Storage.HasState()
                ? game.DisplayName + " / Ultimate + Tuning " + GetAdvancedOptionCount() + "/6"
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
            int optionCount = GetAdvancedOptionCount();
            SetStatus(powerStatus, active, active ? "ULTIMATE ON" : "ULTIMATE READY");
            SetStatus(modeStatus, active, active ? "TUNING " + optionCount + "/6" :
                (optionCount == 6 ? "BEST 6/6" : "CUSTOM " + optionCount + "/6"), Palette.Cyan);
            SetStatus(captureStatus, active && config.DisableBackgroundCapture,
                config.DisableBackgroundCapture ? (active ? "CAPTURE OFF" : "CAPTURE READY") : "CAPTURE KEEP",
                Palette.Amber);
            tray.Text = active ? "Game Boost Pro - Game Mode ON" : "Game Boost Pro - Normal";
            advancedButton.Enabled = !active && !working;
        }

        private void SetControlsEnabled(bool enabled)
        {
            browseButton.Enabled = enabled;
            libraryButton.Enabled = enabled && GameDetector.IsCatalogLoaded;
            autoSwitch.Enabled = enabled && platform.IsSupported;
            launchCheck.Enabled = enabled;
            advancedButton.Enabled = enabled && !Storage.HasState();
            launchButton.Enabled = enabled && CanLaunchConfiguredGame();
        }

        private void OpenAdvancedSettings(object sender, EventArgs e)
        {
            if (Storage.HasState())
            {
                ShowError("กรุณากด RESTORE ก่อนเปลี่ยน Advanced Mode");
                return;
            }
            using (AdvancedSettingsForm dialog = new AdvancedSettingsForm(config))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RefreshAdvancedButton();
                RefreshState(GetAdvancedOptionCount() == 6
                    ? "Advanced Mode: BEST / เปิดครบทุกระบบ"
                    : "Advanced Mode: CUSTOM " + GetAdvancedOptionCount() + "/6");
            }
        }

        private void RefreshAdvancedButton()
        {
            int enabled = GetAdvancedOptionCount();
            advancedButton.Text = enabled == 6 ? "ADVANCED  BEST" : "ADVANCED  " + enabled + "/6";
            advancedButton.ForeColor = enabled == 6 ? Palette.Lime : Palette.Amber;
        }

        private int GetAdvancedOptionCount()
        {
            int enabled = 0;
            if (config.EnableWindowsGameMode) enabled++;
            if (config.DisableBackgroundCapture) enabled++;
            if (config.PreferHighPerformanceGpu) enabled++;
            if (config.UseAboveNormalPriority) enabled++;
            if (config.UseHighQos) enabled++;
            if (config.UseDynamicPriorityBoost) enabled++;
            return enabled;
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
            SetStatus(label, active, text, Palette.Lime);
        }

        private static void SetStatus(Label label, bool active, string text, Color activeColor)
        {
            label.Text = text;
            label.ForeColor = active ? activeColor : Palette.Muted;
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
