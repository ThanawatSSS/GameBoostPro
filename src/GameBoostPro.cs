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
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Game Boost Pro")]
[assembly: System.Reflection.AssemblyDescription("Smart game detection and reversible Windows gaming optimization")]
[assembly: System.Reflection.AssemblyProduct("Game Boost Pro")]
[assembly: System.Reflection.AssemblyCompany("Local PC Tools")]
[assembly: System.Reflection.AssemblyVersion("3.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.2.0.0")]

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

    internal static class PowerPlanPolicy
    {
        public const string Smart = "Smart";
        public const string Ultimate = "Ultimate";
        public const string KeepCurrent = "KeepCurrent";

        public static string Normalize(string value)
        {
            if (String.Equals(value, Ultimate, StringComparison.OrdinalIgnoreCase)) return Ultimate;
            if (String.Equals(value, KeepCurrent, StringComparison.OrdinalIgnoreCase)) return KeepCurrent;
            return Smart;
        }

        public static bool ShouldKeepCurrent(string mode, PlatformProfile platform)
        {
            mode = Normalize(mode);
            if (String.Equals(mode, KeepCurrent, StringComparison.Ordinal)) return true;
            return String.Equals(mode, Smart, StringComparison.Ordinal) && platform != null && platform.IsLaptop;
        }

        public static string GetShortLabel(string mode)
        {
            mode = Normalize(mode);
            if (String.Equals(mode, Ultimate, StringComparison.Ordinal)) return "ULTIMATE";
            if (String.Equals(mode, KeepCurrent, StringComparison.Ordinal)) return "KEEP PLAN";
            return "SMART PLAN";
        }
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
        public string PowerPlanMode { get; set; }

        public AppConfig()
        {
            Version = 5;
            AutoMode = true;
            PowerPlanMode = PowerPlanPolicy.Smart;
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
        public int Version { get; set; }
        public string OwnerSid { get; set; }
        public string EnabledAt { get; set; }
        public string PreviousPowerGuid { get; set; }
        public string PreviousPowerName { get; set; }
        public string TargetPowerGuid { get; set; }
        public string TargetPowerName { get; set; }
        public string PowerPlanMode { get; set; }
        public string PlatformTitle { get; set; }
        public bool AutoTriggered { get; set; }
        public string GamePath { get; set; }
        public int GameProcessId { get; set; }
        public string GameProcessName { get; set; }
        public long GameProcessStartTimeUtcTicks { get; set; }
        public string GameProcessPath { get; set; }
        public string PreviousPriority { get; set; }
        public bool ProcessTuningApplied { get; set; }
        public bool ProcessTuningAttempted { get; set; }
        public string ProcessTuningStatus { get; set; }
        public string ProcessTuningDetail { get; set; }
        public bool PriorityVerified { get; set; }
        public bool PriorityBoostVerified { get; set; }
        public bool PowerThrottlingVerified { get; set; }
        public bool ProcessRetentionVerified { get; set; }
        public bool HadPriorityBoostState { get; set; }
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

    internal static class BoostTargetResolver
    {
        public static string ResolveGamePath(DetectedGame detectedGame, string configuredGamePath)
        {
            if (detectedGame != null) return detectedGame.ExePath ?? "";
            return configuredGamePath ?? "";
        }
    }

    internal static class RecoveryStatePolicy
    {
        public const int CurrentVersion = 3;

        public static bool IsValidPowerGuid(string value)
        {
            Guid parsed;
            return !String.IsNullOrWhiteSpace(value) && Guid.TryParseExact(value, "D", out parsed);
        }

        public static bool IsAllowedRegistrySnapshot(RegistrySnapshot snapshot)
        {
            if (snapshot == null || String.IsNullOrWhiteSpace(snapshot.SubKey) ||
                String.IsNullOrWhiteSpace(snapshot.Name)) return false;
            bool dword = !snapshot.Exists || snapshot.Kind == (int)RegistryValueKind.DWord;
            bool text = !snapshot.Exists || snapshot.Kind == (int)RegistryValueKind.String;
            if (String.Equals(snapshot.SubKey, @"Software\Microsoft\GameBar",
                StringComparison.OrdinalIgnoreCase))
                return dword && (String.Equals(snapshot.Name, "AutoGameModeEnabled",
                    StringComparison.OrdinalIgnoreCase) || String.Equals(snapshot.Name,
                    "AllowAutoGameMode", StringComparison.OrdinalIgnoreCase));
            if (String.Equals(snapshot.SubKey, @"System\GameConfigStore",
                StringComparison.OrdinalIgnoreCase))
                return dword && String.Equals(snapshot.Name, "GameDVR_Enabled",
                    StringComparison.OrdinalIgnoreCase);
            if (String.Equals(snapshot.SubKey, @"Software\Microsoft\Windows\CurrentVersion\GameDVR",
                StringComparison.OrdinalIgnoreCase))
                return dword && String.Equals(snapshot.Name, "AppCaptureEnabled",
                    StringComparison.OrdinalIgnoreCase);
            return text && String.Equals(snapshot.SubKey,
                @"Software\Microsoft\DirectX\UserGpuPreferences", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAllowedLegacyRegistry(string subKey, string name)
        {
            return IsAllowedRegistrySnapshot(new RegistrySnapshot
            {
                SubKey = subKey,
                Name = name,
                Exists = false
            });
        }

        public static BoostState SanitizeMigratedState(BoostState state, string ownerSid)
        {
            if (state == null || !IsValidPowerGuid(state.PreviousPowerGuid))
                throw new InvalidOperationException("ข้อมูล Power Plan รุ่นเก่าไม่ถูกต้อง");
            if (String.IsNullOrWhiteSpace(ownerSid))
                throw new InvalidOperationException("ระบุเจ้าของข้อมูลกู้คืนไม่ได้");
            if (!String.IsNullOrWhiteSpace(state.TargetPowerGuid) &&
                !IsValidPowerGuid(state.TargetPowerGuid)) state.TargetPowerGuid = "";
            List<RegistrySnapshot> safe = new List<RegistrySnapshot>();
            if (state.Registry != null)
            {
                foreach (RegistrySnapshot snapshot in state.Registry)
                {
                    if (!IsAllowedRegistrySnapshot(snapshot)) continue;
                    if (snapshot.Exists && snapshot.Kind == (int)RegistryValueKind.DWord)
                    {
                        int value;
                        if (!Int32.TryParse(snapshot.Value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out value)) continue;
                    }
                    safe.Add(snapshot);
                }
            }
            state.Version = CurrentVersion;
            state.OwnerSid = ownerSid;
            state.Registry = safe;
            state.GameProcessId = 0;
            state.GameProcessName = "";
            state.GameProcessStartTimeUtcTicks = 0;
            state.GameProcessPath = "";
            state.PreviousPriority = "";
            state.ProcessTuningApplied = false;
            state.ProcessTuningAttempted = true;
            state.ProcessTuningStatus = "LegacyUnverified";
            state.ProcessTuningDetail = "ย้ายสถานะรุ่นเก่าอย่างปลอดภัย และไม่คืนค่าโปรเซสที่ยืนยัน identity ไม่ได้";
            state.PriorityVerified = false;
            state.PriorityBoostVerified = false;
            state.PowerThrottlingVerified = false;
            state.ProcessRetentionVerified = true;
            state.HadPriorityBoostState = false;
            state.PreviousPriorityBoostDisabled = false;
            state.HadPowerThrottleState = false;
            state.PreviousThrottleControl = 0;
            state.PreviousThrottleState = 0;
            return state;
        }
    }

    internal sealed class GraphicsCapabilities
    {
        public string GpuName { get; set; }
        public bool IsNvidia { get; set; }
        public bool IsRtx { get; set; }
        public int RtxSeries { get; set; }
        public bool SupportsDlssSuperResolution { get; set; }
        public bool SupportsFrameGeneration { get; set; }
        public bool SupportsMultiFrameGeneration { get; set; }
        public bool SupportsSmoothMotion { get; set; }
    }

    internal sealed class GraphicsAdvisorSnapshot
    {
        public GraphicsCapabilities Capabilities { get; set; }
        public string DriverVersion { get; set; }
        public string DisplayRoute { get; set; }
        public string NisEligibility { get; set; }
        public bool HasHybridGraphics { get; set; }
        public bool HasNvidiaApp { get; set; }
        public string NvidiaAppVersion { get; set; }
        public string GameName { get; set; }
        public string GamePath { get; set; }
        public bool HasDlssLibraryHint { get; set; }
        public bool HasFrameGenerationLibraryHint { get; set; }
        public bool IsCompetitiveGame { get; set; }
    }

    internal static class GraphicsAdvisor
    {
        public static GraphicsCapabilities ClassifyGpu(string name)
        {
            name = name ?? "";
            bool nvidia = name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0;
            Match match = Regex.Match(name, @"RTX\s*(\d{2})\d{2}", RegexOptions.IgnoreCase);
            int series = 0;
            if (match.Success) Int32.TryParse(match.Groups[1].Value, out series);
            bool rtx = nvidia && series >= 20;
            return new GraphicsCapabilities
            {
                GpuName = String.IsNullOrWhiteSpace(name) ? "ไม่พบข้อมูล GPU" : name.Trim(),
                IsNvidia = nvidia,
                IsRtx = rtx,
                RtxSeries = series,
                SupportsDlssSuperResolution = rtx,
                SupportsFrameGeneration = rtx && series >= 40,
                SupportsMultiFrameGeneration = rtx && series >= 50,
                SupportsSmoothMotion = rtx && series >= 40
            };
        }

        public static string GetNisEligibility(bool hasNvidia, string displayRoute)
        {
            if (!hasNvidia) return "Unavailable";
            if (String.Equals(displayRoute, "Active", StringComparison.OrdinalIgnoreCase)) return "Eligible";
            if (String.Equals(displayRoute, "Inactive", StringComparison.OrdinalIgnoreCase)) return "RouteBlocked";
            return "Unverified";
        }

        public static GraphicsAdvisorSnapshot Inspect(string gameName, string gamePath, string gameDirectory)
        {
            string nvidiaName = "";
            string nvidiaDriver = "";
            bool hasOtherGpu = false;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion FROM Win32_VideoController"))
                using (ManagementObjectCollection adapters = searcher.Get())
                {
                    foreach (ManagementObject adapter in adapters)
                    using (adapter)
                    {
                        string name = Convert.ToString(adapter["Name"]);
                        GraphicsCapabilities candidate = ClassifyGpu(name);
                        if (candidate.IsNvidia && String.IsNullOrWhiteSpace(nvidiaName))
                        {
                            nvidiaName = name;
                            nvidiaDriver = Convert.ToString(adapter["DriverVersion"]);
                        }
                        else if (!candidate.IsNvidia && !String.IsNullOrWhiteSpace(name)) hasOtherGpu = true;
                    }
                }
            }
            catch { }

            GraphicsCapabilities capabilities = ClassifyGpu(nvidiaName);
            string smiDriver;
            string route = ReadNvidiaDisplayRoute(capabilities.IsNvidia, out smiDriver);
            if (!String.IsNullOrWhiteSpace(smiDriver)) nvidiaDriver = smiDriver;
            string appVersion;
            bool hasNvidiaApp = TryGetNvidiaAppVersion(out appVersion);
            string scanDirectory = ResolveGameDirectory(gamePath, gameDirectory);
            bool dlssHint = HasAnyFile(scanDirectory, new[] { "nvngx_dlss.dll", "sl.interposer.dll" });
            bool frameGenerationHint = HasAnyFile(scanDirectory,
                new[] { "nvngx_dlssg.dll", "sl.dlss_g.dll" });
            return new GraphicsAdvisorSnapshot
            {
                Capabilities = capabilities,
                DriverVersion = String.IsNullOrWhiteSpace(nvidiaDriver) ? "ไม่ทราบ" : nvidiaDriver,
                DisplayRoute = route,
                NisEligibility = GetNisEligibility(capabilities.IsNvidia, route),
                HasHybridGraphics = capabilities.IsNvidia && hasOtherGpu,
                HasNvidiaApp = hasNvidiaApp,
                NvidiaAppVersion = appVersion,
                GameName = String.IsNullOrWhiteSpace(gameName) ? "ยังไม่ได้เลือกเกม" : gameName,
                GamePath = gamePath ?? "",
                HasDlssLibraryHint = dlssHint,
                HasFrameGenerationLibraryHint = frameGenerationHint,
                IsCompetitiveGame = IsCompetitiveGame(gameName, gamePath)
            };
        }

        private static string ResolveGameDirectory(string gamePath, string gameDirectory)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(gamePath) && File.Exists(gamePath))
                    return Path.GetDirectoryName(gamePath);
                if (!String.IsNullOrWhiteSpace(gameDirectory) && Directory.Exists(gameDirectory))
                    return gameDirectory;
            }
            catch { }
            return "";
        }

        private static bool HasAnyFile(string directory, string[] names)
        {
            if (String.IsNullOrWhiteSpace(directory)) return false;
            try
            {
                foreach (string name in names)
                    if (File.Exists(Path.Combine(directory, name))) return true;
            }
            catch { }
            return false;
        }

        private static bool IsCompetitiveGame(string gameName, string gamePath)
        {
            string value = ((gameName ?? "") + " " + Path.GetFileNameWithoutExtension(gamePath ?? "")).ToLowerInvariant();
            string[] competitive =
            {
                "valorant", "counter-strike", "cs2", "pubg", "tslgame", "apex", "r5apex",
                "fortnite", "call of duty", "rainbow six", "overwatch", "league of legends",
                "dota 2", "the finals", "delta force"
            };
            foreach (string name in competitive)
                if (value.IndexOf(name, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string ReadNvidiaDisplayRoute(bool hasNvidia, out string driverVersion)
        {
            driverVersion = "";
            if (!hasNvidia) return "Unavailable";
            string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"System32\nvidia-smi.exe");
            if (!File.Exists(executable)) executable = "nvidia-smi.exe";
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(executable,
                    "--query-gpu=driver_version,display_active --format=csv,noheader,nounits");
                info.UseShellExecute = false;
                info.RedirectStandardOutput = true;
                info.CreateNoWindow = true;
                using (Process process = Process.Start(info))
                {
                    if (!process.WaitForExit(2500))
                    {
                        try { process.Kill(); }
                        catch { }
                        return "Unknown";
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    string[] rows = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string row in rows)
                    {
                        string[] values = row.Split(',');
                        if (values.Length > 0 && String.IsNullOrWhiteSpace(driverVersion))
                            driverVersion = values[0].Trim();
                    }
                    if (output.IndexOf("Enabled", StringComparison.OrdinalIgnoreCase) >= 0) return "Active";
                    if (output.IndexOf("Disabled", StringComparison.OrdinalIgnoreCase) >= 0) return "Inactive";
                }
            }
            catch { }
            return "Unknown";
        }

        private static bool TryGetNvidiaAppVersion(out string version)
        {
            version = "";
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
                        using (RegistryKey app = key.OpenSubKey(child))
                        {
                            string name = app == null ? "" : Convert.ToString(app.GetValue("DisplayName"));
                            if (!String.Equals(name, "NVIDIA App", StringComparison.OrdinalIgnoreCase)) continue;
                            version = Convert.ToString(app.GetValue("DisplayVersion"));
                            return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }
    }

    internal sealed class FrameBenchmarkResult
    {
        public string Slot { get; set; }
        public string GameName { get; set; }
        public string CapturedAt { get; set; }
        public int FrameCount { get; set; }
        public double DurationSeconds { get; set; }
        public double AverageFps { get; set; }
        public double OnePercentLowFps { get; set; }
        public double MedianFrameTimeMs { get; set; }
        public double P95FrameTimeMs { get; set; }
        public double P99FrameTimeMs { get; set; }
        public string PresentMode { get; set; }
    }

    internal sealed class FrameBenchmarkHistory
    {
        public FrameBenchmarkResult Baseline { get; set; }
        public FrameBenchmarkResult Boosted { get; set; }
    }

    internal static class FrameBenchmarkAnalyzer
    {
        private sealed class FrameGroup
        {
            public readonly List<double> FrameTimes = new List<double>();
            public readonly Dictionary<string, int> PresentModes =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public static FrameBenchmarkResult AnalyzeCsv(string path, string slot, string gameName)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("ไม่พบผล Frame Test");
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2) throw new InvalidOperationException("PresentMon ไม่พบ frame จากเกมนี้");
            List<string> headers = ParseCsvLine(lines[0]);
            int frameTimeIndex = FindColumn(headers, "MsBetweenPresents");
            if (frameTimeIndex < 0) frameTimeIndex = FindColumn(headers, "MsBetweenDisplayChange");
            int swapChainIndex = FindColumn(headers, "SwapChainAddress");
            int presentModeIndex = FindColumn(headers, "PresentMode");
            if (frameTimeIndex < 0) throw new InvalidOperationException("รูปแบบ CSV ของ PresentMon ไม่รองรับ");

            Dictionary<string, FrameGroup> groups = new Dictionary<string, FrameGroup>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                if (String.IsNullOrWhiteSpace(lines[i])) continue;
                List<string> values = ParseCsvLine(lines[i]);
                if (frameTimeIndex >= values.Count) continue;
                double frameTime;
                if (!Double.TryParse(values[frameTimeIndex], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out frameTime) || frameTime <= 0 || frameTime > 1000) continue;
                string key = swapChainIndex >= 0 && swapChainIndex < values.Count ? values[swapChainIndex] : "PRIMARY";
                FrameGroup group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new FrameGroup();
                    groups[key] = group;
                }
                group.FrameTimes.Add(frameTime);
                if (presentModeIndex >= 0 && presentModeIndex < values.Count &&
                    !String.IsNullOrWhiteSpace(values[presentModeIndex]))
                {
                    string mode = values[presentModeIndex].Trim();
                    int count;
                    group.PresentModes.TryGetValue(mode, out count);
                    group.PresentModes[mode] = count + 1;
                }
            }

            FrameGroup primary = null;
            foreach (FrameGroup group in groups.Values)
                if (primary == null || group.FrameTimes.Count > primary.FrameTimes.Count) primary = group;
            if (primary == null || primary.FrameTimes.Count < 30)
                throw new InvalidOperationException("ข้อมูล frame น้อยเกินไป กรุณาเปิดเกมค้างไว้แล้วลองอีกครั้ง");

            List<double> sorted = new List<double>(primary.FrameTimes);
            sorted.Sort();
            double total = 0;
            foreach (double value in primary.FrameTimes) total += value;
            int slowFrameCount = Math.Max(1, (int)Math.Ceiling(sorted.Count * 0.01));
            double slowTotal = 0;
            for (int i = sorted.Count - slowFrameCount; i < sorted.Count; i++) slowTotal += sorted[i];
            return new FrameBenchmarkResult
            {
                Slot = slot,
                GameName = gameName,
                CapturedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                FrameCount = sorted.Count,
                DurationSeconds = total / 1000.0,
                AverageFps = 1000.0 / (total / sorted.Count),
                OnePercentLowFps = 1000.0 / (slowTotal / slowFrameCount),
                MedianFrameTimeMs = Percentile(sorted, 0.50),
                P95FrameTimeMs = Percentile(sorted, 0.95),
                P99FrameTimeMs = Percentile(sorted, 0.99),
                PresentMode = MostCommon(primary.PresentModes)
            };
        }

        private static int FindColumn(List<string> headers, string name)
        {
            for (int i = 0; i < headers.Count; i++)
                if (String.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static double Percentile(List<double> sorted, double percentile)
        {
            int index = Math.Max(0, Math.Min(sorted.Count - 1,
                (int)Math.Ceiling(sorted.Count * percentile) - 1));
            return sorted[index];
        }

        private static string MostCommon(Dictionary<string, int> values)
        {
            string result = "Unknown";
            int maximum = 0;
            foreach (KeyValuePair<string, int> item in values)
            {
                if (item.Value <= maximum) continue;
                maximum = item.Value;
                result = item.Key;
            }
            return result;
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else quoted = !quoted;
                }
                else if (current == ',' && !quoted)
                {
                    fields.Add(value.ToString());
                    value.Length = 0;
                }
                else value.Append(current);
            }
            fields.Add(value.ToString());
            return fields;
        }
    }

    internal static class FrameBenchmarkStore
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, FrameBenchmarkHistory> Histories =
            new Dictionary<string, FrameBenchmarkHistory>(StringComparer.OrdinalIgnoreCase);

        public static FrameBenchmarkHistory Get(string key)
        {
            lock (Sync)
            {
                FrameBenchmarkHistory history;
                if (!Histories.TryGetValue(key, out history))
                {
                    history = new FrameBenchmarkHistory();
                    Histories[key] = history;
                }
                return history;
            }
        }

        public static void Save(string key, FrameBenchmarkResult result)
        {
            lock (Sync)
            {
                FrameBenchmarkHistory history = Get(key);
                if (String.Equals(result.Slot, "Baseline", StringComparison.OrdinalIgnoreCase))
                    history.Baseline = result;
                else history.Boosted = result;
            }
        }
    }

    internal static class PresentMonRunner
    {
        private const string ExpectedSha256 = "9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191";

        public static string ToolPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "PresentMon.exe"); }
        }

        public static bool IsToolReady()
        {
            string path = ToolPath;
            if (!File.Exists(path)) return false;
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    return String.Equals(hash, ExpectedSha256, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static FrameBenchmarkResult Capture(int processId, string expectedProcessName,
            long expectedStartTimeUtcTicks, string slot, string gameName)
        {
            if (!IsToolReady())
                throw new InvalidOperationException("Frame Test component ไม่ครบหรือ hash ไม่ตรง กรุณาติดตั้งใหม่");
            ValidateTargetProcess(processId, expectedProcessName, expectedStartTimeUtcTicks);

            string csvPath = Path.Combine(Path.GetTempPath(), "GameBoostPro-Frame-" +
                Guid.NewGuid().ToString("N") + ".csv");
            string sessionName = "GameBoostPro-" + Guid.NewGuid().ToString("N");
            try
            {
                string arguments = "--process_id " + processId + " --delay 3 --timed 15 " +
                    "--terminate_after_timed --no_console_stats --session_name " +
                    sessionName + " --output_file \"" + csvPath + "\"";
                ProcessStartInfo info = new ProcessStartInfo(ToolPath, arguments);
                info.UseShellExecute = false;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.CreateNoWindow = true;
                using (Process capture = Process.Start(info))
                {
                    if (!capture.WaitForExit(30000))
                    {
                        try { capture.Kill(); }
                        catch { }
                        throw new InvalidOperationException("Frame Test ใช้เวลานานผิดปกติและถูกหยุดแล้ว");
                    }
                    string error = capture.StandardError.ReadToEnd();
                    if (capture.ExitCode != 0 && !File.Exists(csvPath))
                        throw new InvalidOperationException("PresentMon จับ frame ไม่สำเร็จ " + error.Trim());
                }
                ValidateTargetProcess(processId, expectedProcessName, expectedStartTimeUtcTicks);
                return FrameBenchmarkAnalyzer.AnalyzeCsv(csvPath, slot, gameName);
            }
            finally
            {
                try { if (File.Exists(csvPath)) File.Delete(csvPath); }
                catch { }
            }
        }

        private static void ValidateTargetProcess(int processId, string expectedProcessName,
            long expectedStartTimeUtcTicks)
        {
            try
            {
                using (Process game = Process.GetProcessById(processId))
                {
                    if (game.HasExited || !String.Equals(game.ProcessName, expectedProcessName,
                        StringComparison.OrdinalIgnoreCase) ||
                        game.StartTime.ToUniversalTime().Ticks != expectedStartTimeUtcTicks)
                        throw new InvalidOperationException();
                }
            }
            catch
            {
                throw new InvalidOperationException(
                    "เกมที่เลือกปิดหรือเปลี่ยนโปรเซสแล้ว กรุณาเปิด Advisor ใหม่");
            }
        }
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
        private const string StateRegistrySubKey = @"SOFTWARE\GameBoostPro";
        private const string StateRegistryValue = "RecoveryState";
        private static readonly string TestDirectory = ResolveTestDirectory();
        private static readonly bool ProtectedStateStore = String.IsNullOrWhiteSpace(TestDirectory);
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
        private static bool stateMigrationChecked;
        private static string recoveryWarning = "";

        public static bool UsesProtectedStateStore { get { return ProtectedStateStore; } }
        public static bool HasRecoveryWarning { get { return !String.IsNullOrWhiteSpace(recoveryWarning); } }
        public static string RecoveryWarning { get { return recoveryWarning; } }

        private static string ResolveTestDirectory()
        {
            string value = AppDomain.CurrentDomain.GetData("GameBoostPro.TestAppDirectory") as string;
            if (String.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value)) return "";
            return Path.GetFullPath(value);
        }

        private static string ResolveAppDirectory()
        {
            if (!String.IsNullOrWhiteSpace(TestDirectory)) return TestDirectory;
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
                    bool changed = false;
                    if (!fields.ContainsKey("Version") || config.Version < 4)
                    {
                        if (!fields.ContainsKey("AutoMode")) config.AutoMode = true;
                        config.ResetAdvancedDefaults();
                        changed = true;
                    }
                    if (!fields.ContainsKey("PowerPlanMode"))
                    {
                        config.PowerPlanMode = PowerPlanPolicy.Smart;
                        changed = true;
                    }
                    string normalizedPowerMode = PowerPlanPolicy.Normalize(config.PowerPlanMode);
                    if (!String.Equals(config.PowerPlanMode, normalizedPowerMode, StringComparison.Ordinal)) changed = true;
                    config.PowerPlanMode = normalizedPowerMode;
                    if (!fields.ContainsKey("Version") || config.Version < 5)
                    {
                        config.Version = 5;
                        changed = true;
                    }
                    if (changed) SaveConfig(config);
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
                    cachedState = DeserializeCurrentState(ReadCurrentState());
                    stateContentLoaded = true;
                }
                return cachedState;
            }
        }

        public static BoostState LoadStateForRestore()
        {
            lock (StateLock)
            {
                EnsureStateMigration();
                string serialized = ReadCurrentState();
                if (String.IsNullOrWhiteSpace(serialized))
                {
                    currentStateExists = false;
                    statePresenceLoaded = true;
                    stateContentLoaded = true;
                    cachedState = null;
                    return null;
                }

                BoostState state = DeserializeCurrentState(serialized);
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
                    state.Version = RecoveryStatePolicy.CurrentVersion;
                    BindStateOwner(state);
                    WriteCurrentState(StateJson.Serialize(state));
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
                DeleteCurrentState();
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
            EnsureStateMigration();
            currentStateExists = CurrentStateExists();
            legacyStateExists = File.Exists(LegacyStatePath);
            statePresenceLoaded = true;
        }

        private static void EnsureStateMigration()
        {
            if (!ProtectedStateStore || stateMigrationChecked) return;
            stateMigrationChecked = true;
            if (CurrentStateExists() || !File.Exists(StatePath)) return;
            try
            {
                BoostState state = StateJson.Deserialize<BoostState>(File.ReadAllText(StatePath));
                state = RecoveryStatePolicy.SanitizeMigratedState(state, GetCurrentOwnerSid());
                WriteCurrentState(StateJson.Serialize(state));
                File.Delete(StatePath);
            }
            catch
            {
                recoveryWarning = "พบข้อมูลกู้คืนรุ่นเก่าที่ตรวจสอบไม่ได้ ระบบจึงบล็อก Boost เพื่อความปลอดภัย";
            }
        }

        private static bool CurrentStateExists()
        {
            if (!ProtectedStateStore) return File.Exists(StatePath);
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(StateRegistrySubKey, false))
                    return key != null && key.GetValue(StateRegistryValue, null) is string;
            }
            catch { return false; }
        }

        private static string ReadCurrentState()
        {
            if (!ProtectedStateStore)
                return File.Exists(StatePath) ? File.ReadAllText(StatePath) : "";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(StateRegistrySubKey, false))
                return key == null ? "" : Convert.ToString(key.GetValue(StateRegistryValue, ""));
        }

        private static BoostState DeserializeCurrentState(string serialized)
        {
            BoostState state = StateJson.Deserialize<BoostState>(serialized);
            if (!ProtectedStateStore) return state;
            string ownerSid = GetCurrentOwnerSid();
            if (state == null || String.IsNullOrWhiteSpace(ownerSid) ||
                !String.Equals(state.OwnerSid, ownerSid, StringComparison.OrdinalIgnoreCase))
            {
                recoveryWarning = "Game Mode นี้เริ่มจาก Windows account อื่น จึงไม่อนุญาตให้คืนค่าข้ามบัญชี";
                throw new InvalidOperationException(recoveryWarning);
            }
            return state;
        }

        private static void BindStateOwner(BoostState state)
        {
            if (!ProtectedStateStore) return;
            string ownerSid = GetCurrentOwnerSid();
            if (String.IsNullOrWhiteSpace(ownerSid))
                throw new InvalidOperationException("ระบุ Windows account สำหรับข้อมูลกู้คืนไม่ได้");
            if (!String.IsNullOrWhiteSpace(state.OwnerSid) &&
                !String.Equals(state.OwnerSid, ownerSid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ไม่อนุญาตให้เขียนข้อมูลกู้คืนข้าม Windows account");
            state.OwnerSid = ownerSid;
        }

        private static string GetCurrentOwnerSid()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    return identity.User == null ? "" : identity.User.Value;
            }
            catch { return ""; }
        }

        private static void WriteCurrentState(string serialized)
        {
            if (!ProtectedStateStore)
            {
                Directory.CreateDirectory(AppDir);
                WriteAtomic(StatePath, serialized);
                return;
            }
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(StateRegistrySubKey))
            {
                if (key == null) throw new InvalidOperationException("สร้างพื้นที่กู้คืนแบบ Admin ไม่สำเร็จ");
                key.SetValue(StateRegistryValue, serialized, RegistryValueKind.String);
            }
        }

        private static void DeleteCurrentState()
        {
            if (!ProtectedStateStore)
            {
                if (File.Exists(StatePath)) File.Delete(StatePath);
                return;
            }
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(StateRegistrySubKey, true))
                if (key != null) key.DeleteValue(StateRegistryValue, false);
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

        private static PowerPlan ResolveTargetPowerPlan(PowerPlan current, PlatformProfile platform,
            AppConfig options)
        {
            string mode = PowerPlanPolicy.Normalize(options == null ? null : options.PowerPlanMode);
            if (PowerPlanPolicy.ShouldKeepCurrent(mode, platform))
                return new PowerPlan { Guid = current.Guid, Name = current.Name };
            return new PowerPlan { Guid = GetBestPerformanceScheme(), Name = "Game Boost Pro Ultimate" };
        }

        public static BoostState Enable(string gamePath, bool autoTriggered, int processId,
            PlatformProfile platform, AppConfig options)
        {
            if (Storage.HasState()) throw new InvalidOperationException("Game Mode เปิดอยู่แล้ว");
            if (Storage.HasRecoveryWarning)
                throw new InvalidOperationException(Storage.RecoveryWarning +
                    " กรุณาติดต่อผู้ดูแลก่อนเปิด Boost รอบใหม่");
            if (platform == null || !platform.IsSupported)
                throw new InvalidOperationException("แพลตฟอร์มนี้ยังไม่รองรับ Boost เพื่อป้องกันการชนกับซอฟต์แวร์ OEM");
            if (!IsAdmin())
                throw new InvalidOperationException("Best Mode ต้องใช้สิทธิ์ Administrator กรุณาเปิดโปรแกรมใหม่และกดยืนยัน UAC");

            PowerPlan current = GetActivePowerPlan();
            PowerPlan targetPower = ResolveTargetPowerPlan(current, platform, options);
            BoostState state = new BoostState
            {
                Version = RecoveryStatePolicy.CurrentVersion,
                OwnerSid = "",
                EnabledAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                PreviousPowerGuid = current.Guid,
                PreviousPowerName = current.Name,
                TargetPowerGuid = targetPower.Guid,
                TargetPowerName = targetPower.Name,
                PowerPlanMode = PowerPlanPolicy.Normalize(options.PowerPlanMode),
                PlatformTitle = platform.Title,
                AutoTriggered = autoTriggered,
                GamePath = gamePath ?? "",
                GameProcessId = processId,
                PreferHighPerformanceGpu = options.PreferHighPerformanceGpu,
                UseAboveNormalPriority = options.UseAboveNormalPriority,
                UseHighQos = options.UseHighQos,
                UseDynamicPriorityBoost = options.UseDynamicPriorityBoost,
                ProcessTuningStatus = processId > 0 ? "Waiting" : "NoGame",
                ProcessTuningDetail = processId > 0 ? "กำลังยืนยันค่าของโปรเซสเกม" : "รอโปรเซสเกมเริ่มทำงาน",
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
                if (!String.Equals(current.Guid, targetPower.Guid, StringComparison.OrdinalIgnoreCase))
                    RunPowerCfg("/S " + targetPower.Guid);
                PowerPlan active = GetActivePowerPlan();
                if (!String.Equals(active.Guid, targetPower.Guid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Windows ไม่ยอมใช้ Power Plan ที่เลือกไว้");

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

                RestoreStoredProcessScheduling(state);

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
            Process game = null;
            int targetProcessId = detectedProcessId > 0 ? detectedProcessId : state.GameProcessId;
            if (targetProcessId > 0)
            {
                try { game = Process.GetProcessById(targetProcessId); }
                catch { game = null; }
            }
            if (game == null) game = FindGameProcess(state.GamePath);
            if (game == null)
            {
                state.ProcessTuningStatus = "NoGame";
                state.ProcessTuningDetail = "รอโปรเซสเกมเริ่มทำงาน";
                return false;
            }

            try
            {
                if (!state.UseAboveNormalPriority && !state.UseHighQos && !state.UseDynamicPriorityBoost)
                {
                    CaptureProcessIdentity(state, game);
                    state.ProcessTuningAttempted = true;
                    state.ProcessTuningApplied = false;
                    state.ProcessTuningStatus = "NotRequested";
                    state.ProcessTuningDetail = "ไม่ได้เลือกปรับ scheduling ของเกม";
                    Storage.SaveState(state);
                    return true;
                }

                if (state.ProcessTuningAttempted && IsStoredProcessMatch(state, game))
                    return String.Equals(state.ProcessTuningStatus, "Applied", StringComparison.Ordinal);
                if (state.ProcessTuningApplied && state.GameProcessId == game.Id &&
                    !HasStoredProcessIdentity(state))
                    return false;

                if (state.ProcessTuningApplied) RestoreStoredProcessScheduling(state);
                ResetProcessTuningCapture(state);
                CaptureProcessIdentity(state, game);

                int requested = 0;
                bool priorityCaptured = false;
                bool priorityBoostCaptured = false;
                bool throttleCaptured = false;

                if (state.UseAboveNormalPriority)
                {
                    requested++;
                    try
                    {
                        state.PreviousPriority = game.PriorityClass.ToString();
                        priorityCaptured = true;
                    }
                    catch { }
                }
                if (state.UseDynamicPriorityBoost)
                {
                    requested++;
                    try
                    {
                        bool disabled;
                        priorityBoostCaptured = GetProcessPriorityBoost(game.Handle, out disabled);
                        state.HadPriorityBoostState = priorityBoostCaptured;
                        state.PreviousPriorityBoostDisabled = disabled;
                    }
                    catch { }
                }
                if (state.UseHighQos)
                {
                    requested++;
                    try
                    {
                        POWER_THROTTLING_STATE previous = new POWER_THROTTLING_STATE();
                        previous.Version = 1;
                        throttleCaptured = GetProcessInformation(game.Handle, ProcessPowerThrottling,
                            ref previous, Marshal.SizeOf(previous));
                        state.HadPowerThrottleState = throttleCaptured;
                        state.PreviousThrottleControl = throttleCaptured ? previous.ControlMask : 0;
                        state.PreviousThrottleState = throttleCaptured ? previous.StateMask : 0;
                    }
                    catch { }
                }

                state.ProcessTuningAttempted = true;
                state.ProcessTuningApplied = priorityCaptured || priorityBoostCaptured || throttleCaptured;
                state.ProcessTuningStatus = "Applying";
                state.ProcessTuningDetail = "บันทึกค่าเดิมแล้ว กำลังตรวจยืนยันผล";
                Storage.SaveState(state);

                int applied = 0;
                int verified = 0;
                if (priorityCaptured)
                {
                    try
                    {
                        game.PriorityClass = ProcessPriorityClass.AboveNormal;
                        applied++;
                        game.Refresh();
                        state.PriorityVerified = game.PriorityClass == ProcessPriorityClass.AboveNormal;
                        if (state.PriorityVerified) verified++;
                    }
                    catch { }
                }
                if (priorityBoostCaptured)
                {
                    try
                    {
                        if (SetProcessPriorityBoost(game.Handle, false)) applied++;
                        bool disabled;
                        state.PriorityBoostVerified = GetProcessPriorityBoost(game.Handle, out disabled) && !disabled;
                        if (state.PriorityBoostVerified) verified++;
                    }
                    catch { }
                }
                if (throttleCaptured)
                {
                    try
                    {
                        POWER_THROTTLING_STATE highQos = new POWER_THROTTLING_STATE();
                        highQos.Version = 1;
                        highQos.ControlMask = PowerThrottlingExecutionSpeed | PowerThrottlingIgnoreTimerResolution;
                        highQos.StateMask = 0;
                        if (SetProcessInformation(game.Handle, ProcessPowerThrottling,
                            ref highQos, Marshal.SizeOf(highQos))) applied++;
                        state.PowerThrottlingVerified = IsPowerThrottlingDisabled(game);
                        if (state.PowerThrottlingVerified) verified++;
                    }
                    catch { }
                }

                state.ProcessTuningApplied = applied > 0;
                state.ProcessRetentionVerified = false;
                if (verified == requested)
                {
                    state.ProcessTuningStatus = "Applied";
                    state.ProcessTuningDetail = "ยืนยันค่าที่เลือกครบ " + verified + "/" + requested;
                }
                else if (applied > 0)
                {
                    state.ProcessTuningStatus = "Partial";
                    state.ProcessTuningDetail = "ยืนยันได้ " + verified + "/" + requested + " ค่า เกมอาจจำกัดบางรายการ";
                }
                else
                {
                    state.ProcessTuningStatus = "Blocked";
                    state.ProcessTuningDetail = "เกมหรือ anti-cheat ไม่อนุญาตให้เปลี่ยน scheduling";
                }
                Storage.SaveState(state);
                return verified == requested;
            }
            catch
            {
                state.ProcessTuningAttempted = true;
                state.ProcessTuningApplied = false;
                state.ProcessTuningStatus = "Blocked";
                state.ProcessTuningDetail = "อ่าน identity หรือ scheduling ของเกมไม่ได้ และโปรแกรมจะไม่ลองซ้ำ";
                try { Storage.SaveState(state); }
                catch { }
                return false;
            }
            finally { if (game != null) game.Dispose(); }
        }

        public static bool NeedsProcessTuning(BoostState state, Process game)
        {
            if (state == null || game == null) return false;
            if (!state.UseAboveNormalPriority && !state.UseHighQos && !state.UseDynamicPriorityBoost) return false;
            int processId;
            try { processId = game.Id; }
            catch { return false; }
            if (state.GameProcessId != processId) return true;
            if (HasStoredProcessIdentity(state) && !IsStoredProcessMatch(state, game)) return true;
            return !state.ProcessTuningAttempted && !state.ProcessTuningApplied;
        }

        public static bool IsStoredProcessMatch(BoostState state, Process process)
        {
            if (state == null || process == null || !HasStoredProcessIdentity(state)) return false;
            try
            {
                if (process.Id != state.GameProcessId) return false;
                if (!String.Equals(process.ProcessName, state.GameProcessName, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (process.StartTime.ToUniversalTime().Ticks != state.GameProcessStartTimeUtcTicks) return false;
                if (!String.IsNullOrWhiteSpace(state.GameProcessPath))
                {
                    string currentPath = TryGetProcessPath(process);
                    if (!String.IsNullOrWhiteSpace(currentPath) &&
                        !String.Equals(currentPath, state.GameProcessPath, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                return true;
            }
            catch { return false; }
        }

        public static bool VerifyProcessTuningRetention(BoostState state, Process game)
        {
            if (state == null || game == null || state.ProcessRetentionVerified ||
                !String.Equals(state.ProcessTuningStatus, "Applied", StringComparison.Ordinal) ||
                !IsStoredProcessMatch(state, game)) return false;

            int requested = 0;
            int verified = 0;
            try
            {
                if (state.UseAboveNormalPriority)
                {
                    requested++;
                    game.Refresh();
                    if (game.PriorityClass == ProcessPriorityClass.AboveNormal) verified++;
                }
                if (state.UseDynamicPriorityBoost)
                {
                    requested++;
                    bool disabled;
                    if (GetProcessPriorityBoost(game.Handle, out disabled) && !disabled) verified++;
                }
                if (state.UseHighQos)
                {
                    requested++;
                    if (IsPowerThrottlingDisabled(game)) verified++;
                }
            }
            catch { }

            state.ProcessRetentionVerified = true;
            if (verified != requested)
            {
                state.ProcessTuningStatus = "NotRetained";
                state.ProcessTuningDetail = "เกมคืนค่าบางรายการเอง ยืนยันได้ " + verified + "/" + requested +
                    " และโปรแกรมจะไม่ฝืนตั้งซ้ำ";
            }
            else
            {
                state.ProcessTuningDetail = "ยืนยันซ้ำแล้วว่าค่ายังคงอยู่ " + verified + "/" + requested;
            }
            Storage.SaveState(state);
            return verified == requested;
        }

        private static bool HasStoredProcessIdentity(BoostState state)
        {
            return state != null && state.GameProcessId > 0 &&
                !String.IsNullOrWhiteSpace(state.GameProcessName) && state.GameProcessStartTimeUtcTicks > 0;
        }

        private static void CaptureProcessIdentity(BoostState state, Process game)
        {
            state.GameProcessId = game.Id;
            state.GameProcessName = game.ProcessName;
            state.GameProcessStartTimeUtcTicks = game.StartTime.ToUniversalTime().Ticks;
            state.GameProcessPath = TryGetProcessPath(game);
        }

        private static void ResetProcessTuningCapture(BoostState state)
        {
            state.GameProcessId = 0;
            state.GameProcessName = "";
            state.GameProcessStartTimeUtcTicks = 0;
            state.GameProcessPath = "";
            state.PreviousPriority = "";
            state.ProcessTuningApplied = false;
            state.ProcessTuningAttempted = false;
            state.ProcessTuningStatus = "Waiting";
            state.ProcessTuningDetail = "กำลังยืนยันค่าของโปรเซสเกม";
            state.PriorityVerified = false;
            state.PriorityBoostVerified = false;
            state.PowerThrottlingVerified = false;
            state.ProcessRetentionVerified = false;
            state.HadPriorityBoostState = false;
            state.PreviousPriorityBoostDisabled = false;
            state.HadPowerThrottleState = false;
            state.PreviousThrottleControl = 0;
            state.PreviousThrottleState = 0;
        }

        private static bool RestoreStoredProcessScheduling(BoostState state)
        {
            if (state == null || !state.ProcessTuningApplied || !HasStoredProcessIdentity(state)) return false;
            try
            {
                using (Process process = Process.GetProcessById(state.GameProcessId))
                {
                    if (!IsStoredProcessMatch(state, process)) return false;
                    if (!String.IsNullOrWhiteSpace(state.PreviousPriority))
                    {
                        ProcessPriorityClass previous;
                        if (Enum.TryParse<ProcessPriorityClass>(state.PreviousPriority, out previous))
                            process.PriorityClass = previous;
                    }
                    if (state.HadPriorityBoostState)
                        SetProcessPriorityBoost(process.Handle, state.PreviousPriorityBoostDisabled);
                    if (state.HadPowerThrottleState)
                    {
                        POWER_THROTTLING_STATE previousThrottle = new POWER_THROTTLING_STATE();
                        previousThrottle.Version = 1;
                        previousThrottle.ControlMask = state.PreviousThrottleControl;
                        previousThrottle.StateMask = state.PreviousThrottleState;
                        SetProcessInformation(process.Handle, ProcessPowerThrottling,
                            ref previousThrottle, Marshal.SizeOf(previousThrottle));
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private static bool IsPowerThrottlingDisabled(Process process)
        {
            POWER_THROTTLING_STATE current = new POWER_THROTTLING_STATE();
            current.Version = 1;
            if (!GetProcessInformation(process.Handle, ProcessPowerThrottling,
                ref current, Marshal.SizeOf(current))) return false;
            uint expected = PowerThrottlingExecutionSpeed | PowerThrottlingIgnoreTimerResolution;
            return (current.ControlMask & expected) == expected && (current.StateMask & expected) == 0;
        }

        private static string TryGetProcessPath(Process process)
        {
            try { return process.MainModule.FileName; }
            catch { return ""; }
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
                    catch { }
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
            if (plan != null && plan.ContainsKey("Guid"))
            {
                string guid = Convert.ToString(plan["Guid"]);
                if (!RecoveryStatePolicy.IsValidPowerGuid(guid))
                    throw new InvalidOperationException("ข้อมูล Power Plan รุ่นเก่าไม่ถูกต้อง");
                RunPowerCfg("/S " + guid);
            }

            object[] registry = root["Registry"] as object[];
            if (registry != null)
            {
                foreach (object raw in registry)
                {
                    Dictionary<string, object> old = raw as Dictionary<string, object>;
                    if (old == null) continue;
                    string fullPath = Convert.ToString(old["Path"]);
                    if (!fullPath.StartsWith("HKCU:\\", StringComparison.OrdinalIgnoreCase)) continue;
                    string subKey = fullPath.Substring(6);
                    string name = Convert.ToString(old["Name"]);
                    if (!RecoveryStatePolicy.IsAllowedLegacyRegistry(subKey, name)) continue;
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
        private readonly ComboBox powerPlan;
        private readonly Label powerPlanDetail;

        public AdvancedSettingsForm(AppConfig source)
        {
            config = source;
            Text = "Advanced Mode";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(650, 582);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 9);

            Controls.Add(CreateLabel("ADVANCED MODE", 26, 20, 300, 32, 16, FontStyle.Bold, Palette.Text));
            Controls.Add(CreateLabel("เลือกเฉพาะค่าที่เหมาะกับเครื่อง ค่าใหม่จะใช้ใน Boost รอบถัดไป", 27, 55, 560, 22,
                9, FontStyle.Regular, Palette.Muted));

            Panel power = new Panel { Location = new Point(26, 91), Size = new Size(598, 78), BackColor = Palette.SurfaceHigh };
            power.Controls.Add(CreateLabel("POWER PLAN", 14, 8, 130, 18, 8, FontStyle.Bold, Palette.Amber));
            powerPlan = new ComboBox
            {
                Location = new Point(392, 13),
                Size = new Size(190, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                FlatStyle = FlatStyle.Flat,
                BackColor = Palette.Back,
                ForeColor = Palette.Text,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            powerPlan.Items.AddRange(new object[] { "SMART (แนะนำ)", "ULTIMATE", "KEEP CURRENT" });
            string powerMode = PowerPlanPolicy.Normalize(source.PowerPlanMode);
            powerPlan.SelectedIndex = String.Equals(powerMode, PowerPlanPolicy.Ultimate, StringComparison.Ordinal) ? 1 :
                String.Equals(powerMode, PowerPlanPolicy.KeepCurrent, StringComparison.Ordinal) ? 2 : 0;
            powerPlanDetail = CreateLabel("", 14, 31, 365, 38, 8, FontStyle.Regular, Palette.Muted);
            powerPlanDetail.AutoEllipsis = true;
            power.Controls.Add(powerPlanDetail);
            power.Controls.Add(powerPlan);
            powerPlan.SelectedIndexChanged += delegate { UpdatePowerPlanDetail(); };
            powerPlan.DrawItem += DrawPowerPlanItem;
            Controls.Add(power);
            UpdatePowerPlanDetail();

            int y = 186;
            gameMode = AddSetting("Windows Game Mode", "ให้ Windows จัดลำดับทรัพยากรสำหรับเกม", y, source.EnableWindowsGameMode);
            capture = AddSetting("Disable background capture", "หยุด Game DVR และการอัดหน้าจอเบื้องหลัง", y += 52, source.DisableBackgroundCapture);
            gpu = AddSetting("High-performance GPU", "กำหนด GPU ประสิทธิภาพสูงให้ไฟล์เกม", y += 52, source.PreferHighPerformanceGpu);
            priority = AddSetting("AboveNormal priority", "เพิ่มลำดับ CPU โดยไม่ใช้ High หรือ Realtime", y += 52, source.UseAboveNormalPriority);
            highQos = AddSetting("Disable power throttling", "กัน Windows ลดความเร็วเฉพาะโปรเซสเกม", y += 52, source.UseHighQos);
            priorityBoost = AddSetting("Dynamic priority boost", "เปิดกลไกตอบสนองระยะสั้นของ Windows", y += 52, source.UseDynamicPriorityBoost);

            Button reset = CreateButton("RESET BEST", 26, 532, 122, Palette.SurfaceHigh, Palette.Text);
            reset.Click += delegate
            {
                gameMode.Value = capture.Value = gpu.Value = priority.Value = highQos.Value = priorityBoost.Value = true;
                powerPlan.SelectedIndex = 0;
            };
            Controls.Add(reset);
            Button cancel = CreateButton("ยกเลิก", 408, 532, 96, Palette.SurfaceHigh, Palette.Text);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
            Button save = CreateButton("SAVE", 514, 532, 110, Palette.Lime, Palette.Back);
            save.Click += delegate
            {
                config.EnableWindowsGameMode = gameMode.Value;
                config.DisableBackgroundCapture = capture.Value;
                config.PreferHighPerformanceGpu = gpu.Value;
                config.UseAboveNormalPriority = priority.Value;
                config.UseHighQos = highQos.Value;
                config.UseDynamicPriorityBoost = priorityBoost.Value;
                config.PowerPlanMode = powerPlan.SelectedIndex == 1 ? PowerPlanPolicy.Ultimate :
                    powerPlan.SelectedIndex == 2 ? PowerPlanPolicy.KeepCurrent : PowerPlanPolicy.Smart;
                config.Version = 5;
                Storage.SaveConfig(config);
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(save);
        }

        private void UpdatePowerPlanDetail()
        {
            if (powerPlan.SelectedIndex == 1)
            {
                powerPlanDetail.Text = "บังคับ Ultimate Performance และสร้างให้อัตโนมัติถ้ายังไม่มี";
                powerPlanDetail.ForeColor = Palette.Amber;
            }
            else if (powerPlan.SelectedIndex == 2)
            {
                powerPlanDetail.Text = "ไม่สลับแผนพลังงาน ปรับเฉพาะ Windows และโปรเซสเกม";
                powerPlanDetail.ForeColor = Palette.Muted;
            }
            else
            {
                powerPlanDetail.Text = "Acer เก็บแผนเดิม เช่น Nezha / Desktop ใช้ Ultimate";
                powerPlanDetail.ForeColor = Palette.Lime;
            }
        }

        private void DrawPowerPlanItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= powerPlan.Items.Count) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush background = new SolidBrush(selected ? Palette.SurfaceHigh : Palette.Back))
                e.Graphics.FillRectangle(background, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(powerPlan.Items[e.Index]), powerPlan.Font,
                e.Bounds, selected ? Palette.Lime : Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
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

    internal sealed class GraphicsAdvisorForm : Form
    {
        private sealed class AdvisorRow
        {
            public Label Detail { get; set; }
            public Label Status { get; set; }
        }

        private readonly string selectedGameName;
        private readonly string selectedGamePath;
        private readonly string selectedGameDirectory;
        private readonly int selectedProcessId;
        private readonly string selectedProcessName;
        private readonly long selectedProcessStartTimeUtcTicks;
        private readonly Label gameLabel;
        private readonly Label systemLabel;
        private readonly Label appLabel;
        private readonly Label summaryLabel;
        private readonly AdvisorRow dlssRow;
        private readonly AdvisorRow frameGenerationRow;
        private readonly AdvisorRow nisRow;
        private readonly AdvisorRow reflexRow;
        private readonly AdvisorRow smoothMotionRow;
        private readonly Button refreshButton;
        private bool loading;

        public GraphicsAdvisorForm(string gameName, string gamePath, string gameDirectory,
            int processId, string processName, long processStartTimeUtcTicks)
        {
            selectedGameName = gameName ?? "";
            selectedGamePath = gamePath ?? "";
            selectedGameDirectory = gameDirectory ?? "";
            selectedProcessId = processId;
            selectedProcessName = processName ?? "";
            selectedProcessStartTimeUtcTicks = processStartTimeUtcTicks;
            Text = "Graphics Advisor";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(780, 690);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 9);

            Controls.Add(CreateLabel("GRAPHICS ADVISOR", 26, 20, 360, 32, 16, FontStyle.Bold, Palette.Text));
            Label readOnly = CreateLabel("READ ONLY", 646, 22, 108, 28, 8, FontStyle.Bold, Palette.Back);
            readOnly.BackColor = Palette.Cyan;
            readOnly.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(readOnly);
            gameLabel = CreateLabel("กำลังตรวจสอบเกม...", 27, 57, 700, 23, 9, FontStyle.Bold, Palette.Muted);
            gameLabel.AutoEllipsis = true;
            Controls.Add(gameLabel);

            Panel systemBand = new Panel
            {
                Location = new Point(24, 92), Size = new Size(732, 76), BackColor = Palette.SurfaceHigh
            };
            systemLabel = CreateLabel("กำลังอ่าน GPU และเส้นทางจอ", 16, 10, 480, 54, 9,
                FontStyle.Bold, Palette.Text);
            appLabel = CreateLabel("NVIDIA APP  กำลังตรวจสอบ", 500, 10, 216, 54, 8,
                FontStyle.Bold, Palette.Muted);
            appLabel.TextAlign = ContentAlignment.MiddleRight;
            systemBand.Controls.Add(systemLabel);
            systemBand.Controls.Add(appLabel);
            Controls.Add(systemBand);

            Panel summaryBand = new Panel
            {
                Location = new Point(24, 181), Size = new Size(732, 70), BackColor = Palette.Surface
            };
            summaryBand.Controls.Add(CreateLabel("BEST ROUTE", 16, 10, 110, 18, 8,
                FontStyle.Bold, Palette.Amber));
            summaryLabel = CreateLabel("กำลังสร้างคำแนะนำที่เหมาะกับเครื่องนี้", 16, 30, 700, 32, 9,
                FontStyle.Bold, Palette.Text);
            summaryLabel.AutoEllipsis = true;
            summaryBand.Controls.Add(summaryLabel);
            Controls.Add(summaryBand);

            dlssRow = CreateAdvisorRow("DLSS SUPER RESOLUTION", 269);
            frameGenerationRow = CreateAdvisorRow("FRAME GENERATION", 331);
            nisRow = CreateAdvisorRow("NVIDIA IMAGE SCALING", 393);
            reflexRow = CreateAdvisorRow("NVIDIA REFLEX", 455);
            smoothMotionRow = CreateAdvisorRow("SMOOTH MOTION", 517);

            Panel footer = new Panel
            {
                Location = new Point(0, 610), Size = new Size(780, 80), BackColor = Palette.Surface
            };
            Button windowsGraphics = CreateButton("WINDOWS GRAPHICS", 24, 22, 166, Palette.SurfaceHigh, Palette.Text);
            windowsGraphics.Click += delegate { OpenTarget("ms-settings:display-advancedgraphics"); };
            Button nvidiaPanel = CreateButton("NVIDIA PANEL", 200, 22, 142, Palette.SurfaceHigh, Palette.Text);
            nvidiaPanel.Click += delegate { OpenTarget("nvcplui.exe"); };
            Button frameTest = CreateButton("FRAME TEST", 352, 22, 164, Palette.SurfaceHigh, Palette.Amber);
            frameTest.Click += OpenFrameTest;
            refreshButton = CreateButton("REFRESH", 526, 22, 100, Palette.SurfaceHigh, Palette.Cyan);
            refreshButton.Click += delegate { RefreshAdvisor(); };
            Button close = CreateButton("DONE", 636, 22, 120, Palette.Lime, Palette.Back);
            close.Click += delegate { Close(); };
            footer.Controls.Add(windowsGraphics);
            footer.Controls.Add(nvidiaPanel);
            footer.Controls.Add(frameTest);
            footer.Controls.Add(refreshButton);
            footer.Controls.Add(close);
            Controls.Add(footer);

            Shown += delegate { RefreshAdvisor(); };
        }

        private void OpenFrameTest(object sender, EventArgs e)
        {
            if (selectedProcessId <= 0 || String.IsNullOrWhiteSpace(selectedProcessName) ||
                selectedProcessStartTimeUtcTicks <= 0)
            {
                MessageBox.Show(this, "กรุณาเปิดเกมให้ระบบตรวจพบก่อนเริ่ม Frame Test", "Game Boost Pro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (FrameBenchmarkForm dialog = new FrameBenchmarkForm(selectedGameName, selectedProcessId,
                selectedProcessName, selectedProcessStartTimeUtcTicks))
                dialog.ShowDialog(this);
        }

        private AdvisorRow CreateAdvisorRow(string title, int y)
        {
            Label heading = CreateLabel(title, 28, y, 190, 22, 9, FontStyle.Bold, Palette.Text);
            Label detail = CreateLabel("รอตรวจสอบ", 222, y, 384, 42, 8, FontStyle.Regular, Palette.Muted);
            Label status = CreateLabel("CHECKING", 610, y, 142, 24, 8, FontStyle.Bold, Palette.Muted);
            status.TextAlign = ContentAlignment.TopRight;
            Controls.Add(heading);
            Controls.Add(detail);
            Controls.Add(status);
            Controls.Add(new Panel
            {
                BackColor = Palette.Line, Location = new Point(28, y + 48), Size = new Size(724, 1)
            });
            return new AdvisorRow { Detail = detail, Status = status };
        }

        private void RefreshAdvisor()
        {
            if (loading) return;
            loading = true;
            refreshButton.Enabled = false;
            refreshButton.Text = "CHECKING";
            Task.Factory.StartNew<GraphicsAdvisorSnapshot>(delegate
            {
                return GraphicsAdvisor.Inspect(selectedGameName, selectedGamePath, selectedGameDirectory);
            }).ContinueWith(delegate(Task<GraphicsAdvisorSnapshot> task)
            {
                try
                {
                    if (IsDisposed || Disposing) return;
                    BeginInvoke(new Action(delegate
                    {
                        loading = false;
                        refreshButton.Enabled = true;
                        refreshButton.Text = "REFRESH";
                        if (task.Status == TaskStatus.RanToCompletion) ApplySnapshot(task.Result);
                        else summaryLabel.Text = "อ่านข้อมูลกราฟิกไม่สำเร็จ กรุณาลองใหม่";
                    }));
                }
                catch { loading = false; }
            });
        }

        private void ApplySnapshot(GraphicsAdvisorSnapshot snapshot)
        {
            GraphicsCapabilities gpu = snapshot.Capabilities;
            gameLabel.Text = "GAME  " + snapshot.GameName +
                (String.IsNullOrWhiteSpace(snapshot.GamePath) ? " / runtime settings ยังไม่ยืนยัน" :
                " / " + Path.GetFileName(snapshot.GamePath));
            string routeText = snapshot.DisplayRoute == "Active" ? "NVIDIA SCAN-OUT ACTIVE" :
                snapshot.DisplayRoute == "Inactive" ? "NVIDIA SCAN-OUT INACTIVE" : "SCAN-OUT UNKNOWN";
            systemLabel.Text = gpu.GpuName + "\nDRIVER " + snapshot.DriverVersion + "  /  " + routeText;
            appLabel.Text = snapshot.HasNvidiaApp
                ? "NVIDIA APP\n" + (String.IsNullOrWhiteSpace(snapshot.NvidiaAppVersion) ? "INSTALLED" : snapshot.NvidiaAppVersion)
                : "NVIDIA APP\nNOT FOUND";
            appLabel.ForeColor = snapshot.HasNvidiaApp ? Palette.Cyan : Palette.Muted;

            if (snapshot.IsCompetitiveGame)
                summaryLabel.Text = "Competitive: ใช้ native resolution + Reflex ก่อน และไม่ใช้ Frame Generation";
            else if (gpu.SupportsDlssSuperResolution)
                summaryLabel.Text = "ใช้ DLSS ในเกมก่อน; Frame Generation ใช้เมื่อ base FPS ดีและเกมรองรับ";
            else
                summaryLabel.Text = "รักษาค่า native ก่อน แล้ววัด FPS/ความนิ่งจากในเกมก่อนเปลี่ยน scaler";

            if (gpu.SupportsDlssSuperResolution)
                SetRow(dlssRow, "CAPABLE", snapshot.HasDlssLibraryHint
                    ? "พบไฟล์ที่เกี่ยวข้องใกล้เกม แต่ต้องยืนยันสถานะในเมนูเกม"
                    : "RTX รองรับ ต้องตรวจว่าตัวเกมมีเมนู DLSS หรือไม่", Palette.Cyan);
            else
                SetRow(dlssRow, "NOT AVAILABLE", "GPU ที่ตรวจพบไม่ใช่ RTX; โปรแกรมจะไม่อ้างว่าเปิด DLSS ได้", Palette.Muted);

            if (gpu.SupportsFrameGeneration)
                SetRow(frameGenerationRow, snapshot.IsCompetitiveGame ? "SKIP FOR COMP" : "CAPABLE",
                    snapshot.HasFrameGenerationLibraryHint
                        ? "พบไฟล์ Frame Generation; ยังต้องเปิดและยืนยันในเกม"
                        : "RTX 40/50 รองรับเมื่อเกมรองรับ; RTX 40 ไม่มี Multi Frame Generation",
                    snapshot.IsCompetitiveGame ? Palette.Amber : Palette.Cyan);
            else
                SetRow(frameGenerationRow, "NOT AVAILABLE", "ไม่มี hardware path สำหรับ DLSS Frame Generation", Palette.Muted);

            if (snapshot.NisEligibility == "Eligible")
                SetRow(nisRow, "ELIGIBLE", "ต้องใช้ fullscreen ที่เหมาะสม + resolution ต่ำกว่า native และดู NIS indicator สีเขียว",
                    Palette.Lime);
            else if (snapshot.NisEligibility == "RouteBlocked")
                SetRow(nisRow, "ROUTE NOT READY", "RTX ไม่ได้รายงานว่าขับจออยู่; NIS driver scaling จึงไม่น่าใช้ได้บนจอนี้",
                    Palette.Coral);
            else if (snapshot.NisEligibility == "Unavailable")
                SetRow(nisRow, "NOT AVAILABLE", "ไม่พบ NVIDIA GPU สำหรับ driver NIS", Palette.Muted);
            else
                SetRow(nisRow, "UNVERIFIED", "ตรวจเส้นทางจอไม่ได้ จึงจะไม่เปิด NIS ให้อัตโนมัติ", Palette.Amber);

            SetRow(reflexRow, gpu.IsNvidia ? "CHECK IN GAME" : "GAME DEPENDENT",
                snapshot.IsCompetitiveGame
                    ? "ถ้าเกมมี Reflex ให้ใช้ฟังก์ชันในเกมก่อน และหลีกเลี่ยง Frame Generation"
                    : "Reflex เป็นฟังก์ชันในตัวเกม โปรแกรมภายนอกยืนยันว่าเปิดอยู่ไม่ได้",
                gpu.IsNvidia ? Palette.Cyan : Palette.Muted);

            if (!gpu.SupportsSmoothMotion)
                SetRow(smoothMotionRow, "NOT AVAILABLE", "ต้องใช้ RTX 40 Series ขึ้นไป", Palette.Muted);
            else if (snapshot.IsCompetitiveGame)
                SetRow(smoothMotionRow, "SKIP FOR COMP", "เพิ่มเฟรมที่สร้างขึ้น เหมาะกับเกมภาพมากกว่าเกมแข่งขัน", Palette.Amber);
            else if (!snapshot.HasNvidiaApp)
                SetRow(smoothMotionRow, "APP REQUIRED", "รองรับที่ GPU แต่ควรติดตั้ง/อัปเดต NVIDIA App ก่อน", Palette.Amber);
            else
                SetRow(smoothMotionRow, "AVAILABLE", "ใช้เมื่อเกมไม่มี native Frame Generation และอย่าเปิดซ้อนกัน", Palette.Cyan);
        }

        private static void SetRow(AdvisorRow row, string status, string detail, Color color)
        {
            row.Status.Text = status;
            row.Status.ForeColor = color;
            row.Detail.Text = detail;
        }

        private void OpenTarget(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "เปิดไม่สำเร็จ: " + ex.Message, "Game Boost Pro",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
                Text = text, Location = new Point(x, y), Size = new Size(width, 36), BackColor = back,
                ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }

    internal sealed class FrameBenchmarkForm : Form
    {
        private sealed class BenchmarkPane
        {
            public Label Fps { get; set; }
            public Label Detail { get; set; }
            public Label Mode { get; set; }
            public Button Capture { get; set; }
        }

        private readonly string gameName;
        private readonly int gameProcessId;
        private readonly string gameProcessName;
        private readonly long gameProcessStartTimeUtcTicks;
        private readonly string historyKey;
        private readonly BenchmarkPane baselinePane;
        private readonly BenchmarkPane boostedPane;
        private readonly Label currentMode;
        private readonly Label comparisonTitle;
        private readonly Label comparisonDetail;
        private readonly Timer countdown;
        private DateTime captureStarted;
        private bool capturing;

        public FrameBenchmarkForm(string selectedGameName, int processId, string processName,
            long processStartTimeUtcTicks)
        {
            gameName = String.IsNullOrWhiteSpace(selectedGameName) ? processName : selectedGameName;
            gameProcessId = processId;
            gameProcessName = processName;
            gameProcessStartTimeUtcTicks = processStartTimeUtcTicks;
            historyKey = processName + "|" + gameName + "|" +
                processStartTimeUtcTicks.ToString(CultureInfo.InvariantCulture);
            Text = "Frame Lab";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 540);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            BackColor = Palette.Back;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 9);

            Controls.Add(CreateLabel("FRAME LAB", 24, 20, 260, 32, 16, FontStyle.Bold, Palette.Text));
            Label badge = CreateLabel("15 SECOND CAPTURE", 548, 22, 148, 28, 8, FontStyle.Bold, Palette.Back);
            badge.BackColor = Palette.Amber;
            badge.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(badge);
            Controls.Add(CreateLabel(gameName, 25, 58, 470, 22, 9, FontStyle.Bold, Palette.Cyan));
            currentMode = CreateLabel("", 510, 58, 186, 22, 8, FontStyle.Bold, Palette.Muted);
            currentMode.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(currentMode);
            Controls.Add(CreateLabel("วัด app-present frame time ในฉากเดิม ผลหนึ่งรอบเป็นสัญญาณ ควรทำซ้ำก่อนเก็บค่า",
                25, 87, 665, 24, 8, FontStyle.Regular, Palette.Muted));

            baselinePane = CreatePane("BASELINE / NORMAL", 24, Palette.Muted);
            boostedPane = CreatePane("BOOSTED / GAME MODE", 368, Palette.Lime);
            baselinePane.Capture.Click += delegate { StartCapture("Baseline"); };
            boostedPane.Capture.Click += delegate { StartCapture("Boosted"); };

            Panel comparison = new Panel
            {
                Location = new Point(24, 374), Size = new Size(672, 82), BackColor = Palette.SurfaceHigh
            };
            comparisonTitle = CreateLabel("WAITING FOR A/B", 16, 12, 220, 22, 9, FontStyle.Bold, Palette.Amber);
            comparisonDetail = CreateLabel("เก็บ Baseline ตอนโหมดปกติ และ Boosted ตอน Game Mode เปิด",
                16, 37, 635, 34, 8, FontStyle.Regular, Palette.Muted);
            comparison.Controls.Add(comparisonTitle);
            comparison.Controls.Add(comparisonDetail);
            Controls.Add(comparison);

            Panel footer = new Panel
            {
                Location = new Point(0, 478), Size = new Size(720, 62), BackColor = Palette.Surface
            };
            Label privacy = CreateLabel("PresentMon ทำงานเฉพาะช่วงที่กดวัด แล้วลบ CSV ชั่วคราวทันที",
                24, 20, 470, 22, 8, FontStyle.Regular, Palette.Muted);
            Button close = CreateButton("DONE", 576, 13, 120, Palette.Lime, Palette.Back);
            close.Click += delegate { if (!capturing) Close(); };
            footer.Controls.Add(privacy);
            footer.Controls.Add(close);
            Controls.Add(footer);

            countdown = new Timer { Interval = 250 };
            countdown.Tick += UpdateCountdown;
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!capturing) return;
                e.Cancel = true;
                MessageBox.Show(this, "กรุณารอให้ Frame Test รอบนี้เสร็จก่อนปิดหน้าต่าง", "Game Boost Pro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            FormClosed += delegate { countdown.Stop(); countdown.Dispose(); };
            RenderHistory();
        }

        private BenchmarkPane CreatePane(string title, int x, Color accent)
        {
            Panel panel = new Panel
            {
                Location = new Point(x, 126), Size = new Size(328, 228), BackColor = Palette.Surface
            };
            panel.Controls.Add(CreateLabel(title, 16, 14, 250, 22, 9, FontStyle.Bold, accent));
            Label fps = CreateLabel("NOT CAPTURED", 16, 52, 296, 42, 19, FontStyle.Bold, Palette.Text);
            Label detail = CreateLabel("Average FPS  --\n1% Low  --    P95 frame  --",
                16, 100, 296, 48, 9, FontStyle.Regular, Palette.Muted);
            Label mode = CreateLabel("Present mode  --", 16, 151, 296, 20, 8, FontStyle.Regular, Palette.Muted);
            mode.AutoEllipsis = true;
            Button capture = CreateButton("CAPTURE", 16, 184, 296, Palette.SurfaceHigh, accent);
            panel.Controls.Add(fps);
            panel.Controls.Add(detail);
            panel.Controls.Add(mode);
            panel.Controls.Add(capture);
            Controls.Add(panel);
            return new BenchmarkPane { Fps = fps, Detail = detail, Mode = mode, Capture = capture };
        }

        private void StartCapture(string slot)
        {
            if (capturing) return;
            string boostSession = GetBoostSessionToken();
            bool boosted = !String.Equals(boostSession, "NORMAL", StringComparison.Ordinal);
            if (String.Equals(slot, "Baseline", StringComparison.Ordinal) && boosted)
            {
                MessageBox.Show(this, "กด RESTORE ก่อนเก็บ Baseline", "Game Boost Pro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (String.Equals(slot, "Boosted", StringComparison.Ordinal) && !boosted)
            {
                MessageBox.Show(this, "เปิด Game Mode ก่อนเก็บ Boosted", "Game Boost Pro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!PresentMonRunner.IsToolReady())
            {
                MessageBox.Show(this, "Frame Test component ไม่ครบหรือไม่ผ่านการตรวจ hash กรุณาติดตั้งใหม่",
                    "Game Boost Pro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            capturing = true;
            captureStarted = DateTime.UtcNow;
            baselinePane.Capture.Enabled = false;
            boostedPane.Capture.Enabled = false;
            currentMode.Text = "CAPTURING / กลับเข้าเกม";
            currentMode.ForeColor = Palette.Amber;
            countdown.Start();
            FocusGameWindow();

            Task.Factory.StartNew<FrameBenchmarkResult>(delegate
            {
                FrameBenchmarkResult result = PresentMonRunner.Capture(gameProcessId, gameProcessName,
                    gameProcessStartTimeUtcTicks, slot, gameName);
                if (!String.Equals(GetBoostSessionToken(), boostSession, StringComparison.Ordinal))
                    throw new InvalidOperationException("โหมด Boost เปลี่ยนระหว่างทดสอบ ผลรอบนี้จึงไม่ถูกเก็บ");
                return result;
            }).ContinueWith(delegate(Task<FrameBenchmarkResult> task)
            {
                try
                {
                    if (IsDisposed || Disposing) return;
                    BeginInvoke(new Action(delegate
                    {
                        countdown.Stop();
                        capturing = false;
                        WindowState = FormWindowState.Normal;
                        Show();
                        Activate();
                        if (task.Status == TaskStatus.RanToCompletion)
                            FrameBenchmarkStore.Save(historyKey, task.Result);
                        else
                            MessageBox.Show(this, task.Exception == null ? "Frame Test ไม่สำเร็จ" :
                                task.Exception.GetBaseException().Message, "Game Boost Pro",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        RenderHistory();
                    }));
                }
                catch { capturing = false; }
            });
        }

        private void FocusGameWindow()
        {
            try
            {
                using (Process process = Process.GetProcessById(gameProcessId))
                {
                    IntPtr window = process.MainWindowHandle;
                    if (window != IntPtr.Zero)
                    {
                        WindowState = FormWindowState.Minimized;
                        SetForegroundWindow(window);
                    }
                }
            }
            catch { }
        }

        private void UpdateCountdown(object sender, EventArgs e)
        {
            int remaining = Math.Max(0, 18 - (int)(DateTime.UtcNow - captureStarted).TotalSeconds);
            currentMode.Text = "CAPTURING  " + remaining + "s";
        }

        private void RenderHistory()
        {
            bool boosted = Storage.HasState();
            currentMode.Text = boosted ? "CURRENT  GAME MODE ON" : "CURRENT  NORMAL";
            currentMode.ForeColor = boosted ? Palette.Lime : Palette.Muted;
            FrameBenchmarkHistory history = FrameBenchmarkStore.Get(historyKey);
            RenderPane(baselinePane, history.Baseline);
            RenderPane(boostedPane, history.Boosted);
            bool toolReady = PresentMonRunner.IsToolReady();
            baselinePane.Capture.Enabled = !capturing && toolReady;
            boostedPane.Capture.Enabled = !capturing && toolReady;
            baselinePane.Capture.ForeColor = !boosted ? Palette.Muted : Palette.Amber;
            boostedPane.Capture.ForeColor = boosted ? Palette.Lime : Palette.Muted;
            baselinePane.Capture.Text = boosted ? "RESTORE TO CAPTURE" : "CAPTURE BASELINE";
            boostedPane.Capture.Text = boosted ? "CAPTURE BOOSTED" : "BOOST TO CAPTURE";
            RenderComparison(history);
        }

        private static string GetBoostSessionToken()
        {
            BoostState state = Storage.LoadState();
            if (state != null) return "CURRENT|" + (state.EnabledAt ?? "");
            return Storage.HasLegacyState() ? "LEGACY" : "NORMAL";
        }

        private static void RenderPane(BenchmarkPane pane, FrameBenchmarkResult result)
        {
            if (result == null)
            {
                pane.Fps.Text = "NOT CAPTURED";
                pane.Detail.Text = "Average FPS  --\n1% Low  --    P95 frame  --";
                pane.Mode.Text = "Present mode  --";
                return;
            }
            pane.Fps.Text = result.AverageFps.ToString("F1", CultureInfo.InvariantCulture) + " FPS";
            pane.Detail.Text = "1% LOW  " + result.OnePercentLowFps.ToString("F1", CultureInfo.InvariantCulture) +
                " FPS\nP95 FRAME  " + result.P95FrameTimeMs.ToString("F2", CultureInfo.InvariantCulture) +
                " ms  /  " + result.FrameCount + " frames";
            pane.Mode.Text = result.PresentMode;
        }

        private void RenderComparison(FrameBenchmarkHistory history)
        {
            if (history.Baseline == null || history.Boosted == null)
            {
                comparisonTitle.Text = "WAITING FOR A/B";
                comparisonTitle.ForeColor = Palette.Amber;
                comparisonDetail.Text = "เก็บ Baseline ตอนโหมดปกติ และ Boosted ตอน Game Mode เปิด";
                return;
            }
            double averageDelta = PercentChange(history.Baseline.AverageFps, history.Boosted.AverageFps);
            double lowDelta = PercentChange(history.Baseline.OnePercentLowFps, history.Boosted.OnePercentLowFps);
            double p95Improvement = ImprovementPercent(history.Baseline.P95FrameTimeMs,
                history.Boosted.P95FrameTimeMs);
            if (lowDelta >= 3.0 && p95Improvement >= 0)
            {
                comparisonTitle.Text = "BOOST LEADS IN THIS RUN";
                comparisonTitle.ForeColor = Palette.Lime;
            }
            else if (lowDelta <= -3.0 || p95Improvement <= -5.0)
            {
                comparisonTitle.Text = "BASELINE LEADS IN THIS RUN";
                comparisonTitle.ForeColor = Palette.Coral;
            }
            else
            {
                comparisonTitle.Text = "TOO CLOSE TO CALL";
                comparisonTitle.ForeColor = Palette.Amber;
            }
            comparisonDetail.Text = "Average " + FormatDelta(averageDelta) + "   /   1% Low " +
                FormatDelta(lowDelta) + "   /   P95 frame " + FormatDelta(p95Improvement) +
                " better\nทำซ้ำฉากเดิมอย่างน้อย 3 รอบก่อนตัดสินใจ";
        }

        private static double PercentChange(double baseline, double current)
        {
            return baseline <= 0 ? 0 : (current - baseline) / baseline * 100.0;
        }

        private static double ImprovementPercent(double baseline, double current)
        {
            return baseline <= 0 ? 0 : (baseline - current) / baseline * 100.0;
        }

        private static string FormatDelta(double value)
        {
            return (value >= 0 ? "+" : "") + value.ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr windowHandle);

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
                Text = text, Location = new Point(x, y), Size = new Size(width, 32), BackColor = back,
                ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }

    internal sealed class GpuUsageReader : IDisposable
    {
        private PerformanceCounterCategory category;
        private bool initializationAttempted;
        private Dictionary<string, CounterSample> previous = new Dictionary<string, CounterSample>();

        public float NextValue()
        {
            if (!initializationAttempted)
            {
                initializationAttempted = true;
                try { category = new PerformanceCounterCategory("GPU Engine"); }
                catch { category = null; }
                return 0;
            }
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
        private PlatformProfile platform;
        private readonly Label platformLabel;
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
        private readonly Button gpuAdvisorButton;
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
            platform = new PlatformProfile
            {
                Kind = PlatformKind.UnsupportedLaptop,
                IsSupported = false,
                Title = "CHECKING SYSTEM",
                Detail = "กำลังตรวจสอบชนิดเครื่องและ NitroSense"
            };
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
            platformLabel = MakeLabel(platform.Title, 390, 59, 310, 20, 8, FontStyle.Bold, Palette.Amber);
            Controls.Add(platformLabel);

            gpuAdvisorButton = MakeButton("GPU ADVISOR", 566, 23, 134, 34, Palette.SurfaceHigh, Palette.Cyan);
            gpuAdvisorButton.Click += OpenGraphicsAdvisor;
            Controls.Add(gpuAdvisorButton);
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
            activityText.AutoEllipsis = true;
            footer.Controls.Add(activityText);

            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
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
            autoSwitch.Enabled = false;
            dial.Enabled = false;
            RefreshAdvancedButton();
            RefreshGameProfile();
            RefreshState(platform.Detail);
            Shown += delegate
            {
                DetectPlatformAsync();
                RefreshCatalogAsync();
            };
        }

        private void DetectPlatformAsync()
        {
            Task.Factory.StartNew<PlatformProfile>(delegate { return PlatformDetector.Detect(); }).ContinueWith(
                delegate(Task<PlatformProfile> task)
                {
                    try
                    {
                        if (IsDisposed || Disposing) return;
                        BeginInvoke(new Action(delegate
                        {
                            if (task.Status != TaskStatus.RanToCompletion) return;
                            platform = task.Result;
                            platformLabel.Text = platform.Title;
                            platformLabel.ForeColor = platform.IsSupported ? Palette.Cyan : Palette.Coral;
                            dial.Enabled = platform.IsSupported;
                            autoSwitch.Enabled = platform.IsSupported && !working;
                            if (!platform.IsSupported && config.AutoMode)
                            {
                                config.AutoMode = false;
                                autoSwitch.Value = false;
                                Storage.SaveConfig(config);
                            }
                            RefreshState(platform.IsSupported ? platform.Detail : platform.Detail +
                                " / รองรับเฉพาะ Acer + NitroSense และ Desktop PC");
                        }));
                    }
                    catch { }
                });
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
            string targetPath = BoostTargetResolver.ResolveGamePath(candidate, config.GamePath);
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
                            if (SystemTuner.NeedsProcessTuning(latest, snapshot.Game.Process))
                                SystemTuner.ApplyGamePriority(latest, snapshot.Game.Process.Id);
                            else if (!latest.ProcessRetentionVerified)
                                SystemTuner.VerifyProcessTuningRetention(latest, snapshot.Game.Process);
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
            BoostState activeState = null;
            try { activeState = Storage.LoadState(); }
            catch { }
            activityText.Text = activeState != null
                ? game.DisplayName + " / " + GetProcessStatusText(activeState) +
                    (String.IsNullOrWhiteSpace(activeState.ProcessTuningDetail) ? "" :
                    "\n" + activeState.ProcessTuningDetail)
                : "พบ " + game.DisplayName + " / กำลังเตรียม Boost";
        }

        private void RefreshState(string activity)
        {
            activityText.Text = activity;
            UpdateStateVisuals();
        }

        private void UpdateStateVisuals()
        {
            BoostState currentState = null;
            try { currentState = Storage.LoadState(); }
            catch { }
            bool active = currentState != null || Storage.HasState();
            dial.Active = active;
            stateText.Text = active
                ? "เครื่องอยู่ในโหมดเล่นเกม\nกดอีกครั้งเพื่อคืนค่าทุกอย่าง"
                : "เครื่องอยู่ในโหมดปกติ\nค่าทุกอย่างพร้อมถูกจดจำก่อน Boost";
            int optionCount = GetAdvancedOptionCount();
            string configuredPower = PowerPlanPolicy.GetShortLabel(config.PowerPlanMode);
            string powerText = configuredPower + (active ? " ON" : " READY");
            if (active && currentState != null && String.Equals(currentState.PreviousPowerGuid,
                currentState.TargetPowerGuid, StringComparison.OrdinalIgnoreCase)) powerText = "PLAN KEPT";
            else if (active && currentState != null && String.Equals(currentState.PowerPlanMode,
                PowerPlanPolicy.Ultimate, StringComparison.OrdinalIgnoreCase)) powerText = "ULTIMATE ON";
            SetStatus(powerStatus, active, powerText);

            string tuningText = active && currentState != null ? GetProcessStatusText(currentState) :
                (optionCount == 6 ? "BEST 6/6" : "CUSTOM " + optionCount + "/6");
            Color tuningColor = currentState != null &&
                (String.Equals(currentState.ProcessTuningStatus, "Blocked", StringComparison.Ordinal) ||
                String.Equals(currentState.ProcessTuningStatus, "NotRetained", StringComparison.Ordinal))
                ? Palette.Amber : Palette.Cyan;
            SetStatus(modeStatus, active, tuningText, tuningColor);
            SetStatus(captureStatus, active && config.DisableBackgroundCapture,
                config.DisableBackgroundCapture ? (active ? "CAPTURE OFF" : "CAPTURE READY") : "CAPTURE KEEP",
                Palette.Amber);
            if (Storage.HasRecoveryWarning)
            {
                activityText.Text = Storage.RecoveryWarning;
                activityText.ForeColor = Palette.Coral;
            }
            else activityText.ForeColor = Palette.Muted;
            tray.Text = active ? "Game Boost Pro - Game Mode ON" : "Game Boost Pro - Normal";
            advancedButton.Enabled = !active && !working;
        }

        private static string GetProcessStatusText(BoostState state)
        {
            if (state == null) return "TUNING WAIT";
            if (String.Equals(state.ProcessTuningStatus, "Applied", StringComparison.Ordinal)) return "TUNING OK";
            if (String.Equals(state.ProcessTuningStatus, "Partial", StringComparison.Ordinal)) return "PARTIAL";
            if (String.Equals(state.ProcessTuningStatus, "Blocked", StringComparison.Ordinal)) return "GAME BLOCKED";
            if (String.Equals(state.ProcessTuningStatus, "NotRetained", StringComparison.Ordinal)) return "NOT RETAINED";
            if (String.Equals(state.ProcessTuningStatus, "NotRequested", StringComparison.Ordinal)) return "TUNING OFF";
            if (String.Equals(state.ProcessTuningStatus, "LegacyUnverified", StringComparison.Ordinal)) return "LEGACY SAFE";
            return "WAITING GAME";
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

        private void OpenGraphicsAdvisor(object sender, EventArgs e)
        {
            string selectedName = detectedGame != null ? detectedGame.DisplayName : GetConfiguredGameName();
            string selectedPath = detectedGame != null
                ? detectedGame.ExePath : (!String.IsNullOrWhiteSpace(config.GamePath)
                    ? config.GamePath : config.LibraryLaunchTarget);
            int processId = 0;
            string processName = "";
            long processStartTimeUtcTicks = 0;
            if (detectedGame != null && detectedGame.Process != null)
            {
                try
                {
                    processId = detectedGame.Process.Id;
                    processName = detectedGame.Process.ProcessName;
                    processStartTimeUtcTicks = detectedGame.Process.StartTime.ToUniversalTime().Ticks;
                }
                catch { }
            }
            using (GraphicsAdvisorForm dialog = new GraphicsAdvisorForm(selectedName, selectedPath,
                config.LibraryGameDirectory, processId, processName, processStartTimeUtcTicks))
                dialog.ShowDialog(this);
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
