using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace RubikRobotDesktop
{
    public class MainForm : Form
    {
        private static readonly Color BgDark = Color.FromArgb(15, 23, 42);
        private static readonly Color BgCard = Color.FromArgb(30, 41, 59);
        private static readonly Color BgInput = Color.FromArgb(15, 23, 42);
        private static readonly Color BgTerminal = Color.FromArgb(9, 13, 22);
        private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);
        private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color AccentCyan = Color.FromArgb(34, 211, 238);
        private static readonly Color AccentGreen = Color.FromArgb(74, 222, 128);
        private static readonly Color AccentAmber = Color.FromArgb(251, 191, 36);
        private static readonly Color AccentRed = Color.FromArgb(248, 113, 113);
        private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);

        private readonly Random _rng = new Random();
        private string _currentState54 = RubikVision2D.SOLVED_STATE;
        private char _selectedPaintFace = 'F';
        private List<string> _currentSolutionMoves = new List<string>();
        private CompiledRLBFResult _compiledResult;
        private Bitmap _previewBitmap;

        // Column 1 controls
        private Button _btnOpenImage;
        private Button _btnGenScanDemo;
        private Button _btnSaveImage;
        private Button _btnResetSolved;
        private readonly Dictionary<char, Button> _paletteButtons = new Dictionary<char, Button>();
        private CubeNetPanel _cubeNetPanel;
        private PictureBox _picPreview;
        private TextBox _txtState54;
        private Label _lblValidation;

        // Column 2 controls
        private Button _btnScramble3x3;
        private Button _btnSolveCompile;
        private TextBox _txtStandardMoves;
        private TextBox _txtSerialPayload;
        private Button _btnCopySignal;
        private Label _lblCompileStats;
        private ListView _lvSteps;
        private Button _btnSimulateWave;
        private Label _lblWaveStatus;
        private WaveformPanel _waveformPanel;
        private Timer _waveTimer;
        private int _playbackMs;
        private bool _wavePlaying;

        // Column 3 controls
        private ComboBox _cmbComPort;
        private Button _btnRefreshPorts;
        private ComboBox _cmbBaudRate;
        private Button _btnConnectUsb;
        private Label _lblUsbStatus;
        private Button _btnSendSolution;
        private Button _btnQuickTest;
        private Button _btnEmergencyStop;
        private TextBox _txtManualCmd;
        private Button _btnSendManual;
        private Button _btnClearLog;
        private TextBox _txtSerialMonitor;
        private Button _btnViewIno;
        private Label _lblHeaderStatus;

        // Serial & Virtual Nano simulator
        private SerialPort _serialPort;
        private bool _usbConnected;
        private Timer _virtualNanoTimer;
        private readonly Queue<VirtualPulseEvent> _virtualQueue = new Queue<VirtualPulseEvent>();
        private bool _virtualRunning;
        private string _rxPartialBuffer = string.Empty;

        public MainForm()
        {
            InitializeLayout();
            InitializeTimers();
            RefreshComPorts();
            UpdatePreviewBitmapFromState();
            ValidateAndRefreshStateUi();
            AppendSerialBanner();
        }

        private void InitializeLayout()
        {
            Text = "🤖 RUBIK 3x3 ROBOT CONTROLLER — 4 THANH ĐẨY (R-L-F-B) & USB ARDUINO NANO";
            Size = new Size(1380, 840);
            MinimumSize = new Size(1240, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgDark;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Top Header Banner
            Panel headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 76;
            headerPanel.BackColor = Color.FromArgb(20, 30, 50);
            headerPanel.Padding = new Padding(14, 8, 14, 8);

            Panel headerBorder = new Panel();
            headerBorder.Dock = DockStyle.Bottom;
            headerBorder.Height = 2;
            headerBorder.BackColor = Color.FromArgb(34, 211, 238);
            headerPanel.Controls.Add(headerBorder);

            Label lblTitle = new Label();
            lblTitle.Text = "🤖 RUBIK 3x3 ROBOT CONTROLLER — 4 THANH ĐẨY (R-L-F-B) & USB ARDUINO NANO";
            lblTitle.Font = new Font("Segoe UI", 12.5F, FontStyle.Bold);
            lblTitle.ForeColor = AccentCyan;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(14, 8);
            headerPanel.Controls.Add(lblTitle);

            Label lblPinMap = new Label();
            lblPinMap.Text = "[D5 = Servo R]  [D6 = Servo L]  [D9 = Servo F]  [D10 = Servo B]  [D2 = Nút START]  [D3 = Nút STOP]  [USB Serial: 9600 bps | Góc đẩy: 75° (150ms)]";
            lblPinMap.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lblPinMap.ForeColor = AccentAmber;
            lblPinMap.AutoSize = true;
            lblPinMap.Location = new Point(14, 39);
            headerPanel.Controls.Add(lblPinMap);

            _lblHeaderStatus = new Label();
            _lblHeaderStatus.Text = "Sẵn sàng";
            _lblHeaderStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _lblHeaderStatus.ForeColor = AccentGreen;
            _lblHeaderStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblHeaderStatus.TextAlign = ContentAlignment.MiddleRight;
            _lblHeaderStatus.Size = new Size(280, 24);
            _lblHeaderStatus.Location = new Point(headerPanel.Width - 294, 10);
            headerPanel.Controls.Add(_lblHeaderStatus);

            // Main 3-Column Table
            TableLayoutPanel mainGrid = new TableLayoutPanel();
            mainGrid.Dock = DockStyle.Fill;
            mainGrid.ColumnCount = 3;
            mainGrid.RowCount = 1;
            mainGrid.BackColor = BgDark;
            mainGrid.Padding = new Padding(8);
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37F));
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel col1 = BuildLeftColumn();
            Panel col2 = BuildCenterColumn();
            Panel col3 = BuildRightColumn();

            mainGrid.Controls.Add(col1, 0, 0);
            mainGrid.Controls.Add(col2, 1, 0);
            mainGrid.Controls.Add(col3, 2, 0);

            Controls.Add(mainGrid);
            Controls.Add(headerPanel);
        }

        private Panel CreateCardPanel(string title)
        {
            Panel card = new Panel();
            card.Dock = DockStyle.Fill;
            card.BackColor = BgCard;
            card.Margin = new Padding(5);
            card.Padding = new Padding(10);

            Label lblHeader = new Label();
            lblHeader.Dock = DockStyle.Top;
            lblHeader.Height = 28;
            lblHeader.Text = title;
            lblHeader.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblHeader.ForeColor = AccentCyan;
            lblHeader.TextAlign = ContentAlignment.MiddleLeft;

            Panel divider = new Panel();
            divider.Dock = DockStyle.Top;
            divider.Height = 1;
            divider.BackColor = BorderColor;

            card.Controls.Add(divider);
            card.Controls.Add(lblHeader);
            return card;
        }

        private Button CreateStyledButton(string text, Color backColor, Color borderColor, Color foreColor)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = backColor;
            btn.ForeColor = foreColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = borderColor;
            btn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Margin = new Padding(3);
            btn.Dock = DockStyle.Fill;
            return btn;
        }

        private Panel BuildLeftColumn()
        {
            Panel card = CreateCardPanel("1. NHẬN DIỆN ẢNH 2D SCRAMBLE & CHỈNH SỬA 54 Ô");

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.Padding = new Padding(0, 6, 0, 0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));  // 2x2 buttons
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));  // 6-color palette
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));   // interactive 2D net
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));   // preview bitmap + label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));  // 54-char state string
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));  // validation status

            // Row 0: 2x2 buttons
            TableLayoutPanel btnGrid = new TableLayoutPanel();
            btnGrid.Dock = DockStyle.Fill;
            btnGrid.ColumnCount = 2;
            btnGrid.RowCount = 2;
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            btnGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            btnGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            _btnOpenImage = CreateStyledButton("📂 Mở Ảnh 2D Scramble (.png/.jpg/.bmp)", Color.FromArgb(30, 58, 138), AccentBlue, TextPrimary);
            _btnGenScanDemo = CreateStyledButton("🎨 Tạo & Quét Ảnh 2D Scramble Mẫu", Color.FromArgb(14, 116, 144), AccentCyan, TextPrimary);
            _btnSaveImage = CreateStyledButton("💾 Lưu Ảnh 2D (.png)", Color.FromArgb(51, 65, 85), BorderColor, TextPrimary);
            _btnResetSolved = CreateStyledButton("🔄 Reset Solved", Color.FromArgb(51, 65, 85), BorderColor, TextPrimary);

            _btnOpenImage.Click += OnOpenImageClick;
            _btnGenScanDemo.Click += OnGenScanDemoClick;
            _btnSaveImage.Click += OnSaveImageClick;
            _btnResetSolved.Click += OnResetSolvedClick;

            btnGrid.Controls.Add(_btnOpenImage, 0, 0);
            btnGrid.Controls.Add(_btnGenScanDemo, 1, 0);
            btnGrid.Controls.Add(_btnSaveImage, 0, 1);
            btnGrid.Controls.Add(_btnResetSolved, 1, 1);

            // Row 1: 6-color palette bar
            TableLayoutPanel paletteRow = new TableLayoutPanel();
            paletteRow.Dock = DockStyle.Fill;
            paletteRow.ColumnCount = 6;
            paletteRow.RowCount = 1;
            paletteRow.Padding = new Padding(0, 4, 0, 4);
            for (int c = 0; c < 6; c++)
            {
                paletteRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
            }

            char[] faces = new char[] { 'U', 'R', 'F', 'D', 'L', 'B' };
            string[] names = new string[] { "U-Trắng", "R-Đỏ", "F-Lá", "D-Vàng", "L-Cam", "B-Dương" };
            for (int i = 0; i < faces.Length; i++)
            {
                char face = faces[i];
                Color faceColor = RubikVision2D.GetFaceColor(face);
                Button palBtn = new Button();
                palBtn.Text = names[i];
                palBtn.Tag = face;
                palBtn.Dock = DockStyle.Fill;
                palBtn.FlatStyle = FlatStyle.Flat;
                palBtn.BackColor = faceColor;
                palBtn.ForeColor = (face == 'U' || face == 'D') ? Color.Black : Color.White;
                palBtn.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                palBtn.Margin = new Padding(2);
                palBtn.Cursor = Cursors.Hand;
                palBtn.Click += OnPaletteButtonClick;
                _paletteButtons[face] = palBtn;
                paletteRow.Controls.Add(palBtn, i, 0);
            }
            UpdatePaletteSelectionVisuals();

            // Row 2: Interactive 2D Cross Net
            _cubeNetPanel = new CubeNetPanel();
            _cubeNetPanel.Dock = DockStyle.Fill;
            _cubeNetPanel.State54 = _currentState54;
            _cubeNetPanel.StickerClicked += OnCubeNetStickerClicked;

            // Row 3: Bitmap Preview Box
            Panel previewContainer = new Panel();
            previewContainer.Dock = DockStyle.Fill;
            previewContainer.BackColor = BgInput;
            previewContainer.Padding = new Padding(4);

            Label lblPreviewCap = new Label();
            lblPreviewCap.Dock = DockStyle.Top;
            lblPreviewCap.Height = 18;
            lblPreviewCap.Text = "Ảnh 2D Scramble Đang Quét / Nhận Diện:";
            lblPreviewCap.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            lblPreviewCap.ForeColor = TextMuted;

            _picPreview = new PictureBox();
            _picPreview.Dock = DockStyle.Fill;
            _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _picPreview.BackColor = Color.FromArgb(10, 15, 28);

            previewContainer.Controls.Add(_picPreview);
            previewContainer.Controls.Add(lblPreviewCap);

            // Row 4: State54 TextBox
            _txtState54 = new TextBox();
            _txtState54.Dock = DockStyle.Fill;
            _txtState54.ReadOnly = true;
            _txtState54.BackColor = BgInput;
            _txtState54.ForeColor = AccentCyan;
            _txtState54.BorderStyle = BorderStyle.FixedSingle;
            _txtState54.Font = new Font("Consolas", 8.5F, FontStyle.Regular);

            // Row 5: Validation Label
            _lblValidation = new Label();
            _lblValidation.Dock = DockStyle.Fill;
            _lblValidation.TextAlign = ContentAlignment.MiddleLeft;
            _lblValidation.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);

            layout.Controls.Add(btnGrid, 0, 0);
            layout.Controls.Add(paletteRow, 0, 1);
            layout.Controls.Add(_cubeNetPanel, 0, 2);
            layout.Controls.Add(previewContainer, 0, 3);
            layout.Controls.Add(_txtState54, 0, 4);
            layout.Controls.Add(_lblValidation, 0, 5);

            card.Controls.Add(layout);
            layout.BringToFront();
            return card;
        }

        private Panel BuildCenterColumn()
        {
            Panel card = CreateCardPanel("2. BỘ GIẢI 3x3 & BIÊN DỊCH TÍN HIỆU 4 THANH ĐẨY (R, L, F, B)");

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 7;
            layout.Padding = new Padding(0, 6, 0, 0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));  // scramble & solve buttons
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));  // standard 6-face moves
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));  // 4-pusher signal payload + copy
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));  // compile stats
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // steps ListView
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));  // waveform header + simulate button
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // 4-channel waveform panel

            // Row 0: Scramble & Solve buttons
            TableLayoutPanel topBtns = new TableLayoutPanel();
            topBtns.Dock = DockStyle.Fill;
            topBtns.ColumnCount = 2;
            topBtns.RowCount = 1;
            topBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            topBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));

            _btnScramble3x3 = CreateStyledButton("🎲 Trộn Rubik 3x3 Ngẫu Nhiên", Color.FromArgb(67, 56, 202), Color.FromArgb(129, 140, 248), TextPrimary);
            _btnSolveCompile = CreateStyledButton("⚡ GIẢI & XUẤT TÍN HIỆU (R, L, F, B)", Color.FromArgb(21, 128, 61), AccentGreen, Color.White);

            _btnScramble3x3.Click += OnScramble3x3Click;
            _btnSolveCompile.Click += OnSolveCompileClick;

            topBtns.Controls.Add(_btnScramble3x3, 0, 0);
            topBtns.Controls.Add(_btnSolveCompile, 1, 0);

            // Row 1: Standard 6-face solution
            Panel stdBox = new Panel();
            stdBox.Dock = DockStyle.Fill;
            Label lblStd = new Label();
            lblStd.Dock = DockStyle.Top;
            lblStd.Height = 18;
            lblStd.Text = "Bước giải 6 mặt chuẩn (U, D, R, L, F, B):";
            lblStd.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblStd.ForeColor = TextMuted;

            _txtStandardMoves = new TextBox();
            _txtStandardMoves.Dock = DockStyle.Fill;
            _txtStandardMoves.Multiline = true;
            _txtStandardMoves.ReadOnly = true;
            _txtStandardMoves.ScrollBars = ScrollBars.Vertical;
            _txtStandardMoves.BackColor = BgInput;
            _txtStandardMoves.ForeColor = TextPrimary;
            _txtStandardMoves.BorderStyle = BorderStyle.FixedSingle;
            _txtStandardMoves.Font = new Font("Consolas", 9F, FontStyle.Regular);

            stdBox.Controls.Add(_txtStandardMoves);
            stdBox.Controls.Add(lblStd);

            // Row 2: 4-pusher signal payload
            Panel sigBox = new Panel();
            sigBox.Dock = DockStyle.Fill;
            Label lblSig = new Label();
            lblSig.Dock = DockStyle.Top;
            lblSig.Height = 18;
            lblSig.Text = "Chuỗi Tín Hiệu 4 Thanh Đẩy chuẩn .ino.hex (chỉ dùng R, L, F, B - Nhịp 1, 2, 3):";
            lblSig.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblSig.ForeColor = AccentCyan;

            TableLayoutPanel sigRow = new TableLayoutPanel();
            sigRow.Dock = DockStyle.Fill;
            sigRow.ColumnCount = 2;
            sigRow.RowCount = 1;
            sigRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76F));
            sigRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));

            _txtSerialPayload = new TextBox();
            _txtSerialPayload.Dock = DockStyle.Fill;
            _txtSerialPayload.Multiline = true;
            _txtSerialPayload.ReadOnly = true;
            _txtSerialPayload.ScrollBars = ScrollBars.Vertical;
            _txtSerialPayload.BackColor = BgTerminal;
            _txtSerialPayload.ForeColor = AccentGreen;
            _txtSerialPayload.BorderStyle = BorderStyle.FixedSingle;
            _txtSerialPayload.Font = new Font("Consolas", 10F, FontStyle.Bold);

            _btnCopySignal = CreateStyledButton("📋 Copy Tín Hiệu", Color.FromArgb(51, 65, 85), AccentCyan, AccentCyan);
            _btnCopySignal.Click += OnCopySignalClick;

            sigRow.Controls.Add(_txtSerialPayload, 0, 0);
            sigRow.Controls.Add(_btnCopySignal, 1, 0);

            sigBox.Controls.Add(sigRow);
            sigBox.Controls.Add(lblSig);

            // Row 3: Compile stats
            _lblCompileStats = new Label();
            _lblCompileStats.Dock = DockStyle.Fill;
            _lblCompileStats.Text = "Chưa biên dịch. Nhấn 'Trộn Rubik' hoặc 'Giải & Xuất Tín Hiệu'.";
            _lblCompileStats.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblCompileStats.ForeColor = TextMuted;
            _lblCompileStats.TextAlign = ContentAlignment.MiddleLeft;

            // Row 4: Step table ListView
            _lvSteps = new ListView();
            _lvSteps.Dock = DockStyle.Fill;
            _lvSteps.View = View.Details;
            _lvSteps.FullRowSelect = true;
            _lvSteps.GridLines = true;
            _lvSteps.MultiSelect = false;
            _lvSteps.BackColor = BgInput;
            _lvSteps.ForeColor = TextPrimary;
            _lvSteps.Font = new Font("Consolas", 8.5F, FontStyle.Regular);
            _lvSteps.Columns.Add("#", 34);
            _lvSteps.Columns.Add("Gốc", 56);
            _lvSteps.Columns.Add("Mặt Đẩy", 64);
            _lvSteps.Columns.Add("Chân Nano", 72);
            _lvSteps.Columns.Add("Lệnh Xuất", 74);
            _lvSteps.Columns.Add("Số Nhịp (75°)", 90);
            _lvSteps.Columns.Add("Thời Gian", 78);

            // Row 5: Waveform header
            TableLayoutPanel waveHeader = new TableLayoutPanel();
            waveHeader.Dock = DockStyle.Fill;
            waveHeader.ColumnCount = 2;
            waveHeader.RowCount = 1;
            waveHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            waveHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            _lblWaveStatus = new Label();
            _lblWaveStatus.Dock = DockStyle.Fill;
            _lblWaveStatus.Text = "📊 Phân Tích Xung 4 Kênh (75° 150ms / 0° 150ms)";
            _lblWaveStatus.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblWaveStatus.ForeColor = AccentCyan;
            _lblWaveStatus.TextAlign = ContentAlignment.MiddleLeft;

            _btnSimulateWave = CreateStyledButton("▶️ Mô Phỏng Xung 4 Kênh", Color.FromArgb(14, 116, 144), AccentCyan, TextPrimary);
            _btnSimulateWave.Click += OnSimulateWaveClick;

            waveHeader.Controls.Add(_lblWaveStatus, 0, 0);
            waveHeader.Controls.Add(_btnSimulateWave, 1, 0);

            // Row 6: Waveform analyzer panel
            _waveformPanel = new WaveformPanel();
            _waveformPanel.Dock = DockStyle.Fill;

            layout.Controls.Add(topBtns, 0, 0);
            layout.Controls.Add(stdBox, 0, 1);
            layout.Controls.Add(sigBox, 0, 2);
            layout.Controls.Add(_lblCompileStats, 0, 3);
            layout.Controls.Add(_lvSteps, 0, 4);
            layout.Controls.Add(waveHeader, 0, 5);
            layout.Controls.Add(_waveformPanel, 0, 6);

            card.Controls.Add(layout);
            layout.BringToFront();
            return card;
        }

        private Panel BuildRightColumn()
        {
            Panel card = CreateCardPanel("3. KẾT NỐI CỔNG USB ARDUINO NANO & SERIAL MONITOR");

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.Padding = new Padding(0, 6, 0, 0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));  // USB connection bar + status
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F)); // Action buttons (Send, Test, Stop)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));  // Manual command input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  // Monitor label + Clear button
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Serial Monitor TextBox
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));  // View .ino source button

            // Row 0: USB connection bar
            Panel usbPanel = new Panel();
            usbPanel.Dock = DockStyle.Fill;

            TableLayoutPanel usbBar = new TableLayoutPanel();
            usbBar.Dock = DockStyle.Top;
            usbBar.Height = 36;
            usbBar.ColumnCount = 4;
            usbBar.RowCount = 1;
            usbBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            usbBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            usbBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            usbBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            _cmbComPort = new ComboBox();
            _cmbComPort.Dock = DockStyle.Fill;
            _cmbComPort.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbComPort.BackColor = BgInput;
            _cmbComPort.ForeColor = TextPrimary;
            _cmbComPort.Margin = new Padding(2, 5, 2, 2);

            _btnRefreshPorts = CreateStyledButton("🔄 Quét USB", Color.FromArgb(51, 65, 85), BorderColor, TextPrimary);
            _btnRefreshPorts.Click += OnRefreshPortsClick;

            _cmbBaudRate = new ComboBox();
            _cmbBaudRate.Dock = DockStyle.Fill;
            _cmbBaudRate.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbBaudRate.BackColor = BgInput;
            _cmbBaudRate.ForeColor = TextPrimary;
            _cmbBaudRate.Items.Add("9600");
            _cmbBaudRate.Items.Add("115200");
            _cmbBaudRate.SelectedIndex = 0;
            _cmbBaudRate.Margin = new Padding(2, 5, 2, 2);

            _btnConnectUsb = CreateStyledButton("🔌 Kết Nối USB", Color.FromArgb(30, 58, 138), AccentBlue, TextPrimary);
            _btnConnectUsb.Click += OnConnectUsbClick;

            usbBar.Controls.Add(_cmbComPort, 0, 0);
            usbBar.Controls.Add(_btnRefreshPorts, 1, 0);
            usbBar.Controls.Add(_cmbBaudRate, 2, 0);
            usbBar.Controls.Add(_btnConnectUsb, 3, 0);

            _lblUsbStatus = new Label();
            _lblUsbStatus.Dock = DockStyle.Bottom;
            _lblUsbStatus.Height = 24;
            _lblUsbStatus.Text = "⚪ Chế độ Mô Phỏng Arduino Nano Ảo (Chưa kết nối cổng COM)";
            _lblUsbStatus.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblUsbStatus.ForeColor = AccentAmber;
            _lblUsbStatus.TextAlign = ContentAlignment.MiddleLeft;

            usbPanel.Controls.Add(_lblUsbStatus);
            usbPanel.Controls.Add(usbBar);

            // Row 1: Action buttons
            TableLayoutPanel actionGrid = new TableLayoutPanel();
            actionGrid.Dock = DockStyle.Fill;
            actionGrid.ColumnCount = 2;
            actionGrid.RowCount = 2;
            actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            actionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
            actionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));

            _btnSendSolution = CreateStyledButton("🚀 GỬI LỆNH GIẢI XUỐNG MẠCH QUA USB", Color.FromArgb(21, 128, 61), AccentGreen, Color.White);
            _btnSendSolution.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnSendSolution.Click += OnSendSolutionClick;

            _btnQuickTest = CreateStyledButton("🧪 Test Nhanh (R L2 F3 B)", Color.FromArgb(14, 116, 144), AccentCyan, TextPrimary);
            _btnQuickTest.Click += OnQuickTestClick;

            _btnEmergencyStop = CreateStyledButton("🛑 DỪNG KHẨN CẤP (!)", Color.FromArgb(153, 27, 27), AccentRed, Color.White);
            _btnEmergencyStop.Click += OnEmergencyStopClick;

            actionGrid.Controls.Add(_btnSendSolution, 0, 0);
            actionGrid.SetColumnSpan(_btnSendSolution, 2);
            actionGrid.Controls.Add(_btnQuickTest, 0, 1);
            actionGrid.Controls.Add(_btnEmergencyStop, 1, 1);

            // Row 2: Manual command input
            TableLayoutPanel manualBar = new TableLayoutPanel();
            manualBar.Dock = DockStyle.Fill;
            manualBar.ColumnCount = 2;
            manualBar.RowCount = 1;
            manualBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76F));
            manualBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));

            _txtManualCmd = new TextBox();
            _txtManualCmd.Dock = DockStyle.Fill;
            _txtManualCmd.BackColor = BgInput;
            _txtManualCmd.ForeColor = TextPrimary;
            _txtManualCmd.BorderStyle = BorderStyle.FixedSingle;
            _txtManualCmd.Font = new Font("Consolas", 9.5F, FontStyle.Regular);
            _txtManualCmd.Margin = new Padding(2, 4, 2, 2);
            _txtManualCmd.KeyDown += OnManualCmdKeyDown;

            _btnSendManual = CreateStyledButton("Gửi", Color.FromArgb(51, 65, 85), AccentCyan, TextPrimary);
            _btnSendManual.Click += OnSendManualClick;

            manualBar.Controls.Add(_txtManualCmd, 0, 0);
            manualBar.Controls.Add(_btnSendManual, 1, 0);

            // Row 3: Serial Monitor Header
            TableLayoutPanel monHeader = new TableLayoutPanel();
            monHeader.Dock = DockStyle.Fill;
            monHeader.ColumnCount = 2;
            monHeader.RowCount = 1;
            monHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
            monHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));

            Label lblMon = new Label();
            lblMon.Dock = DockStyle.Fill;
            lblMon.Text = "📟 Live Serial Monitor (9600 bps):";
            lblMon.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblMon.ForeColor = AccentGreen;
            lblMon.TextAlign = ContentAlignment.MiddleLeft;

            _btnClearLog = CreateStyledButton("🧹 Xóa Log", Color.FromArgb(30, 41, 59), BorderColor, TextMuted);
            _btnClearLog.Click += OnClearLogClick;

            monHeader.Controls.Add(lblMon, 0, 0);
            monHeader.Controls.Add(_btnClearLog, 1, 0);

            // Row 4: Serial Monitor TextBox
            _txtSerialMonitor = new TextBox();
            _txtSerialMonitor.Dock = DockStyle.Fill;
            _txtSerialMonitor.Multiline = true;
            _txtSerialMonitor.ReadOnly = true;
            _txtSerialMonitor.ScrollBars = ScrollBars.Vertical;
            _txtSerialMonitor.BackColor = BgTerminal;
            _txtSerialMonitor.ForeColor = AccentGreen;
            _txtSerialMonitor.BorderStyle = BorderStyle.FixedSingle;
            _txtSerialMonitor.Font = new Font("Consolas", 9F, FontStyle.Regular);

            // Row 5: View .ino Source button
            _btnViewIno = CreateStyledButton("📜 Xem Code Arduino (.ino) Dịch Ngược Từ File .hex", Color.FromArgb(51, 65, 85), AccentAmber, AccentAmber);
            _btnViewIno.Click += OnViewInoClick;

            layout.Controls.Add(usbPanel, 0, 0);
            layout.Controls.Add(actionGrid, 0, 1);
            layout.Controls.Add(manualBar, 0, 2);
            layout.Controls.Add(monHeader, 0, 3);
            layout.Controls.Add(_txtSerialMonitor, 0, 4);
            layout.Controls.Add(_btnViewIno, 0, 5);

            card.Controls.Add(layout);
            layout.BringToFront();
            return card;
        }

        private void InitializeTimers()
        {
            _waveTimer = new Timer();
            _waveTimer.Interval = 30;
            _waveTimer.Tick += OnWaveTimerTick;

            _virtualNanoTimer = new Timer();
            _virtualNanoTimer.Interval = 180;
            _virtualNanoTimer.Tick += OnVirtualNanoTimerTick;
        }

        // ====================================================================
        // Panel 1 Handlers: 2D Image Vision & Interactive Net
        // ====================================================================

        private void OnOpenImageClick(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn Ảnh 2D Scramble (.png / .jpg / .bmp)";
                dlg.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    Bitmap loaded = new Bitmap(dlg.FileName);
                    SetPreviewBitmap(loaded);
                    string parsed = RubikVision2D.ParseScramble2DBitmap(loaded);
                    _currentState54 = parsed;
                    _cubeNetPanel.State54 = _currentState54;
                    ValidateAndRefreshStateUi();
                    AppendSerialLog(string.Format("[VISION] Đã nạp & quét ảnh: {0}", Path.GetFileName(dlg.FileName)));
                    RunSolverAndCompiler(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Không thể đọc hoặc quét ảnh 2D Scramble:\n" + ex.Message, "Lỗi Ảnh 2D", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnGenScanDemoClick(object sender, EventArgs e)
        {
            List<string> scrambleMoves;
            string scrambled = RubikSolver3x3.GenerateScramble(18, _rng, out scrambleMoves);
            Bitmap bmp = RubikVision2D.RenderScramble2DBitmap(scrambled, 440, 330);
            SetPreviewBitmap(bmp);

            string parsed = RubikVision2D.ParseScramble2DBitmap(bmp);
            _currentState54 = parsed;
            _cubeNetPanel.State54 = _currentState54;
            ValidateAndRefreshStateUi();
            AppendSerialLog(string.Format("[VISION] Tạo & Quét ảnh 2D mẫu ({0} bước trộn): {1}", scrambleMoves.Count, string.Join(" ", scrambleMoves.ToArray())));
            RunSolverAndCompiler(true);
        }

        private void OnSaveImageClick(object sender, EventArgs e)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Lưu Ảnh 2D Cross Net (.png)";
                dlg.Filter = "PNG Image (*.png)|*.png";
                dlg.FileName = "rubik_2d_scramble.png";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    using (Bitmap exportBmp = RubikVision2D.RenderScramble2DBitmap(_currentState54, 640, 480))
                    {
                        exportBmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    AppendSerialLog("[VISION] Đã lưu ảnh 2D PNG: " + dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Lỗi khi lưu ảnh PNG:\n" + ex.Message, "Lỗi Lưu Ảnh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnResetSolvedClick(object sender, EventArgs e)
        {
            StopWaveSimulation();
            _currentState54 = RubikVision2D.SOLVED_STATE;
            _cubeNetPanel.State54 = _currentState54;
            UpdatePreviewBitmapFromState();
            ValidateAndRefreshStateUi();
            _currentSolutionMoves = new List<string>();
            _compiledResult = null;
            _txtStandardMoves.Text = string.Empty;
            _txtSerialPayload.Text = string.Empty;
            _lvSteps.Items.Clear();
            _waveformPanel.SetCompiledResult(null, 0);
            _lblCompileStats.Text = "Khối Rubik đã ở trạng thái Solved (54/54 ô chuẩn).";
            _lblCompileStats.ForeColor = AccentGreen;
            _lblHeaderStatus.Text = "Đã Reset Solved";
            _lblHeaderStatus.ForeColor = AccentGreen;
        }

        private void OnPaletteButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag is char)
            {
                _selectedPaintFace = (char)btn.Tag;
                UpdatePaletteSelectionVisuals();
            }
        }

        private void UpdatePaletteSelectionVisuals()
        {
            foreach (KeyValuePair<char, Button> kvp in _paletteButtons)
            {
                if (kvp.Key == _selectedPaintFace)
                {
                    kvp.Value.FlatAppearance.BorderSize = 3;
                    kvp.Value.FlatAppearance.BorderColor = AccentCyan;
                }
                else
                {
                    kvp.Value.FlatAppearance.BorderSize = 1;
                    kvp.Value.FlatAppearance.BorderColor = BorderColor;
                }
            }
        }

        private void OnCubeNetStickerClicked(object sender, StickerClickEventArgs e)
        {
            if (e.StickerIndex < 0 || e.StickerIndex >= 54)
            {
                return;
            }

            char[] chars = _currentState54.ToCharArray();
            chars[e.StickerIndex] = _selectedPaintFace;
            _currentState54 = new string(chars);
            _cubeNetPanel.State54 = _currentState54;
            UpdatePreviewBitmapFromState();
            ValidateAndRefreshStateUi();
        }

        private void SetPreviewBitmap(Bitmap newBitmap)
        {
            Bitmap old = _previewBitmap;
            _previewBitmap = newBitmap;
            _picPreview.Image = _previewBitmap;
            if (old != null)
            {
                old.Dispose();
            }
        }

        private void UpdatePreviewBitmapFromState()
        {
            Bitmap bmp = RubikVision2D.RenderScramble2DBitmap(_currentState54, 400, 300);
            SetPreviewBitmap(bmp);
        }

        private bool ValidateAndRefreshStateUi()
        {
            _txtState54.Text = _currentState54;
            string errorMsg;
            bool valid = RubikVision2D.ValidateState54(_currentState54, out errorMsg);
            if (valid)
            {
                _lblValidation.Text = "✅ Hợp lệ 54 ô (9 ô/màu, đúng 6 tâm)";
                _lblValidation.ForeColor = AccentGreen;
            }
            else
            {
                _lblValidation.Text = "❌ Lỗi: " + errorMsg;
                _lblValidation.ForeColor = AccentRed;
            }
            return valid;
        }

        // ====================================================================
        // Panel 2 Handlers: 3x3 Solver & 4-Pusher Compiler + Waveform
        // ====================================================================

        private void OnScramble3x3Click(object sender, EventArgs e)
        {
            List<string> scrambleMoves;
            _currentState54 = RubikSolver3x3.GenerateScramble(18, _rng, out scrambleMoves);
            _cubeNetPanel.State54 = _currentState54;
            UpdatePreviewBitmapFromState();
            ValidateAndRefreshStateUi();
            AppendSerialLog(string.Format("[SCRAMBLE] Đã trộn 18 bước: {0}", string.Join(" ", scrambleMoves.ToArray())));
            RunSolverAndCompiler(true);
        }

        private void OnSolveCompileClick(object sender, EventArgs e)
        {
            RunSolverAndCompiler(false);
        }

        private void RunSolverAndCompiler(bool silentOnSolved)
        {
            StopWaveSimulation();

            string errMsg;
            if (!RubikVision2D.ValidateState54(_currentState54, out errMsg))
            {
                _lblCompileStats.Text = "❌ Trạng thái 54 ô chưa hợp lệ: " + errMsg;
                _lblCompileStats.ForeColor = AccentRed;
                if (!silentOnSolved)
                {
                    MessageBox.Show(this, "Trạng thái 54 ô chưa hợp lệ để giải:\n" + errMsg, "Lỗi Trạng Thái Rubik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            if (RubikSolver3x3.IsSolved(_currentState54, true))
            {
                _currentSolutionMoves = new List<string>();
                _compiledResult = new CompiledRLBFResult();
                _txtStandardMoves.Text = "(Đã giải sẵn - 0 bước)";
                _txtSerialPayload.Text = string.Empty;
                _lvSteps.Items.Clear();
                _waveformPanel.SetCompiledResult(null, 0);
                _lblCompileStats.Text = "✅ Khối Rubik đã ở trạng thái Solved (0 lệnh đẩy).";
                _lblCompileStats.ForeColor = AccentGreen;
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                List<string> solution = RubikSolver3x3.Solve3x3(_currentState54);
                CompiledRLBFResult compiled = RLBFCompiler4Pusher.CompileTo4Pusher(solution, _currentState54);

                _currentSolutionMoves = solution;
                _compiledResult = compiled;

                _txtStandardMoves.Text = string.IsNullOrEmpty(compiled.OriginalMovesText)
                    ? string.Join(" ", solution.ToArray())
                    : compiled.OriginalMovesText;
                _txtSerialPayload.Text = compiled.SerialPayload;

                PopulateStepsListView(compiled);
                _waveformPanel.SetCompiledResult(compiled, 0);

                string verifyTag = compiled.VerifiedSolved ? "✅ Đã kiểm chứng Solved" : "⚠️ Chưa khớp Solved";
                _lblCompileStats.Text = string.Format("{0} | {1} bước | {2} nhịp đẩy (75°) | {3} ms",
                    verifyTag, compiled.Steps.Count, compiled.TotalPushes, compiled.TotalDurationMs);
                _lblCompileStats.ForeColor = compiled.VerifiedSolved ? AccentGreen : AccentAmber;

                _lblHeaderStatus.Text = string.Format("Sẵn sàng gửi {0} bước ({1} nhịp)", compiled.Steps.Count, compiled.TotalPushes);
                _lblHeaderStatus.ForeColor = AccentCyan;

                AppendSerialLog(string.Format("[COMPILER] Biên dịch thành công {0} bước -> {1} nhịp ({2} ms).",
                    compiled.Steps.Count, compiled.TotalPushes, compiled.TotalDurationMs));
            }
            catch (Exception ex)
            {
                _lblCompileStats.Text = "❌ Không giải được trạng thái hiện tại: " + ex.Message;
                _lblCompileStats.ForeColor = AccentRed;
                if (!silentOnSolved)
                {
                    MessageBox.Show(this, "Không thể giải trạng thái Rubik này (có thể góc/cạnh bị vặn vật lý không hợp lệ):\n" + ex.Message, "Lỗi Bộ Giải 3x3", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void PopulateStepsListView(CompiledRLBFResult compiled)
        {
            _lvSteps.BeginUpdate();
            _lvSteps.Items.Clear();
            if (compiled != null && compiled.Steps != null)
            {
                for (int i = 0; i < compiled.Steps.Count; i++)
                {
                    PusherStep s = compiled.Steps[i];
                    string origDisplay = string.IsNullOrEmpty(s.PreRotation)
                        ? s.OriginalMove
                        : string.Format("{0} ({1})", s.OriginalMove, s.PreRotation);

                    ListViewItem item = new ListViewItem(s.StepIndex.ToString());
                    item.SubItems.Add(origDisplay);
                    item.SubItems.Add(s.Face.ToString());
                    item.SubItems.Add(s.PinName);
                    item.SubItems.Add(s.Token);
                    item.SubItems.Add(string.Format("{0} nhịp", s.PushCount));
                    item.SubItems.Add(string.Format("{0} ms", s.DurationMs));
                    item.Tag = s;
                    _lvSteps.Items.Add(item);
                }
            }
            _lvSteps.EndUpdate();
        }

        private void OnCopySignalClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_txtSerialPayload.Text))
            {
                return;
            }
            Clipboard.SetText(_txtSerialPayload.Text);
            AppendSerialLog("[CLIPBOARD] Đã sao chép chuỗi tín hiệu 4 thanh đẩy.");
        }

        private void OnSimulateWaveClick(object sender, EventArgs e)
        {
            if (_wavePlaying)
            {
                StopWaveSimulation();
                return;
            }

            if (_compiledResult == null || _compiledResult.Steps == null || _compiledResult.Steps.Count == 0)
            {
                MessageBox.Show(this, "Chưa có chuỗi xung 4 kênh để mô phỏng. Hãy nhấn 'Giải & Xuất Tín Hiệu' trước.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _playbackMs = 0;
            _wavePlaying = true;
            _btnSimulateWave.Text = "⏹️ Dừng Mô Phỏng";
            _waveformPanel.SetCompiledResult(_compiledResult, 0);
            _waveTimer.Start();
        }

        private void StopWaveSimulation()
        {
            _wavePlaying = false;
            if (_waveTimer != null)
            {
                _waveTimer.Stop();
            }
            if (_btnSimulateWave != null)
            {
                _btnSimulateWave.Text = "▶️ Mô Phỏng Xung 4 Kênh";
            }
            if (_lblWaveStatus != null)
            {
                _lblWaveStatus.Text = "📊 Phân Tích Xung 4 Kênh (75° 150ms / 0° 150ms)";
            }
        }

        private void OnWaveTimerTick(object sender, EventArgs e)
        {
            if (!_wavePlaying || _compiledResult == null || _compiledResult.TotalDurationMs <= 0)
            {
                StopWaveSimulation();
                return;
            }

            _playbackMs += 30;
            if (_playbackMs >= _compiledResult.TotalDurationMs)
            {
                _playbackMs = _compiledResult.TotalDurationMs;
                _waveformPanel.SetPlaybackMs(_playbackMs);
                StopWaveSimulation();
                _lblWaveStatus.Text = string.Format("✅ Hoàn tất mô phỏng {0} ms", _compiledResult.TotalDurationMs);
                return;
            }

            _waveformPanel.SetPlaybackMs(_playbackMs);

            // Highlight active step in ListView
            for (int i = 0; i < _compiledResult.Steps.Count; i++)
            {
                PusherStep step = _compiledResult.Steps[i];
                if (_playbackMs >= step.StartMs && _playbackMs < step.EndMs)
                {
                    if (i < _lvSteps.Items.Count && !_lvSteps.Items[i].Selected)
                    {
                        _lvSteps.Items[i].Selected = true;
                        _lvSteps.EnsureVisible(i);
                    }
                    _lblWaveStatus.Text = string.Format("▶ {0} [{1}] ({2}/{3} ms)", step.Token, step.PinName, _playbackMs, _compiledResult.TotalDurationMs);
                    break;
                }
            }
        }

        // ====================================================================
        // Panel 3 Handlers: USB SerialPort & Virtual Nano Simulator
        // ====================================================================

        private void AppendSerialBanner()
        {
            AppendSerialLog("=== ROBOT GIAI RUBIK 4 THANH DAY SAN SANG ===");
            AppendSerialLog("Nhap lenh don (VD: R, R2, R3) hoac chuoi (VD: R L2 F3 B):");
        }

        private void AppendSerialLog(string line)
        {
            if (IsDisposed)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendSerialLog), line);
                return;
            }

            if (_txtSerialMonitor.TextLength > 24000)
            {
                _txtSerialMonitor.Text = _txtSerialMonitor.Text.Substring(_txtSerialMonitor.TextLength - 12000);
            }
            _txtSerialMonitor.AppendText(line + Environment.NewLine);
            _txtSerialMonitor.SelectionStart = _txtSerialMonitor.TextLength;
            _txtSerialMonitor.ScrollToCaret();
        }

        private void RefreshComPorts()
        {
            string previous = _cmbComPort.SelectedItem as string;
            _cmbComPort.Items.Clear();

            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);

            if (ports.Length == 0)
            {
                _cmbComPort.Items.Add("(Không có COM)");
                _cmbComPort.SelectedIndex = 0;
            }
            else
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    _cmbComPort.Items.Add(ports[i]);
                }
                int idx = !string.IsNullOrEmpty(previous) ? _cmbComPort.Items.IndexOf(previous) : -1;
                _cmbComPort.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }

        private void OnRefreshPortsClick(object sender, EventArgs e)
        {
            RefreshComPorts();
            AppendSerialLog("[USB] Đã quét lại danh sách cổng COM.");
        }

        private void OnConnectUsbClick(object sender, EventArgs e)
        {
            if (_usbConnected)
            {
                DisconnectUsb();
                return;
            }

            string portName = _cmbComPort.SelectedItem as string;
            if (string.IsNullOrEmpty(portName) || portName.StartsWith("("))
            {
                MessageBox.Show(this, "Không tìm thấy cổng COM vật lý. Bạn vẫn có thể chạy ở chế độ Mô Phỏng Arduino Nano Ảo!", "Chưa có cổng USB", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int baud = 9600;
            string baudText = _cmbBaudRate.SelectedItem as string;
            if (!string.IsNullOrEmpty(baudText))
            {
                int.TryParse(baudText, out baud);
            }

            try
            {
                _serialPort = new SerialPort(portName, baud, Parity.None, 8, StopBits.One);
                _serialPort.NewLine = "\n";
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 1000;
                _serialPort.DtrEnable = true;
                _serialPort.DataReceived += OnSerialDataReceived;
                _serialPort.Open();

                _usbConnected = true;
                _btnConnectUsb.Text = "❌ Ngắt Kết Nối";
                _btnConnectUsb.BackColor = Color.FromArgb(153, 27, 27);
                _btnConnectUsb.FlatAppearance.BorderColor = AccentRed;
                _lblUsbStatus.Text = string.Format("🟢 Đã kết nối USB {0} ({1} bps)", portName, baud);
                _lblUsbStatus.ForeColor = AccentGreen;
                AppendSerialLog(string.Format("[USB] Đã mở cổng {0} @ {1} bps.", portName, baud));
            }
            catch (Exception ex)
            {
                CleanupSerialPort();
                MessageBox.Show(this, "Không thể mở cổng " + portName + ":\n" + ex.Message, "Lỗi Kết Nối USB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisconnectUsb()
        {
            CleanupSerialPort();
            _usbConnected = false;
            _btnConnectUsb.Text = "🔌 Kết Nối USB";
            _btnConnectUsb.BackColor = Color.FromArgb(30, 58, 138);
            _btnConnectUsb.FlatAppearance.BorderColor = AccentBlue;
            _lblUsbStatus.Text = "⚪ Chế độ Mô Phỏng Arduino Nano Ảo (Chưa kết nối cổng COM)";
            _lblUsbStatus.ForeColor = AccentAmber;
            AppendSerialLog("[USB] Đã ngắt kết nối cổng Serial.");
        }

        private void CleanupSerialPort()
        {
            if (_serialPort != null)
            {
                try
                {
                    _serialPort.DataReceived -= OnSerialDataReceived;
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                }
                catch
                {
                }
                _serialPort = null;
            }
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = sender as SerialPort;
            if (sp == null || !sp.IsOpen)
            {
                return;
            }

            try
            {
                string chunk = sp.ReadExisting();
                if (!string.IsNullOrEmpty(chunk))
                {
                    BeginInvoke(new Action<string>(ProcessIncomingSerialChunk), chunk);
                }
            }
            catch
            {
            }
        }

        private void ProcessIncomingSerialChunk(string chunk)
        {
            _rxPartialBuffer += chunk.Replace("\r", string.Empty);
            int idx;
            while ((idx = _rxPartialBuffer.IndexOf('\n')) >= 0)
            {
                string line = _rxPartialBuffer.Substring(0, idx).TrimEnd();
                _rxPartialBuffer = _rxPartialBuffer.Substring(idx + 1);
                if (line.Length > 0)
                {
                    AppendSerialLog("[RX] " + line);
                }
            }
        }

        private void OnSendSolutionClick(object sender, EventArgs e)
        {
            if (_compiledResult == null || string.IsNullOrEmpty(_compiledResult.SerialPayload))
            {
                RunSolverAndCompiler(true);
            }

            string payload = _txtSerialPayload.Text.Trim();
            if (string.IsNullOrEmpty(payload))
            {
                MessageBox.Show(this, "Chưa có chuỗi lệnh R/L/F/B để gửi xuống mạch.", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SendCommandPayload(payload, true);
        }

        private void OnQuickTestClick(object sender, EventArgs e)
        {
            SendCommandPayload("R L2 F3 B", false);
        }

        private void OnEmergencyStopClick(object sender, EventArgs e)
        {
            StopWaveSimulation();
            if (_virtualRunning)
            {
                _virtualNanoTimer.Stop();
                _virtualQueue.Clear();
                _virtualRunning = false;
            }

            AppendSerialLog("[TX] !");
            if (_usbConnected && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.WriteLine("!");
                }
                catch (Exception ex)
                {
                    AppendSerialLog("[ERR] Lỗi gửi lệnh dừng: " + ex.Message);
                }
            }
            else
            {
                AppendSerialLog("[RX] [!] DA DUNG KHAN CAP!");
            }
        }

        private void OnManualCmdKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                OnSendManualClick(sender, EventArgs.Empty);
            }
        }

        private void OnSendManualClick(object sender, EventArgs e)
        {
            string cmd = _txtManualCmd.Text.Trim();
            if (string.IsNullOrEmpty(cmd))
            {
                return;
            }
            _txtManualCmd.Clear();
            SendCommandPayload(cmd, false);
        }

        private void SendCommandPayload(string payload, bool animateWaveform)
        {
            string clean = payload.Trim();
            if (string.IsNullOrEmpty(clean))
            {
                return;
            }

            AppendSerialLog("[TX] " + clean);

            if (clean == "!" || string.Equals(clean, "STOP", StringComparison.OrdinalIgnoreCase))
            {
                OnEmergencyStopClick(this, EventArgs.Empty);
                return;
            }

            if (animateWaveform && _compiledResult != null && _compiledResult.Steps != null && _compiledResult.Steps.Count > 0)
            {
                StopWaveSimulation();
                _playbackMs = 0;
                _wavePlaying = true;
                _btnSimulateWave.Text = "⏹️ Dừng Mô Phỏng";
                _waveformPanel.SetCompiledResult(_compiledResult, 0);
                _waveTimer.Start();
            }

            if (_usbConnected && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.WriteLine(clean);
                }
                catch (Exception ex)
                {
                    AppendSerialLog("[ERR] Lỗi ghi USB Serial: " + ex.Message);
                }
            }
            else
            {
                StartVirtualNanoSimulation(clean);
            }
        }

        private void StartVirtualNanoSimulation(string commandLine)
        {
            _virtualNanoTimer.Stop();
            _virtualQueue.Clear();

            string[] tokens = commandLine.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = tokens[i].Trim().ToUpperInvariant();
                if (tok.Length == 0)
                {
                    continue;
                }
                if (tok == "STOP" || tok == "!")
                {
                    _virtualQueue.Enqueue(new VirtualPulseEvent { IsStop = true });
                    break;
                }

                char face = tok[0];
                if (face != 'R' && face != 'L' && face != 'F' && face != 'B')
                {
                    continue;
                }

                int pushes = 1;
                if (tok.Length >= 2)
                {
                    if (tok[1] == '2')
                    {
                        pushes = 2;
                    }
                    else if (tok[1] == '3' || tok[1] == '\'')
                    {
                        pushes = 3;
                    }
                }

                for (int p = 1; p <= pushes; p++)
                {
                    _virtualQueue.Enqueue(new VirtualPulseEvent { Face = face, PushIndex = p, IsStop = false });
                }
            }

            if (_virtualQueue.Count == 0)
            {
                return;
            }

            _virtualRunning = true;
            _virtualNanoTimer.Start();
        }

        private void OnVirtualNanoTimerTick(object sender, EventArgs e)
        {
            if (!_virtualRunning)
            {
                _virtualNanoTimer.Stop();
                return;
            }

            if (_virtualQueue.Count == 0)
            {
                _virtualNanoTimer.Stop();
                _virtualRunning = false;
                AppendSerialLog("[RX] --> HOAN THANH CHUOI LENH!");
                return;
            }

            VirtualPulseEvent ev = _virtualQueue.Dequeue();
            if (ev.IsStop)
            {
                _virtualNanoTimer.Stop();
                _virtualQueue.Clear();
                _virtualRunning = false;
                AppendSerialLog("[RX] [!] DA DUNG KHAN CAP!");
                return;
            }

            AppendSerialLog(string.Format("[RX] Dang day mat {0} - Nhip {1}", ev.Face, ev.PushIndex));

            if (_virtualQueue.Count == 0)
            {
                _virtualNanoTimer.Stop();
                _virtualRunning = false;
                AppendSerialLog("[RX] --> HOAN THANH CHUOI LENH!");
            }
        }

        private void OnClearLogClick(object sender, EventArgs e)
        {
            _txtSerialMonitor.Clear();
            AppendSerialBanner();
        }

        private void OnViewInoClick(object sender, EventArgs e)
        {
            string inoContent = LoadInoFirmwareText();
            using (Form modal = new Form())
            {
                modal.Text = "📜 Mã Nguồn Arduino (.ino) Dịch Ngược Từ File RobotGiaiRubik.ino.hex";
                modal.Size = new Size(820, 620);
                modal.StartPosition = FormStartPosition.CenterParent;
                modal.BackColor = BgDark;
                modal.ForeColor = TextPrimary;

                TextBox txtCode = new TextBox();
                txtCode.Dock = DockStyle.Fill;
                txtCode.Multiline = true;
                txtCode.ReadOnly = true;
                txtCode.ScrollBars = ScrollBars.Both;
                txtCode.WordWrap = false;
                txtCode.BackColor = BgTerminal;
                txtCode.ForeColor = AccentGreen;
                txtCode.Font = new Font("Consolas", 9.5F, FontStyle.Regular);
                txtCode.Text = inoContent;

                Panel bottomBar = new Panel();
                bottomBar.Dock = DockStyle.Bottom;
                bottomBar.Height = 44;
                bottomBar.Padding = new Padding(8);
                bottomBar.BackColor = BgCard;

                Button btnCopyIno = new Button();
                btnCopyIno.Text = "📋 Copy Code .ino";
                btnCopyIno.Dock = DockStyle.Right;
                btnCopyIno.Width = 170;
                btnCopyIno.FlatStyle = FlatStyle.Flat;
                btnCopyIno.BackColor = Color.FromArgb(14, 116, 144);
                btnCopyIno.ForeColor = Color.White;
                btnCopyIno.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                btnCopyIno.Click += delegate(object s, EventArgs args)
                {
                    Clipboard.SetText(inoContent);
                    MessageBox.Show(modal, "Đã sao chép mã nguồn Arduino (.ino) vào Clipboard!", "Copy Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                bottomBar.Controls.Add(btnCopyIno);
                modal.Controls.Add(txtCode);
                modal.Controls.Add(bottomBar);
                modal.ShowDialog(this);
            }
        }

        private string LoadInoFirmwareText()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Application.StartupPath, "firmware", "RobotGiaiRubik.ino"),
                Path.Combine(Application.StartupPath, "..", "firmware", "RobotGiaiRubik.ino"),
                Path.Combine(Environment.CurrentDirectory, "firmware", "RobotGiaiRubik.ino"),
                @"C:\Users\Admin\.gemini\antigravity\scratch\RubikRobotDesktop\firmware\RobotGiaiRubik.ino"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    string full = Path.GetFullPath(candidates[i]);
                    if (File.Exists(full))
                    {
                        return File.ReadAllText(full, Encoding.UTF8);
                    }
                }
                catch
                {
                }
            }

            return "// Không tìm thấy file firmware\\RobotGiaiRubik.ino.";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopWaveSimulation();
            if (_virtualNanoTimer != null)
            {
                _virtualNanoTimer.Stop();
                _virtualNanoTimer.Dispose();
            }
            if (_waveTimer != null)
            {
                _waveTimer.Stop();
                _waveTimer.Dispose();
            }
            CleanupSerialPort();
            if (_previewBitmap != null)
            {
                _previewBitmap.Dispose();
                _previewBitmap = null;
            }
            base.OnFormClosing(e);
        }

        private struct VirtualPulseEvent
        {
            public char Face;
            public int PushIndex;
            public bool IsStop;
        }
    }

    public class StickerClickEventArgs : EventArgs
    {
        public int StickerIndex { get; private set; }

        public StickerClickEventArgs(int stickerIndex)
        {
            StickerIndex = stickerIndex;
        }
    }

    public class CubeNetPanel : Panel
    {
        private string _state54 = RubikVision2D.SOLVED_STATE;
        private readonly Rectangle[] _stickerRects = new Rectangle[54];

        public event EventHandler<StickerClickEventArgs> StickerClicked;

        public CubeNetPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(15, 23, 42);
            Cursor = Cursors.Hand;
        }

        public string State54
        {
            get { return _state54; }
            set
            {
                _state54 = (value != null && value.Length == 54) ? value : RubikVision2D.SOLVED_STATE;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int pad = 8;
            int availW = Math.Max(40, ClientSize.Width - pad * 2);
            int availH = Math.Max(40, ClientSize.Height - pad * 2);

            // 4x3 faces, each face 3x3 cells -> 12x9 cells + face gaps
            int faceGap = 6;
            int cellSize = Math.Min((availW - faceGap * 3) / 12, (availH - faceGap * 2) / 9);
            if (cellSize < 8)
            {
                cellSize = 8;
            }

            int totalW = cellSize * 12 + faceGap * 3;
            int totalH = cellSize * 9 + faceGap * 2;
            int originX = (ClientSize.Width - totalW) / 2;
            int originY = (ClientSize.Height - totalH) / 2;

            // Face mapping in URFDLB order:
            // U (0): macro (1,0), R (1): macro (2,1), F (2): macro (1,1),
            // D (3): macro (1,2), L (4): macro (0,1), B (5): macro (3,1)
            int[] faceCols = new int[] { 1, 2, 1, 1, 0, 3 };
            int[] faceRows = new int[] { 0, 1, 1, 2, 1, 1 };
            char[] faceNames = new char[] { 'U', 'R', 'F', 'D', 'L', 'B' };

            using (Pen borderPen = new Pen(Color.FromArgb(15, 23, 42), 1.5F))
            using (Font centerFont = new Font("Segoe UI", Math.Max(7F, cellSize * 0.42F), FontStyle.Bold))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                for (int f = 0; f < 6; f++)
                {
                    int fx = originX + faceCols[f] * (cellSize * 3 + faceGap);
                    int fy = originY + faceRows[f] * (cellSize * 3 + faceGap);

                    for (int r = 0; r < 3; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            int idx = f * 9 + r * 3 + c;
                            Rectangle rect = new Rectangle(fx + c * cellSize, fy + r * cellSize, cellSize - 1, cellSize - 1);
                            _stickerRects[idx] = rect;

                            char stickerChar = _state54[idx];
                            Color fill = RubikVision2D.GetFaceColor(stickerChar);
                            using (SolidBrush br = new SolidBrush(fill))
                            {
                                g.FillRectangle(br, rect);
                            }
                            g.DrawRectangle(borderPen, rect);

                            if (r == 1 && c == 1)
                            {
                                Color txtColor = (stickerChar == 'U' || stickerChar == 'D') ? Color.Black : Color.White;
                                using (SolidBrush txtBrush = new SolidBrush(txtColor))
                                {
                                    g.DrawString(faceNames[f].ToString(), centerFont, txtBrush, rect, sf);
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < 54; i++)
            {
                if (_stickerRects[i].Contains(e.Location))
                {
                    EventHandler<StickerClickEventArgs> handler = StickerClicked;
                    if (handler != null)
                    {
                        handler(this, new StickerClickEventArgs(i));
                    }
                    break;
                }
            }
        }
    }

    public class WaveformPanel : Panel
    {
        private CompiledRLBFResult _compiled;
        private int _playbackMs;

        public WaveformPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(9, 13, 22);
        }

        public void SetCompiledResult(CompiledRLBFResult compiled, int playbackMs)
        {
            _compiled = compiled;
            _playbackMs = playbackMs;
            Invalidate();
        }

        public void SetPlaybackMs(int playbackMs)
        {
            _playbackMs = playbackMs;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int leftMargin = 68;
            int rightMargin = 14;
            int topMargin = 10;
            int bottomMargin = 20;

            int plotW = Math.Max(40, ClientSize.Width - leftMargin - rightMargin);
            int plotH = Math.Max(40, ClientSize.Height - topMargin - bottomMargin);
            int trackH = plotH / 4;

            char[] trackFaces = new char[] { 'R', 'L', 'F', 'B' };
            string[] trackLabels = new string[] { "D5 (R)", "D6 (L)", "D9 (F)", "D10 (B)" };
            Color[] trackColors = new Color[]
            {
                Color.FromArgb(239, 68, 68),
                Color.FromArgb(249, 115, 22),
                Color.FromArgb(34, 197, 94),
                Color.FromArgb(59, 130, 246)
            };

            using (Pen gridPen = new Pen(Color.FromArgb(30, 41, 59), 1F))
            using (Pen basePen = new Pen(Color.FromArgb(51, 65, 85), 1F))
            using (Font labelFont = new Font("Consolas", 8.5F, FontStyle.Bold))
            using (Font axisFont = new Font("Consolas", 7.5F, FontStyle.Regular))
            using (SolidBrush mutedBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                for (int t = 0; t < 4; t++)
                {
                    int yTop = topMargin + t * trackH + 4;
                    int yBase = topMargin + (t + 1) * trackH - 8;

                    g.DrawLine(gridPen, leftMargin, yTop, leftMargin + plotW, yTop);
                    g.DrawLine(basePen, leftMargin, yBase, leftMargin + plotW, yBase);

                    using (SolidBrush lblBrush = new SolidBrush(trackColors[t]))
                    {
                        g.DrawString(trackLabels[t], labelFont, lblBrush, 6, (yTop + yBase) / 2 - 7);
                    }
                }

                if (_compiled == null || _compiled.Steps == null || _compiled.Steps.Count == 0 || _compiled.TotalDurationMs <= 0)
                {
                    g.DrawString("Chưa có dữ liệu xung 4 kênh. Giải Rubik để xem dạng sóng 75° (150ms) / 0° (150ms).",
                        axisFont, mutedBrush, leftMargin + 8, topMargin + plotH / 2 - 8);
                    return;
                }

                float totalMs = (float)_compiled.TotalDurationMs;

                // Draw pulse waveforms for each step
                for (int s = 0; s < _compiled.Steps.Count; s++)
                {
                    PusherStep step = _compiled.Steps[s];
                    int trackIdx = Array.IndexOf(trackFaces, step.Face);
                    if (trackIdx < 0)
                    {
                        continue;
                    }

                    int yHigh = topMargin + trackIdx * trackH + 6;
                    int yLow = topMargin + (trackIdx + 1) * trackH - 8;

                    using (Pen wavePen = new Pen(trackColors[trackIdx], 2F))
                    using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(75, trackColors[trackIdx])))
                    {
                        for (int p = 0; p < step.PushCount; p++)
                        {
                            int pulseStart = step.StartMs + p * 300;
                            int pulseHoldEnd = pulseStart + 150;
                            int pulseEnd = pulseStart + 300;

                            float x1 = leftMargin + (pulseStart / totalMs) * plotW;
                            float x2 = leftMargin + (pulseHoldEnd / totalMs) * plotW;
                            float x3 = leftMargin + (pulseEnd / totalMs) * plotW;

                            float wHigh = Math.Max(2F, x2 - x1);
                            g.FillRectangle(fillBrush, x1, yHigh, wHigh, yLow - yHigh);

                            g.DrawLine(wavePen, x1, yLow, x1, yHigh);
                            g.DrawLine(wavePen, x1, yHigh, x1 + wHigh, yHigh);
                            g.DrawLine(wavePen, x1 + wHigh, yHigh, x1 + wHigh, yLow);
                            if (x3 > x1 + wHigh)
                            {
                                g.DrawLine(wavePen, x1 + wHigh, yLow, x3, yLow);
                            }
                        }
                    }
                }

                // Time axis labels
                g.DrawString("0 ms", axisFont, mutedBrush, leftMargin, topMargin + plotH + 2);
                string endText = string.Format("{0} ms ({1} nhịp)", _compiled.TotalDurationMs, _compiled.TotalPushes);
                g.DrawString(endText, axisFont, mutedBrush, Math.Max(leftMargin + 40, leftMargin + plotW - 115), topMargin + plotH + 2);

                // Playback cursor
                if (_playbackMs > 0)
                {
                    float clamped = Math.Min(_playbackMs, totalMs);
                    float cx = leftMargin + (clamped / totalMs) * plotW;
                    using (Pen cursorPen = new Pen(Color.FromArgb(34, 211, 238), 2F))
                    {
                        g.DrawLine(cursorPen, cx, topMargin, cx, topMargin + plotH);
                    }
                }
            }
        }
    }
}
