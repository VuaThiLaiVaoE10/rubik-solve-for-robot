using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Rubik Solve For Robot")]
[assembly: AssemblyDescription("Rubik 3x3 2D Scramble Vision to 4-Pusher (R, L, F, B) USB Arduino Nano Controller")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("RubikSolveForRobot")]
[assembly: AssemblyProduct("Rubik Solve For Robot Desktop")]
[assembly: AssemblyCopyright("Copyright (c) 2026 RubikSolveForRobot")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("7a4e9d12-5c8b-4f31-9a2e-6d8c1b3e5f70")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace RubikRobotDesktop
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--self-test", StringComparison.OrdinalIgnoreCase))
                    {
                        return RunSelfTest();
                    }
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static int RunSelfTest()
        {
            try
            {
                Console.WriteLine("=== RUBIK ROBOT DESKTOP: 20-ITERATION FULL-SYSTEM VERIFICATION ===");
                System.Diagnostics.Stopwatch totalSw = System.Diagnostics.Stopwatch.StartNew();

                // 0. Kiểm tra Validator (Positive & Negative cases)
                string errMsg;
                if (!RubikVision2D.ValidateState54(RubikVision2D.SOLVED_STATE, out errMsg))
                {
                    Console.Error.WriteLine("[FAIL] SOLVED_STATE validation failed: " + errMsg);
                    return 1;
                }
                if (RubikVision2D.ValidateState54("INVALID_SHORT", out errMsg))
                {
                    Console.Error.WriteLine("[FAIL] Validator accepted short string.");
                    return 1;
                }
                char[] badCenter = RubikVision2D.SOLVED_STATE.ToCharArray();
                badCenter[4] = 'R';
                if (RubikVision2D.ValidateState54(new string(badCenter), out errMsg))
                {
                    Console.Error.WriteLine("[FAIL] Validator accepted invalid center piece.");
                    return 1;
                }

                Random rng = new Random(20261119);
                int totalMovesSolved = 0;
                int totalPusherSteps = 0;
                int totalPhysicalPushes = 0;

                for (int iter = 1; iter <= 20; iter++)
                {
                    System.Diagnostics.Stopwatch iterSw = System.Diagnostics.Stopwatch.StartNew();
                    int scrambleLen = 10 + (iter % 12); // Độ sâu trộn từ 11 đến 21 bước
                    List<string> scrambleMoves;
                    string scrambledState = RubikSolver3x3.GenerateScramble(scrambleLen, rng, out scrambleMoves);

                    // Xóa cache ở các lượt lẻ để ép Kociemba 2-Phase IDA* giải mù 100% từ chuỗi 54 ô màu
                    bool blindKociemba = (iter % 2 == 1);
                    if (blindKociemba)
                    {
                        RubikSolver3x3.KnownScrambles.Clear();
                    }

                    // 1. Kiểm thử Vision 2D: Render ra Bitmap -> Xuất luồng PNG thật -> Đọc lại -> Quét 54 ô màu
                    int bmpW = 400 + (iter * 16);
                    int bmpH = 300 + (iter * 12);
                    string parsedState;
                    using (Bitmap rendered = RubikVision2D.RenderScramble2DBitmap(scrambledState, bmpW, bmpH))
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        rendered.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        using (Bitmap reloaded = new Bitmap(ms))
                        {
                            parsedState = RubikVision2D.ParseScramble2DBitmap(reloaded);
                        }
                    }

                    if (!string.Equals(parsedState, scrambledState, StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Vision 2D PNG round-trip mismatch!", iter));
                        return 1;
                    }
                    if (!RubikVision2D.ValidateState54(parsedState, out errMsg))
                    {
                        Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Parsed state invalid: {1}", iter, errMsg));
                        return 1;
                    }

                    // 2. Kiểm thử Bộ giải 3x3 (Kociemba Two-Phase IDA* & Hint Solver)
                    List<string> solutionMoves = RubikSolver3x3.Solve3x3(parsedState);
                    if (solutionMoves == null || solutionMoves.Count == 0)
                    {
                        Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Solver returned empty moves.", iter));
                        return 1;
                    }
                    string afterStandardSolve = RubikSolver3x3.ApplyMoves(parsedState, solutionMoves);
                    if (!RubikSolver3x3.IsSolved(afterStandardSolve, false))
                    {
                        Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Standard 6-face solution did not reach exact SOLVED_STATE.", iter));
                        return 1;
                    }

                    // 3. Kiểm thử Trình biên dịch 4 Thanh Đẩy {R, L, F, B} & Giao thức Mạch Arduino Nano (.ino.hex)
                    CompiledRLBFResult compiled = RLBFCompiler4Pusher.CompileTo4Pusher(solutionMoves, parsedState);
                    if (compiled == null || !compiled.VerifiedSolved || compiled.Steps == null || compiled.Steps.Count == 0)
                    {
                        Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] RLBFCompiler4Pusher verification failed.", iter));
                        return 1;
                    }

                    int expectedTimeMs = 0;
                    int expectedPushes = 0;
                    for (int s = 0; s < compiled.Steps.Count; s++)
                    {
                        PusherStep step = compiled.Steps[s];
                        if (step.Face != 'R' && step.Face != 'L' && step.Face != 'F' && step.Face != 'B')
                        {
                            Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Non-RLFB face leaked: {1}", iter, step.Face));
                            return 1;
                        }
                        string expectedPin = RobotHardware.GetPinForFace(step.Face);
                        if (!string.Equals(step.PinName, expectedPin, StringComparison.Ordinal))
                        {
                            Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Pin mismatch for {1}: got {2}, expected {3}", iter, step.Face, step.PinName, expectedPin));
                            return 1;
                        }
                        if (step.PushCount < 1 || step.PushCount > 3 || step.DurationMs != step.PushCount * RobotHardware.PUSH_CYCLE_MS)
                        {
                            Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Invalid push count or duration on step #{1}", iter, s + 1));
                            return 1;
                        }
                        if (step.StartMs != expectedTimeMs || step.EndMs != expectedTimeMs + step.DurationMs)
                        {
                            Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Timeline discontinuity at step #{1}", iter, s + 1));
                            return 1;
                        }
                        expectedTimeMs = step.EndMs;
                        expectedPushes += step.PushCount;
                    }

                    if (compiled.TotalDurationMs != expectedTimeMs || compiled.TotalPushes != expectedPushes)
                    {
                        Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] Total duration/pushes mismatch.", iter));
                        return 1;
                    }

                    // 4. Kiểm thử Giao diện Desktop GDI+ Rendering (MainForm, CubeNetPanel, WaveformPanel)
                    using (MainForm form = new MainForm())
                    using (Bitmap uiSnap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(uiSnap, new Rectangle(0, 0, form.Width, form.Height));
                        Color samplePixel = uiSnap.GetPixel(50, 20);
                        if (samplePixel.A == 0)
                        {
                            Console.Error.WriteLine(string.Format("[FAIL][Iter #{0}] MainForm GDI+ DrawToBitmap rendered transparent pixel.", iter));
                            return 1;
                        }
                    }

                    iterSw.Stop();
                    totalMovesSolved += solutionMoves.Count;
                    totalPusherSteps += compiled.Steps.Count;
                    totalPhysicalPushes += compiled.TotalPushes;

                    Console.WriteLine(string.Format(
                        "[PASS #{0:00}/20] Mode={1,-13} | Scramble={2,2} | Vision={3}x{4} (54/54) | Solve={5,2} moves | RLBF={6,2} steps ({7,2} pushes, {8,5}ms) | UI=OK | {9,3}ms",
                        iter,
                        blindKociemba ? "BlindKociemba" : "CachedFast",
                        scrambleLen,
                        bmpW, bmpH,
                        solutionMoves.Count,
                        compiled.Steps.Count,
                        compiled.TotalPushes,
                        compiled.TotalDurationMs,
                        iterSw.ElapsedMilliseconds));
                }

                totalSw.Stop();
                Console.WriteLine("----------------------------------------------------------------------------------------------------");
                Console.WriteLine(string.Format(
                    "[SUMMARY] 20/20 FULL-SYSTEM TESTS PASSED (100%) in {0} ms | Avg Solve: {1:0.0} moves | Avg Pusher Steps: {2:0.0} ({3:0.0} pushes)",
                    totalSw.ElapsedMilliseconds,
                    totalMovesSolved / 20.0,
                    totalPusherSteps / 20.0,
                    totalPhysicalPushes / 20.0));
                Console.WriteLine("[SELF-TEST PASS]");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Exception during self-test: " + ex.ToString());
                return 1;
            }
        }
    }
}
