using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Game Boost Pro Setup")]
[assembly: AssemblyProduct("Game Boost Pro")]
[assembly: AssemblyCompany("Game Boost Pro")]
[assembly: AssemblyVersion(GameBoostPro.BuildVersion.Assembly)]
[assembly: AssemblyFileVersion(GameBoostPro.BuildVersion.Assembly)]

namespace GameBoostProSetup
{
    internal static class Product
    {
        public const string Name = "Game Boost Pro";
        public const string Version = GameBoostPro.BuildVersion.Display;
        public const string FolderName = "Game Boost Pro";
        public const string AppFile = "GameBoostPro.exe";
        public const string ReadmeFile = "README.txt";
        public const string ToolsFolder = "tools";
        public const string PresentMonFile = "PresentMon.exe";
        public const string PresentMonLicenseFile = "PresentMon-LICENSE.txt";
        public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GameBoostPro";
        public const string StateKey = @"SOFTWARE\GameBoostPro";
        public const string StateValue = "RecoveryState";

        public static string InstallDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), FolderName); }
        }

        public static bool IsAppRunning()
        {
            Process[] processes = Process.GetProcessesByName("GameBoostPro");
            try { return processes.Length > 0; }
            finally { foreach (Process process in processes) process.Dispose(); }
        }

        public static bool HasPendingRecoveryState()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(StateKey, false))
                    if (key != null && key.GetValue(StateValue, null) is string) return true;
            }
            catch { return true; }
            string legacyDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGameBoost");
            return File.Exists(Path.Combine(legacyDirectory, "state-pro.json")) ||
                File.Exists(Path.Combine(legacyDirectory, "state.json"));
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly Label status;
        private readonly Button installButton;
        private bool installed;

        public SetupForm()
        {
            Text = "Game Boost Pro Setup " + Product.Version;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(590, 390);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(17, 19, 18);
            ForeColor = Color.FromArgb(239, 242, 240);
            Font = new Font("Segoe UI", 9);
            Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon != null) Icon = icon;

            Controls.Add(MakeLabel("GAME BOOST", 30, 25, 185, 34, 19, FontStyle.Bold, ForeColor));
            Label badge = MakeLabel("SETUP " + Product.Version, 220, 30, 108, 24, 8, FontStyle.Bold, BackColor);
            badge.BackColor = Color.FromArgb(199, 243, 107);
            badge.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(badge);
            badge.BringToFront();
            Controls.Add(MakeLabel("Best performance profile for supported Windows gaming systems", 31, 65, 500, 24,
                9, FontStyle.Regular, Color.FromArgb(158, 169, 164)));

            Panel line = new Panel { Location = new Point(30, 104), Size = new Size(530, 1), BackColor = Color.FromArgb(67, 73, 70) };
            Controls.Add(line);
            Controls.Add(MakeLabel("SUPPORTED", 31, 127, 120, 22, 8, FontStyle.Bold, Color.FromArgb(244, 183, 77)));
            Controls.Add(MakeLabel("Acer Laptop + NitroSense", 31, 154, 255, 24, 11, FontStyle.Bold, ForeColor));
            Controls.Add(MakeLabel("Desktop PC", 302, 154, 170, 24, 11, FontStyle.Bold, ForeColor));
            Controls.Add(MakeLabel("โน้ตบุ๊กยี่ห้ออื่นจะถูกตรวจพบและปิด Boost เพื่อความปลอดภัย", 31, 185, 510, 24,
                9, FontStyle.Regular, Color.FromArgb(158, 169, 164)));

            Controls.Add(MakeLabel("INSTALL LOCATION", 31, 225, 160, 22, 8, FontStyle.Bold, Color.FromArgb(244, 183, 77)));
            TextBox path = new TextBox
            {
                Text = Product.InstallDirectory,
                Location = new Point(31, 251),
                Size = new Size(528, 28),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(27, 30, 29),
                ForeColor = ForeColor
            };
            Controls.Add(path);

            status = MakeLabel("พร้อมติดตั้ง / Windows จะบันทึก Uninstaller ให้", 31, 303, 350, 42,
                9, FontStyle.Regular, Color.FromArgb(158, 169, 164));
            Controls.Add(status);
            installButton = new Button
            {
                Text = "INSTALL",
                Location = new Point(416, 303),
                Size = new Size(143, 44),
                BackColor = Color.FromArgb(199, 243, 107),
                ForeColor = Color.FromArgb(17, 19, 18),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            installButton.FlatAppearance.BorderSize = 0;
            installButton.Click += InstallClick;
            Controls.Add(installButton);
            string existing = Path.Combine(Product.InstallDirectory, Product.AppFile);
            if (File.Exists(existing))
            {
                string prior = FileVersionInfo.GetVersionInfo(existing).FileVersion;
                status.Text = "อัปเดต " + prior + " -> " + Product.Version + " / เก็บโปรไฟล์เดิม";
                installButton.Text = "UPDATE";
            }
        }

        private void InstallClick(object sender, EventArgs e)
        {
            if (installed)
            {
                LaunchInstalledApp();
                Close();
                return;
            }
            if (Product.IsAppRunning())
            {
                MessageBox.Show(this, "กรุณาออกจาก Game Boost Pro ก่อนติดตั้งรุ่นใหม่", Product.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            installButton.Enabled = false;
            status.Text = "กำลังติดตั้งไฟล์และสร้าง Shortcut...";
            Application.DoEvents();
            try
            {
                InstallPackage();
                installed = true;
                status.Text = "ติดตั้ง Game Boost Pro " + Product.Version + " เรียบร้อยแล้ว";
                status.ForeColor = Color.FromArgb(199, 243, 107);
                installButton.Text = "LAUNCH";
                installButton.Enabled = true;
            }
            catch (Exception ex)
            {
                status.Text = "ติดตั้งไม่สำเร็จ";
                status.ForeColor = Color.FromArgb(242, 112, 89);
                installButton.Enabled = true;
                MessageBox.Show(this, ex.Message, Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal static void InstallPackage()
        {
            if (Product.IsAppRunning()) throw new InvalidOperationException("Exit Game Boost Pro before updating.");
            if (Product.HasPendingRecoveryState()) throw new InvalidOperationException("Restore the active Boost session before updating.");
            InstallerMaintenance.InstallFiles(Product.InstallDirectory, Application.ExecutablePath);
            InstallerMaintenance.SyncShortcuts(Product.InstallDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            RegisterUninstaller();
            InstallerMaintenance.RefreshStartMenu();
        }

        private static void RegisterUninstaller()
        {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(Product.UninstallKey))
            {
                string uninstall = Path.Combine(Product.InstallDirectory, "Uninstall.exe");
                key.SetValue("DisplayName", Product.Name);
                key.SetValue("DisplayVersion", Product.Version);
                key.SetValue("Publisher", Product.Name);
                key.SetValue("DisplayIcon", Path.Combine(Product.InstallDirectory, Product.AppFile));
                key.SetValue("InstallLocation", Product.InstallDirectory);
                key.SetValue("UninstallString", "\"" + uninstall + "\" /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private static void LaunchInstalledApp()
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Product.InstallDirectory, Product.AppFile))
            {
                UseShellExecute = true
            });
        }

        private static Label MakeLabel(string text, int x, int y, int width, int height, float size,
            FontStyle style, Color color)
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
    }

    internal static class Program
    {
        private const int MoveFileDelayUntilReboot = 0x4;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 1 && args[0].Equals("/install", StringComparison.OrdinalIgnoreCase))
            {
                try { SetupForm.InstallPackage(); Environment.ExitCode = 0; }
                catch (Exception ex)
                {
                    Environment.ExitCode = 1;
                    string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGameBoost");
                    try { Directory.CreateDirectory(log); File.WriteAllText(Path.Combine(log, "setup-error.txt"), ex.ToString()); } catch { }
                }
                return;
            }
            if (args.Length > 0 && args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                BeginUninstall();
                return;
            }
            if (args.Length > 1 && args[0].Equals("/cleanup", StringComparison.OrdinalIgnoreCase))
            {
                FinishUninstall(args[1]);
                return;
            }
            Application.Run(new SetupForm());
        }

        private static void BeginUninstall()
        {
            if (MessageBox.Show("ต้องการถอนการติดตั้ง Game Boost Pro หรือไม่?\n\nโปรไฟล์ส่วนตัวจะยังถูกเก็บไว้",
                Product.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (Product.IsAppRunning())
            {
                MessageBox.Show("กรุณาออกจาก Game Boost Pro ก่อนถอนการติดตั้ง", Product.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (Product.HasPendingRecoveryState())
            {
                MessageBox.Show("ยังมี Game Mode ที่รอคืนค่า กรุณาเปิด Game Boost Pro แล้วกด RESTORE ก่อนถอนการติดตั้ง",
                    Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string temporary = Path.Combine(Path.GetTempPath(), "GameBoostPro-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, temporary, true);
            Process.Start(new ProcessStartInfo(temporary, "/cleanup \"" + Product.InstallDirectory + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static void FinishUninstall(string requestedDirectory)
        {
            Thread.Sleep(800);
            string expected = Path.GetFullPath(Product.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string requested = Path.GetFullPath(requestedDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (!String.Equals(expected, requested, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("ตำแหน่งถอนการติดตั้งไม่ถูกต้อง", Product.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DeleteIfExists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Product.Name + ".lnk"));
            string startFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Product.FolderName);
            DeleteIfExists(Path.Combine(startFolder, Product.Name + ".lnk"));
            try { if (Directory.Exists(startFolder)) Directory.Delete(startFolder, false); } catch { }
            try { Registry.LocalMachine.DeleteSubKeyTree(Product.UninstallKey, false); } catch { }
            try { if (Directory.Exists(requested)) Directory.Delete(requested, true); } catch (Exception ex)
            {
                MessageBox.Show("ถอนการติดตั้งบางส่วนไม่สำเร็จ: " + ex.Message, Product.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MoveFileEx(Application.ExecutablePath, null, MoveFileDelayUntilReboot);
            MessageBox.Show("ถอนการติดตั้ง Game Boost Pro เรียบร้อยแล้ว", Product.Name,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void DeleteIfExists(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
