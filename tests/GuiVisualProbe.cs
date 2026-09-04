using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class GuiVisualProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2) return 2;
        string testDirectory = Path.Combine(Path.GetTempPath(),
            "GameBoostPro-Visual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        Directory.CreateDirectory(args[1]);
        AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", testDirectory);
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Assembly assembly = Assembly.LoadFile(args[0]);
            CaptureMainForm(assembly, args[1]);
            CaptureAdvancedForm(assembly, args[1]);
            CaptureGraphicsAdvisor(assembly, args[1]);
            CaptureFrameLab(assembly, args[1]);
            return 0;
        }
        finally
        {
            AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", null);
            try { Directory.Delete(testDirectory, true); }
            catch { }
        }
    }

    private static void CaptureMainForm(Assembly assembly, string outputDirectory)
    {
        Type formType = assembly.GetType("GameBoostPro.MainForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType, true))
        {
            BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            object config = formType.GetField("config", fields).GetValue(form);
            config.GetType().GetProperty("AutoMode").SetValue(config, false, null);
            Type detector = assembly.GetType("GameBoostPro.PlatformDetector", true);
            MethodInfo evaluate = detector.GetMethod("Evaluate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object platform = evaluate.Invoke(null, new object[] { "Acer", "Nitro ANV15-51", true, true });
            formType.GetField("platform", fields).SetValue(form, platform);
            Label platformLabel = (Label)formType.GetField("platformLabel", fields).GetValue(form);
            platformLabel.Text = "ACER + NITROSENSE";
            platformLabel.ForeColor = Color.FromArgb(92, 207, 219);
            object autoSwitch = formType.GetField("autoSwitch", fields).GetValue(form);
            autoSwitch.GetType().GetProperty("Value").SetValue(autoSwitch, false, null);
            Capture(form, Path.Combine(outputDirectory, "main.png"));
        }
    }

    private static void CaptureAdvancedForm(Assembly assembly, string outputDirectory)
    {
        Type configType = assembly.GetType("GameBoostPro.AppConfig", true);
        object config = Activator.CreateInstance(configType, true);
        Type formType = assembly.GetType("GameBoostPro.AdvancedSettingsForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { config }, null))
            Capture(form, Path.Combine(outputDirectory, "advanced.png"));
    }

    private static void CaptureGraphicsAdvisor(Assembly assembly, string outputDirectory)
    {
        Type formType = assembly.GetType("GameBoostPro.GraphicsAdvisorForm", true);
        using (Form form = (Form)Activator.CreateInstance(formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { "VALORANT", "", @"C:\Riot Games\VALORANT", 0, "", 0L }, null))
        {
            Type capabilitiesType = assembly.GetType("GameBoostPro.GraphicsCapabilities", true);
            object capabilities = Activator.CreateInstance(capabilitiesType, true);
            Set(capabilities, "GpuName", "NVIDIA GeForce RTX 4050 Laptop GPU");
            Set(capabilities, "IsNvidia", true);
            Set(capabilities, "IsRtx", true);
            Set(capabilities, "RtxSeries", 40);
            Set(capabilities, "SupportsDlssSuperResolution", true);
            Set(capabilities, "SupportsFrameGeneration", true);
            Set(capabilities, "SupportsMultiFrameGeneration", false);
            Set(capabilities, "SupportsSmoothMotion", true);

            Type snapshotType = assembly.GetType("GameBoostPro.GraphicsAdvisorSnapshot", true);
            object snapshot = Activator.CreateInstance(snapshotType, true);
            Set(snapshot, "Capabilities", capabilities);
            Set(snapshot, "DriverVersion", "32.0.15.1664");
            Set(snapshot, "DisplayRoute", "Inactive");
            Set(snapshot, "NisEligibility", "RouteBlocked");
            Set(snapshot, "HasHybridGraphics", true);
            Set(snapshot, "HasNvidiaApp", true);
            Set(snapshot, "NvidiaAppVersion", "11.0.9.251");
            Set(snapshot, "GameName", "VALORANT");
            Set(snapshot, "GamePath", "");
            Set(snapshot, "HasDlssLibraryHint", false);
            Set(snapshot, "HasFrameGenerationLibraryHint", false);
            Set(snapshot, "IsCompetitiveGame", true);
            MethodInfo apply = formType.GetMethod("ApplySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Capture(form, Path.Combine(outputDirectory, "graphics-advisor.png"), delegate
            {
                apply.Invoke(form, new object[] { snapshot });
            });
        }
    }

    private static void CaptureFrameLab(Assembly assembly, string outputDirectory)
    {
        Type formType = assembly.GetType("GameBoostPro.FrameBenchmarkForm", true);
        using (System.Diagnostics.Process current = System.Diagnostics.Process.GetCurrentProcess())
        using (Form form = (Form)Activator.CreateInstance(formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { "VALORANT", current.Id, current.ProcessName,
                current.StartTime.ToUniversalTime().Ticks }, null))
            Capture(form, Path.Combine(outputDirectory, "frame-lab.png"));
    }

    private static void Set(object target, string property, object value)
    {
        target.GetType().GetProperty(property).SetValue(target, value, null);
    }

    private static void Capture(Form form, string outputPath)
    {
        Capture(form, outputPath, null);
    }

    private static void Capture(Form form, string outputPath, Action afterShow)
    {
        form.Show();
        Application.DoEvents();
        if (afterShow != null) afterShow();
        Application.DoEvents();
        form.PerformLayout();
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(outputPath, ImageFormat.Png);
        }
        form.Hide();
    }
}
