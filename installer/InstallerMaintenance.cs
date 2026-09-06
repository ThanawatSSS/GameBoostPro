using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GameBoostProSetup
{
    internal static class InstallerMaintenance
    {
        private static readonly string[] OwnedFiles = {
            Product.AppFile, Product.ReadmeFile, @"tools\PresentMon.exe",
            @"tools\PresentMon-LICENSE.txt", "Uninstall.exe"
        };

        public static void InstallFiles(string directory, string setupPath)
        {
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            Directory.CreateDirectory(root);
            string stage = Path.Combine(root, ".update-" + Guid.NewGuid().ToString("N"));
            string backup = Path.Combine(stage, "previous");
            Directory.CreateDirectory(backup);
            List<string> replaced = new List<string>();
            bool rollbackComplete = true;
            try
            {
                Extract("GameBoostPro.Payload.exe", Path.Combine(stage, Product.AppFile));
                Extract("GameBoostPro.Readme.txt", Path.Combine(stage, Product.ReadmeFile));
                Extract("GameBoostPro.PresentMon.exe", Path.Combine(stage, @"tools\PresentMon.exe"));
                Extract("GameBoostPro.PresentMonLicense.txt", Path.Combine(stage, @"tools\PresentMon-LICENSE.txt"));
                File.Copy(setupPath, Path.Combine(stage, "Uninstall.exe"), true);
                foreach (string name in OwnedFiles)
                {
                    string target = Path.Combine(root, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    string prior = Path.Combine(backup, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(prior));
                    if (File.Exists(target)) File.Replace(Path.Combine(stage, name), target, prior);
                    else File.Move(Path.Combine(stage, name), target);
                    replaced.Add(name);
                }
            }
            catch (Exception installError)
            {
                List<Exception> failures = new List<Exception> { installError };
                replaced.Reverse();
                foreach (string name in replaced)
                {
                    try
                    {
                        string target = Path.Combine(root, name), prior = Path.Combine(backup, name);
                        if (File.Exists(prior)) File.Replace(prior, target, null);
                        else if (File.Exists(target)) File.Delete(target);
                    }
                    catch (Exception rollbackError) { rollbackComplete = false; failures.Add(rollbackError); }
                }
                if (!rollbackComplete) throw new AggregateException("Update failed. Previous files are retained at " + backup, failures);
                throw;
            }
            finally
            {
                if (rollbackComplete && Path.GetFullPath(stage).StartsWith(root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                    try { Directory.Delete(stage, true); } catch { }
            }
        }

        private static void Extract(string resourceName, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("Missing payload: " + resourceName);
                using (FileStream output = File.Create(destination)) input.CopyTo(output);
            }
        }

        public static bool IsOwnedTarget(string target)
        {
            try
            {
                return Path.IsPathRooted(target) && File.Exists(target) &&
                    String.Equals(Path.GetFileName(target), Product.AppFile, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(FileVersionInfo.GetVersionInfo(target).ProductName, Product.Name, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        public static void SyncShortcuts(string directory, string commonPrograms, string commonDesktop,
            string userPrograms, string userDesktop)
        {
            string start = Path.Combine(commonPrograms, Product.FolderName, Product.Name + ".lnk");
            string desktop = Path.Combine(commonDesktop, Product.Name + ".lnk");
            string target = Path.Combine(directory, Product.AppFile);
            if (!IsOwnedTarget(target)) throw new InvalidOperationException("Installed application identity could not be verified");
            CreateShortcut(start, target);
            CreateShortcut(desktop, target);
            foreach (string root in new[] { commonPrograms, userPrograms, commonDesktop, userDesktop })
            {
                if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                bool recursive = String.Equals(root, commonPrograms, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(root, userPrograms, StringComparison.OrdinalIgnoreCase);
                foreach (string link in Directory.GetFiles(root, "*.lnk", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(link);
                    if (!String.Equals(name, Product.Name + ".lnk", StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals(name, "GameBoostPro.lnk", StringComparison.OrdinalIgnoreCase)) continue;
                    if (String.Equals(link, start, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(link, desktop, StringComparison.OrdinalIgnoreCase)) continue;
                    // Remove only verified product shortcuts. Unknown links and legacy binaries stay untouched.
                    if (IsOwnedTarget(ReadShortcutTarget(link))) File.Delete(link);
                }
            }
        }

        public static string ReadShortcutTarget(string path)
        {
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                return Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
            }
            finally { Release(shortcut); Release(shell); }
        }

        public static void CreateShortcut(string path, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                Type type = shortcut.GetType();
                type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                type.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { "" });
                type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(target) });
                type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
                type.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { Product.Name + " " + Product.Version });
                type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally { Release(shortcut); Release(shell); }
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(uint eventId, uint flags, string item1, IntPtr item2);

        public static void RefreshStartMenu()
        {
            SHChangeNotify(0x1000, 0x0005, Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), IntPtr.Zero);
            SHChangeNotify(0x1000, 0x0005, Environment.GetFolderPath(Environment.SpecialFolder.Programs), IntPtr.Zero);
        }
    }
}
