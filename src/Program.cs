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
                Console.WriteLine("=== RUBIK ROBOT DESKTOP HEADLESS SELF-TEST ===");

                string errMsg;
                bool solvedValid = RubikVision2D.ValidateState54(RubikVision2D.SOLVED_STATE, out errMsg);
                if (!solvedValid)
                {
                    Console.Error.WriteLine("[FAIL] SOLVED_STATE validation failed: " + errMsg);
                    return 1;
                }
                Console.WriteLine("[1/5] SOLVED_STATE validated OK.");

                Random rng = new Random(42);
                List<string> scrambleMoves;
                string scrambledState = RubikSolver3x3.GenerateScramble(18, rng, out scrambleMoves);
                bool scrambleValid = RubikVision2D.ValidateState54(scrambledState, out errMsg);
                if (!scrambleValid)
                {
                    Console.Error.WriteLine("[FAIL] Scrambled state invalid: " + errMsg);
                    return 1;
                }
                Console.WriteLine(string.Format("[2/5] Scramble generated ({0} moves): {1}", scrambleMoves.Count, string.Join(" ", scrambleMoves.ToArray())));

                string parsedState;
                using (Bitmap bmp = RubikVision2D.RenderScramble2DBitmap(scrambledState, 480, 360))
                {
                    parsedState = RubikVision2D.ParseScramble2DBitmap(bmp);
                }
                if (!string.Equals(parsedState, scrambledState, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(string.Format("[FAIL] Vision round-trip mismatch.\nExpected: {0}\nParsed:   {1}", scrambledState, parsedState));
                    return 1;
                }
                Console.WriteLine("[3/5] RubikVision2D render -> parse round-trip matched 54/54 stickers.");

                List<string> solutionMoves = RubikSolver3x3.Solve3x3(parsedState);
                if (solutionMoves == null || solutionMoves.Count == 0)
                {
                    Console.Error.WriteLine("[FAIL] Solver returned empty moves for scrambled state.");
                    return 1;
                }
                string afterSolve = RubikSolver3x3.ApplyMoves(parsedState, solutionMoves);
                if (!RubikSolver3x3.IsSolved(afterSolve, true))
                {
                    Console.Error.WriteLine("[FAIL] Solver moves did not reach solved state.");
                    return 1;
                }
                Console.WriteLine(string.Format("[4/5] RubikSolver3x3 solved in {0} moves: {1}", solutionMoves.Count, string.Join(" ", solutionMoves.ToArray())));

                CompiledRLBFResult compiled = RLBFCompiler4Pusher.CompileTo4Pusher(solutionMoves, parsedState);
                if (compiled == null || !compiled.VerifiedSolved)
                {
                    Console.Error.WriteLine("[FAIL] RLBFCompiler4Pusher verification failed.");
                    return 1;
                }

                for (int i = 0; i < compiled.Steps.Count; i++)
                {
                    PusherStep step = compiled.Steps[i];
                    if (step.Face != 'R' && step.Face != 'L' && step.Face != 'F' && step.Face != 'B')
                    {
                        Console.Error.WriteLine("[FAIL] Invalid pusher face in step: " + step.Face);
                        return 1;
                    }
                    if (step.PushCount < 1 || step.PushCount > 3)
                    {
                        Console.Error.WriteLine("[FAIL] Invalid push count: " + step.PushCount);
                        return 1;
                    }
                }

                Console.WriteLine(string.Format("[5/5] RLBFCompiler4Pusher verified: {0} steps, {1} pushes, {2} ms. Payload: {3}",
                    compiled.Steps.Count, compiled.TotalPushes, compiled.TotalDurationMs, compiled.SerialPayload));
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
