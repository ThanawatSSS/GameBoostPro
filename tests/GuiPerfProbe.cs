using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

internal static class GuiPerfProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        Assembly assembly = Assembly.LoadFile(args[0]);
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

        Type detectorType = assembly.GetType("GameBoostPro.GameDetector", true);
        MethodInfo detect = detectorType.GetMethod("FindRunningGame",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        double detectionP95 = Measure("game_detection", delegate
        {
            detect.Invoke(null, new object[] { "" });
        });

        Console.WriteLine("ui_budget_ms=16.00");
        Console.WriteLine("background_budget_ms=25.00");
        Console.WriteLine("startup_budget_ms=1500.00");
        form.Dispose();
        return startup.Elapsed.TotalMilliseconds <= 1500.0 && monitorP95 <= 16.0 &&
            metricsP95 <= 25.0 && detectionP95 <= 25.0 ? 0 : 1;
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
