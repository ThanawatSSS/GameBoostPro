using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameBoostPro
{
    internal sealed class GraphicsDestination
    {
        public string Target { get; private set; }
        public bool IsStoreApp { get; private set; }
        public GraphicsDestination(string target, bool store) { Target = target; IsStoreApp = store; }
    }

    internal sealed class GraphicsDestinations
    {
        public GraphicsDestination NvidiaApp { get; private set; }
        public GraphicsDestination ControlPanel { get; private set; }
        public const string ControlPanelStore = "https://www.microsoft.com/store/apps/9NF8H0H7WMLT";
        public const string NvidiaDownload = "https://www.nvidia.com/en-us/software/nvidia-app/";

        public static GraphicsDestinations Discover()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            List<string> apps = new List<string>();
            List<string> panels = new List<string>();
            foreach (string root in new[] { programFiles, programFilesX86 })
            {
                if (String.IsNullOrWhiteSpace(root)) continue;
                apps.Add(Path.Combine(root, @"NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe"));
                apps.Add(Path.Combine(root, @"NVIDIA Corporation\NVIDIA App\NVIDIA App.exe"));
                panels.Add(Path.Combine(root, @"NVIDIA Corporation\Control Panel Client\nvcplui.exe"));
            }
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\nvcplui.exe"))
                        if (key != null) panels.Add(Convert.ToString(key.GetValue("")).Trim('"'));
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            return Resolve(apps, panels, ReadStoreIds(), File.Exists);
        }

        internal static GraphicsDestinations Resolve(IEnumerable<string> appPaths, IEnumerable<string> panelPaths,
            IEnumerable<string> storeIds, Func<string, bool> exists)
        {
            GraphicsDestinations result = new GraphicsDestinations();
            result.NvidiaApp = FindExecutable(appPaths, exists);
            result.ControlPanel = FindExecutable(panelPaths, exists);
            if (result.ControlPanel == null)
                foreach (string id in storeIds)
                    if (IsControlPanelId(id))
                    {
                        result.ControlPanel = new GraphicsDestination(id, true);
                        break;
                    }
            return result;
        }

        private static GraphicsDestination FindExecutable(IEnumerable<string> paths, Func<string, bool> exists)
        {
            foreach (string path in paths)
                if (!String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) &&
                    String.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) && exists(path))
                    return new GraphicsDestination(path, false);
            return null;
        }

        private static bool IsControlPanelId(string id)
        {
            return id != null && System.Text.RegularExpressions.Regex.IsMatch(id,
                @"\ANVIDIACorp\.NVIDIAControlPanel_56jybvy8sckqj![A-Za-z0-9.]+\z",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Enumerate the Shell's installed-app namespace; never guess a versioned WindowsApps path.
        private static List<string> ReadStoreIds()
        {
            List<string> ids = new List<string>();
            object shell = null, folder = null, items = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                folder = Call(shell, "NameSpace", BindingFlags.InvokeMethod, "shell:AppsFolder");
                if (folder == null) return ids;
                items = Call(folder, "Items", BindingFlags.InvokeMethod);
                int count = Convert.ToInt32(Call(items, "Count", BindingFlags.GetProperty));
                for (int i = 0; i < count; i++)
                {
                    object item = null;
                    try
                    {
                        item = Call(items, "Item", BindingFlags.InvokeMethod, i);
                        string id = Convert.ToString(Call(item, "Path", BindingFlags.GetProperty));
                        if (IsControlPanelId(id)) ids.Add(id);
                    }
                    finally { Release(item); }
                }
            }
            catch (COMException) { }
            catch (TargetInvocationException) { }
            finally { Release(items); Release(folder); Release(shell); }
            return ids;
        }

        private static object Call(object target, string member, BindingFlags flags, params object[] args)
        {
            return target.GetType().InvokeMember(member, flags, null, target, args);
        }
        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }

        public static void Open(IWin32Window owner, GraphicsDestination destination)
        {
            if (destination == null) return;
            Open(owner, destination.IsStoreApp ? "shell:AppsFolder\\" + destination.Target : destination.Target);
        }

        public static void Open(IWin32Window owner, string target)
        {
            object shell = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                Call(shell, "ShellExecute", BindingFlags.InvokeMethod, target, "", "", "open", 1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, UiText.T("เปิดไม่สำเร็จ: ", "Could not open: ") + ex.GetBaseException().Message,
                    "Game Boost Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Release(shell); }
        }
    }

