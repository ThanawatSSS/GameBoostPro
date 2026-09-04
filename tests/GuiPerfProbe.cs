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
    private static bool processIdentityValid;
    private static bool frameTargetIdentityValid;

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
        bool boostTargetResolutionValid = ValidateBoostTargetResolution(assembly);
        bool powerPolicyValid = ValidatePowerPolicy(assembly);
        bool graphicsPolicyValid = ValidateGraphicsPolicy(assembly);
        bool frameAnalyzerValid = ValidateFrameBenchmarkAnalyzer(assembly);
        bool recoveryPolicyValid = ValidateRecoveryStatePolicy(assembly);
        Type formType = assembly.GetType("GameBoostPro.MainForm", true);
        Stopwatch startup = Stopwatch.StartNew();
        Form form = (Form)Activator.CreateInstance(formType, true);
        startup.Stop();
        Console.WriteLine("startup_ms={0:F2}", startup.Elapsed.TotalMilliseconds);
        bool frameSessionIsolationValid = ValidateFrameSessionIsolation(assembly);
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
        Console.WriteLine("process_identity_guard={0}", processIdentityValid);
        Console.WriteLine("boost_target_resolution={0}", boostTargetResolutionValid);
        Console.WriteLine("smart_power_policy={0}", powerPolicyValid);
        Console.WriteLine("graphics_capability_policy={0}", graphicsPolicyValid);
        Console.WriteLine("frame_benchmark_analyzer={0}", frameAnalyzerValid);
        Console.WriteLine("frame_session_isolation={0}", frameSessionIsolationValid);
        Console.WriteLine("frame_target_identity={0}", frameTargetIdentityValid);
        Console.WriteLine("recovery_state_policy={0}", recoveryPolicyValid);
        form.Dispose();
        return startup.Elapsed.TotalMilliseconds <= 1500.0 && monitorP95 <= 16.0 &&
            metricsP95 <= 25.0 && detectionP95 <= 25.0 && dialPaintP95 <= 16.0 &&
            cachedDetectionP95 <= 10.0 && stateCacheValid && processRetuneValid &&
            processIdentityValid && boostTargetResolutionValid && powerPolicyValid &&
            graphicsPolicyValid && frameAnalyzerValid && frameSessionIsolationValid &&
            frameTargetIdentityValid && recoveryPolicyValid ? 0 : 1;
    }

    private static bool ValidateFrameSessionIsolation(Assembly assembly)
    {
        Type formType = assembly.GetType("GameBoostPro.FrameBenchmarkForm", false);
        if (formType == null) return false;
        BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo historyKey = formType.GetField("historyKey", fields);
        if (historyKey == null) return false;
        Form first = null;
        Form second = null;
        try
        {
            first = (Form)Activator.CreateInstance(formType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new object[] { "Fixture", 42, "fixture", 100L }, null);
            second = (Form)Activator.CreateInstance(formType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new object[] { "Fixture", 42, "fixture", 101L }, null);
            return !String.Equals(Convert.ToString(historyKey.GetValue(first)),
                Convert.ToString(historyKey.GetValue(second)), StringComparison.Ordinal);
        }
        finally
        {
            if (first != null) first.Dispose();
            if (second != null) second.Dispose();
        }
    }

    private static bool ValidateRecoveryStatePolicy(Assembly assembly)
    {
        Type policyType = assembly.GetType("GameBoostPro.RecoveryStatePolicy", false);
        Type snapshotType = assembly.GetType("GameBoostPro.RegistrySnapshot", false);
        Type storageType = assembly.GetType("GameBoostPro.Storage", false);
        Type stateType = assembly.GetType("GameBoostPro.BoostState", false);
        if (policyType == null || snapshotType == null || storageType == null || stateType == null) return false;
        BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo validGuid = policyType.GetMethod("IsValidPowerGuid", methods);
        MethodInfo allowedSnapshot = policyType.GetMethod("IsAllowedRegistrySnapshot", methods);
        MethodInfo sanitize = policyType.GetMethod("SanitizeMigratedState", methods);
        if (validGuid == null || allowedSnapshot == null || sanitize == null) return false;

        object safe = Activator.CreateInstance(snapshotType, true);
        snapshotType.GetProperty("SubKey").SetValue(safe, @"Software\Microsoft\GameBar", null);
        snapshotType.GetProperty("Name").SetValue(safe, "AutoGameModeEnabled", null);
        snapshotType.GetProperty("Exists").SetValue(safe, true, null);
        snapshotType.GetProperty("Kind").SetValue(safe, 4, null);
        snapshotType.GetProperty("Value").SetValue(safe, "1", null);
        object unsafeSnapshot = Activator.CreateInstance(snapshotType, true);
        snapshotType.GetProperty("SubKey").SetValue(unsafeSnapshot, @"Software\Microsoft\Windows\Run", null);
        snapshotType.GetProperty("Name").SetValue(unsafeSnapshot, "Injected", null);

        Type listType = typeof(List<>).MakeGenericType(snapshotType);
        object snapshots = Activator.CreateInstance(listType);
        listType.GetMethod("Add").Invoke(snapshots, new object[] { safe });
        listType.GetMethod("Add").Invoke(snapshots, new object[] { unsafeSnapshot });
        object migrated = Activator.CreateInstance(stateType, true);
        stateType.GetProperty("PreviousPowerGuid").SetValue(migrated,
            "e9a42b02-d5df-448d-aa00-03f14749eb61", null);
        stateType.GetProperty("TargetPowerGuid").SetValue(migrated, "invalid", null);
        stateType.GetProperty("GameProcessId").SetValue(migrated, 42, null);
        stateType.GetProperty("ProcessTuningApplied").SetValue(migrated, true, null);
        stateType.GetProperty("Registry").SetValue(migrated, snapshots, null);
        migrated = sanitize.Invoke(null, new object[] { migrated, "S-1-5-21-test" });
        object migratedSnapshots = stateType.GetProperty("Registry").GetValue(migrated, null);
        bool migrationSanitized = (int)listType.GetProperty("Count").GetValue(migratedSnapshots, null) == 1 &&
            (int)stateType.GetProperty("GameProcessId").GetValue(migrated, null) == 0 &&
            !(bool)stateType.GetProperty("ProcessTuningApplied").GetValue(migrated, null) &&
            Convert.ToString(stateType.GetProperty("TargetPowerGuid").GetValue(migrated, null)) == "" &&
            Convert.ToString(stateType.GetProperty("OwnerSid").GetValue(migrated, null)) == "S-1-5-21-test";

        bool isolatedStore = !(bool)storageType.GetProperty("UsesProtectedStateStore",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null);
        return (bool)validGuid.Invoke(null, new object[] { "e9a42b02-d5df-448d-aa00-03f14749eb61" }) &&
            !(bool)validGuid.Invoke(null, new object[] { "not-a-guid --extra" }) &&
            (bool)allowedSnapshot.Invoke(null, new object[] { safe }) &&
            !(bool)allowedSnapshot.Invoke(null, new object[] { unsafeSnapshot }) &&
            migrationSanitized && isolatedStore;
    }

    private static bool ValidateBoostTargetResolution(Assembly assembly)
    {
        Type resolverType = assembly.GetType("GameBoostPro.BoostTargetResolver", false);
        Type detectedType = assembly.GetType("GameBoostPro.DetectedGame", false);
        if (resolverType == null || detectedType == null) return false;
        BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo resolve = resolverType.GetMethod("ResolveGamePath", methods);
        if (resolve == null) return false;

        object inaccessibleDetectedGame = Activator.CreateInstance(detectedType, true);
        detectedType.GetProperty("ExePath").SetValue(inaccessibleDetectedGame, "", null);
        object knownDetectedGame = Activator.CreateInstance(detectedType, true);
        detectedType.GetProperty("ExePath").SetValue(knownDetectedGame, @"C:\Games\Detected.exe", null);
        string configured = @"D:\Games\Selected.exe";
        string inaccessibleResult = Convert.ToString(resolve.Invoke(null,
            new object[] { inaccessibleDetectedGame, configured }));
        string knownResult = Convert.ToString(resolve.Invoke(null,
            new object[] { knownDetectedGame, configured }));
        string manualResult = Convert.ToString(resolve.Invoke(null, new object[] { null, configured }));
        return inaccessibleResult == "" && knownResult == @"C:\Games\Detected.exe" &&
            manualResult == configured;
    }

    private static bool ValidatePowerPolicy(Assembly assembly)
    {
        Type policyType = assembly.GetType("GameBoostPro.PowerPlanPolicy", false);
        Type platformDetectorType = assembly.GetType("GameBoostPro.PlatformDetector", false);
        if (policyType == null || platformDetectorType == null) return false;
        BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo evaluate = platformDetectorType.GetMethod("Evaluate", methods);
        MethodInfo keepCurrent = policyType.GetMethod("ShouldKeepCurrent", methods);
        if (evaluate == null || keepCurrent == null) return false;
        object acer = evaluate.Invoke(null, new object[] { "Acer", "Nitro", true, true });
        object desktop = evaluate.Invoke(null, new object[] { "ASUS", "Desktop", false, false });
        bool acerSmart = (bool)keepCurrent.Invoke(null, new object[] { "Smart", acer });
        bool desktopSmart = (bool)keepCurrent.Invoke(null, new object[] { "Smart", desktop });
        bool desktopKeep = (bool)keepCurrent.Invoke(null, new object[] { "KeepCurrent", desktop });
        bool acerUltimate = (bool)keepCurrent.Invoke(null, new object[] { "Ultimate", acer });
        return acerSmart && !desktopSmart && desktopKeep && !acerUltimate;
    }

    private static bool ValidateGraphicsPolicy(Assembly assembly)
    {
        Type advisorType = assembly.GetType("GameBoostPro.GraphicsAdvisor", false);
        if (advisorType == null) return false;
        BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo classify = advisorType.GetMethod("ClassifyGpu", methods);
        MethodInfo nisEligibility = advisorType.GetMethod("GetNisEligibility", methods);
        if (classify == null || nisEligibility == null) return false;

        object rtx4050 = classify.Invoke(null, new object[] { "NVIDIA GeForce RTX 4050 Laptop GPU" });
        object rtx5090 = classify.Invoke(null, new object[] { "NVIDIA GeForce RTX 5090" });
        object gtx1660 = classify.Invoke(null, new object[] { "NVIDIA GeForce GTX 1660" });
        Type capabilityType = rtx4050.GetType();
        Func<object, string, bool> read = delegate(object value, string property)
        {
            return (bool)capabilityType.GetProperty(property).GetValue(value, null);
        };
        string activeNis = Convert.ToString(nisEligibility.Invoke(null,
            new object[] { true, "Active" }));
        string inactiveNis = Convert.ToString(nisEligibility.Invoke(null,
            new object[] { true, "Inactive" }));

        return read(rtx4050, "SupportsDlssSuperResolution") &&
            read(rtx4050, "SupportsFrameGeneration") &&
            !read(rtx4050, "SupportsMultiFrameGeneration") &&
            read(rtx4050, "SupportsSmoothMotion") &&
            read(rtx5090, "SupportsMultiFrameGeneration") &&
            !read(gtx1660, "SupportsDlssSuperResolution") &&
            activeNis == "Eligible" && inactiveNis == "RouteBlocked";
    }

    private static bool ValidateFrameBenchmarkAnalyzer(Assembly assembly)
    {
        Type analyzerType = assembly.GetType("GameBoostPro.FrameBenchmarkAnalyzer", false);
        if (analyzerType == null) return false;
        BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo analyze = analyzerType.GetMethod("AnalyzeCsv", methods);
        if (analyze == null) return false;
        string path = Path.Combine(Path.GetTempPath(), "gbp-frame-fixture-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            List<string> rows = new List<string>();
            rows.Add("Application,ProcessID,PresentMode,MsBetweenPresents,MsBetweenDisplayChange");
            for (int i = 0; i < 100; i++)
                rows.Add("fixture.exe,42,Hardware: Independent Flip," +
                    (i == 99 ? "20.0" : "10.0") + "," + (i == 99 ? "20.0" : "10.0"));
            File.WriteAllLines(path, rows.ToArray());
            object result = analyze.Invoke(null, new object[] { path, "Baseline", "Fixture" });
            Type resultType = result.GetType();
            int frames = (int)resultType.GetProperty("FrameCount").GetValue(result, null);
            double averageFps = (double)resultType.GetProperty("AverageFps").GetValue(result, null);
            double onePercentLow = (double)resultType.GetProperty("OnePercentLowFps").GetValue(result, null);
            double p95 = (double)resultType.GetProperty("P95FrameTimeMs").GetValue(result, null);
            string presentMode = Convert.ToString(resultType.GetProperty("PresentMode").GetValue(result, null));
            return frames == 100 && averageFps > 98.0 && averageFps < 100.0 &&
                onePercentLow > 49.0 && onePercentLow < 51.0 && p95 == 10.0 &&
                presentMode == "Hardware: Independent Flip";
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
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
            frameTargetIdentityValid = ValidateFrameTargetIdentity(assembly, helper);
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

    private static bool ValidateFrameTargetIdentity(Assembly assembly, Process helper)
    {
        Type runnerType = assembly.GetType("GameBoostPro.PresentMonRunner", false);
        if (runnerType == null) return false;
        MethodInfo validate = runnerType.GetMethod("ValidateTargetProcess",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (validate == null) return false;
        long startTicks = helper.StartTime.ToUniversalTime().Ticks;
        try
        {
            validate.Invoke(null, new object[] { helper.Id, helper.ProcessName, startTicks });
        }
        catch { return false; }

        try
        {
            validate.Invoke(null, new object[] { helper.Id, helper.ProcessName, startTicks + 1 });
            return false;
        }
        catch (TargetInvocationException ex)
        {
            return ex.GetBaseException() is InvalidOperationException;
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
        MethodInfo identityMatches = tunerType.GetMethod("IsStoredProcessMatch", methods);
        MethodInfo needsTuning = tunerType.GetMethod("NeedsProcessTuning", methods);
        MethodInfo restoreScheduling = tunerType.GetMethod("RestoreStoredProcessScheduling",
            BindingFlags.Static | BindingFlags.NonPublic);
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
            bool attempted = (bool)stateType.GetProperty("ProcessTuningAttempted").GetValue(state, null);
            string processName = Convert.ToString(stateType.GetProperty("GameProcessName").GetValue(state, null));
            long startTicks = (long)stateType.GetProperty("GameProcessStartTimeUtcTicks").GetValue(state, null);
            string status = Convert.ToString(stateType.GetProperty("ProcessTuningStatus").GetValue(state, null));
            bool exactIdentity = identityMatches != null &&
                (bool)identityMatches.Invoke(null, new object[] { state, helper });
            bool sameProcessNeedsRetry = needsTuning == null ||
                (bool)needsTuning.Invoke(null, new object[] { state, helper });

            stateType.GetProperty("GameProcessStartTimeUtcTicks").SetValue(state, startTicks + 1, null);
            bool staleIdentityRejected = identityMatches != null &&
                !(bool)identityMatches.Invoke(null, new object[] { state, helper });
            bool staleRestoreSkipped = restoreScheduling != null &&
                !(bool)restoreScheduling.Invoke(null, new object[] { state });
            helper.Refresh();
            bool staleRestoreDidNotTouchProcess = helper.PriorityClass == ProcessPriorityClass.AboveNormal;
            stateType.GetProperty("GameProcessStartTimeUtcTicks").SetValue(state, startTicks, null);
            bool exactRestoreApplied = restoreScheduling != null &&
                (bool)restoreScheduling.Invoke(null, new object[] { state });
            helper.Refresh();
            bool exactRestoreReturnedOriginal = helper.PriorityClass == originalHelperPriority;

            processIdentityValid = attempted && processName == helper.ProcessName &&
                startTicks == helper.StartTime.ToUniversalTime().Ticks && status == "Applied" &&
                exactIdentity && staleIdentityRejected && staleRestoreSkipped &&
                staleRestoreDidNotTouchProcess && exactRestoreApplied && exactRestoreReturnedOriginal &&
                !sameProcessNeedsRetry;
            return applied && tunedProcessId == helper.Id && processIdentityValid;
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
