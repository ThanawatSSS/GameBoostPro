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
[assembly: System.Reflection.AssemblyVersion(GameBoostPro.BuildVersion.Assembly)]
[assembly: System.Reflection.AssemblyFileVersion(GameBoostPro.BuildVersion.Assembly)]

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
                    Detail = UiText.T("Game Boost ควบคุม Power Plan และ Windows gaming stack", "Game Boost manages the power plan and Windows gaming settings")
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
                    Detail = UiText.T("Game Boost คุม Power / NitroSense คุมพัดลมและฮาร์ดแวร์", "Game Boost manages power / NitroSense manages fans and hardware")
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
                Detail = manufacturer + " " + model + UiText.T(" / รุ่นนี้ยังไม่เปิด Boost", " / Boost is unavailable on this model")
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
        public bool ShowTelemetry { get; set; }
        public string Language { get; set; }
        public string DefaultPreset { get; set; }
        public Dictionary<string, string> GamePresets { get; set; }
        public List<GameInstall> ManualGames { get; set; }

        public AppConfig()
        {
            Version = 6;
            Language = "TH";
            DefaultPreset = BoostProfiles.Balanced;
            GamePresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ManualGames = new List<GameInstall>();
            AutoMode = true;
            ShowTelemetry = true;
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
        public string Preset { get; set; }
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
                throw new InvalidOperationException(UiText.T("ข้อมูล Power Plan รุ่นเก่าไม่ถูกต้อง", "Invalid legacy power plan data"));
            if (String.IsNullOrWhiteSpace(ownerSid))
                throw new InvalidOperationException(UiText.T("ระบุเจ้าของข้อมูลกู้คืนไม่ได้", "Cannot identify the recovery data owner"));
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
            state.ProcessTuningDetail = UiText.T("ย้ายสถานะรุ่นเก่าอย่างปลอดภัย และไม่คืนค่าโปรเซสที่ยืนยัน identity ไม่ได้", "Legacy state migrated; processes with unverified identity will not be restored");
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
                GpuName = String.IsNullOrWhiteSpace(name) ? UiText.T("ไม่พบข้อมูล GPU", "No GPU data") : name.Trim(),
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
                DriverVersion = String.IsNullOrWhiteSpace(nvidiaDriver) ? UiText.T("ไม่ทราบ", "Unknown") : nvidiaDriver,
                DisplayRoute = route,
                NisEligibility = GetNisEligibility(capabilities.IsNvidia, route),
                HasHybridGraphics = capabilities.IsNvidia && hasOtherGpu,
                HasNvidiaApp = hasNvidiaApp,
                NvidiaAppVersion = appVersion,
                GameName = String.IsNullOrWhiteSpace(gameName) ? UiText.T("ยังไม่ได้เลือกเกม", "No game selected") : gameName,
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
            if (!File.Exists(path)) throw new InvalidOperationException(UiText.T("ไม่พบผล Frame Test", "No Frame Test results"));
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2) throw new InvalidOperationException(UiText.T("PresentMon ไม่พบ frame จากเกมนี้", "PresentMon found no frames for this game"));
            List<string> headers = ParseCsvLine(lines[0]);
            int frameTimeIndex = FindColumn(headers, "MsBetweenPresents");
            if (frameTimeIndex < 0) frameTimeIndex = FindColumn(headers, "MsBetweenDisplayChange");
            int swapChainIndex = FindColumn(headers, "SwapChainAddress");
            int presentModeIndex = FindColumn(headers, "PresentMode");
            if (frameTimeIndex < 0) throw new InvalidOperationException(UiText.T("รูปแบบ CSV ของ PresentMon ไม่รองรับ", "Unsupported PresentMon CSV format"));

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
                throw new InvalidOperationException(UiText.T("ข้อมูล frame น้อยเกินไป กรุณาเปิดเกมค้างไว้แล้วลองอีกครั้ง", "Not enough frames. Keep the game running and try again"));

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
                throw new InvalidOperationException(UiText.T("Frame Test component ไม่ครบหรือ hash ไม่ตรง กรุณาติดตั้งใหม่", "Frame Test component is missing or its hash does not match. Reinstall the app"));
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
                        throw new InvalidOperationException(UiText.T("Frame Test ใช้เวลานานผิดปกติและถูกหยุดแล้ว", "Frame Test timed out and was stopped"));
                    }
                    string error = capture.StandardError.ReadToEnd();
                    if (capture.ExitCode != 0 && !File.Exists(csvPath))
                        throw new InvalidOperationException(UiText.T("PresentMon จับ frame ไม่สำเร็จ ", "PresentMon capture failed: ") + error.Trim());
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
                    UiText.T("เกมที่เลือกปิดหรือเปลี่ยนโปรเซสแล้ว กรุณาเปิด Advisor ใหม่", "The game exited or changed process. Reopen the Advisor"));
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
        private static volatile Dictionary<string, List<string>> manualGamePaths =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static void ConfigureManualGames(IList<GameInstall> games)
        {
            Dictionary<string, List<string>> paths = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (GameInstall game in games)
            {
                if (game == null || BoostProfiles.PathKey(game.LaunchTarget, false).Length == 0) continue;
                string name = Path.GetFileNameWithoutExtension(game.LaunchTarget);
                if (SafetyPolicy.IsProtectedProcess(name)) continue;
                List<string> matches;
                if (!paths.TryGetValue(name, out matches)) paths[name] = matches = new List<string>();
                matches.Add(Path.GetFullPath(game.LaunchTarget));
            }
            manualGamePaths = paths;
        }

        internal static bool IsConfiguredManualPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            List<string> paths;
            return manualGamePaths.TryGetValue(Path.GetFileNameWithoutExtension(path), out paths) &&
                paths.Exists(delegate(string entry) { return String.Equals(entry, path, StringComparison.OrdinalIgnoreCase); });
        }
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
                        if (manualGamePaths.ContainsKey(processName))
                        {
                            string configuredPath = TryGetProcessPath(process);
                            if (IsConfiguredManualPath(configuredPath))
                            {
                                result = CreateDetectedGame(processName, configuredPath, "MANUAL", process);
                                break;
                            }
                        }
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
                !String.Equals(exePath, manualPath, StringComparison.OrdinalIgnoreCase) && !IsConfiguredManualPath(exePath))
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
                    BoostProfiles.NormalizeConfig(config);
                    if (config.Version < 6) { config.Version = 6; changed = true; }
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
                recoveryWarning = UiText.T("พบข้อมูลกู้คืนรุ่นเก่าที่ตรวจสอบไม่ได้ ระบบจึงบล็อก Boost เพื่อความปลอดภัย", "Unverified legacy recovery data found. Boost is blocked for safety");
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
                recoveryWarning = UiText.T("Game Mode นี้เริ่มจาก Windows account อื่น จึงไม่อนุญาตให้คืนค่าข้ามบัญชี", "This session belongs to another Windows account. Cross-account restore is blocked");
                throw new InvalidOperationException(recoveryWarning);
            }
            return state;
        }

        private static void BindStateOwner(BoostState state)
        {
            if (!ProtectedStateStore) return;
            string ownerSid = GetCurrentOwnerSid();
            if (String.IsNullOrWhiteSpace(ownerSid))
                throw new InvalidOperationException(UiText.T("ระบุ Windows account สำหรับข้อมูลกู้คืนไม่ได้", "Cannot identify the Windows account for recovery data"));
            if (!String.IsNullOrWhiteSpace(state.OwnerSid) &&
                !String.Equals(state.OwnerSid, ownerSid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(UiText.T("ไม่อนุญาตให้เขียนข้อมูลกู้คืนข้าม Windows account", "Cannot write recovery data for another Windows account"));
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
                if (key == null) throw new InvalidOperationException(UiText.T("สร้างพื้นที่กู้คืนแบบ Admin ไม่สำเร็จ", "Cannot create protected recovery storage"));
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
            IntPtr pointer = IntPtr.Zero;
            try
            {
                uint error = PowerGetActiveScheme(IntPtr.Zero, out pointer);
                if (error != 0) throw new Win32Exception((int)error, UiText.T("อ่านแผนพลังงานไม่สำเร็จ", "Cannot read the active power plan"));
                if (pointer == IntPtr.Zero) throw new InvalidOperationException(UiText.T("Windows ไม่ส่งแผนพลังงานกลับมา", "Windows returned no active power plan"));
                Guid guid = (Guid)Marshal.PtrToStructure(pointer, typeof(Guid));
                string name = UiText.T("แผนปัจจุบัน", "Current plan");
                uint size = 0;
                if (PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, null, ref size) == 0 &&
                    size > 0 && size <= 65536)
                {
                    byte[] buffer = new byte[size];
                    if (PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, buffer, ref size) == 0)
                        name = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
                }
                return new PowerPlan { Guid = guid.ToString(), Name = name };
            }
            finally { if (pointer != IntPtr.Zero) LocalFree(pointer); }
        }

        private static void SetActivePowerPlan(string value)
        {
            Guid guid;
            if (!Guid.TryParse(value, out guid)) throw new InvalidOperationException(UiText.T("Power Plan ไม่ถูกต้อง", "Invalid power plan"));
            value = guid.ToString();
            if (String.Equals(GetActivePowerPlan().Guid, value, StringComparison.OrdinalIgnoreCase)) return;
            uint error = PowerSetActiveScheme(IntPtr.Zero, ref guid);
            if (error != 0) throw new Win32Exception((int)error, UiText.T("สลับแผนพลังงานไม่สำเร็จ", "Cannot switch power plan"));
            if (!String.Equals(GetActivePowerPlan().Guid, value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(UiText.T("Windows ไม่คงแผนพลังงานที่เลือกไว้", "Windows did not retain the selected power plan"));
        }

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
        [DllImport("powrprof.dll")]
        private static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme, IntPtr subgroup,
            IntPtr setting, [Out] byte[] buffer, ref uint size);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        private static void ReportProgress(Action<string> progress, string message)
        {
            if (progress != null) progress(message);
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
                throw new InvalidOperationException(UiText.T("สร้าง Ultimate Performance power plan ไม่สำเร็จ", "Cannot create the Ultimate Performance plan"));
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
            PlatformProfile platform, AppConfig options, Action<string> progress = null)
        {
            if (!Storage.UsesProtectedStateStore)
                throw new InvalidOperationException("System tuning is disabled in the isolated test host.");
            if (Storage.HasState()) throw new InvalidOperationException(UiText.T("Game Mode เปิดอยู่แล้ว", "Game Mode is already active"));
            if (Storage.HasRecoveryWarning)
                throw new InvalidOperationException(Storage.RecoveryWarning +
                    UiText.T(" กรุณาติดต่อผู้ดูแลก่อนเปิด Boost รอบใหม่", " Contact an administrator before starting another Boost"));
            if (platform == null || !platform.IsSupported)
                throw new InvalidOperationException(UiText.T("แพลตฟอร์มนี้ยังไม่รองรับ Boost เพื่อป้องกันการชนกับซอฟต์แวร์ OEM", "Boost is unsupported on this platform to prevent OEM software conflicts"));
            if (!IsAdmin())
                throw new InvalidOperationException(UiText.T("Best Mode ต้องใช้สิทธิ์ Administrator กรุณาเปิดโปรแกรมใหม่และกดยืนยัน UAC", "Administrator access is required. Restart the app and approve UAC"));

            ReportProgress(progress, UiText.T("กำลังอ่านแผนพลังงาน", "Reading power plan"));
            PowerPlan current = GetActivePowerPlan();
            PowerPlan targetPower = ResolveTargetPowerPlan(current, platform, options);
            BoostState state = new BoostState
            {
                Version = RecoveryStatePolicy.CurrentVersion,
                OwnerSid = "",
                EnabledAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                Preset = options.DefaultPreset,
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
                ProcessTuningDetail = processId > 0 ? UiText.T("กำลังยืนยันค่าของโปรเซสเกม", "Verifying game process settings") : UiText.T("รอโปรเซสเกมเริ่มทำงาน", "Waiting for the game process"),
                Registry = new List<RegistrySnapshot>()
            };

            List<Tuple<string, string, object, RegistryValueKind>> enabledTweaks = GetEnabledTweaks(options);
            foreach (Tuple<string, string, object, RegistryValueKind> tweak in enabledTweaks)
                state.Registry.Add(Capture(tweak.Item1, tweak.Item2));

            if (options.PreferHighPerformanceGpu && !String.IsNullOrWhiteSpace(gamePath))
                state.Registry.Add(Capture(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath));

            ReportProgress(progress, UiText.T("กำลังบันทึกค่าเดิมสำหรับคืนค่า", "Saving original settings for recovery"));
            Storage.SaveState(state);
            try
            {
                ReportProgress(progress, UiText.T("กำลังตั้งค่าพลังงาน", "Applying power settings"));
                if (!String.Equals(current.Guid, targetPower.Guid, StringComparison.OrdinalIgnoreCase))
                    SetActivePowerPlan(targetPower.Guid);
                PowerPlan active = GetActivePowerPlan();
                if (!String.Equals(active.Guid, targetPower.Guid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(UiText.T("Windows ไม่ยอมใช้ Power Plan ที่เลือกไว้", "Windows rejected the selected power plan"));

                ReportProgress(progress, UiText.T("กำลังปรับ Game Mode และการบันทึกหน้าจอ", "Applying Game Mode and capture settings"));
                foreach (Tuple<string, string, object, RegistryValueKind> tweak in enabledTweaks)
                    SetRegistry(tweak.Item1, tweak.Item2, tweak.Item3, tweak.Item4);

                if (options.PreferHighPerformanceGpu && !String.IsNullOrWhiteSpace(gamePath))
                    SetRegistry(@"Software\Microsoft\DirectX\UserGpuPreferences", gamePath,
                        "GpuPreference=2;", RegistryValueKind.String);

                ReportProgress(progress, UiText.T("กำลังตรวจสอบค่าของโปรเซสเกม", "Checking game process settings"));
                ApplyGamePriority(state);
                return state;
            }
            catch
            {
                ReportProgress(progress, UiText.T("กำลังคืนค่าหลังพบปัญหา", "Restoring after an error"));
                try { Disable(progress); } catch { }
                throw;
            }
        }

        public static void Disable(Action<string> progress = null)
        {
            if (!Storage.UsesProtectedStateStore)
                throw new InvalidOperationException("System tuning is disabled in the isolated test host.");
            ReportProgress(progress, UiText.T("กำลังอ่านข้อมูลคืนค่า", "Reading recovery data"));
            if (Storage.HasCurrentState())
            {
                BoostState state = Storage.LoadStateForRestore();
                if (state == null) throw new InvalidOperationException(UiText.T("ข้อมูลคืนค่าเสียหาย กรุณาอย่าลบไฟล์สถานะ", "Recovery data is damaged. Do not delete the state file"));

                ReportProgress(progress, UiText.T("กำลังคืนค่าของโปรเซสเกม", "Restoring game process settings"));
                RestoreStoredProcessScheduling(state);

                ReportProgress(progress, UiText.T("กำลังคืนแผนพลังงานเดิม", "Restoring original power plan"));
                SetActivePowerPlan(state.PreviousPowerGuid);
                ReportProgress(progress, UiText.T("กำลังคืนค่าของ Windows", "Restoring Windows settings"));
                foreach (RegistrySnapshot item in state.Registry) Restore(item);
                Storage.DeleteState();
                return;
            }

            if (Storage.HasLegacyState())
            {
                RestoreLegacyState();
                return;
            }

            throw new InvalidOperationException(UiText.T("ไม่พบข้อมูลเดิมสำหรับคืนค่า", "No original settings found"));
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
                state.ProcessTuningDetail = UiText.T("รอโปรเซสเกมเริ่มทำงาน", "Waiting for the game process");
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
                    state.ProcessTuningDetail = UiText.T("ไม่ได้เลือกปรับ scheduling ของเกม", "Game scheduling was not requested");
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
                state.ProcessTuningDetail = UiText.T("บันทึกค่าเดิมแล้ว กำลังตรวจยืนยันผล", "Original settings saved; verifying results");
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
                    state.ProcessTuningDetail = UiText.T("ยืนยันค่าที่เลือกครบ ", "All selected settings verified: ") + verified + "/" + requested;
                }
                else if (applied > 0)
                {
                    state.ProcessTuningStatus = "Partial";
                    state.ProcessTuningDetail = UiText.T("ยืนยันได้ ", "Verified: ") + verified + "/" + requested + UiText.T(" ค่า เกมอาจจำกัดบางรายการ", " settings. The game may restrict some changes");
                }
                else
                {
                    state.ProcessTuningStatus = "Blocked";
                    state.ProcessTuningDetail = UiText.T("เกมหรือ anti-cheat ไม่อนุญาตให้เปลี่ยน scheduling", "The game or anti-cheat blocked scheduling changes");
                }
                Storage.SaveState(state);
                return verified == requested;
            }
            catch
            {
                state.ProcessTuningAttempted = true;
                state.ProcessTuningApplied = false;
                state.ProcessTuningStatus = "Blocked";
                state.ProcessTuningDetail = UiText.T("อ่าน identity หรือ scheduling ของเกมไม่ได้ และโปรแกรมจะไม่ลองซ้ำ", "Cannot read game identity or scheduling. No repeated attempts will be made");
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
                state.ProcessTuningDetail = UiText.T("เกมคืนค่าบางรายการเอง ยืนยันได้ ", "The game reverted some settings. Verified: ") + verified + "/" + requested +
                    UiText.T(" และโปรแกรมจะไม่ฝืนตั้งซ้ำ", "; no forced reapplication");
            }
            else
            {
                state.ProcessTuningDetail = UiText.T("ยืนยันซ้ำแล้วว่าค่ายังคงอยู่ ", "Retention verified: ") + verified + "/" + requested;
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
            state.ProcessTuningDetail = UiText.T("กำลังยืนยันค่าของโปรเซสเกม", "Verifying game process settings");
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
                    throw new InvalidOperationException(UiText.T("ตั้งค่าแผนพลังงานไม่สำเร็จ ", "Power plan update failed: ") + error.Trim());
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
                    throw new InvalidOperationException(UiText.T("ข้อมูล Power Plan รุ่นเก่าไม่ถูกต้อง", "Invalid legacy power plan data"));
                SetActivePowerPlan(guid);
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
        private readonly SolidBrush background = new SolidBrush(Palette.Lime);
        public event EventHandler BoostClick;
        public bool Active
        {
            get { return active; }
            set { if (active == value) return; active = value; Invalidate(); }
        }
        public bool Busy
        {
            get { return busy; }
            set { if (busy == value) return; busy = value; Invalidate(); }
        }
        public BoostDial()
        {
            Size = new Size(186, 52);
            Font = UiText.Body(13, FontStyle.Bold);
            DoubleBuffered = true;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = "Boost / Restore";
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (Enabled && e.Button == MouseButtons.Left && !busy && BoostClick != null) BoostClick(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && !busy && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                if (BoostClick != null) BoostClick(this, EventArgs.Empty);
                e.Handled = e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            background.Color = !Enabled ? Palette.SurfaceHigh : busy ? Palette.Amber : active ? Palette.Coral :
                hover ? Color.FromArgb(218, 255, 143) : Palette.Lime;
            e.Graphics.FillRectangle(background, ClientRectangle);
            string text = busy ? UiText.T("กำลังทำงาน", "Working") : active ? UiText.T("คืนค่าเดิม", "Restore") : "BOOST";
            TextRenderer.DrawText(e.Graphics, text, Font, ClientRectangle, Enabled ? Palette.Back : Palette.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), Palette.Back, background.Color);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) background.Dispose();
            base.Dispose(disposing);
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
            BackColor = Palette.Back;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
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
            GraphicsState state = e.Graphics.Save();
            e.Graphics.ScaleTransform(Width / 48f, Height / 26f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color track = Enabled && value ? Palette.Lime : Palette.Line;
            using (SolidBrush brush = new SolidBrush(track))
            {
                e.Graphics.FillEllipse(brush, 0, 0, 26, 26);
                e.Graphics.FillEllipse(brush, 22, 0, 26, 26);
                e.Graphics.FillRectangle(brush, 13, 0, 22, 26);
            }
            int x = value ? 25 : 3;
            using (SolidBrush knob = new SolidBrush(value ? Palette.Back : Palette.Text))
                e.Graphics.FillEllipse(knob, x, 3, 20, 20);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(1, 1, 46, 24), Palette.Text, track);
            e.Graphics.Restore(state);
        }
    }

    internal sealed class MetricBar : Control
    {
        private float value = Single.NaN;
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
                float next = Single.IsNaN(value) || Single.IsInfinity(value) ? Single.NaN : Math.Max(0, Math.Min(100, value));
                if ((Single.IsNaN(next) && Single.IsNaN(this.value)) || Math.Abs(next - this.value) < 0.25f) return;
                this.value = next;
                Invalidate();
            }
        }

        public MetricBar()
        {
            Size = new Size(180, 48);
            BackColor = Palette.Back;
            Caption = "";
            DoubleBuffered = true;
            labelFont = new Font("Leelawadee UI", 9);
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
            GraphicsState state = e.Graphics.Save();
            float scale = Font.SizeInPoints / 10f;
            e.Graphics.ScaleTransform(scale, scale);
            float drawingWidth = Width / scale;
            e.Graphics.DrawString(Caption, labelFont, mutedBrush, 0, 1);
            string numberText = Single.IsNaN(value) ? "--" : Math.Round(value).ToString(CultureInfo.InvariantCulture) + "%";
            SizeF size = e.Graphics.MeasureString(numberText, numberFont);
            e.Graphics.DrawString(numberText, numberFont, textBrush, drawingWidth - size.Width, 0);
            float barY = Math.Max(30, size.Height + 8);
            e.Graphics.FillRectangle(trackBrush, 0, barY, drawingWidth, 4);
            fillBrush.Color = Caption == "RAM" && value >= 90 ? Palette.Amber :
                Caption == "GPU 3D" ? Palette.Cyan : Palette.Lime;
            if (!Single.IsNaN(value)) e.Graphics.FillRectangle(fillBrush, 0, barY, (int)(drawingWidth * value / 100f), 4);
            e.Graphics.Restore(state);
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
            WorkspaceLayout.Form(this, UiText.T("คลังเกม", "Game library"), new Size(860, 620), new Size(640, 480));
            TableLayoutPanel layout = WorkspaceLayout.Grid(1, 44, 28, 72, -1, 30, 96);
            layout.Padding = new Padding(20, 12, 20, 12);
            layout.Controls.Add(WorkspaceLayout.Label(UiText.T("คลังเกม", "Game library"), 20, Palette.Text), 0, 0);
            layout.Controls.Add(WorkspaceLayout.Label(UiText.T("Steam / Epic / Riot / เกมที่เพิ่มเอง", "Steam / Epic / Riot / Manual games"), 10, Palette.Muted), 0, 1);
            TableLayoutPanel filters = WorkspaceLayout.Grid(2, 28, -1);
            filters.ColumnStyles[0].Width = 70; filters.ColumnStyles[1].Width = 30;
            filters.Controls.Add(WorkspaceLayout.Label(UiText.T("ค้นหาเกม", "Search games"), 10, Palette.Text), 0, 0);
            filters.Controls.Add(WorkspaceLayout.Label(UiText.T("แหล่งที่มา", "Source"), 10, Palette.Text), 1, 0);
            searchBox = new TextBox { Dock = DockStyle.Fill, BackColor = Palette.SurfaceHigh, ForeColor = Palette.Text,
                BorderStyle = BorderStyle.FixedSingle, Font = Font, Margin = new Padding(0, 4, 12, 6) };
            sourceFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Palette.Surface, ForeColor = Palette.Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 4, 0, 6) };
            sourceFilter.Items.AddRange(new object[] { "ALL", "STEAM", "EPIC", "RIOT", "MANUAL" });
            searchBox.TextChanged += delegate { RefreshItems(null); };
            sourceFilter.SelectedIndexChanged += delegate { RefreshItems(null); };
            filters.Controls.Add(searchBox, 0, 1); filters.Controls.Add(sourceFilter, 1, 1);
            layout.Controls.Add(filters, 0, 2);
            games = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
                HideSelection = false, BackColor = Palette.Surface, ForeColor = Palette.Text, BorderStyle = BorderStyle.None };
            games.Columns.Add(UiText.T("เกม", "Game"), 250);
            games.Columns.Add(UiText.T("แหล่งที่มา", "Source"), 95);
            games.Columns.Add(UiText.T("สถานะ", "Status"), 110);
            games.Columns.Add(UiText.T("ตำแหน่งติดตั้ง", "Install location"), 330);
            games.SizeChanged += delegate
            {
                if (games.ClientSize.Width < 100) return;
                int width = games.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
                games.Columns[0].Width = Math.Max(160, width * 32 / 100);
                games.Columns[1].Width = Math.Max(70, width * 13 / 100);
                games.Columns[2].Width = Math.Max(90, width * 17 / 100);
                games.Columns[3].Width = Math.Max(160, width - games.Columns[0].Width - games.Columns[1].Width - games.Columns[2].Width);
            };
            catalog.Sort(delegate(GameInstall left, GameInstall right) { return StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName, right.DisplayName); });
            games.SelectedIndexChanged += delegate { UpdateActions(); };
            games.DoubleClick += delegate { UseSelection(CanLaunch(GetSelected())); };
            layout.Controls.Add(games, 0, 3);
            countLabel = WorkspaceLayout.Label("", 9, Palette.Muted);
            layout.Controls.Add(countLabel, 0, 4);
            TableLayoutPanel actions = WorkspaceLayout.Grid(3, 48, 48);
            Button add = WorkspaceLayout.Button(UiText.T("เพิ่มไฟล์เกม", "Add game"), Palette.Text);
            add.Click += AddManualGame;
            folderButton = WorkspaceLayout.Button(UiText.T("เปิดโฟลเดอร์", "Open folder"), Palette.Text);
            folderButton.Enabled = false; folderButton.Click += OpenSelectedFolder;
            Button cancel = WorkspaceLayout.Button(UiText.T("ยกเลิก", "Cancel"), Palette.Text);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            launchButton = WorkspaceLayout.Button(UiText.T("เปิดเกม", "Launch"), Palette.Lime);
            launchButton.Enabled = false; launchButton.Click += delegate { UseSelection(true); };
            useButton = WorkspaceLayout.Button(UiText.T("เลือกเกมนี้", "Select game"), Palette.Lime);
            useButton.Enabled = false; useButton.Click += delegate { UseSelection(false); };
            actions.Controls.Add(add, 0, 0); actions.Controls.Add(folderButton, 1, 0);
            actions.Controls.Add(cancel, 0, 1); actions.Controls.Add(launchButton, 1, 1); actions.Controls.Add(useButton, 2, 1);
            layout.Controls.Add(actions, 0, 5);
            Controls.Add(layout);
            AcceptButton = useButton; CancelButton = cancel;
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
                item.SubItems.Add(CanLaunch(game) ? UiText.T("พร้อมเปิด", "Ready") : UiText.T("ใช้ Launcher", "Use launcher"));
                item.SubItems.Add(game.DirectoryPath);
                item.Tag = game;
                games.Items.Add(item);
                if (select != null && String.Equals(game.DirectoryPath, select.DirectoryPath,
                    StringComparison.OrdinalIgnoreCase)) item.Selected = true;
            }
            games.EndUpdate();
            countLabel.Text = games.Items.Count + UiText.T(" จาก ", " of ") + catalog.Count + UiText.T(" เกม", " games");
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
                dialog.Title = UiText.T("เพิ่มไฟล์เกมใน Library", "Add game to library");
                dialog.Filter = UiText.T("ไฟล์เกม (*.exe)|*.exe", "Game executable (*.exe)|*.exe");
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

        private readonly TableLayoutPanel settingsPage;

        public AdvancedSettingsForm(AppConfig source)
        {
            config = source;
            WorkspaceLayout.Form(this, UiText.T("ตั้งค่าขั้นสูง", "Advanced"), new Size(760, 730), new Size(640, 500));
            settingsPage = WorkspaceLayout.ScrollPage(this, 720);
            settingsPage.RowCount = 11; settingsPage.RowStyles.Clear();
            foreach (int height in new[] { 48, 40, 110, 68, 68, 68, 68, 68, 68, 64 })
                settingsPage.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            settingsPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settingsPage.Controls.Add(WorkspaceLayout.Label(UiText.T("ตั้งค่าขั้นสูง", "Advanced"), 20, Palette.Text), 0, 0);
            settingsPage.Controls.Add(WorkspaceLayout.Label(UiText.T("ค่าที่อนุญาตให้โปรไฟล์ใช้ / มีผลรอบถัดไป", "Allowed settings for profiles / Next session"), 10, Palette.Cyan), 0, 1);
            TableLayoutPanel power = WorkspaceLayout.Grid(2, 48, -1);
            power.ColumnStyles[0].Width = 60; power.ColumnStyles[1].Width = 40;
            power.Controls.Add(WorkspaceLayout.Label("Power plan", 12, Palette.Amber), 0, 0);
            powerPlan = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24, FlatStyle = FlatStyle.Flat,
                BackColor = Palette.Surface, ForeColor = Palette.Text, Font = Font, Margin = new Padding(6, 8, 0, 6) };
            powerPlan.Items.AddRange(new object[] { UiText.T("SMART (แนะนำ)", "SMART (recommended)"), "ULTIMATE", "KEEP CURRENT" });
            string powerMode = PowerPlanPolicy.Normalize(source.PowerPlanMode);
            powerPlan.SelectedIndex = String.Equals(powerMode, PowerPlanPolicy.Ultimate, StringComparison.Ordinal) ? 1 :
                String.Equals(powerMode, PowerPlanPolicy.KeepCurrent, StringComparison.Ordinal) ? 2 : 0;
            powerPlanDetail = WorkspaceLayout.Label("", 10, Palette.Muted);
            power.Controls.Add(powerPlan, 1, 0); power.Controls.Add(powerPlanDetail, 0, 1);
            power.SetColumnSpan(powerPlanDetail, 2);
            powerPlan.SelectedIndexChanged += delegate { UpdatePowerPlanDetail(); };
            powerPlan.DrawItem += DrawPowerPlanItem;
            settingsPage.Controls.Add(power, 0, 2);
            UpdatePowerPlanDetail();

            gameMode = AddSetting("Windows Game Mode", UiText.T("ให้ Windows จัดลำดับทรัพยากรสำหรับเกม", "Allow Windows Game Mode"), 3, source.EnableWindowsGameMode);
            capture = AddSetting("Background capture", UiText.T("หยุดการอัดหน้าจอเบื้องหลังระหว่าง Boost", "Pause Windows background recording during Boost"), 4, source.DisableBackgroundCapture);
            gpu = AddSetting("High-performance GPU", UiText.T("กำหนด GPU ประสิทธิภาพสูงให้ไฟล์เกม", "Prefer the high-performance GPU for the game executable"), 5, source.PreferHighPerformanceGpu);
            priority = AddSetting("AboveNormal priority", UiText.T("เพิ่มลำดับ CPU โดยไม่ใช้ High หรือ Realtime", "Use AboveNormal, never High or Realtime"), 6, source.UseAboveNormalPriority);
            highQos = AddSetting("Disable power throttling", UiText.T("กัน Windows ลดความเร็วเฉพาะโปรเซสเกม", "Disable Windows power throttling for the game process"), 7, source.UseHighQos);
            priorityBoost = AddSetting("Dynamic priority boost", UiText.T("เปิดกลไกตอบสนองระยะสั้นของ Windows", "Allow Windows dynamic priority boosts"), 8, source.UseDynamicPriorityBoost);
            TableLayoutPanel footer = WorkspaceLayout.Grid(3, -1);
            Button reset = WorkspaceLayout.Button(UiText.T("ค่าเริ่มต้น", "Defaults"), Palette.Text);
            reset.Click += delegate
            {
                gameMode.Value = capture.Value = gpu.Value = priority.Value = highQos.Value = priorityBoost.Value = true;
                powerPlan.SelectedIndex = 0;
            };
            Button cancel = WorkspaceLayout.Button(UiText.T("ยกเลิก", "Cancel"), Palette.Text);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Button save = WorkspaceLayout.Button(UiText.T("บันทึก", "Save"), Palette.Lime);
            save.Click += delegate
            {
                bool oldMode = config.EnableWindowsGameMode, oldCapture = config.DisableBackgroundCapture,
                    oldGpu = config.PreferHighPerformanceGpu, oldPriority = config.UseAboveNormalPriority,
                    oldQos = config.UseHighQos, oldDynamic = config.UseDynamicPriorityBoost;
                string oldPower = config.PowerPlanMode;
                config.EnableWindowsGameMode = gameMode.Value;
                config.DisableBackgroundCapture = capture.Value;
                config.PreferHighPerformanceGpu = gpu.Value;
                config.UseAboveNormalPriority = priority.Value;
                config.UseHighQos = highQos.Value;
                config.UseDynamicPriorityBoost = priorityBoost.Value;
                config.PowerPlanMode = powerPlan.SelectedIndex == 1 ? PowerPlanPolicy.Ultimate :
                    powerPlan.SelectedIndex == 2 ? PowerPlanPolicy.KeepCurrent : PowerPlanPolicy.Smart;
                config.Version = 6;
                try { Storage.SaveConfig(config); DialogResult = DialogResult.OK; Close(); }
                catch (Exception ex)
                {
                    config.EnableWindowsGameMode = oldMode; config.DisableBackgroundCapture = oldCapture;
                    config.PreferHighPerformanceGpu = oldGpu; config.UseAboveNormalPriority = oldPriority;
                    config.UseHighQos = oldQos; config.UseDynamicPriorityBoost = oldDynamic;
                    config.PowerPlanMode = oldPower;
                    MessageBox.Show(this, ex.Message, "Game Boost Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            footer.Controls.Add(reset, 0, 0); footer.Controls.Add(cancel, 1, 0); footer.Controls.Add(save, 2, 0);
            settingsPage.Controls.Add(footer, 0, 9);
        }

        private void UpdatePowerPlanDetail()
        {
            if (powerPlan.SelectedIndex == 1)
            {
                powerPlanDetail.Text = UiText.T("ใช้กับ Performance บน Desktop เท่านั้น / Laptop คงแผนเดิม", "Desktop Performance only; laptops keep their plan");
                powerPlanDetail.ForeColor = Palette.Amber;
            }
            else if (powerPlan.SelectedIndex == 2)
            {
                powerPlanDetail.Text = UiText.T("ไม่สลับแผนพลังงาน ปรับเฉพาะ Windows และโปรเซสเกม", "Keep the current plan; change only allowed Windows and process settings");
                powerPlanDetail.ForeColor = Palette.Muted;
            }
            else
            {
                powerPlanDetail.Text = UiText.T("Acer เก็บแผนเดิม เช่น Nezha / Desktop ใช้ Ultimate", "Performance: Acer keeps its plan / Desktop uses Ultimate");
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

        private ToggleSwitch AddSetting(string title, string detail, int row, bool value)
        {
            TableLayoutPanel line = WorkspaceLayout.Grid(2, -1);
            line.ColumnStyles[0].SizeType = SizeType.Percent; line.ColumnStyles[0].Width = 100;
            line.ColumnStyles[1].SizeType = SizeType.Absolute; line.ColumnStyles[1].Width = 66;
            TableLayoutPanel identity = WorkspaceLayout.Grid(1, 28, -1);
            identity.Controls.Add(WorkspaceLayout.Label(title, 11, Palette.Text), 0, 0);
            identity.Controls.Add(WorkspaceLayout.Label(detail, 9, Palette.Muted), 0, 1);
            ToggleSwitch toggle = new ToggleSwitch { Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 12, 0), Value = value,
                AccessibleName = title };
            line.Controls.Add(identity, 0, 0); line.Controls.Add(toggle, 1, 0);
            settingsPage.Controls.Add(line, 0, row);
            return toggle;
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

        private readonly AppConfig settings;
        private readonly CheckBox preferGpu, pauseCapture;
        private readonly Label saveStatus, nvidiaStatus, panelStatus;
        private readonly Button saveButton, nvidiaButton, panelButton;
        private GraphicsDestinations destinations;

        public GraphicsAdvisorForm(string gameName, string gamePath, string gameDirectory, AppConfig config)
        {
            settings = config;
            selectedGameName = gameName ?? "";
            selectedGamePath = gamePath ?? "";
            selectedGameDirectory = gameDirectory ?? "";
            WorkspaceLayout.Form(this, UiText.T("กราฟิก", "Graphics"), new Size(860, 720), new Size(640, 500));
            TableLayoutPanel root = WorkspaceLayout.Grid(1, 48, 34, -1, 54);
            root.Padding = new Padding(20, 12, 20, 8);
            root.Controls.Add(WorkspaceLayout.Label(UiText.T("กราฟิก", "Graphics"), 20, Palette.Text), 0, 0);
            gameLabel = WorkspaceLayout.Label(selectedGameName, 10, Palette.Cyan);
            root.Controls.Add(gameLabel, 0, 1);
            WorkspaceTabs tabs = new WorkspaceTabs();
            Panel settingsTab = tabs.AddPage(UiText.T("ตั้งค่า", "Settings"));
            Panel capabilitiesTab = tabs.AddPage(UiText.T("ความเข้ากันได้ของ GPU", "GPU compatibility"));
            root.Controls.Add(tabs, 0, 2);
            TableLayoutPanel page = WorkspaceLayout.ScrollPage(settingsTab, 450);
            page.RowCount = 9; page.RowStyles.Clear();
            foreach (int height in new[] { 32, 38, 38, 54, 42, 74, 74, 74 })
                page.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(WorkspaceLayout.Label(UiText.T("กติกา Boost / ทุกเกม", "Boost rules / All games"), 12, Palette.Cyan), 0, 0);
            preferGpu = new CheckBox { Dock = DockStyle.Fill, Checked = config.PreferHighPerformanceGpu,
                Text = UiText.T("เลือก High-performance GPU เมื่อ Boost", "Prefer the high-performance GPU during Boost"), ForeColor = Palette.Text };
            pauseCapture = new CheckBox { Dock = DockStyle.Fill, Checked = config.DisableBackgroundCapture,
                Text = UiText.T("หยุด Background Capture เมื่อ Boost", "Pause background capture during Boost"), ForeColor = Palette.Text };
            page.Controls.Add(preferGpu, 0, 1); page.Controls.Add(pauseCapture, 0, 2);
            TableLayoutPanel saveRow = WorkspaceLayout.Grid(2, -1);
            saveRow.ColumnStyles[0].Width = 72; saveRow.ColumnStyles[1].Width = 28;
            saveStatus = WorkspaceLayout.Label(UiText.T("ใช้ตามระดับ Boost / มีผลรอบถัดไป", "Applied by Boost level / Next session"), 9, Palette.Muted);
            saveButton = WorkspaceLayout.Button(UiText.T("บันทึก", "Save"), Palette.Lime);
            saveButton.Enabled = false;
            saveButton.Click += delegate { SaveSettings(); };
            EventHandler settingsChanged = delegate
            {
                saveButton.Enabled = preferGpu.Checked != settings.PreferHighPerformanceGpu ||
                    pauseCapture.Checked != settings.DisableBackgroundCapture;
                saveStatus.Text = saveButton.Enabled ? UiText.T("ยังไม่ได้บันทึก", "Unsaved changes") :
                    UiText.T("ไม่มีการเปลี่ยนแปลง", "No changes");
                saveStatus.ForeColor = saveButton.Enabled ? Palette.Amber : Palette.Muted;
            };
            preferGpu.CheckedChanged += settingsChanged;
            pauseCapture.CheckedChanged += settingsChanged;
            saveRow.Controls.Add(saveStatus, 0, 0); saveRow.Controls.Add(saveButton, 1, 0);
            page.Controls.Add(saveRow, 0, 3);
            page.Controls.Add(WorkspaceLayout.Label(UiText.T("การตั้งค่า Windows และ Driver", "Windows and driver settings"), 12, Palette.Text), 0, 4);
            Label windowsStatus;
            Button windows = DestinationRow(page, 5, "Windows Graphics",
                UiText.T("เลือก GPU และการตั้งค่าจอ", "GPU preference and display settings"), out windowsStatus);
            windows.Text = UiText.T("เปิดตั้งค่า", "Open settings");
            windows.Click += delegate { GraphicsDestinations.Open(this, "ms-settings:display-advancedgraphics"); };
            nvidiaButton = DestinationRow(page, 6, "NVIDIA App", UiText.T("กำลังค้นหา...", "Checking..."), out nvidiaStatus);
            panelButton = DestinationRow(page, 7, "NVIDIA Control Panel", UiText.T("กำลังค้นหา...", "Checking..."), out panelStatus);
            nvidiaButton.Enabled = panelButton.Enabled = false;
            nvidiaButton.Click += delegate
            {
                if (destinations == null) return;
                if (destinations.NvidiaApp != null) GraphicsDestinations.Open(this, destinations.NvidiaApp);
                else GraphicsDestinations.Open(this, GraphicsDestinations.NvidiaDownload);
            };
            panelButton.Click += delegate
            {
                if (destinations == null) return;
                if (destinations.ControlPanel != null) GraphicsDestinations.Open(this, destinations.ControlPanel);
                else GraphicsDestinations.Open(this, GraphicsDestinations.ControlPanelStore);
            };

            TableLayoutPanel compatibility = WorkspaceLayout.ScrollPage(capabilitiesTab, 720);
            compatibility.RowCount = 10; compatibility.RowStyles.Clear();
            foreach (int height in new[] { 56, 30, 64, 38, 100, 100, 100, 100, 100 })
                compatibility.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            compatibility.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            systemLabel = WorkspaceLayout.Label(UiText.T("กำลังอ่าน GPU...", "Reading GPU..."), 10, Palette.Text);
            appLabel = WorkspaceLayout.Label("", 9, Palette.Muted);
            summaryLabel = WorkspaceLayout.Label("", 10, Palette.Amber);
            compatibility.Controls.Add(systemLabel, 0, 0); compatibility.Controls.Add(appLabel, 0, 1);
            compatibility.Controls.Add(summaryLabel, 0, 2);
            compatibility.Controls.Add(WorkspaceLayout.Label(UiText.T("รองรับที่ Hardware ไม่ได้แปลว่าเปิดใช้อยู่", "Hardware support does not confirm an active setting"), 9, Palette.Muted), 0, 3);
            dlssRow = CreateAdvisorRow(compatibility, "DLSS SUPER RESOLUTION", UiText.T("ตั้งค่าในเกม", "Set in game"), 4);
            frameGenerationRow = CreateAdvisorRow(compatibility, "FRAME GENERATION", UiText.T("ตั้งค่าในเกม", "Set in game"), 5);
            nisRow = CreateAdvisorRow(compatibility, "NVIDIA IMAGE SCALING", "NVIDIA / Display", 6);
            reflexRow = CreateAdvisorRow(compatibility, "NVIDIA REFLEX", UiText.T("ตั้งค่าในเกม", "Set in game"), 7);
            smoothMotionRow = CreateAdvisorRow(compatibility, "SMOOTH MOTION", "NVIDIA App / Graphics", 8);

            TableLayoutPanel footer = WorkspaceLayout.Grid(3, -1);
            footer.ColumnStyles[0].Width = 50; footer.ColumnStyles[1].Width = 25; footer.ColumnStyles[2].Width = 25;
            footer.Controls.Add(WorkspaceLayout.Label(UiText.T("DLSS / Reflex / Frame Gen: ตั้งค่าในเกม", "DLSS / Reflex / Frame Gen: in-game settings"), 9, Palette.Muted), 0, 0);
            refreshButton = WorkspaceLayout.Button(UiText.T("ตรวจอีกครั้ง", "Check again"), Palette.Cyan);
            refreshButton.Click += delegate { RefreshAdvisor(); };
            Button close = WorkspaceLayout.Button(UiText.T("ปิด", "Close"), Palette.Text);
            close.Click += delegate { Close(); };
            footer.Controls.Add(refreshButton, 1, 0); footer.Controls.Add(close, 2, 0);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
            Shown += delegate { if (Storage.UsesProtectedStateStore) RefreshAdvisor(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (saveButton.Enabled && MessageBox.Show(this,
                    UiText.T("ละทิ้งค่าที่ยังไม่ได้บันทึกหรือไม่?", "Discard unsaved changes?"), "Game Boost Pro",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    e.Cancel = true;
            };
        }

        private Button DestinationRow(TableLayoutPanel page, int row, string title, string detail, out Label status)
        {
            TableLayoutPanel line = WorkspaceLayout.Grid(2, -1);
            line.ColumnStyles[0].Width = 68; line.ColumnStyles[1].Width = 32;
            line.Margin = new Padding(0, 4, 0, 4);
            TableLayoutPanel identity = WorkspaceLayout.Grid(1, 30, -1);
            identity.Controls.Add(WorkspaceLayout.Label(title, 11, Palette.Text), 0, 0);
            status = WorkspaceLayout.Label(detail, 9, Palette.Muted);
            identity.Controls.Add(status, 0, 1);
            Button open = WorkspaceLayout.Button(UiText.T("กำลังค้นหา", "Checking"), Palette.Cyan);
            line.Controls.Add(identity, 0, 0); line.Controls.Add(open, 1, 0);
            page.Controls.Add(line, 0, row);
            return open;
        }

        private AdvisorRow CreateAdvisorRow(TableLayoutPanel page, string title, string destination, int index)
        {
            TableLayoutPanel row = WorkspaceLayout.Grid(2, 28, 28, -1);
            row.ColumnStyles[0].Width = 58; row.ColumnStyles[1].Width = 42;
            row.Controls.Add(WorkspaceLayout.Label(title, 10, Palette.Text), 0, 0);
            Label status = WorkspaceLayout.Label("CHECKING", 9, Palette.Muted);
            status.TextAlign = ContentAlignment.MiddleRight;
            row.Controls.Add(status, 1, 0);
            row.Controls.Add(WorkspaceLayout.Label(destination, 9, Palette.Cyan), 0, 1);
            row.SetColumnSpan(row.GetControlFromPosition(0, 1), 2);
            Label detail = WorkspaceLayout.Label(UiText.T("รอตรวจสอบ", "Pending"), 9, Palette.Muted);
            row.Controls.Add(detail, 0, 2); row.SetColumnSpan(detail, 2);
            page.Controls.Add(row, 0, index);
            return new AdvisorRow { Detail = detail, Status = status };
        }

        private void SaveSettings()
        {
            bool previousGpu = settings.PreferHighPerformanceGpu, previousCapture = settings.DisableBackgroundCapture;
            try
            {
                settings.PreferHighPerformanceGpu = preferGpu.Checked;
                settings.DisableBackgroundCapture = pauseCapture.Checked;
                Storage.SaveConfig(settings);
                saveButton.Enabled = false;
                saveStatus.Text = UiText.T("บันทึกแล้ว / ใช้ตามระดับ Boost รอบถัดไป", "Saved / Applied by level next session");
                saveStatus.ForeColor = Palette.Cyan;
            }
            catch (Exception ex)
            {
                settings.PreferHighPerformanceGpu = previousGpu;
                settings.DisableBackgroundCapture = previousCapture;
                saveStatus.Text = UiText.T("บันทึกไม่สำเร็จ: ", "Could not save: ") + ex.Message;
                saveStatus.ForeColor = Palette.Coral;
            }
        }

        private void ApplyDestinations(GraphicsDestinations found)
        {
            destinations = found;
            nvidiaButton.Enabled = panelButton.Enabled = true;
            nvidiaButton.Text = found.NvidiaApp != null ? UiText.T("เปิด NVIDIA App", "Open NVIDIA App") : UiText.T("ดาวน์โหลด", "Download");
            panelButton.Text = found.ControlPanel != null ? UiText.T("เปิด Control Panel", "Open Control Panel") : UiText.T("ดูใน Store", "View in Store");
            nvidiaStatus.Text = found.NvidiaApp != null ? UiText.T("ติดตั้งแล้ว / Graphics และ Driver", "Installed / Graphics and drivers") : UiText.T("ยังไม่พบแอปบนเครื่อง", "App not found on this PC");
            panelStatus.Text = found.ControlPanel != null ? (found.ControlPanel.IsStoreApp ? "Microsoft Store" : "Desktop app") :
                found.NvidiaApp != null ? UiText.T("ยังไม่พบ / ใช้ NVIDIA App ที่มีอยู่ได้", "Not found / NVIDIA App is available separately") :
                UiText.T("ยังไม่พบ Control Panel บนเครื่อง", "Control Panel not found on this PC");
            nvidiaStatus.ForeColor = found.NvidiaApp != null ? Palette.Cyan : Palette.Amber;
            panelStatus.ForeColor = found.ControlPanel != null ? Palette.Cyan : Palette.Amber;
        }

        private void RefreshAdvisor()
        {
            if (loading) return;
            loading = true;
            refreshButton.Enabled = false;
            refreshButton.Text = UiText.T("กำลังตรวจสอบ", "Checking");
            GraphicsDestinations found = null;
            Task.Factory.StartNew<GraphicsAdvisorSnapshot>(delegate
            {
                found = GraphicsDestinations.Discover();
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
                        refreshButton.Text = UiText.T("ตรวจอีกครั้ง", "Check again");
                        if (found != null) ApplyDestinations(found);
                        else
                        {
                            nvidiaStatus.Text = panelStatus.Text = UiText.T("ตรวจไม่สำเร็จ / ตรวจอีกครั้งได้", "Discovery failed / Check again");
                            nvidiaStatus.ForeColor = panelStatus.ForeColor = Palette.Coral;
                        }
                        if (task.Status == TaskStatus.RanToCompletion) ApplySnapshot(task.Result);
                        else summaryLabel.Text = UiText.T("อ่านข้อมูลกราฟิกไม่สำเร็จ กรุณาลองใหม่", "Cannot read graphics data. Try again");
                    }));
                }
                catch { loading = false; }
            });
        }

        private void ApplySnapshot(GraphicsAdvisorSnapshot snapshot)
        {
            GraphicsCapabilities gpu = snapshot.Capabilities;
            gameLabel.Text = UiText.T("เกมที่เลือก: ", "Selected game: ") + snapshot.GameName;
            string routeText = snapshot.DisplayRoute == "Active" ? "NVIDIA SCAN-OUT ACTIVE" :
                snapshot.DisplayRoute == "Inactive" ? "NVIDIA SCAN-OUT INACTIVE" : "SCAN-OUT UNKNOWN";
            systemLabel.Text = gpu.GpuName + "\nDRIVER " + snapshot.DriverVersion + "  /  " + routeText;
            appLabel.Text = snapshot.HasNvidiaApp
                ? "NVIDIA APP\n" + (String.IsNullOrWhiteSpace(snapshot.NvidiaAppVersion) ? "INSTALLED" : snapshot.NvidiaAppVersion)
                : "NVIDIA APP\nNOT FOUND";
            appLabel.ForeColor = snapshot.HasNvidiaApp ? Palette.Cyan : Palette.Muted;

            if (snapshot.IsCompetitiveGame)
                summaryLabel.Text = UiText.T("Competitive: ใช้ native resolution + Reflex ก่อน และไม่ใช้ Frame Generation", "Competitive: start with native resolution + Reflex; avoid Frame Generation");
            else if (gpu.SupportsDlssSuperResolution)
                summaryLabel.Text = UiText.T("ใช้ DLSS ในเกมก่อน; Frame Generation ใช้เมื่อ base FPS ดีและเกมรองรับ", "Prefer in-game DLSS; use Frame Generation with good base FPS and game support");
            else
                summaryLabel.Text = UiText.T("รักษาค่า native ก่อน แล้ววัด FPS/ความนิ่งจากในเกมก่อนเปลี่ยน scaler", "Keep native settings first; measure FPS and stability before switching scalers");

            if (gpu.SupportsDlssSuperResolution)
                SetRow(dlssRow, "CAPABLE", snapshot.HasDlssLibraryHint
                    ? UiText.T("พบไฟล์ที่เกี่ยวข้องใกล้เกม แต่ต้องยืนยันสถานะในเมนูเกม", "Related files found near the game; confirm settings in its menu")
                    : UiText.T("RTX รองรับ ต้องตรวจว่าตัวเกมมีเมนู DLSS หรือไม่", "RTX is supported; check whether the game offers DLSS"), Palette.Cyan);
            else
                SetRow(dlssRow, "NOT AVAILABLE", UiText.T("GPU ที่ตรวจพบไม่ใช่ RTX; โปรแกรมจะไม่อ้างว่าเปิด DLSS ได้", "No RTX GPU detected; DLSS cannot be claimed as available"), Palette.Muted);

            if (gpu.SupportsFrameGeneration)
                SetRow(frameGenerationRow, snapshot.IsCompetitiveGame ? "SKIP FOR COMP" : "CAPABLE",
                    snapshot.HasFrameGenerationLibraryHint
                        ? UiText.T("พบไฟล์ Frame Generation; ยังต้องเปิดและยืนยันในเกม", "Frame Generation files found; enable and verify in the game")
                        : UiText.T("RTX 40/50 รองรับเมื่อเกมรองรับ; RTX 40 ไม่มี Multi Frame Generation", "Supported RTX 40/50 games only; RTX 40 has no Multi Frame Generation"),
                    snapshot.IsCompetitiveGame ? Palette.Amber : Palette.Cyan);
            else
                SetRow(frameGenerationRow, "NOT AVAILABLE", UiText.T("ไม่มี hardware path สำหรับ DLSS Frame Generation", "No hardware path for DLSS Frame Generation"), Palette.Muted);

            if (snapshot.NisEligibility == "Eligible")
                SetRow(nisRow, "ELIGIBLE", UiText.T("ต้องใช้ fullscreen ที่เหมาะสม + resolution ต่ำกว่า native และดู NIS indicator สีเขียว", "Use compatible fullscreen, below-native resolution and a green NIS indicator"),
                    Palette.Lime);
            else if (snapshot.NisEligibility == "RouteBlocked")
                SetRow(nisRow, "ROUTE NOT READY", UiText.T("RTX ไม่ได้รายงานว่าขับจออยู่; NIS driver scaling จึงไม่น่าใช้ได้บนจอนี้", "RTX is not reported as driving this display; driver NIS is unlikely to work here"),
                    Palette.Coral);
            else if (snapshot.NisEligibility == "Unavailable")
                SetRow(nisRow, "NOT AVAILABLE", UiText.T("ไม่พบ NVIDIA GPU สำหรับ driver NIS", "No NVIDIA GPU detected for driver NIS"), Palette.Muted);
            else
                SetRow(nisRow, "UNVERIFIED", UiText.T("ตรวจเส้นทางจอไม่ได้ จึงจะไม่เปิด NIS ให้อัตโนมัติ", "Unknown display route; NIS will not be enabled automatically"), Palette.Amber);

            SetRow(reflexRow, gpu.IsNvidia ? "CHECK IN GAME" : "GAME DEPENDENT",
                snapshot.IsCompetitiveGame
                    ? UiText.T("ถ้าเกมมี Reflex ให้ใช้ฟังก์ชันในเกมก่อน และหลีกเลี่ยง Frame Generation", "Prefer in-game Reflex where available; avoid Frame Generation in competitive games")
                    : UiText.T("Reflex เป็นฟังก์ชันในตัวเกม โปรแกรมภายนอกยืนยันว่าเปิดอยู่ไม่ได้", "Reflex is an in-game feature; an external app cannot verify its active state"),
                gpu.IsNvidia ? Palette.Cyan : Palette.Muted);

            if (!gpu.SupportsSmoothMotion)
                SetRow(smoothMotionRow, "NOT AVAILABLE", UiText.T("ต้องใช้ RTX 40 Series ขึ้นไป", "Requires RTX 40 Series or newer"), Palette.Muted);
            else if (snapshot.IsCompetitiveGame)
                SetRow(smoothMotionRow, "SKIP FOR COMP", UiText.T("เพิ่มเฟรมที่สร้างขึ้น เหมาะกับเกมภาพมากกว่าเกมแข่งขัน", "Generated frames suit visual games more than competitive play"), Palette.Amber);
            else if (!snapshot.HasNvidiaApp)
                SetRow(smoothMotionRow, "APP REQUIRED", UiText.T("รองรับที่ GPU แต่ควรติดตั้ง/อัปเดต NVIDIA App ก่อน", "GPU supported; install or update NVIDIA App first"), Palette.Amber);
            else
                SetRow(smoothMotionRow, "AVAILABLE", UiText.T("ใช้เมื่อเกมไม่มี native Frame Generation และอย่าเปิดซ้อนกัน", "Use only without native Frame Generation; do not stack them"), Palette.Cyan);
        }

        private static void SetRow(AdvisorRow row, string status, string detail, Color color)
        {
            row.Status.Text = status;
            row.Status.ForeColor = color;
            row.Detail.Text = detail;
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

        private string gameName, gameProcessName, historyKey;
        private int gameProcessId;
        private long gameProcessStartTimeUtcTicks;
        private readonly BenchmarkPane baselinePane, boostedPane;
        private readonly Label currentMode, comparisonTitle, comparisonDetail, targetLabel, prerequisites;
        private readonly Timer countdown;
        private readonly bool captureToolReady;
        private bool lastBoosted;
        private DateTime captureStarted;
        private bool capturing;
        public bool IsCapturing { get { return capturing; } }

        public FrameBenchmarkForm(string selectedGameName, int processId, string processName,
            long processStartTimeUtcTicks)
        {
            WorkspaceLayout.Form(this, UiText.T("วัดเฟรม / Frame Lab", "Frame Lab"), new Size(820, 670), new Size(640, 500));
            TableLayoutPanel page = WorkspaceLayout.ScrollPage(this, 634);
            page.RowCount = 8; page.RowStyles.Clear();
            foreach (int height in new[] { 48, 36, 34, 62, 244, 40, 82, 64 })
                page.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            page.RowStyles[6].SizeType = SizeType.Percent;
            page.RowStyles[6].Height = 100;
            page.Controls.Add(WorkspaceLayout.Label(UiText.T("วัดเฟรม / Frame Lab", "Frame Lab"), 20, Palette.Text), 0, 0);
            targetLabel = WorkspaceLayout.Label("", 11, Palette.Cyan);
            page.Controls.Add(targetLabel, 0, 1);
            currentMode = WorkspaceLayout.Label("", 10, Palette.Lime);
            page.Controls.Add(currentMode, 0, 2);
            prerequisites = WorkspaceLayout.Label("", 10, Palette.Amber);
            page.Controls.Add(prerequisites, 0, 3);
            TableLayoutPanel panes = WorkspaceLayout.Grid(2, -1);
            baselinePane = CreatePane(panes, UiText.T("1. ก่อน Boost / Baseline", "1. Before Boost / Baseline"), 0, Palette.Cyan);
            boostedPane = CreatePane(panes, UiText.T("2. หลัง Boost / Boosted", "2. With Boost / Boosted"), 1, Palette.Lime);
            baselinePane.Capture.Click += delegate { StartCapture("Baseline"); };
            boostedPane.Capture.Click += delegate { StartCapture("Boosted"); };
            page.Controls.Add(panes, 0, 4);
            comparisonTitle = WorkspaceLayout.Label("", 11, Palette.Amber);
            comparisonDetail = WorkspaceLayout.Label("", 10, Palette.Muted);
            page.Controls.Add(comparisonTitle, 0, 5); page.Controls.Add(comparisonDetail, 0, 6);
            TableLayoutPanel footer = WorkspaceLayout.Grid(2, -1);
            footer.ColumnStyles[0].Width = 65; footer.ColumnStyles[1].Width = 35;
            footer.Controls.Add(WorkspaceLayout.Label(UiText.T("App-present frame time / 15 วินาทีต่อรอบ\nPresentMon ทำงานเฉพาะขณะวัด", "App-present frame time / 15 seconds per run\nPresentMon runs only during capture"), 9, Palette.Muted), 0, 0);
            Button dashboard = WorkspaceLayout.Button(UiText.T("กลับหน้า Boost", "Back to Boost"), Palette.Text);
            dashboard.Click += delegate
            {
                if (capturing) return;
                if (Owner != null) { Owner.WindowState = FormWindowState.Normal; Owner.Show(); Owner.Activate(); Hide(); }
                else Close();
            };
            footer.Controls.Add(dashboard, 1, 0); page.Controls.Add(footer, 0, 7);
            countdown = new Timer { Interval = 250 };
            countdown.Tick += UpdateCountdown;
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!capturing) return;
                e.Cancel = true;
                prerequisites.Text = UiText.T("รอให้รอบวัดเสร็จก่อนปิด", "Wait for this capture to finish before closing");
            };
            FormClosed += delegate { countdown.Stop(); countdown.Dispose(); };
            captureToolReady = PresentMonRunner.IsToolReady();
            SetTarget(selectedGameName, processId, processName, processStartTimeUtcTicks);
        }

        public void SetTarget(string display, int id, string name, long processStartTimeUtcTicks)
        {
            if (capturing) return;
            bool changed = id != gameProcessId || processStartTimeUtcTicks != gameProcessStartTimeUtcTicks || historyKey == null;
            if (!changed && lastBoosted == Storage.HasState()) return;
            gameName = String.IsNullOrWhiteSpace(display) ? name ?? "" : display;
            gameProcessId = id; gameProcessName = name ?? ""; gameProcessStartTimeUtcTicks = processStartTimeUtcTicks;
            historyKey = gameProcessName + "|" + gameName + "|" + processStartTimeUtcTicks.ToString(CultureInfo.InvariantCulture);
            RenderHistory();
        }

        private BenchmarkPane CreatePane(TableLayoutPanel parent, string title, int column, Color accent)
        {
            TableLayoutPanel pane = WorkspaceLayout.Grid(1, 34, 52, 54, 32, 50);
            pane.BackColor = Palette.Surface;
            pane.Padding = new Padding(12, 6, 12, 6);
            pane.Margin = new Padding(column == 0 ? 0 : 8, 4, column == 0 ? 8 : 0, 4);
            pane.Controls.Add(WorkspaceLayout.Label(title, 11, accent), 0, 0);
            Label fps = WorkspaceLayout.Label("", 20, Palette.Text);
            Label detail = WorkspaceLayout.Label("", 10, Palette.Muted);
            Label mode = WorkspaceLayout.Label("", 9, Palette.Muted);
            mode.AutoEllipsis = true;
            Button capture = WorkspaceLayout.Button("", accent);
            pane.Controls.Add(fps, 0, 1); pane.Controls.Add(detail, 0, 2);
            pane.Controls.Add(mode, 0, 3); pane.Controls.Add(capture, 0, 4);
            parent.Controls.Add(pane, column, 0);
            return new BenchmarkPane { Fps = fps, Detail = detail, Mode = mode, Capture = capture };
        }

        private void StartCapture(string slot)
        {
            if (capturing) return;
            if (!HasLiveTarget()) { RenderHistory(); return; }
            string boostSession = GetBoostSessionToken();
            bool boosted = !String.Equals(boostSession, "NORMAL", StringComparison.Ordinal);
            if (String.Equals(slot, "Baseline", StringComparison.Ordinal) && boosted)
            {
                MessageBox.Show(this, UiText.T("กด RESTORE ก่อนเก็บ Baseline", "Restore before recording Baseline"), "Game Boost Pro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (String.Equals(slot, "Boosted", StringComparison.Ordinal) && !boosted)
            {
                MessageBox.Show(this, UiText.T("เปิด Game Mode ก่อนเก็บ Boosted", "Enable Game Mode before recording Boosted"), "Game Boost Pro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!PresentMonRunner.IsToolReady())
            {
                MessageBox.Show(this, UiText.T("Frame Test component ไม่ครบหรือไม่ผ่านการตรวจ hash กรุณาติดตั้งใหม่", "Frame Test component is missing or failed hash verification. Reinstall the app"),
                    "Game Boost Pro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            capturing = true;
            captureStarted = DateTime.UtcNow;
            baselinePane.Capture.Enabled = false;
            boostedPane.Capture.Enabled = false;
            currentMode.Text = UiText.T("CAPTURING / กลับเข้าเกม", "CAPTURING / Return to game");
            currentMode.ForeColor = Palette.Amber;
            countdown.Start();
            FocusGameWindow();

            Task.Factory.StartNew<FrameBenchmarkResult>(delegate
            {
                FrameBenchmarkResult result = PresentMonRunner.Capture(gameProcessId, gameProcessName,
                    gameProcessStartTimeUtcTicks, slot, gameName);
                if (!String.Equals(GetBoostSessionToken(), boostSession, StringComparison.Ordinal))
                    throw new InvalidOperationException(UiText.T("โหมด Boost เปลี่ยนระหว่างทดสอบ ผลรอบนี้จึงไม่ถูกเก็บ", "Boost state changed during capture. This result was discarded"));
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
                            MessageBox.Show(this, task.Exception == null ? UiText.T("Frame Test ไม่สำเร็จ", "Frame Test failed") :
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

        private bool HasLiveTarget()
        {
            if (gameProcessId <= 0 || String.IsNullOrWhiteSpace(gameProcessName) || gameProcessStartTimeUtcTicks <= 0) return false;
            try
            {
                using (Process process = Process.GetProcessById(gameProcessId))
                    return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == gameProcessStartTimeUtcTicks &&
                        String.Equals(process.ProcessName, gameProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private void RenderHistory()
        {
            bool boosted = Storage.HasState();
            lastBoosted = boosted;
            bool live = HasLiveTarget();
            targetLabel.Text = live ? gameName + " / PID " + gameProcessId :
                UiText.T("ยังไม่พบเกมที่กำลังเล่น", "No running game detected");
            currentMode.Text = boosted ? UiText.T("สถานะเครื่อง: Boost เปิดอยู่", "System: Boost active") :
                UiText.T("สถานะเครื่อง: ปกติ", "System: Normal");
            currentMode.ForeColor = boosted ? Palette.Lime : Palette.Cyan;
            prerequisites.Text = !live ? UiText.T("รอเกมที่เปิดอยู่ / การตรวจจับเกมทำงานต่อที่หน้าหลัก", "Waiting for a running game / Dashboard detection is active") :
                !captureToolReady ? UiText.T("ไม่พบ PresentMon ที่ผ่านการตรวจสอบ / ต้องติดตั้งแอปใหม่", "Verified PresentMon is missing / Reinstall required") :
                boosted ? UiText.T("พร้อมวัด Boosted 15 วินาที / Baseline ต้องคืนค่าที่หน้าหลักก่อน", "Ready for a 15-second Boosted run / Baseline requires Restore on the dashboard") :
                UiText.T("พร้อมวัด Baseline 15 วินาที / Boosted ต้องเปิด Boost ที่หน้าหลักก่อน", "Ready for a 15-second Baseline run / Enable Boost on the dashboard for the Boosted run");
            FrameBenchmarkHistory history = FrameBenchmarkStore.Get(historyKey);
            RenderPane(baselinePane, history.Baseline); RenderPane(boostedPane, history.Boosted);
            baselinePane.Capture.Enabled = !capturing && captureToolReady && live && !boosted;
            boostedPane.Capture.Enabled = !capturing && captureToolReady && live && boosted;
            baselinePane.Capture.Text = UiText.T("วัด Baseline", "Capture Baseline");
            boostedPane.Capture.Text = UiText.T("วัด Boosted", "Capture Boosted");
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
                pane.Fps.Text = UiText.T("ยังไม่มีผลวัด", "Not captured");
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
                comparisonTitle.Text = UiText.T("รอผลก่อนและหลัง Boost", "Waiting for both runs");
                comparisonTitle.ForeColor = Palette.Amber;
                comparisonDetail.Text = UiText.T("ฉากและการตั้งค่าเกมต้องเหมือนกันทั้งสองรอบ\nAverage FPS / 1% Low ยิ่งสูงยิ่งดี และ P95 frame time ยิ่งต่ำยิ่งดี", "Use the same scene and game settings for both runs\nHigher Average FPS / 1% Low and lower P95 frame time are better");
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
                UiText.T(" better\nทำซ้ำฉากเดิมอย่างน้อย 3 รอบก่อนตัดสินใจ", " better\nRepeat the same scene at least three times before deciding");
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

    }

    internal sealed class NativeCpuReader : IDisposable
    {
        private ulong previousIdle, previousKernel, previousUser;
        private bool sampled;
        private readonly bool supported;
        public NativeCpuReader()
        {
            uint count = GetActiveProcessorCount(0xffff);
            supported = count > 0 && count <= 64;
        }
        public float NextValue()
        {
            ulong idle, kernel, user;
            if (!supported || !GetSystemTimes(out idle, out kernel, out user)) { sampled = false; return Single.NaN; }
            return Sample(idle, kernel, user);
        }
        internal float Sample(ulong idle, ulong kernel, ulong user)
        {
            float value = Single.NaN;
            if (sampled && idle >= previousIdle && kernel >= previousKernel && user >= previousUser)
            {
                double total = (double)(kernel - previousKernel) + (user - previousUser);
                if (total > 0) value = (float)Math.Max(0, Math.Min(100, (1 - (idle - previousIdle) / total) * 100));
            }
            previousIdle = idle; previousKernel = kernel; previousUser = user; sampled = true;
            return value;
        }
        public void Dispose() { }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetActiveProcessorCount(ushort group);
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
                return Single.NaN;
            }
            if (category == null) return Single.NaN;
            Dictionary<string, CounterSample> current = new Dictionary<string, CounterSample>();
            Dictionary<string, float> engines = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
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
                        AddEngineUsage(engines, name, value);
                    }
                }
                previous = current;
            }
            catch { previous.Clear(); return Single.NaN; }
            return GetBusiestEngine(engines);
        }

        internal static void AddEngineUsage(Dictionary<string, float> engines, string instance, float value)
        {
            if (String.IsNullOrWhiteSpace(instance) || Single.IsNaN(value) || Single.IsInfinity(value) || value < 0) return;
            int luid = instance.IndexOf("_luid_", StringComparison.OrdinalIgnoreCase);
            if (luid < 0 || instance.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) < 0) return;
            string engine = instance.Substring(luid);
            float total;
            engines.TryGetValue(engine, out total);
            engines[engine] = total + value;
        }

        internal static float GetBusiestEngine(Dictionary<string, float> engines)
        {
            if (engines.Count == 0) return Single.NaN;
            float busiest = 0;
            foreach (float usage in engines.Values) busiest = Math.Max(busiest, usage);
            return Math.Min(100, busiest);
        }

        public void ResetSamples()
        {
            previous.Clear();
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

    internal sealed partial class MainForm : Form
    {
        private const int DiscoveryMonitorIntervalMs = 1500;
        private const int ActiveMonitorIntervalMs = 3000;
        private readonly AppConfig config;
        private PlatformProfile platform;
        private Label platformLabel;
        private BoostDial dial;
        private Label stateText;
        private Label gameName;
        private Label gamePath;
        private Label powerStatus;
        private Label modeStatus;
        private Label captureStatus;
        private Label activityText;
        private ToggleSwitch autoSwitch;
        private CheckBox launchCheck;
        private Button browseButton;
        private Button libraryButton;
        private Button launchButton;
        private Button advancedButton;
        private Button gpuAdvisorButton;
        private Label adminStatus;
        private FrameBenchmarkForm frameLab;
        private CheckBox telemetryCheck;
        private readonly ToolTip tips = new ToolTip();
        private readonly bool isAdmin;
        private string activityNotice = "";
        private DateTime activityNoticeUntil;
        private bool metricsWereCollected;
        private bool autoBoostPausedUntilExit;
        private MetricBar cpuBar;
        private MetricBar ramBar;
        private MetricBar gpuBar;
        private readonly Timer monitor;
        private NativeCpuReader cpuCounter;
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
            isAdmin = SystemTuner.IsAdmin();
            config = Storage.LoadConfig();
            UiText.Language = config.Language;
            platform = new PlatformProfile
            {
                Kind = PlatformKind.UnsupportedLaptop,
                IsSupported = false,
                Title = "CHECKING SYSTEM",
                Detail = UiText.T("กำลังตรวจสอบชนิดเครื่องและ NitroSense", "Checking device type and NitroSense")
            };
            Icon appIcon = Icon.ExtractAssociatedIcon(typeof(MainForm).Assembly.Location);
            if (appIcon != null) Icon = appIcon;
            BuildDashboard();
            gpuReader = new GpuUsageReader();

            monitor = new Timer();
            monitor.Interval = DiscoveryMonitorIntervalMs;
            monitor.Tick += MonitorTick;
            if (Storage.UsesProtectedStateStore) monitor.Start();

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add(UiText.T("เปิด Game Boost Pro", "Open Game Boost Pro"), null, delegate { ShowFromTray(); });
            trayMenu.Items.Add(UiText.T("สลับ Game Mode", "Toggle Game Mode"), null, delegate { ToggleBoost(false); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(UiText.T("ออกจากโปรแกรม", "Exit"), null, ExitApplication);
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
                    tray.ShowBalloonTip(1200, "Game Boost Pro", UiText.T("ยังทำงานอยู่ที่มุมจอ", "Still running in the system tray"), ToolTipIcon.Info);
                }
            };
            FormClosing += OnFormClosing;
            FormClosed += delegate
            {
                monitor.Stop();
                monitor.Dispose();
                if (detectedGame != null && detectedGame.Process != null) detectedGame.Process.Dispose();
                gpuReader.Dispose();
                if (cpuCounter != null) cpuCounter.Dispose();
                tips.Dispose();
                tray.Dispose();
            };

            autoSwitch.Value = config.AutoMode;
            launchCheck.Checked = config.LaunchOnBoost;
            autoSwitch.Enabled = false;
            dial.Enabled = false;
            libraryButton.Enabled = GameDetector.IsCatalogLoaded;
            RefreshAdvancedButton();
            RefreshGameProfile();
            RefreshState(platform.Detail);
            Shown += delegate
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                if (!Storage.UsesProtectedStateStore) return;
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
                            dial.Enabled = isAdmin && (platform.IsSupported || Storage.HasState());
                            autoSwitch.Enabled = isAdmin && platform.IsSupported && !working;
                            if (!platform.IsSupported && config.AutoMode)
                            {
                                config.AutoMode = false;
                                autoSwitch.Value = false;
                                Storage.SaveConfig(config);
                            }
                            RefreshState(platform.IsSupported ? platform.Detail : platform.Detail +
                                UiText.T(" / รองรับเฉพาะ Acer + NitroSense และ Desktop PC", " / Supports Acer + NitroSense and Desktop PC only"));
                        }));
                    }
                    catch { }
                });
        }

        private void BrowseGame(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = UiText.T("เลือกไฟล์เกม", "Select game executable");
                dialog.Filter = UiText.T("ไฟล์เกม (*.exe)|*.exe", "Game executable (*.exe)|*.exe");
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
                    RememberManualGame();
                    Storage.SaveConfig(config);
                    RefreshDashboardGames();
                    RefreshGameProfile();
                    RefreshState(UiText.T("บันทึกโปรไฟล์ ", "Profile saved: ") + Path.GetFileNameWithoutExtension(config.GamePath) + UiText.T(" แล้ว", ""));
                }
            }
        }

        private void OpenGameLibrary(object sender, EventArgs e)
        {
            List<GameInstall> catalog = new List<GameInstall>(dashboardGames);
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
                RememberManualGame();
                Storage.SaveConfig(config);
                RefreshDashboardGames();
                RefreshGameProfile();
                RefreshState(UiText.T("เลือก ", "Selected ") + config.LibraryGameName + UiText.T(" จาก ", " of ") + dialog.SelectedGame.Source + UiText.T(" แล้ว", ""));
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
                ShowNotice(UiText.T("เกมนี้ไม่มีคำสั่งเปิดอัตโนมัติ กรุณาเปิดจาก Launcher ตามปกติ", "No launch command is available. Open the game from its launcher"));
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
                RefreshState(UiText.T("กำลังเปิด ", "Launching ") + GetConfiguredGameName());
            }
            catch (Exception ex) { ShowNotice(UiText.T("เปิดเกมไม่สำเร็จ: ", "Could not launch game: ") + ex.Message); }
        }

        private static void LaunchThroughDesktopShell(string target, string arguments)
        {
            object shell = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) throw new InvalidOperationException(UiText.T("ไม่พบ Windows Desktop Shell", "Windows Desktop Shell is unavailable"));
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
            bool restore = Storage.HasState();
            if (!isAdmin)
            {
                ShowNotice(UiText.T("ต้องใช้สิทธิ์ Administrator เพื่อเปิดโหมดเกมหรือคืนค่า", "Administrator access is required to Boost or restore"));
                return;
            }
            if (!restore && !platform.IsSupported)
            {
                ShowError(platform.Detail + UiText.T("\n\nรุ่นนี้รองรับเฉพาะ Acer Laptop ที่มี NitroSense และ Desktop PC", "\n\nSupports Acer laptops with NitroSense and Desktop PCs only"));
                return;
            }
            working = true;
            dial.Busy = true;
            SetControlsEnabled(false);
            activityNotice = "";
            ReportTransitionProgress(restore ? UiText.T("กำลังเตรียมคืนค่า", "Preparing restore") : UiText.T("กำลังเตรียมโหมดเกม", "Preparing Game Mode"));
            DetectedGame candidate = detectedGame;
            string targetPath = BoostTargetResolver.ResolveGamePath(candidate, config.GamePath);
            int processId = candidate != null && candidate.Process != null ? candidate.Process.Id : 0;
            bool shouldLaunch = config.LaunchOnBoost && !autoTriggered && !restore;
            string profileKey = candidate == null ? BoostProfiles.SelectedKey(config) :
                String.IsNullOrWhiteSpace(candidate.ExePath) ? "" :
                BoostProfiles.ResolveKey(config, candidate.ExePath, dashboardGames);
            AppConfig sessionOptions = BoostProfiles.Snapshot(config, profileKey, platform);

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
                        if (restore) SystemTuner.Disable(ReportTransitionProgress);
                        else SystemTuner.Enable(targetPath, autoTriggered, processId, platform, sessionOptions, ReportTransitionProgress);
                    }
                    result.Activity = restore
                        ? (autoTriggered ? UiText.T("เกมปิดแล้ว คืนค่าเครื่องเรียบร้อย", "Game exited; original settings restored") : UiText.T("คืนค่าเครื่องกลับเป็นปกติแล้ว", "Original settings restored"))
                        : (autoTriggered && candidate != null
                            ? UiText.T("ตรวจพบ ", "Detected ") + candidate.DisplayName + UiText.T(" / เปิดโหมดเกมแล้ว", " / Game Mode active")
                            : UiText.T("เปิดโหมดเกมแล้ว / รอโปรเซสเกม", "Game Mode active / Waiting for game process"));
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
                            autoBoostPausedUntilExit = result.WasRestore && !autoTriggered;
                            if (result.ShouldLaunch) LaunchGame();
                            RefreshState(result.Activity);
                        }
                        else
                        {
                            RefreshState(UiText.T("ตรวจพบปัญหา กรุณาดูข้อความแจ้งเตือน", "A problem was found. Check the error message"));
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
            bool collectMetrics = config.ShowTelemetry && !working && Visible && WindowState != FormWindowState.Minimized;
            Task.Factory.StartNew<MonitorSnapshot>(delegate { return BuildMonitorSnapshot(collectMetrics); }).ContinueWith(delegate(Task<MonitorSnapshot> task)
            {
                try
                {
                    if (IsDisposed || Disposing)
                    {
                        if (task.Status == TaskStatus.RanToCompletion) DisposeSnapshotGame(task.Result);
                        System.Threading.Interlocked.Exchange(ref monitorInFlight, 0);
                        return;
                    }
                    BeginInvoke(new Action(delegate
                    {
                        System.Threading.Interlocked.Exchange(ref monitorInFlight, 0);
                        if (task.Status == TaskStatus.RanToCompletion)
                        {
                            if (IsDisposed || Disposing) DisposeSnapshotGame(task.Result);
                            else ApplyMonitorSnapshot(task.Result);
                        }
                    }));
                }
                catch
                {
                    if (task.Status == TaskStatus.RanToCompletion) DisposeSnapshotGame(task.Result);
                    System.Threading.Interlocked.Exchange(ref monitorInFlight, 0);
                }
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
                snapshot.Cpu = snapshot.Memory = snapshot.Gpu = Single.NaN;
                try
                {
                    EnsureCpuCounter();
                    float cpu = cpuCounter == null ? Single.NaN : cpuCounter.NextValue();
                    if (metricsWereCollected) snapshot.Cpu = cpu;
                }
                catch { }
                try { snapshot.Gpu = gpuReader.NextValue(); } catch { }
                MEMORYSTATUSEX memory = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memory)) snapshot.Memory = memory.dwMemoryLoad;
            }
            else if (metricsWereCollected) gpuReader.ResetSamples();
            metricsWereCollected = collectMetrics;
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
            if (snapshot.HasMetrics && config.ShowTelemetry)
            {
                cpuBar.Value = snapshot.Cpu;
                ramBar.Value = snapshot.Memory;
                gpuBar.Value = snapshot.Gpu;
            }
            if (working) { DisposeSnapshotGame(snapshot); return; }

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
                if (isAdmin && platform.IsSupported && config.AutoMode && !autoBoostPausedUntilExit &&
                    !Storage.HasState() && !Storage.HasRecoveryWarning)
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
                if (autoBoostPausedUntilExit && ++missingGameTicks >= 2)
                {
                    autoBoostPausedUntilExit = false;
                    missingGameTicks = 0;
                }
                RefreshGameProfile();
            }

            launchButton.Text = running ? UiText.T("กำลังเล่น", "Playing") : UiText.T("เปิดเกม", "Launch");
            launchButton.Enabled = !running && !working && CanLaunchConfiguredGame();
            UpdateStateVisuals();
        }

        private void UpdateMetrics()
        {
            EnsureCpuCounter();
            try { cpuBar.Value = cpuCounter.NextValue(); } catch { }
            try { gpuBar.Value = gpuReader.NextValue(); } catch { }
            MEMORYSTATUSEX memory = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memory)) ramBar.Value = memory.dwMemoryLoad;
        }

        private void ClearMetrics()
        {
            cpuBar.Value = ramBar.Value = gpuBar.Value = Single.NaN;
        }

        private bool cpuCounterInitialized;
        private void EnsureCpuCounter()
        {
            if (cpuCounterInitialized) return;
            cpuCounterInitialized = true;
            try { cpuCounter = new NativeCpuReader(); }
            catch { }
        }

        private void DisposeSnapshotGame(MonitorSnapshot snapshot)
        {
            if (snapshot != null && snapshot.Game != null && snapshot.Game.Process != null &&
                (detectedGame == null || !Object.ReferenceEquals(detectedGame.Process, snapshot.Game.Process)))
                snapshot.Game.Process.Dispose();
        }

        private void ReportTransitionProgress(string message)
        {
            try
            {
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                if (InvokeRequired) { BeginInvoke(new Action<string>(ReportTransitionProgress), message); return; }
                if (!working) return;
                stateText.Text = message;
                activityText.Text = message;
            }
            catch (InvalidOperationException) { }
        }

        private void ShowNotice(string message)
        {
            activityNotice = message;
            activityNoticeUntil = DateTime.UtcNow.AddSeconds(12);
            activityText.Text = message;
            activityText.ForeColor = Palette.Amber;
        }

        private void RestartAsAdmin(object sender, EventArgs e)
        {
            if (isAdmin) return;
            try
            {
                // The child waits until the old process releases the single-instance mutex.
                Process.Start(new ProcessStartInfo(Application.ExecutablePath,
                    "--wait-for-exit " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture))
                    { UseShellExecute = true, Verb = "runas" });
                allowClose = true;
                Close();
            }
            catch (Win32Exception ex)
            {
                ShowNotice(ex.NativeErrorCode == 1223 ? UiText.T("ยกเลิกการขอสิทธิ์ Admin แล้ว", "Admin request cancelled") : UiText.T("เปิดโปรแกรมใหม่ไม่สำเร็จ: ", "Could not restart app: ") + ex.Message);
            }
        }

        private static TableLayoutPanel CreateGrid(int columns, int rows)
        {
            TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows,
                Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent };
            panel.SuspendLayout();
            return panel;
        }

        private static void ResumeGridLayout(Control control)
        {
            TableLayoutPanel grid = control as TableLayoutPanel;
            if (grid != null)
            {
                while (grid.ColumnStyles.Count < grid.ColumnCount)
                    grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / grid.ColumnCount));
                while (grid.RowStyles.Count < grid.RowCount)
                    grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / grid.RowCount));
            }
            foreach (Control child in control.Controls) ResumeGridLayout(child);
            control.ResumeLayout(true);
        }

        private void RefreshGameProfile()
        {
            bool manualValid = !String.IsNullOrWhiteSpace(config.GamePath) && File.Exists(config.GamePath);
            bool libraryValid = !String.IsNullOrWhiteSpace(config.LibraryGameName) &&
                !String.IsNullOrWhiteSpace(config.LibraryGameDirectory) && Directory.Exists(config.LibraryGameDirectory);
            gameName.ForeColor = Palette.Text;
            gameName.Text = !String.IsNullOrWhiteSpace(config.LibraryGameName) ? config.LibraryGameName :
                !String.IsNullOrWhiteSpace(config.GamePath) ? Path.GetFileNameWithoutExtension(config.GamePath) :
                UiText.T("ยังไม่ได้เลือกเกม", "No game selected");
            string catalogStatus = GameDetector.IsCatalogLoaded
                ? UiText.T("เกมที่ตรวจพบ: ", "Discovered games: ") + GameDetector.InstalledCount
                : UiText.T("กำลังอ่านคลัง Steam / Epic / Riot", "Scanning Steam / Epic / Riot");
            gamePath.Text = catalogStatus +
                (manualValid ? UiText.T(" / สำรอง: ", " / fallback: ") + Path.GetFileNameWithoutExtension(config.GamePath) :
                libraryValid ? " / " + config.LibraryGameDirectory : "");
            launchButton.Enabled = !working && detectedGame == null && CanLaunchConfiguredGame();
            RefreshPresetControls();
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
                        RefreshDashboardGames();
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
                ? UiText.T("เกม", "Game") : Path.GetFileNameWithoutExtension(config.GamePath);
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
            // Keep the selected profile stable while another game is detected.
            gameName.ForeColor = Palette.Text;
            string detail = game.Source + " / " + Path.GetFileName(game.ExePath ?? UiText.T("กำลังทำงาน", "Working"));
            tips.SetToolTip(stateText, UiText.T("กำลังเล่น: ", "Playing: ") + game.DisplayName + " / " + detail);
            BoostState activeState = null;
            try { activeState = Storage.LoadState(); }
            catch { }
            if (working || DateTime.UtcNow < activityNoticeUntil) return;
            activityText.Text = activeState != null
                ? game.DisplayName + " / " + GetProcessStatusText(activeState) +
                    (String.IsNullOrWhiteSpace(activeState.ProcessTuningDetail) ? "" :
                    "\n" + activeState.ProcessTuningDetail)
                : UiText.T("พบ ", "Found ") + game.DisplayName + (config.AutoMode && isAdmin && !autoBoostPausedUntilExit ?
                    UiText.T(" / รอเปิดโหมดเกม", " / Waiting to Boost") : UiText.T(" / ยังไม่เปิดโหมดเกม", " / Boost inactive"));
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
            adminStatus.Text = UiText.T("สิทธิ์ Admin พร้อม", "Admin granted");
            adminStatus.Visible = isAdmin;
            elevationButton.Visible = !isAdmin;
            elevationButton.Text = UiText.T("ขอสิทธิ์ Admin", "Run as Admin");
            launchButton.BackColor = launchButton.Enabled ? Palette.Lime : Palette.SurfaceHigh;
            launchButton.ForeColor = launchButton.Enabled ? Palette.Back : Palette.Muted;
            dial.Active = active;
            dial.Enabled = isAdmin && (active || (platform.IsSupported && !Storage.HasRecoveryWarning));
            if (!working) stateText.Text = !isAdmin ? UiText.T("ต้องใช้สิทธิ์ Admin", "Admin required") : active
                ? (currentState != null && !String.IsNullOrWhiteSpace(currentState.Preset)
                    ? UiText.Preset(currentState.Preset) + UiText.T(" / ใช้อยู่", " / active") : UiText.T("Boost เปิดอยู่", "Boost active"))
                : autoBoostPausedUntilExit && config.AutoMode ?
                    UiText.T("คืนค่าแล้ว / พัก Auto", "Restored / Auto paused") : platform.IsSupported
                    ? UiText.T("พร้อมเล่น", "Ready to play") : platform.Title == "CHECKING SYSTEM"
                        ? UiText.T("กำลังตรวจสอบเครื่อง", "Checking system") : UiText.T("เครื่องนี้ยังไม่รองรับ", "Device not supported");
            stateText.ForeColor = active ? Palette.Lime : !platform.IsSupported && platform.Title != "CHECKING SYSTEM"
                ? Palette.Coral : Palette.Text;
            int optionCount = GetAdvancedOptionCount();
            string configuredPower = PowerPlanPolicy.GetShortLabel(config.PowerPlanMode);
            string powerText = configuredPower + (active ? " ON" : " READY");
            if (active && currentState != null && String.Equals(currentState.PreviousPowerGuid,
                currentState.TargetPowerGuid, StringComparison.OrdinalIgnoreCase)) powerText = "PLAN KEPT";
            else if (active && currentState != null && String.Equals(currentState.PowerPlanMode,
                PowerPlanPolicy.Ultimate, StringComparison.OrdinalIgnoreCase)) powerText = "ULTIMATE ON";
            SetStatus(powerStatus, active, powerText);

            string tuningText = active && currentState != null ? GetProcessStatusText(currentState) :
                UiText.T("เลือกไว้ ", "Selected ") + optionCount + "/6";
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
            else if (DateTime.UtcNow < activityNoticeUntil)
            {
                activityText.Text = activityNotice;
                activityText.ForeColor = Palette.Amber;
            }
            else activityText.ForeColor = Palette.Muted;
            tray.Text = active ? "Game Boost Pro - Game Mode ON" : "Game Boost Pro - Normal";
            advancedButton.Enabled = !working;
            RefreshPresetControls();
            UpdateFrameLabTarget();
            languageButton.Enabled = frameLab == null || !frameLab.IsCapturing;
        }

        private static string GetProcessStatusText(BoostState state)
        {
            if (state == null) return UiText.T("รอเปิดเกม", "Waiting for game");
            if (String.Equals(state.ProcessTuningStatus, "Applied", StringComparison.Ordinal)) return UiText.T("ยืนยันแล้ว", "Verified");
            if (String.Equals(state.ProcessTuningStatus, "Partial", StringComparison.Ordinal)) return UiText.T("ใช้ได้บางค่า", "Partly applied");
            if (String.Equals(state.ProcessTuningStatus, "Blocked", StringComparison.Ordinal)) return UiText.T("เกมไม่อนุญาต", "Blocked by game");
            if (String.Equals(state.ProcessTuningStatus, "NotRetained", StringComparison.Ordinal)) return UiText.T("เกมเปลี่ยนค่ากลับ", "Reverted by game");
            if (String.Equals(state.ProcessTuningStatus, "NotRequested", StringComparison.Ordinal)) return UiText.T("ไม่ปรับ CPU", "CPU unchanged");
            if (String.Equals(state.ProcessTuningStatus, "LegacyUnverified", StringComparison.Ordinal)) return "LEGACY SAFE";
            return "WAITING GAME";
        }

        private void SetControlsEnabled(bool enabled)
        {
            gameList.Enabled = enabled;
            gameSearch.Enabled = enabled;
            browseButton.Enabled = enabled;
            libraryButton.Enabled = enabled && GameDetector.IsCatalogLoaded;
            autoSwitch.Enabled = enabled && isAdmin && platform.IsSupported;
            launchCheck.Enabled = enabled;
            advancedButton.Enabled = enabled;
            launchButton.Enabled = enabled && detectedGame == null && CanLaunchConfiguredGame();
            gpuAdvisorButton.Enabled = enabled;
            frameLabButton.Enabled = enabled;
        }

        private void OpenAdvancedSettings(object sender, EventArgs e)
        {
            if (working) return;
            using (AdvancedSettingsForm dialog = new AdvancedSettingsForm(config))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RefreshAdvancedButton();
                RefreshState(GetAdvancedOptionCount() == 6
                    ? UiText.T("Advanced Mode: BEST / เปิดครบทุกระบบ", "Advanced: all settings allowed")
                    : "Advanced Mode: CUSTOM " + GetAdvancedOptionCount() + "/6");
                ShowNotice(UiText.T("บันทึกค่าที่อนุญาตแล้ว", "Allowed settings saved") + NextSessionNotice());
            }
        }

        private void OpenGraphicsAdvisor(object sender, EventArgs e)
        {
            using (GraphicsAdvisorForm dialog = new GraphicsAdvisorForm(GetConfiguredGameName(),
                config.GamePath, config.LibraryGameDirectory, config))
                dialog.ShowDialog(this);
            RefreshPresetControls();
            RefreshAdvancedButton();
        }

        private void OpenFrameLab(object sender, EventArgs e)
        {
            if (frameLab == null || frameLab.IsDisposed)
            {
                frameLab = new FrameBenchmarkForm("", 0, "", 0);
                frameLab.FormClosed += delegate { frameLab = null; };
            }
            UpdateFrameLabTarget();
            if (!frameLab.Visible) frameLab.Show(this);
            frameLab.WindowState = FormWindowState.Normal;
            frameLab.Activate();
        }

        private void UpdateFrameLabTarget()
        {
            if (frameLab == null || frameLab.IsDisposed) return;
            int id = 0;
            string name = "", display = "";
            long start = 0;
            if (detectedGame != null && detectedGame.Process != null)
            {
                try
                {
                    id = detectedGame.Process.Id;
                    name = detectedGame.Process.ProcessName;
                    start = detectedGame.Process.StartTime.ToUniversalTime().Ticks;
                    display = detectedGame.DisplayName;
                }
                catch { id = 0; }
            }
            frameLab.SetTarget(display, id, name, start);
        }

        private void RefreshAdvancedButton()
        {
            int enabled = GetAdvancedOptionCount();
            advancedButton.Text = UiText.T("ตั้งค่าขั้นสูง", "Advanced");
            advancedButton.ForeColor = Palette.Text;
            tips.SetToolTip(advancedButton, UiText.T("เลือกไว้ ", "Selected ") + enabled + UiText.T(" จาก 6 ค่า / แผนพลังงาน", " of 6 settings / Power plan"));
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
            if (working)
            {
                ShowNotice(UiText.T("รอให้ปรับระบบหรือคืนค่าเสร็จก่อนออกจากโปรแกรม", "Wait for the transition or restore to finish before exiting"));
                ShowFromTray();
                return;
            }
            if (Storage.HasState())
            {
                DialogResult result = MessageBox.Show(this,
                    UiText.T("Game Mode ยังเปิดอยู่ ต้องการคืนค่าเครื่องก่อนออกหรือไม่?", "Game Mode is active. Restore original settings before exiting?"),
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
            if (working)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            if (!config.AutoMode && !Storage.HasState())
            {
                tray.Visible = false;
                return;
            }
            e.Cancel = true;
            Hide();
            tray.ShowBalloonTip(1200, "Game Boost Pro", UiText.T("โปรแกรมยังทำงานอยู่ กดไอคอนเพื่อเปิดอีกครั้ง", "Still running in the tray. Click the icon to reopen"), ToolTipIcon.Info);
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
                Font = new Font("Leelawadee UI", size, style),
                ForeColor = color,
                BackColor = Color.Transparent
            };
        }

        private static Button MakeButton(string text, int x, int y, int width, int height, Color back, Color fore)
        {
            Button button = new DashboardButton();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.BackColor = back;
            button.ForeColor = fore;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Leelawadee UI", 9, FontStyle.Bold);
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
        private static void Main(string[] args)
        {
            int oldPid;
            if (args.Length == 2 && args[0] == "--wait-for-exit" && Int32.TryParse(args[1], out oldPid))
            {
                try { using (Process old = Process.GetProcessById(oldPid)) old.WaitForExit(10000); }
                catch (ArgumentException) { }
            }
            bool created;
            using (System.Threading.Mutex instance = new System.Threading.Mutex(true, @"Local\Codex.GameBoostPro", out created))
            {
                if (!created)
                {
                    MessageBox.Show(UiText.T("Game Boost Pro เปิดอยู่แล้วที่มุมจอ", "Game Boost Pro is already running in the tray"), "Game Boost Pro",
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
