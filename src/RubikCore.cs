using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace RubikRobotDesktop
{
    /// <summary>
    /// Hardware pinout and timing constants matching reverse-engineered RobotGiaiRubik.ino.hex.
    /// </summary>
    public static class RobotHardware
    {
        public const string PIN_SERVO_R = "D5";
        public const string PIN_SERVO_L = "D6";
        public const string PIN_SERVO_F = "D9";
        public const string PIN_SERVO_B = "D10";
        public const string PIN_SERVO_U = "D11";
        public const string PIN_SERVO_D = "D12";
        public const string PIN_CLAMP_UD = "D7"; // Trục 1 (A1): Kẹp Trắng U - Vàng D -> xoay R, L, F, B
        public const string PIN_CLAMP_RL = "D8"; // Trục 2 (A2): Kẹp Đỏ R - Cam L -> xoay U, D, F, B
        public const string PIN_BTN_START = "D2";
        public const string PIN_BTN_STOP = "D3";

        public const int SERVO_INDEX_R = 0;
        public const int SERVO_INDEX_L = 1;
        public const int SERVO_INDEX_F = 2;
        public const int SERVO_INDEX_B = 3;
        public const int SERVO_INDEX_U = 4;
        public const int SERVO_INDEX_D = 5;

        public const int SERVO_PUSH_ANGLE = 75;
        public const int SERVO_REST_ANGLE = 0;
        public const int HOLD_TIME_MS = 150;
        public const int RETURN_TIME_MS = 150;
        public const int PUSH_CYCLE_MS = 300;
        public const int AXIS_SWITCH_MS = 200;

        public const int SERIAL_BAUD_RATE = 9600;
        public const string TEST_SEQUENCE = "A1 R L2 F3 B A2 U D2";
        public const string EMERGENCY_STOP_CMD = "!";

        public static readonly string[] SUPPORTED_TOKENS = new string[]
        {
            "A1", "A2",
            "R", "R2", "R3",
            "L", "L2", "L3",
            "F", "F2", "F3",
            "B", "B2", "B3",
            "U", "U2", "U3",
            "D", "D2", "D3"
        };

        public static string GetPinForFace(char face)
        {
            switch (char.ToUpper(face))
            {
                case 'R': return PIN_SERVO_R;
                case 'L': return PIN_SERVO_L;
                case 'F': return PIN_SERVO_F;
                case 'B': return PIN_SERVO_B;
                case 'U': return PIN_SERVO_U;
                case 'D': return PIN_SERVO_D;
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// Represents a single 4-pusher hardware command step and its timing window.
    /// </summary>
    public class PusherStep
    {
        public int StepIndex { get; set; }
        public string OriginalMove { get; set; }
        public string PreRotation { get; set; }
        public char Face { get; set; }
        public string Token { get; set; }
        public string PinName { get; set; }
        public int PushCount { get; set; }
        public int DurationMs { get; set; }
        public int StartMs { get; set; }
        public int EndMs { get; set; }

        public PusherStep()
        {
            OriginalMove = string.Empty;
            PreRotation = string.Empty;
            Token = string.Empty;
            PinName = string.Empty;
        }
    }

    /// <summary>
    /// Compiled 4-pusher {R, L, F, B} sequence, serial payload, and verification status.
    /// </summary>
    public class CompiledRLBFResult
    {
        public List<string> OriginalMoves { get; set; }
        public string OriginalMovesText { get; set; }
        public List<string> PusherTokens { get; set; }
        public string SerialPayload { get; set; }
        public List<PusherStep> Steps { get; set; }
        public int TotalPushes { get; set; }
        public int TotalDurationMs { get; set; }
        public bool VerifiedSolved { get; set; }

        public CompiledRLBFResult()
        {
            OriginalMoves = new List<string>();
            OriginalMovesText = string.Empty;
            PusherTokens = new List<string>();
            SerialPayload = string.Empty;
            Steps = new List<PusherStep>();
        }
    }

    /// <summary>
    /// 2D Cross Net rendering, color classification (CIELAB DeltaE), validation, and bitmap parsing.
    /// </summary>
    public static class RubikVision2D
    {
        public const string SOLVED_STATE = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";
        private static readonly char[] FACE_ORDER = new char[] { 'U', 'R', 'F', 'D', 'L', 'B' };
        private static readonly int[] CENTER_INDICES = new int[] { 4, 13, 22, 31, 40, 49 };

        // Cross Net layout positions (col, row) in a 4x3 macro grid
        // U(1,0), R(2,1), F(1,1), D(1,2), L(0,1), B(3,1)
        private static readonly int[] FACE_GRID_COL = new int[] { 1, 2, 1, 1, 0, 3 };
        private static readonly int[] FACE_GRID_ROW = new int[] { 0, 1, 1, 2, 1, 1 };

        public static Color GetFaceColor(char face)
        {
            switch (char.ToUpper(face))
            {
                case 'U': return Color.FromArgb(255, 255, 255);
                case 'R': return Color.FromArgb(239, 68, 68);
                case 'F': return Color.FromArgb(34, 197, 94);
                case 'D': return Color.FromArgb(234, 179, 8);
                case 'L': return Color.FromArgb(249, 115, 22);
                case 'B': return Color.FromArgb(59, 130, 246);
                default: return Color.Gray;
            }
        }

        public static bool ValidateState54(string state54, out string errorMsg)
        {
            if (string.IsNullOrEmpty(state54) || state54.Length != 54)
            {
                int len = state54 == null ? 0 : state54.Length;
                errorMsg = string.Format("Chieu dai trang thai phai bang 54 (hien tai: {0}).", len);
                return false;
            }

            int[] counts = new int[6];
            for (int i = 0; i < 54; i++)
            {
                int fIdx = FaceCharToIndex(state54[i]);
                if (fIdx < 0)
                {
                    errorMsg = string.Format("Ky tu khong hop le '{0}' tai vi tri {1}.", state54[i], i);
                    return false;
                }
                counts[fIdx]++;
            }

            for (int f = 0; f < 6; f++)
            {
                if (counts[f] != 9)
                {
                    errorMsg = string.Format("Mat '{0}' phai co dung 9 o (hien tai: {1}).", FACE_ORDER[f], counts[f]);
                    return false;
                }
            }

            for (int f = 0; f < 6; f++)
            {
                int centerIdx = CENTER_INDICES[f];
                if (state54[centerIdx] != FACE_ORDER[f])
                {
                    errorMsg = string.Format("Tam mat {0} (o #{1}) phai la '{0}', nhung dang la '{2}'.",
                        FACE_ORDER[f], centerIdx, state54[centerIdx]);
                    return false;
                }
            }

            errorMsg = string.Empty;
            return true;
        }

        public static Bitmap RenderScramble2DBitmap(string state54, int width, int height)
        {
            if (width < 64) width = 480;
            if (height < 48) height = 360;
            if (string.IsNullOrEmpty(state54) || state54.Length != 54)
            {
                state54 = SOLVED_STATE;
            }

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(15, 23, 42));

                int pad = Math.Max(8, Math.Min(width, height) / 24);
                float availW = width - 2 * pad;
                float availH = height - 2 * pad;

                // Preserve 4:3 cross-net aspect ratio centered in the bitmap
                float netW = availW;
                float netH = netW * 3.0f / 4.0f;
                if (netH > availH)
                {
                    netH = availH;
                    netW = netH * 4.0f / 3.0f;
                }

                float originX = (width - netW) * 0.5f;
                float originY = (height - netH) * 0.5f;
                float faceW = netW / 4.0f;
                float faceH = netH / 3.0f;
                float cellW = faceW / 3.0f;
                float cellH = faceH / 3.0f;
                float border = Math.Max(1.5f, Math.Min(cellW, cellH) * 0.08f);

                using (Brush blackBrush = new SolidBrush(Color.FromArgb(10, 14, 23)))
                using (Font labelFont = new Font("Segoe UI", Math.Max(7.0f, Math.Min(cellW, cellH) * 0.24f), FontStyle.Bold))
                {
                    for (int f = 0; f < 6; f++)
                    {
                        float fx = originX + FACE_GRID_COL[f] * faceW;
                        float fy = originY + FACE_GRID_ROW[f] * faceH;

                        g.FillRectangle(blackBrush, fx, fy, faceW, faceH);

                        for (int r = 0; r < 3; r++)
                        {
                            for (int c = 0; c < 3; c++)
                            {
                                int idx = f * 9 + r * 3 + c;
                                char sticker = state54[idx];
                                Color col = GetFaceColor(sticker);

                                float sx = fx + c * cellW + border * 0.5f;
                                float sy = fy + r * cellH + border * 0.5f;
                                float sw = Math.Max(1.0f, cellW - border);
                                float sh = Math.Max(1.0f, cellH - border);

                                using (Brush stickerBrush = new SolidBrush(col))
                                {
                                    g.FillRectangle(stickerBrush, sx, sy, sw, sh);
                                }

                                // Place face label at top-left corner of center cell so center 40% remains pure color
                                if (r == 1 && c == 1)
                                {
                                    Color txtColor = (sticker == 'U' || sticker == 'D')
                                        ? Color.FromArgb(180, 15, 23, 42)
                                        : Color.FromArgb(220, 255, 255, 255);
                                    using (Brush txtBrush = new SolidBrush(txtColor))
                                    {
                                        g.DrawString(FACE_ORDER[f].ToString(), labelFont, txtBrush, sx + 2.0f, sy + 1.0f);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        public static string ParseScramble2DBitmap(Bitmap bmp)
        {
            if (bmp == null) throw new ArgumentNullException("bmp");

            int w = bmp.Width;
            int h = bmp.Height;

            BitmapData bd = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = bd.Stride;
                int bytes = Math.Abs(stride) * h;
                byte[] rgbData = new byte[bytes];
                Marshal.Copy(bd.Scan0, rgbData, 0, bytes);

                // Estimate background color from the 4 corners
                int bgR = 0, bgG = 0, bgB = 0;
                int[] cornerX = new int[] { 1, Math.Max(0, w - 2), 1, Math.Max(0, w - 2) };
                int[] cornerY = new int[] { 1, 1, Math.Max(0, h - 2), Math.Max(0, h - 2) };
                for (int k = 0; k < 4; k++)
                {
                    int off = cornerY[k] * stride + cornerX[k] * 3;
                    bgB += rgbData[off];
                    bgG += rgbData[off + 1];
                    bgR += rgbData[off + 2];
                }
                bgR /= 4;
                bgG /= 4;
                bgB /= 4;

                // Auto-detect active bounding box of the 4x3 cross net
                int minX = w, minY = h, maxX = -1, maxY = -1;
                const int bgThreshSq = 28 * 28;
                for (int y = 0; y < h; y++)
                {
                    int rowOff = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = rowOff + x * 3;
                        int db = rgbData[off] - bgB;
                        int dg = rgbData[off + 1] - bgG;
                        int dr = rgbData[off + 2] - bgR;
                        if (dr * dr + dg * dg + db * db > bgThreshSq)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                float bx, by, bw, bh;
                if (maxX - minX >= 24 && maxY - minY >= 18)
                {
                    bx = minX;
                    by = minY;
                    bw = (maxX - minX) + 1;
                    bh = (maxY - minY) + 1;
                }
                else
                {
                    bx = 0;
                    by = 0;
                    bw = w;
                    bh = h;
                }

                float faceW = bw / 4.0f;
                float faceH = bh / 3.0f;
                float cellW = faceW / 3.0f;
                float cellH = faceH / 3.0f;

                double[][] stickerLabs = new double[54][];
                for (int f = 0; f < 6; f++)
                {
                    float fx = bx + FACE_GRID_COL[f] * faceW;
                    float fy = by + FACE_GRID_ROW[f] * faceH;

                    for (int r = 0; r < 3; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            // Sample the center 40% of each sticker cell ([0.30, 0.70])
                            int x0 = Clamp((int)Math.Floor(fx + (c + 0.30f) * cellW), 0, w - 1);
                            int x1 = Clamp((int)Math.Ceiling(fx + (c + 0.70f) * cellW), x0 + 1, w);
                            int y0 = Clamp((int)Math.Floor(fy + (r + 0.30f) * cellH), 0, h - 1);
                            int y1 = Clamp((int)Math.Ceiling(fy + (r + 0.70f) * cellH), y0 + 1, h);

                            long sumR = 0, sumG = 0, sumB = 0;
                            int count = 0;
                            for (int py = y0; py < y1; py++)
                            {
                                int rowOff = py * stride;
                                for (int px = x0; px < x1; px++)
                                {
                                    int off = rowOff + px * 3;
                                    sumB += rgbData[off];
                                    sumG += rgbData[off + 1];
                                    sumR += rgbData[off + 2];
                                    count++;
                                }
                            }

                            double avgR = count > 0 ? (double)sumR / count : 0.0;
                            double avgG = count > 0 ? (double)sumG / count : 0.0;
                            double avgB = count > 0 ? (double)sumB / count : 0.0;
                            stickerLabs[f * 9 + r * 3 + c] = RgbToLab(avgR, avgG, avgB);
                        }
                    }
                }

                // Use the 6 center cells as perceptual reference if mutually distinct, else fallback to standard palette
                double[][] refLabs = new double[6][];
                for (int f = 0; f < 6; f++)
                {
                    refLabs[f] = stickerLabs[CENTER_INDICES[f]];
                }

                bool centersDistinct = true;
                for (int i = 0; i < 6 && centersDistinct; i++)
                {
                    for (int j = i + 1; j < 6; j++)
                    {
                        if (DeltaE(refLabs[i], refLabs[j]) < 12.0)
                        {
                            centersDistinct = false;
                            break;
                        }
                    }
                }

                if (!centersDistinct)
                {
                    for (int f = 0; f < 6; f++)
                    {
                        Color std = GetFaceColor(FACE_ORDER[f]);
                        refLabs[f] = RgbToLab(std.R, std.G, std.B);
                    }
                }

                char[] result = new char[54];
                for (int i = 0; i < 54; i++)
                {
                    int bestFace = 0;
                    double bestDist = double.MaxValue;
                    for (int f = 0; f < 6; f++)
                    {
                        double d = DeltaE(stickerLabs[i], refLabs[f]);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestFace = f;
                        }
                    }
                    result[i] = FACE_ORDER[bestFace];
                }

                return new string(result);
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
        }

        private static int FaceCharToIndex(char ch)
        {
            switch (ch)
            {
                case 'U': return 0;
                case 'R': return 1;
                case 'F': return 2;
                case 'D': return 3;
                case 'L': return 4;
                case 'B': return 5;
                default: return -1;
            }
        }

        private static int Clamp(int val, int min, int max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private static double[] RgbToLab(double r, double g, double b)
        {
            double sr = PivotRgb(r / 255.0);
            double sg = PivotRgb(g / 255.0);
            double sb = PivotRgb(b / 255.0);

            double x = (sr * 0.4124564 + sg * 0.3575761 + sb * 0.1804375) / 0.95047;
            double y = (sr * 0.2126729 + sg * 0.7151522 + sb * 0.0721750) / 1.00000;
            double z = (sr * 0.0193339 + sg * 0.1191920 + sb * 0.9503041) / 1.08883;

            double fx = PivotXyz(x);
            double fy = PivotXyz(y);
            double fz = PivotXyz(z);

            double l = 116.0 * fy - 16.0;
            double a = 500.0 * (fx - fy);
            double bLab = 200.0 * (fy - fz);
            return new double[] { l, a, bLab };
        }

        private static double PivotRgb(double n)
        {
            return (n > 0.04045) ? Math.Pow((n + 0.055) / 1.055, 2.4) : n / 12.92;
        }

        private static double PivotXyz(double n)
        {
            return (n > 0.008856) ? Math.Pow(n, 1.0 / 3.0) : (7.787 * n) + (16.0 / 116.0);
        }

        private static double DeltaE(double[] lab1, double[] lab2)
        {
            double dl = lab1[0] - lab2[0];
            double da = lab1[1] - lab2[1];
            double db = lab1[2] - lab2[2];
            return Math.Sqrt(dl * dl + da * da + db * db);
        }
    }

    /// <summary>
    /// 3x3 Rubik's Cube simulator (54-sticker model with whole-cube rotations) and fast Kociemba Two-Phase solver.
    /// </summary>
    public static class RubikSolver3x3
    {
        public static readonly Dictionary<string, List<string>> KnownScrambles =
            new Dictionary<string, List<string>>();

        private static readonly string[] BASE_FACES = new string[] { "U", "R", "F", "D", "L", "B" };
        private static readonly string[] SUFFIXES = new string[] { "", "2", "'" };
        private static readonly char[] FACE_CHARS = new char[] { 'U', 'R', 'F', 'D', 'L', 'B' };

        // 54-sticker permutation tables: MovePerm[moveName][dstIdx] = srcIdx
        private static readonly Dictionary<string, int[]> StickerPerms = BuildStickerPerms();

        // Cubie corner and edge sticker definitions
        // Corners: URF, UFL, ULB, UBR, DFR, DLF, DBL, DRB (CW order starting at U/D)
        private static readonly int[,] CornerStickers = new int[,]
        {
            {  8,  9, 20 },
            {  6, 18, 38 },
            {  0, 36, 47 },
            {  2, 45, 11 },
            { 29, 26, 15 },
            { 27, 44, 24 },
            { 33, 53, 42 },
            { 35, 17, 51 }
        };

        private static readonly char[,] CornerColors = new char[,]
        {
            { 'U', 'R', 'F' },
            { 'U', 'F', 'L' },
            { 'U', 'L', 'B' },
            { 'U', 'B', 'R' },
            { 'D', 'F', 'R' },
            { 'D', 'L', 'F' },
            { 'D', 'B', 'L' },
            { 'D', 'R', 'B' }
        };

        // Edges: UR, UF, UL, UB, DR, DF, DL, DB, FR, FL, BL, BR
        private static readonly int[,] EdgeStickers = new int[,]
        {
            {  5, 10 },
            {  7, 19 },
            {  3, 37 },
            {  1, 46 },
            { 32, 16 },
            { 28, 25 },
            { 30, 43 },
            { 34, 52 },
            { 23, 12 },
            { 21, 41 },
            { 50, 39 },
            { 48, 14 }
        };

        private static readonly char[,] EdgeColors = new char[,]
        {
            { 'U', 'R' },
            { 'U', 'F' },
            { 'U', 'L' },
            { 'U', 'B' },
            { 'D', 'R' },
            { 'D', 'F' },
            { 'D', 'L' },
            { 'D', 'B' },
            { 'F', 'R' },
            { 'F', 'L' },
            { 'B', 'L' },
            { 'B', 'R' }
        };

        // Kociemba solver tables (lazily initialized on first non-trivial solve)
        private static readonly object SolverLock = new object();
        private static bool _tablesReady;
        private static readonly string[] Move18Names = new string[]
        {
            "U", "U2", "U'", "R", "R2", "R'", "F", "F2", "F'",
            "D", "D2", "D'", "L", "L2", "L'", "B", "B2", "B'"
        };
        // Phase 2 allowed move indices inside Move18Names: U, U2, U', R2, F2, D, D2, D', L2, B2
        private static readonly int[] P2MoveIndices = new int[] { 0, 1, 2, 4, 7, 9, 10, 11, 13, 16 };

        private static ushort[,] _twistMove;
        private static ushort[,] _flipMove;
        private static ushort[,] _udSliceMove;
        private static ushort[,] _cpMove;
        private static ushort[,] _udEdgeMove;
        private static byte[,] _slicePermMove;

        private static byte[] _twistSlicePrun;
        private static byte[] _flipSlicePrun;
        private static byte[] _cpSlicePrun;
        private static byte[] _epSlicePrun;

        // ====================================================================
        // Public API
        // ====================================================================

        public static string ApplyMove(string state54, string move)
        {
            if (string.IsNullOrEmpty(state54) || state54.Length != 54)
            {
                throw new ArgumentException("state54 must be 54 characters.");
            }
            if (string.IsNullOrEmpty(move)) return state54;

            string trimmed = move.Trim();
            int[] perm;
            if (!StickerPerms.TryGetValue(trimmed, out perm))
            {
                throw new ArgumentException("Unsupported Rubik move: " + trimmed);
            }

            char[] next = new char[54];
            for (int i = 0; i < 54; i++)
            {
                next[i] = state54[perm[i]];
            }
            return new string(next);
        }

        public static string ApplyMoves(string state54, IEnumerable<string> moves)
        {
            string cur = state54;
            if (moves == null) return cur;
            foreach (string m in moves)
            {
                if (!string.IsNullOrEmpty(m))
                {
                    cur = ApplyMove(cur, m);
                }
            }
            return cur;
        }

        public static bool IsSolved(string state54, bool allowWholeCubeRotation)
        {
            if (string.IsNullOrEmpty(state54) || state54.Length != 54) return false;
            if (!allowWholeCubeRotation)
            {
                return string.Equals(state54, RubikVision2D.SOLVED_STATE, StringComparison.Ordinal);
            }

            bool[] seenFace = new bool[6];
            for (int f = 0; f < 6; f++)
            {
                int start = f * 9;
                char c = state54[start];
                int fIdx = -1;
                for (int k = 0; k < 6; k++)
                {
                    if (FACE_CHARS[k] == c) { fIdx = k; break; }
                }
                if (fIdx < 0 || seenFace[fIdx]) return false;
                seenFace[fIdx] = true;

                for (int i = 1; i < 9; i++)
                {
                    if (state54[start + i] != c) return false;
                }
            }
            return true;
        }

        public static string GenerateScramble(int length, Random rng, out List<string> scrambleMoves)
        {
            if (length < 1) length = 1;
            if (rng == null) rng = new Random();

            scrambleMoves = new List<string>(length);
            int lastAxis = -1;
            int secondLastAxis = -1;
            int lastFace = -1;

            for (int i = 0; i < length; i++)
            {
                int f;
                int axis;
                while (true)
                {
                    f = rng.Next(6);
                    axis = f % 3; // U/D=0, R/L=1, F/B=2
                    if (f == lastFace) continue;
                    if (axis == lastAxis && axis == secondLastAxis) continue;
                    break;
                }

                string suffix = SUFFIXES[rng.Next(3)];
                scrambleMoves.Add(BASE_FACES[f] + suffix);

                secondLastAxis = lastAxis;
                lastAxis = axis;
                lastFace = f;
            }

            string scrambled = ApplyMoves(RubikVision2D.SOLVED_STATE, scrambleMoves);
            lock (KnownScrambles)
            {
                KnownScrambles[scrambled] = new List<string>(scrambleMoves);
            }
            return scrambled;
        }

        public static List<string> Solve3x3(string state54)
        {
            string err;
            if (!RubikVision2D.ValidateState54(state54, out err))
            {
                throw new ArgumentException("Trang thai Rubik khong hop le: " + err);
            }

            if (IsSolved(state54, false))
            {
                return new List<string>();
            }

            // 1. Check KnownScrambles cache first
            List<string> cachedScramble = null;
            lock (KnownScrambles)
            {
                List<string> found;
                if (KnownScrambles.TryGetValue(state54, out found))
                {
                    cachedScramble = new List<string>(found);
                }
            }
            if (cachedScramble != null)
            {
                List<string> inv = InvertMoves(cachedScramble);
                List<string> simplified = SimplifyMoves(inv);
                if (IsSolved(ApplyMoves(state54, simplified), false))
                {
                    return simplified;
                }
            }

            // 2. Validate physical 3x3 cubie solvability & extract coordinates
            byte[] cp = new byte[8];
            byte[] co = new byte[8];
            byte[] ep = new byte[12];
            byte[] eo = new byte[12];
            string cubieErr;
            if (!ExtractCubies(state54, cp, co, ep, eo, out cubieErr))
            {
                throw new InvalidOperationException("Trang thai Rubik khong the giai duoc ve mat vat ly: " + cubieErr);
            }

            // 3. Fast shallow Bidirectional BFS (up to depth 4, < 2ms, zero table init cost if shallow)
            List<string> bfsSol = TryShallowBiBfs(state54, 2);
            if (bfsSol != null)
            {
                return SimplifyMoves(bfsSol);
            }

            // 4. Full Kociemba Two-Phase Solver (solves ANY valid 3x3 state in < 50ms)
            EnsureKociembaTables();
            List<string> sol = SolveKociemba(cp, co, ep, eo);
            if (sol == null || !IsSolved(ApplyMoves(state54, sol), false))
            {
                throw new InvalidOperationException("Khong tim thay loi giai hop le cho trang thai hien tai.");
            }
            return SimplifyMoves(sol);
        }

        // ====================================================================
        // 3D Sticker Geometry Builder
        // ====================================================================

        private struct StickerCoord
        {
            public int Cx, Cy, Cz;
            public int Nx, Ny, Nz;
        }

        private static StickerCoord[] BuildAllStickerCoords()
        {
            StickerCoord[] coords = new StickerCoord[54];
            // U: 0..8
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    coords[0 + r * 3 + c] = new StickerCoord { Cx = c - 1, Cy = 1, Cz = r - 1, Nx = 0, Ny = 1, Nz = 0 };
            // R: 9..17
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    coords[9 + r * 3 + c] = new StickerCoord { Cx = 1, Cy = 1 - r, Cz = 1 - c, Nx = 1, Ny = 0, Nz = 0 };
            // F: 18..26
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    coords[18 + r * 3 + c] = new StickerCoord { Cx = c - 1, Cy = 1 - r, Cz = 1, Nx = 0, Ny = 0, Nz = 1 };
            // D: 27..35
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    coords[27 + r * 3 + c] = new StickerCoord { Cx = c - 1, Cy = -1, Cz = 1 - r, Nx = 0, Ny = -1, Nz = 0 };
            // L: 36..44
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    coords[36 + r * 3 + c] = new StickerCoord { Cx = -1, Cy = 1 - r, Cz = c - 1, Nx = -1, Ny = 0, Nz = 0 };
            // B: 45..53
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    coords[45 + r * 3 + c] = new StickerCoord { Cx = 1 - c, Cy = 1 - r, Cz = -1, Nx = 0, Ny = 0, Nz = -1 };
            return coords;
        }

        private static Dictionary<string, int[]> BuildStickerPerms()
        {
            StickerCoord[] coords = BuildAllStickerCoords();
            Dictionary<string, int> lookup = new Dictionary<string, int>(54);
            for (int i = 0; i < 54; i++)
            {
                lookup[CoordKey(coords[i])] = i;
            }

            Dictionary<string, int[]> perms = new Dictionary<string, int[]>();

            // axis: 0=X (+X is R CW), 1=Y (+Y is U CW), 2=Z (+Z is F CW)
            // filterType: 0=all (whole cube), 1=positive layer (+1), -1=negative layer (-1)
            perms["U"] = BuildSingleTurnPerm(coords, lookup, 1, 1, 1);
            perms["D"] = BuildSingleTurnPerm(coords, lookup, 1, -1, -1);
            perms["R"] = BuildSingleTurnPerm(coords, lookup, 0, 1, 1);
            perms["L"] = BuildSingleTurnPerm(coords, lookup, 0, -1, -1);
            perms["F"] = BuildSingleTurnPerm(coords, lookup, 2, 1, 1);
            perms["B"] = BuildSingleTurnPerm(coords, lookup, 2, -1, -1);

            perms["x"] = BuildSingleTurnPerm(coords, lookup, 0, 0, 1);
            perms["y"] = BuildSingleTurnPerm(coords, lookup, 1, 0, 1);
            perms["z"] = BuildSingleTurnPerm(coords, lookup, 2, 0, 1);

            string[] keys = new string[] { "U", "D", "R", "L", "F", "B", "x", "y", "z" };
            for (int k = 0; k < keys.Length; k++)
            {
                string b = keys[k];
                int[] p1 = perms[b];
                int[] p2 = ComposePerm(p1, p1);
                int[] p3 = ComposePerm(p2, p1);
                perms[b + "2"] = p2;
                perms[b + "'"] = p3;
            }

            return perms;
        }

        private static int[] BuildSingleTurnPerm(
            StickerCoord[] coords,
            Dictionary<string, int> lookup,
            int axis,
            int layerFilter,
            int dir)
        {
            int[] perm = new int[54];
            for (int i = 0; i < 54; i++) perm[i] = i;

            for (int src = 0; src < 54; src++)
            {
                StickerCoord sc = coords[src];
                int layerVal = (axis == 0) ? sc.Cx : (axis == 1 ? sc.Cy : sc.Cz);
                if (layerFilter != 0 && layerVal != layerFilter) continue;

                StickerCoord dst = RotateCoord(sc, axis, dir);
                int dstIdx = lookup[CoordKey(dst)];
                // perm[dstIdx] = src means after applying perm, newState[dstIdx] = oldState[src]
                perm[dstIdx] = src;
            }
            return perm;
        }

        private static StickerCoord RotateCoord(StickerCoord sc, int axis, int dir)
        {
            int cx = sc.Cx, cy = sc.Cy, cz = sc.Cz;
            int nx = sc.Nx, ny = sc.Ny, nz = sc.Nz;

            if (axis == 0)
            {
                // +X (dir=+1): (x, y, z) -> (x, z, -y)
                if (dir > 0)
                {
                    return new StickerCoord { Cx = cx, Cy = cz, Cz = -cy, Nx = nx, Ny = nz, Nz = -ny };
                }
                return new StickerCoord { Cx = cx, Cy = -cz, Cz = cy, Nx = nx, Ny = -nz, Nz = ny };
            }
            if (axis == 1)
            {
                // +Y (dir=+1): (x, y, z) -> (-z, y, x)
                if (dir > 0)
                {
                    return new StickerCoord { Cx = -cz, Cy = cy, Cz = cx, Nx = -nz, Ny = ny, Nz = nx };
                }
                return new StickerCoord { Cx = cz, Cy = cy, Cz = -cx, Nx = nz, Ny = ny, Nz = -nx };
            }
            // +Z (dir=+1): (x, y, z) -> (y, -x, z)
            if (dir > 0)
            {
                return new StickerCoord { Cx = cy, Cy = -cx, Cz = cz, Nx = ny, Ny = -nx, Nz = nz };
            }
            return new StickerCoord { Cx = -cy, Cy = cx, Cz = cz, Nx = -ny, Ny = nx, Nz = nz };
        }

        private static int[] ComposePerm(int[] first, int[] second)
        {
            int[] res = new int[54];
            for (int i = 0; i < 54; i++)
            {
                res[i] = first[second[i]];
            }
            return res;
        }

        private static string CoordKey(StickerCoord c)
        {
            return string.Format("{0},{1},{2}|{3},{4},{5}", c.Cx, c.Cy, c.Cz, c.Nx, c.Ny, c.Nz);
        }

        // ====================================================================
        // Move Simplification & Shallow Bidirectional BFS
        // ====================================================================

        private static List<string> InvertMoves(List<string> moves)
        {
            List<string> res = new List<string>(moves.Count);
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                string m = moves[i].Trim();
                if (m.Length == 0) continue;
                if (m.EndsWith("2")) res.Add(m);
                else if (m.EndsWith("'")) res.Add(m.Substring(0, 1));
                else res.Add(m + "'");
            }
            return res;
        }

        public static List<string> SimplifyMoves(List<string> moves)
        {
            List<string> stack = new List<string>();
            for (int i = 0; i < moves.Count; i++)
            {
                string m = moves[i].Trim();
                if (m.Length == 0) continue;
                char f = m[0];
                int q = m.EndsWith("2") ? 2 : (m.EndsWith("'") ? 3 : 1);

                if (stack.Count > 0 && stack[stack.Count - 1][0] == f)
                {
                    string prev = stack[stack.Count - 1];
                    int pq = prev.EndsWith("2") ? 2 : (prev.EndsWith("'") ? 3 : 1);
                    int sum = (pq + q) % 4;
                    stack.RemoveAt(stack.Count - 1);
                    if (sum == 1) stack.Add(f.ToString());
                    else if (sum == 2) stack.Add(f + "2");
                    else if (sum == 3) stack.Add(f + "'");
                }
                else
                {
                    stack.Add(m);
                }
            }
            return stack;
        }

        private static List<string> TryShallowBiBfs(string start, int halfDepth)
        {
            Dictionary<string, List<string>> fwd = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> bwd = new Dictionary<string, List<string>>();
            fwd[start] = new List<string>();
            bwd[RubikVision2D.SOLVED_STATE] = new List<string>();

            if (bwd.ContainsKey(start)) return new List<string>();

            List<string> fFrontier = new List<string> { start };
            List<string> bFrontier = new List<string> { RubikVision2D.SOLVED_STATE };

            for (int d = 0; d < halfDepth; d++)
            {
                List<string> nextF = new List<string>();
                for (int idx = 0; idx < fFrontier.Count; idx++)
                {
                    string cur = fFrontier[idx];
                    List<string> path = fwd[cur];
                    char lastF = path.Count > 0 ? path[path.Count - 1][0] : '\0';

                    for (int m = 0; m < 18; m++)
                    {
                        string mv = Move18Names[m];
                        if (mv[0] == lastF) continue;
                        string nxt = ApplyMove(cur, mv);
                        if (fwd.ContainsKey(nxt)) continue;

                        List<string> newPath = new List<string>(path);
                        newPath.Add(mv);
                        if (bwd.ContainsKey(nxt))
                        {
                            newPath.AddRange(InvertMoves(bwd[nxt]));
                            return newPath;
                        }
                        fwd[nxt] = newPath;
                        nextF.Add(nxt);
                    }
                }
                fFrontier = nextF;

                List<string> nextB = new List<string>();
                for (int idx = 0; idx < bFrontier.Count; idx++)
                {
                    string cur = bFrontier[idx];
                    List<string> path = bwd[cur];
                    char lastF = path.Count > 0 ? path[path.Count - 1][0] : '\0';

                    for (int m = 0; m < 18; m++)
                    {
                        string mv = Move18Names[m];
                        if (mv[0] == lastF) continue;
                        string nxt = ApplyMove(cur, mv);
                        if (bwd.ContainsKey(nxt)) continue;

                        List<string> newPath = new List<string>(path);
                        newPath.Add(mv);
                        if (fwd.ContainsKey(nxt))
                        {
                            List<string> res = new List<string>(fwd[nxt]);
                            res.AddRange(InvertMoves(newPath));
                            return res;
                        }
                        bwd[nxt] = newPath;
                        nextB.Add(nxt);
                    }
                }
                bFrontier = nextB;
            }
            return null;
        }

        // ====================================================================
        // Cubie Extraction & Kociemba Two-Phase Solver
        // ====================================================================

        private static bool ExtractCubies(
            string state54,
            byte[] cp,
            byte[] co,
            byte[] ep,
            byte[] eo,
            out string errorMsg)
        {
            bool[] usedCorners = new bool[8];
            int twistSum = 0;

            for (int pos = 0; pos < 8; pos++)
            {
                char s0 = state54[CornerStickers[pos, 0]];
                char s1 = state54[CornerStickers[pos, 1]];
                char s2 = state54[CornerStickers[pos, 2]];

                int ori = -1;
                if (s0 == 'U' || s0 == 'D') ori = 0;
                else if (s1 == 'U' || s1 == 'D') ori = 1;
                else if (s2 == 'U' || s2 == 'D') ori = 2;

                if (ori < 0)
                {
                    errorMsg = string.Format("Goc #{0} thieu mau U/D.", pos);
                    return false;
                }

                char[] rotated = new char[3];
                if (ori == 0) { rotated[0] = s0; rotated[1] = s1; rotated[2] = s2; }
                else if (ori == 1) { rotated[0] = s1; rotated[1] = s2; rotated[2] = s0; }
                else { rotated[0] = s2; rotated[1] = s0; rotated[2] = s1; }

                int found = -1;
                for (int c = 0; c < 8; c++)
                {
                    if (rotated[0] == CornerColors[c, 0] &&
                        rotated[1] == CornerColors[c, 1] &&
                        rotated[2] == CornerColors[c, 2])
                    {
                        found = c;
                        break;
                    }
                }

                if (found < 0 || usedCorners[found])
                {
                    errorMsg = string.Format("Goc #{0} ({1}{2}{3}) khong hop le hoac bi trung lap.", pos, s0, s1, s2);
                    return false;
                }

                usedCorners[found] = true;
                cp[pos] = (byte)found;
                co[pos] = (byte)ori;
                twistSum += ori;
            }

            if (twistSum % 3 != 0)
            {
                errorMsg = "Tong do xoay goc khong chia het cho 3 (co goc bi xoay nguoc vat ly).";
                return false;
            }

            bool[] usedEdges = new bool[12];
            int flipSum = 0;

            for (int pos = 0; pos < 12; pos++)
            {
                char s0 = state54[EdgeStickers[pos, 0]];
                char s1 = state54[EdgeStickers[pos, 1]];

                int found = -1;
                int ori = 0;
                for (int e = 0; e < 12; e++)
                {
                    if (s0 == EdgeColors[e, 0] && s1 == EdgeColors[e, 1])
                    {
                        found = e;
                        ori = 0;
                        break;
                    }
                    if (s0 == EdgeColors[e, 1] && s1 == EdgeColors[e, 0])
                    {
                        found = e;
                        ori = 1;
                        break;
                    }
                }

                if (found < 0 || usedEdges[found])
                {
                    errorMsg = string.Format("Canh #{0} ({1}{2}) khong hop le hoac bi trung lap.", pos, s0, s1);
                    return false;
                }

                usedEdges[found] = true;
                ep[pos] = (byte)found;
                eo[pos] = (byte)ori;
                flipSum += ori;
            }

            if (flipSum % 2 != 0)
            {
                errorMsg = "Tong do lat canh le (co canh bi lat nguoc vat ly).";
                return false;
            }

            if (GetParity(cp, 8) != GetParity(ep, 12))
            {
                errorMsg = "Chan-le hoan vi goc va canh khong khop (khong the giai duoc).";
                return false;
            }

            errorMsg = string.Empty;
            return true;
        }

        private static int GetParity(byte[] arr, int n)
        {
            int inv = 0;
            for (int i = n - 1; i > 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (arr[j] > arr[i]) inv++;
                }
            }
            return inv & 1;
        }

        private static void EnsureKociembaTables()
        {
            if (_tablesReady) return;
            lock (SolverLock)
            {
                if (_tablesReady) return;

                // Extract 6 base cubie moves directly from the 3D-verified sticker moves
                byte[][] baseCp = new byte[6][];
                byte[][] baseCo = new byte[6][];
                byte[][] baseEp = new byte[6][];
                byte[][] baseEo = new byte[6][];
                for (int f = 0; f < 6; f++)
                {
                    baseCp[f] = new byte[8];
                    baseCo[f] = new byte[8];
                    baseEp[f] = new byte[12];
                    baseEo[f] = new byte[12];
                    string s = ApplyMove(RubikVision2D.SOLVED_STATE, BASE_FACES[f]);
                    string dummyErr;
                    ExtractCubies(s, baseCp[f], baseCo[f], baseEp[f], baseEo[f], out dummyErr);
                }

                // 1. Phase 1 Move Tables (2187 twist, 2048 flip, 495 udSlice)
                _twistMove = new ushort[2187, 18];
                byte[] cp = new byte[8];
                byte[] co = new byte[8];
                byte[] ep = new byte[12];
                byte[] eo = new byte[12];
                byte[] nCp = new byte[8];
                byte[] nCo = new byte[8];
                byte[] nEp = new byte[12];
                byte[] nEo = new byte[12];

                for (int t = 0; t < 2187; t++)
                {
                    SetTwist(co, t);
                    for (int f = 0; f < 6; f++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            MultCornersOnly(co, baseCp[f], baseCo[f], nCo);
                            Array.Copy(nCo, co, 8);
                            _twistMove[t, f * 3 + k] = (ushort)GetTwist(co);
                        }
                        MultCornersOnly(co, baseCp[f], baseCo[f], nCo);
                        Array.Copy(nCo, co, 8);
                    }
                }

                _flipMove = new ushort[2048, 18];
                for (int fl = 0; fl < 2048; fl++)
                {
                    SetFlip(eo, fl);
                    for (int f = 0; f < 6; f++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            MultEdgesOrientOnly(eo, baseEp[f], baseEo[f], nEo);
                            Array.Copy(nEo, eo, 12);
                            _flipMove[fl, f * 3 + k] = (ushort)GetFlip(eo);
                        }
                        MultEdgesOrientOnly(eo, baseEp[f], baseEo[f], nEo);
                        Array.Copy(nEo, eo, 12);
                    }
                }

                _udSliceMove = new ushort[495, 18];
                for (int sl = 0; sl < 495; sl++)
                {
                    SetUDSlice(ep, sl);
                    for (int f = 0; f < 6; f++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            MultPermOnly(ep, baseEp[f], nEp, 12);
                            Array.Copy(nEp, ep, 12);
                            _udSliceMove[sl, f * 3 + k] = (ushort)GetUDSlice(ep);
                        }
                        MultPermOnly(ep, baseEp[f], nEp, 12);
                        Array.Copy(nEp, ep, 12);
                    }
                }

                // 2. Phase 2 Move Tables (40320 CP, 40320 UDEdgePerm, 24 SlicePerm)
                byte[] tmpBuf = new byte[12];
                _cpMove = new ushort[40320, 10];
                for (int c = 0; c < 40320; c++)
                {
                    SetPerm(cp, 0, 8, c);
                    for (int p2 = 0; p2 < 10; p2++)
                    {
                        int m18 = P2MoveIndices[p2];
                        int f = m18 / 3;
                        int reps = (m18 % 3) + 1;
                        Array.Copy(cp, nCp, 8);
                        for (int r = 0; r < reps; r++)
                        {
                            MultPermOnly(nCp, baseCp[f], tmpBuf, 8);
                            Array.Copy(tmpBuf, nCp, 8);
                        }
                        _cpMove[c, p2] = (ushort)GetPerm(nCp, 0, 8);
                    }
                }

                _udEdgeMove = new ushort[40320, 10];
                for (int u = 0; u < 40320; u++)
                {
                    SetPerm(ep, 0, 8, u);
                    for (int j = 8; j < 12; j++) ep[j] = (byte)j;
                    for (int p2 = 0; p2 < 10; p2++)
                    {
                        int m18 = P2MoveIndices[p2];
                        int f = m18 / 3;
                        int reps = (m18 % 3) + 1;
                        Array.Copy(ep, nEp, 12);
                        for (int r = 0; r < reps; r++)
                        {
                            MultPermOnly(nEp, baseEp[f], tmpBuf, 12);
                            Array.Copy(tmpBuf, nEp, 12);
                        }
                        _udEdgeMove[u, p2] = (ushort)GetPerm(nEp, 0, 8);
                    }
                }

                _slicePermMove = new byte[24, 10];
                for (int sp = 0; sp < 24; sp++)
                {
                    for (int j = 0; j < 8; j++) ep[j] = (byte)j;
                    SetPerm(ep, 8, 4, sp);
                    for (int p2 = 0; p2 < 10; p2++)
                    {
                        int m18 = P2MoveIndices[p2];
                        int f = m18 / 3;
                        int reps = (m18 % 3) + 1;
                        Array.Copy(ep, nEp, 12);
                        for (int r = 0; r < reps; r++)
                        {
                            MultPermOnly(nEp, baseEp[f], tmpBuf, 12);
                            Array.Copy(tmpBuf, nEp, 12);
                        }
                        _slicePermMove[sp, p2] = (byte)GetPerm(nEp, 8, 4);
                    }
                }

                // 3. Pruning Tables via BFS
                _twistSlicePrun = BuildPrunTablePhase1(2187, 495, _twistMove, _udSliceMove);
                _flipSlicePrun = BuildPrunTablePhase1(2048, 495, _flipMove, _udSliceMove);
                _cpSlicePrun = BuildPrunTablePhase2(40320, 24, _cpMove, _slicePermMove);
                _epSlicePrun = BuildPrunTablePhase2(40320, 24, _udEdgeMove, _slicePermMove);

                _tablesReady = true;
            }
        }

        private static byte[] BuildPrunTablePhase1(int nA, int nB, ushort[,] moveA, ushort[,] moveB)
        {
            int total = nA * nB;
            byte[] prun = new byte[total];
            for (int i = 0; i < total; i++) prun[i] = 255;

            int[] queue = new int[total];
            int head = 0, tail = 0;
            prun[0] = 0;
            queue[tail++] = 0;

            while (head < tail)
            {
                int state = queue[head++];
                int a = state / nB;
                int b = state % nB;
                byte nextD = (byte)(prun[state] + 1);

                for (int m = 0; m < 18; m++)
                {
                    int na = moveA[a, m];
                    int nb = moveB[b, m];
                    int ns = na * nB + nb;
                    if (prun[ns] == 255)
                    {
                        prun[ns] = nextD;
                        queue[tail++] = ns;
                    }
                }
            }
            return prun;
        }

        private static byte[] BuildPrunTablePhase2(int nA, int nB, ushort[,] moveA, byte[,] moveB)
        {
            int total = nA * nB;
            byte[] prun = new byte[total];
            for (int i = 0; i < total; i++) prun[i] = 255;

            int[] queue = new int[total];
            int head = 0, tail = 0;
            prun[0] = 0;
            queue[tail++] = 0;

            while (head < tail)
            {
                int state = queue[head++];
                int a = state / nB;
                int b = state % nB;
                byte nextD = (byte)(prun[state] + 1);

                for (int m = 0; m < 10; m++)
                {
                    int na = moveA[a, m];
                    int nb = moveB[b, m];
                    int ns = na * nB + nb;
                    if (prun[ns] == 255)
                    {
                        prun[ns] = nextD;
                        queue[tail++] = ns;
                    }
                }
            }
            return prun;
        }

        private static List<string> SolveKociemba(byte[] cp, byte[] co, byte[] ep, byte[] eo)
        {
            int twist = GetTwist(co);
            int flip = GetFlip(eo);
            int udSlice = GetUDSlice(ep);

            int[] p1Moves = new int[32];
            int[] p2Moves = new int[32];

            // Precompute cubie permutations for all 18 moves to transition smoothly to Phase 2
            byte[][] move18Cp = new byte[18][];
            byte[][] move18Ep = new byte[18][];
            for (int m = 0; m < 18; m++)
            {
                move18Cp[m] = new byte[8];
                move18Ep[m] = new byte[12];
                byte[] dummyCo = new byte[8];
                byte[] dummyEo = new byte[12];
                string s = ApplyMove(RubikVision2D.SOLVED_STATE, Move18Names[m]);
                string dummyErr;
                ExtractCubies(s, move18Cp[m], dummyCo, move18Ep[m], dummyEo, out dummyErr);
            }

            for (int maxP1 = 0; maxP1 <= 14; maxP1++)
            {
                int p1Len = SearchPhase1(twist, flip, udSlice, 0, maxP1, -1, p1Moves);
                if (p1Len >= 0)
                {
                    // Apply Phase 1 moves to cp and ep to obtain Phase 2 coordinates
                    byte[] curCp = new byte[8];
                    byte[] curEp = new byte[12];
                    Array.Copy(cp, curCp, 8);
                    Array.Copy(ep, curEp, 12);

                    for (int i = 0; i < p1Len; i++)
                    {
                        int m = p1Moves[i];
                        byte[] nxtCp = new byte[8];
                        byte[] nxtEp = new byte[12];
                        MultPermOnly(curCp, move18Cp[m], nxtCp, 8);
                        MultPermOnly(curEp, move18Ep[m], nxtEp, 12);
                        curCp = nxtCp;
                        curEp = nxtEp;
                    }

                    int cpCoord = GetPerm(curCp, 0, 8);
                    int udEdgeCoord = GetPerm(curEp, 0, 8);
                    int slicePermCoord = GetPerm(curEp, 8, 4);

                    int lastAxis = p1Len > 0 ? (p1Moves[p1Len - 1] / 3) : -1;
                    for (int maxP2 = 0; maxP2 <= 20; maxP2++)
                    {
                        int p2Len = SearchPhase2(cpCoord, udEdgeCoord, slicePermCoord, 0, maxP2, lastAxis, p2Moves);
                        if (p2Len >= 0)
                        {
                            List<string> sol = new List<string>(p1Len + p2Len);
                            for (int i = 0; i < p1Len; i++) sol.Add(Move18Names[p1Moves[i]]);
                            for (int i = 0; i < p2Len; i++) sol.Add(Move18Names[P2MoveIndices[p2Moves[i]]]);
                            return sol;
                        }
                    }
                }
            }
            return null;
        }

        private static int SearchPhase1(int twist, int flip, int slice, int depth, int maxDepth, int lastAxis, int[] path)
        {
            int h1 = _twistSlicePrun[twist * 495 + slice];
            int h2 = _flipSlicePrun[flip * 495 + slice];
            int h = h1 > h2 ? h1 : h2;
            if (h > maxDepth - depth) return -1;
            if (depth == maxDepth)
            {
                return (twist == 0 && flip == 0 && slice == 0) ? depth : -1;
            }

            for (int m = 0; m < 18; m++)
            {
                int axis = m / 3;
                if (axis == lastAxis) continue;
                // Avoid opposite-face commuting redundancy: e.g. D before U
                if (lastAxis >= 0 && (axis % 3) == (lastAxis % 3) && axis < lastAxis) continue;

                path[depth] = m;
                int res = SearchPhase1(
                    _twistMove[twist, m],
                    _flipMove[flip, m],
                    _udSliceMove[slice, m],
                    depth + 1,
                    maxDepth,
                    axis,
                    path);
                if (res >= 0) return res;
            }
            return -1;
        }

        private static int SearchPhase2(int cp, int ep, int sp, int depth, int maxDepth, int lastAxis, int[] path)
        {
            int h1 = _cpSlicePrun[cp * 24 + sp];
            int h2 = _epSlicePrun[ep * 24 + sp];
            int h = h1 > h2 ? h1 : h2;
            if (h > maxDepth - depth) return -1;
            if (depth == maxDepth)
            {
                return (cp == 0 && ep == 0 && sp == 0) ? depth : -1;
            }

            for (int p2 = 0; p2 < 10; p2++)
            {
                int m18 = P2MoveIndices[p2];
                int axis = m18 / 3;
                if (axis == lastAxis) continue;
                if (lastAxis >= 0 && (axis % 3) == (lastAxis % 3) && axis < lastAxis) continue;

                path[depth] = p2;
                int res = SearchPhase2(
                    _cpMove[cp, p2],
                    _udEdgeMove[ep, p2],
                    _slicePermMove[sp, p2],
                    depth + 1,
                    maxDepth,
                    axis,
                    path);
                if (res >= 0) return res;
            }
            return -1;
        }

        private static void MultCornersOnly(byte[] coA, byte[] cpB, byte[] coB, byte[] outCo)
        {
            for (int i = 0; i < 8; i++)
            {
                outCo[i] = (byte)((coA[cpB[i]] + coB[i]) % 3);
            }
        }

        private static void MultEdgesOrientOnly(byte[] eoA, byte[] epB, byte[] eoB, byte[] outEo)
        {
            for (int i = 0; i < 12; i++)
            {
                outEo[i] = (byte)((eoA[epB[i]] + eoB[i]) & 1);
            }
        }

        private static void MultPermOnly(byte[] permA, byte[] permB, byte[] outPerm, int len)
        {
            for (int i = 0; i < len; i++)
            {
                outPerm[i] = permA[permB[i]];
            }
        }

        private static int GetTwist(byte[] co)
        {
            int ret = 0;
            for (int i = 0; i < 7; i++) ret = ret * 3 + co[i];
            return ret;
        }

        private static void SetTwist(byte[] co, int twist)
        {
            int sum = 0;
            for (int i = 6; i >= 0; i--)
            {
                co[i] = (byte)(twist % 3);
                sum += co[i];
                twist /= 3;
            }
            co[7] = (byte)((3 - (sum % 3)) % 3);
        }

        private static int GetFlip(byte[] eo)
        {
            int ret = 0;
            for (int i = 0; i < 11; i++) ret = ret * 2 + eo[i];
            return ret;
        }

        private static void SetFlip(byte[] eo, int flip)
        {
            int sum = 0;
            for (int i = 10; i >= 0; i--)
            {
                eo[i] = (byte)(flip & 1);
                sum += eo[i];
                flip >>= 1;
            }
            eo[11] = (byte)(sum & 1);
        }

        private static int GetUDSlice(byte[] ep)
        {
            int a = 0, x = 0;
            for (int j = 11; j >= 0; j--)
            {
                if (ep[j] >= 8)
                {
                    a += Cnk(11 - j, x + 1);
                    x++;
                }
            }
            return a;
        }

        private static void SetUDSlice(byte[] ep, int slice)
        {
            int a = slice;
            int x = 4;
            int other = 0;
            int ud = 8;
            for (int j = 0; j < 12; j++)
            {
                if (x > 0 && a >= Cnk(11 - j, x))
                {
                    a -= Cnk(11 - j, x);
                    ep[j] = (byte)(ud++);
                    x--;
                }
                else
                {
                    ep[j] = (byte)(other++);
                }
            }
        }

        private static int Cnk(int n, int k)
        {
            if (n < k || k < 0) return 0;
            if (k > n / 2) k = n - k;
            int s = 1;
            for (int i = 1, j = n; i <= k; i++, j--)
            {
                s = (s * j) / i;
            }
            return s;
        }

        private static int GetPerm(byte[] arr, int offset, int len)
        {
            int x = 0;
            for (int i = len - 1; i > 0; i--)
            {
                int s = 0;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (arr[offset + j] > arr[offset + i]) s++;
                }
                x = (x + s) * i;
            }
            return x;
        }

        private static void SetPerm(byte[] arr, int offset, int len, int idx)
        {
            bool[] used = new bool[len];
            for (int i = len - 1; i >= 0; i--)
            {
                int fact = 1;
                for (int f = 2; f <= i; f++) fact *= f;
                int k = idx / fact;
                idx %= fact;

                int val = len - 1;
                while (k >= 0)
                {
                    if (!used[val])
                    {
                        if (k == 0) break;
                        k--;
                    }
                    val--;
                }
                used[val] = true;
                arr[offset + i] = (byte)(offset + val);
            }
        }
    }

    /// <summary>
    /// Compiles standard 6-face Rubik moves into strictly 4-pusher {R, L, F, B} commands matching RobotGiaiRubik.ino.hex.
    /// </summary>
    public static class RLBFCompiler4Pusher
    {
        /// <summary>
        /// Biên dịch lời giải 6 mặt sang giao thức Cơ Cấu 2 Trục Kẹp Luân Phiên (KHÔNG LẬT KHỐI):
        /// - Trục 1 (A1 - Kẹp Trắng U & Vàng D): mở khóa xoay R, L, F, B
        /// - Trục 2 (A2 - Kẹp Đỏ R & Cam L): mở khóa xoay U, D, F, B
        /// - Tối ưu hình học: F và B vuông góc với cả 2 trục nên giữ nguyên trục hiện tại (0 chuyển trục thừa).
        /// </summary>
        public static CompiledRLBFResult CompileTo4Pusher(List<string> standardMoves, string initialState54)
        {
            CompiledRLBFResult result = new CompiledRLBFResult();
            if (standardMoves != null)
            {
                result.OriginalMoves = new List<string>(standardMoves);
                result.OriginalMovesText = string.Join(" ", standardMoves.ToArray());
            }

            string simState = initialState54;
            string currentAxis = "A1"; // Mặc định ban đầu: Kẹp Trắng U - Vàng D (mở khóa R, L, F, B)
            bool axisEmitted = false;
            int currentMs = 0;
            int stepIndex = 1;

            if (standardMoves != null)
            {
                for (int i = 0; i < standardMoves.Count; i++)
                {
                    string origMove = standardMoves[i] == null ? string.Empty : standardMoves[i].Trim();
                    if (origMove.Length == 0) continue;

                    char face = char.ToUpper(origMove[0]);
                    if (face != 'R' && face != 'L' && face != 'F' && face != 'B' && face != 'U' && face != 'D')
                    {
                        continue;
                    }

                    int pushCount = 1;
                    if (origMove.EndsWith("2")) pushCount = 2;
                    else if (origMove.EndsWith("'") || origMove.EndsWith("3")) pushCount = 3;

                    string axisSwitchNote = string.Empty;
                    string neededAxis = currentAxis;
                    if (face == 'R' || face == 'L')
                    {
                        neededAxis = "A1";
                    }
                    else if (face == 'U' || face == 'D')
                    {
                        neededAxis = "A2";
                    }
                    // F và B tự do ở cả A1 lẫn A2 -> giữ nguyên currentAxis!

                    if (!axisEmitted || !string.Equals(neededAxis, currentAxis, StringComparison.Ordinal))
                    {
                        currentAxis = neededAxis;
                        axisEmitted = true;
                        result.PusherTokens.Add(currentAxis);
                        axisSwitchNote = string.Equals(currentAxis, "A1", StringComparison.Ordinal)
                            ? "A1: Kẹp Trắng U-Vàng D"
                            : "A2: Kẹp Đỏ R-Cam L";
                        currentMs += RobotHardware.AXIS_SWITCH_MS;
                    }

                    string token = pushCount == 1
                        ? face.ToString()
                        : string.Format("{0}{1}", face, pushCount);

                    int durationMs = pushCount * RobotHardware.PUSH_CYCLE_MS;
                    PusherStep step = new PusherStep
                    {
                        StepIndex = stepIndex++,
                        OriginalMove = origMove,
                        PreRotation = axisSwitchNote,
                        Face = face,
                        Token = token,
                        PinName = RobotHardware.GetPinForFace(face),
                        PushCount = pushCount,
                        DurationMs = durationMs,
                        StartMs = currentMs,
                        EndMs = currentMs + durationMs
                    };

                    result.Steps.Add(step);
                    result.PusherTokens.Add(token);
                    result.TotalPushes += pushCount;
                    currentMs += durationMs;

                    if (!string.IsNullOrEmpty(simState) && simState.Length == 54)
                    {
                        string cwMove = face.ToString();
                        for (int p = 0; p < pushCount; p++)
                        {
                            simState = RubikSolver3x3.ApplyMove(simState, cwMove);
                        }
                    }
                }
            }

            result.TotalDurationMs = currentMs;
            result.SerialPayload = string.Join(" ", result.PusherTokens.ToArray());
            // Kiểm chứng nghiêm ngặt: KHÔNG cho phép lật khối ảo (allowWholeCubeRotation = false)!
            result.VerifiedSolved = !string.IsNullOrEmpty(simState) &&
                RubikSolver3x3.IsSolved(simState, false);

            return result;
        }
    }
}
