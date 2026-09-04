using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class GuiPerfProbe
{
    private static bool processRetuneValid;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--idle-child")
        {
            System.Threading.Thread.Sleep(15000);
            return 0;
        }
        if (args.Length != 1) return 2;
        string testDirectory = Path.Combine(Path.GetTempPath(),
            "GameBoostPro-Perf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", testDirectory);
        try { return RunProbe(args[0]); }
        finally
        {
            AppDomain.CurrentDomain.SetData("GameBoostPro.TestAppDirectory", null);
            try { if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true); }
            catch { }
        }
    }

    private static int RunProbe(string appPath)
    {
        Assembly assembly = Assembly.LoadFile(appPath);
        bool stateCacheValid = ValidateStateCache(assembly);
        Type formType = assembly.GetType("GameBoostPro.MainForm", true);
        Stopwatch startup = Stopwatch.StartNew();
        Form form = (Form)Activator.CreateInstance(formType, true);
        startup.Stop();
        Console.WriteLine("startup_ms={0:F2}", startup.Elapsed.TotalMilliseconds);
        BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        Timer timer = (Timer)formType.GetField("monitor", fields).GetValue(form);
        timer.Stop();

        object config = formType.GetField("config", fields).GetValue(form);
        config.GetType().GetProperty("AutoMode").SetValue(config, false, null);
        MethodInfo tick = formType.GetMethod("MonitorTick", fields);
        double monitorP95 = Measure("monitor_tick", delegate
        {
            tick.Invoke(form, new object[] { null, EventArgs.Empty });
        });

        MethodInfo metrics = formType.GetMethod("UpdateMetrics", fields);
        double metricsP95 = Measure("metrics_only", delegate { metrics.Invoke(form, null); });

        Control dial = (Control)formType.GetField("dial", fields).GetValue(form);
        MethodInfo paint = dial.GetType().GetMethod("OnPaint", fields);
        double dialPaintP95;
        using (Bitmap bitmap = new Bitmap(dial.Width, dial.Height))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            PaintEventArgs paintArgs = new PaintEventArgs(graphics, dial.ClientRectangle);
            dialPaintP95 = Measure("boost_dial_paint", delegate
            {
                paint.Invoke(dial, new object[] { paintArgs });
            });
        }

        Type detectorType = assembly.GetType("GameBoostPro.GameDetector", true);
        MethodInfo detect = detectorType.GetMethod("FindRunningGame",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        double detectionP95 = Measure("game_detection", delegate
        {
            detect.Invoke(null, new object[] { "" });
        });
        double cachedDetectionP95 = MeasureCachedDetection(assembly, detect);

        Console.WriteLine("ui_budget_ms=16.00");
        Console.WriteLine("background_budget_ms=25.00");
        Console.WriteLine("paint_budget_ms=16.00");
        Console.WriteLine("cached_detection_budget_ms=10.00");
        Console.WriteLine("startup_budget_ms=1500.00");
        Console.WriteLine("stable_monitor_core_duty_p95_percent={0:F3}",
            (metricsP95 + detectionP95) / 3000.0 * 100.0);
        Console.WriteLine("tray_monitor_core_duty_p95_percent={0:F3}",
            cachedDetectionP95 / 3000.0 * 100.0);
        Console.WriteLine("new_game_process_retuned={0}", processRetuneValid);
        form.Dispose();
        return startup.Elapsed.TotalMilliseconds <= 1500.0 && monitorP95 <= 16.0 &&
            metricsP95 <= 25.0 && detectionP95 <= 25.0 && dialPaintP95 <= 16.0 &&
            cachedDetectionP95 <= 10.0 && stateCacheValid && processRetuneValid ? 0 : 1;
    }

    private static bool ValidateStateCache(Assembly assembly)
    {
        Type storageType = assembly.GetType("GameBoostPro.Storage", true);
        Type stateType = assembly.GetType("GameBoostPro.BoostState", true);
        BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo save = storageType.GetMethod("SaveState", methods);
        MethodInfo load = storageType.GetMethod("LoadState", methods);
        MethodInfo loadForRestore = storageType.GetMethod("LoadStateForRestore", methods);
        MethodInfo delete = storageType.GetMethod("DeleteState", methods);
        MethodInfo hasState = storageType.GetMethod("HasState", methods);
        try
        {
            object state = Activator.CreateInstance(stateType, true);
            stateType.GetProperty("EnabledAt").SetValue(state, "cache-probe", null);
            save.Invoke(null, new object[] { state });
            object first = load.Invoke(null, null);
            object second = load.Invoke(null, null);
            object fresh = loadForRestore.Invoke(null, null);
            bool reused = Object.ReferenceEquals(first, second);
            bool restoreWasFresh = fresh != null && !Object.ReferenceEquals(second, fresh) &&
                Convert.ToString(stateType.GetProperty("EnabledAt").GetValue(fresh, null)) == "cache-probe";
            delete.Invoke(null, null);
            bool cleared = !(bool)hasState.Invoke(null, null);
            Console.WriteLine("state_cache_reused={0}", reused);
            Console.WriteLine("restore_disk_refresh={0}", restoreWasFresh);
            Console.WriteLine("state_cache_cleared={0}", cleared);
            return reused && restoreWasFresh && cleared;
        }
        finally
        {
            try { delete.Invoke(null, null); }
            catch { }
        }
    }

    private static double MeasureCachedDetection(Assembly assembly, MethodInfo detect)
    {
        string helperPath = Path.Combine(Path.GetTempPath(),
            "gbp-probe-game-" + Guid.NewGuid().ToString("N") + ".exe");
        Process helper = null;
        try
        {
            File.Copy(Process.GetCurrentProcess().MainModule.FileName, helperPath);
            helper = Process.Start(new ProcessStartInfo(helperPath, "--idle-child")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });

            object first = null;
            for (int i = 0; i < 20 && first == null; i++)
            {
                System.Threading.Thread.Sleep(25);
                first = detect.Invoke(null, new object[] { helperPath });
            }
            if (first == null) return Double.MaxValue;
            DisposeDetectedGame(first);

            double p95 = Measure("cached_game_detection", delegate
            {
                object game = detect.Invoke(null, new object[] { helperPath });
                if (game == null) throw new InvalidOperationException("Cached game was not detected");
                DisposeDetectedGame(game);
            });
            processRetuneValid = ValidateDetectedProcessRetuning(assembly, helper, helperPath);
            return p95;
        }
        finally
        {
            if (helper != null)
            {
                try { if (!helper.HasExited) helper.Kill(); }
                catch { }
                try { helper.WaitForExit(3000); }
                catch { }
                helper.Dispose();
            }
            try { if (File.Exists(helperPath)) File.Delete(helperPath); }
            catch { }
        }
    }

    private static bool ValidateDetectedProcessRetuning(Assembly assembly, Process helper, string helperPath)
    {
        Type stateType = assembly.GetType("GameBoostPro.BoostState", true);
        Type tunerType = assembly.GetType("GameBoostPro.SystemTuner", true);
        Type storageType = assembly.GetType("GameBoostPro.Storage", true);
        BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo apply = tunerType.GetMethod("ApplyGamePriority", methods, null,
            new Type[] { stateType, typeof(int) }, null);
        MethodInfo delete = storageType.GetMethod("DeleteState", methods);
        Process current = Process.GetCurrentProcess();
        ProcessPriorityClass originalHelperPriority = helper.PriorityClass;
        try
        {
            object state = Activator.CreateInstance(stateType, true);
            stateType.GetProperty("GamePath").SetValue(state, helperPath, null);
            stateType.GetProperty("GameProcessId").SetValue(state, current.Id, null);
            stateType.GetProperty("ProcessTuningApplied").SetValue(state, true, null);
            stateType.GetProperty("UseAboveNormalPriority").SetValue(state, true, null);
            bool applied = (bool)apply.Invoke(null, new object[] { state, helper.Id });
            helper.Refresh();
            int tunedProcessId = (int)stateType.GetProperty("GameProcessId").GetValue(state, null);
            return applied && tunedProcessId == helper.Id &&
                helper.PriorityClass == ProcessPriorityClass.AboveNormal;
        }
        finally
        {
            try { helper.PriorityClass = originalHelperPriority; }
            catch { }
            try { delete.Invoke(null, null); }
            catch { }
            current.Dispose();
        }
    }

    private static void DisposeDetectedGame(object game)
    {
        Process process = (Process)game.GetType().GetProperty("Process").GetValue(game, null);
        if (process != null) process.Dispose();
    }

    private static double Measure(string name, Action action)
    {
        List<double> timings = new List<double>();
        for (int i = 0; i < 20; i++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            action();
            watch.Stop();
            timings.Add(watch.Elapsed.TotalMilliseconds);
        }
        timings.Sort();
        double median = timings[timings.Count / 2];
        double p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
        double maximum = timings[timings.Count - 1];
        Console.WriteLine("{0}_median_ms={1:F2}", name, median);
        Console.WriteLine("{0}_p95_ms={1:F2}", name, p95);
        Console.WriteLine("{0}_max_ms={1:F2}", name, maximum);
        return p95;
    }
}
