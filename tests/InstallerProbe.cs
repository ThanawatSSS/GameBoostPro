using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

internal static class InstallerProbe
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        checks++;
    }
    private static string Hash(string path)
    {
        using (SHA256 hash = SHA256.Create())
        using (Stream stream = File.OpenRead(path))
            return BitConverter.ToString(hash.ComputeHash(stream));
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        string temp = Path.Combine(Path.GetTempPath(), "GameBoostPro-InstallerProbe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Assembly setup = Assembly.LoadFile(Path.GetFullPath(args[0]));
            Type maintenance = setup.GetType("GameBoostProSetup.InstallerMaintenance", true);
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public;
            MethodInfo install = maintenance.GetMethod("InstallFiles", flags);
            string destination = Path.Combine(temp, "installed");
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "keep-personal.txt"), "preserve");
            install.Invoke(null, new object[] { destination, args[0] });
            string app = Path.Combine(destination, "GameBoostPro.exe");
            Check(System.Diagnostics.FileVersionInfo.GetVersionInfo(app).FileVersion == "3.3.1.0", "Installed payload version");
            string initialHash = Hash(app);
            install.Invoke(null, new object[] { destination, args[0] });
            Check(Hash(app) == initialHash && File.ReadAllText(Path.Combine(destination, "keep-personal.txt")) == "preserve",
                "Repeated update preserves payload and unrelated files");
            string readme = Path.Combine(destination, "README.txt");
            File.WriteAllText(app, "prior-app");
            File.WriteAllText(readme, "prior-readme");
            bool failed = false;
            using (FileStream locked = new FileStream(readme, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                try { install.Invoke(null, new object[] { destination, args[0] }); }
                catch (TargetInvocationException) { failed = true; }
            Check(failed && File.ReadAllText(app) == "prior-app" && File.ReadAllText(readme) == "prior-readme",
                "Locked file rolls back the already-replaced app");
            Check(Directory.GetDirectories(destination, ".update-*").Length == 0, "Successful rollback cleans owned staging only");
            install.Invoke(null, new object[] { destination, args[0] });
            string commonPrograms = Path.Combine(temp, "common-programs"), commonDesktop = Path.Combine(temp, "common-desktop"),
                userPrograms = Path.Combine(temp, "user-programs"), userDesktop = Path.Combine(temp, "user-desktop");
            string legacy = Path.Combine(temp, "legacy", "GameBoostPro.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(legacy));
            File.Copy(app, legacy);
            MethodInfo shortcut = maintenance.GetMethod("CreateShortcut", flags);
            string old = Path.Combine(userPrograms, "GameBoostPro.lnk");
            shortcut.Invoke(null, new object[] { old, legacy });
            string userOther = Path.Combine(userPrograms, "NotOurFolder", "Game Boost Pro.lnk");
            shortcut.Invoke(null, new object[] { userOther, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe") });
            MethodInfo sync = maintenance.GetMethod("SyncShortcuts", flags);
            object[] locations = { destination, commonPrograms, commonDesktop, userPrograms, userDesktop };
            sync.Invoke(null, locations);
            string canonical = Path.Combine(commonPrograms, "Game Boost Pro", "Game Boost Pro.lnk");
            Check(!File.Exists(old) && File.Exists(legacy), "Migration removes the stale link, not the legacy executable");
            Check(File.Exists(userOther), "Unverified similarly named shortcuts are preserved");
            Check(Convert.ToString(maintenance.GetMethod("ReadShortcutTarget", flags).Invoke(null, new object[] { canonical })) == app,
                "Start Menu points to the canonical installed executable");
            sync.Invoke(null, locations);
            Check(Directory.GetFiles(commonPrograms, "*.lnk", SearchOption.AllDirectories).Length == 1, "Shortcut repair is idempotent");
            Console.WriteLine("installer_checks=" + checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            string full = Path.GetFullPath(temp);
            if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                try { Directory.Delete(full, true); } catch { }
        }
    }
}