internal sealed class WorkspaceTabs : UserControl
    {
        private readonly FlowLayoutPanel tabs;
        private readonly Panel pages;
        private readonly List<Panel> views = new List<Panel>();
        private readonly List<RadioButton> selectors = new List<RadioButton>();
        public WorkspaceTabs()
        {
            Dock = DockStyle.Fill;
            BackColor = Palette.Back;
            AccessibleRole = AccessibleRole.PageTabList;
            TableLayoutPanel layout = WorkspaceLayout.Grid(1, 44, -1);
            tabs = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            pages = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            layout.Controls.Add(tabs, 0, 0); layout.Controls.Add(pages, 0, 1);
            Controls.Add(layout);
        }
        public Panel AddPage(string title)
        {
            Panel page = new Panel { Dock = DockStyle.Fill, AccessibleName = title };
            int index = views.Count;
            RadioButton tab = new RadioButton { Text = title, Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
                AutoSize = true, Font = UiText.Body(10, FontStyle.Regular), Padding = new Padding(14, 7, 14, 7),
                Margin = new Padding(0, 0, 10, 0), ForeColor = Palette.Muted, BackColor = Palette.Back,
                AccessibleRole = AccessibleRole.PageTab, Cursor = Cursors.Hand };
            tab.FlatAppearance.BorderSize = 0;
            tab.FlatAppearance.CheckedBackColor = ProfileColors.MasterSurface;
            tab.FlatAppearance.MouseOverBackColor = Palette.SurfaceHigh;
            tab.FlatAppearance.MouseDownBackColor = ProfileColors.MasterSurface;
            tab.UseVisualStyleBackColor = false;
            tab.CheckedChanged += delegate
            {
                if (!tab.Checked) return;
                SelectPage(index);
            };
            views.Add(page); selectors.Add(tab); pages.Controls.Add(page); tabs.Controls.Add(tab);
            SelectPage(0);
            return page;
        }
        public void SelectPage(int index)
        {
            for (int i = 0; i < views.Count; i++)
            {
                views[i].Visible = i == index;
                selectors[i].Checked = i == index;
                selectors[i].ForeColor = i == index ? Palette.Cyan : Palette.Muted;
                selectors[i].BackColor = i == index ? ProfileColors.MasterSurface : Palette.Back;
            }
        }
    }

    internal static class WorkspaceLayout
    {
        public static TableLayoutPanel Grid(int columns, params float[] rows)
        {
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns,
                RowCount = rows.Length, Margin = Padding.Empty, BackColor = Color.Transparent };
            for (int i = 0; i < columns; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            foreach (float row in rows) grid.RowStyles.Add(new RowStyle(row < 0 ? SizeType.Percent : SizeType.Absolute, row < 0 ? 100 : row));
            return grid;
        }

        public static Label Label(string text, float size, Color color)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, AutoEllipsis = false,
                Font = UiText.Body(size, FontStyle.Regular), ForeColor = color,
                TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 2, 10, 2) };
        }

        public static Button Button(string text, Color color)
        {
            DashboardButton button = new DashboardButton { Text = text, Dock = DockStyle.Fill,
                Margin = new Padding(6, 6, 0, 6), FlatStyle = FlatStyle.Flat, BackColor = Palette.SurfaceHigh,
                ForeColor = color, Font = UiText.Body(10, FontStyle.Regular), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        public static TableLayoutPanel ScrollPage(Control parent, int minimumHeight)
        {
            Panel viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Palette.Back };
            TableLayoutPanel page = Grid(1, -1);
            page.Dock = DockStyle.Top;
            page.Padding = new Padding(20, 12, 20, 12);
            page.Height = minimumHeight;
            viewport.Controls.Add(page);
            parent.Controls.Add(viewport);
            EventHandler resize = delegate
            {
                float scale;
                using (Graphics g = viewport.CreateGraphics()) scale = Math.Max(parent.Font.SizeInPoints / 10f, g.DpiX / 96f);
                page.Height = Math.Max((int)(minimumHeight * scale), viewport.ClientSize.Height);
            };
            viewport.SizeChanged += resize;
            viewport.FontChanged += resize;
            return page;
        }

        public static void Form(Form form, string title, Size size, Size minimum)
        {
            form.Text = title;
            form.AutoScaleDimensions = new SizeF(96, 96);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.StartPosition = FormStartPosition.CenterParent;
            form.ClientSize = size;
            form.MinimumSize = minimum;
            form.BackColor = Palette.Back;
            form.ForeColor = Palette.Text;
            form.Font = UiText.Body(10, FontStyle.Regular);
            form.Icon = Icon.ExtractAssociatedIcon(typeof(MainForm).Assembly.Location);
        }
    }
}
