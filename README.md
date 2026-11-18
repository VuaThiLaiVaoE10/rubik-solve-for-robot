# 🤖 Rubik Solve For Robot (`RubikRobotDesktop.exe`)

Ứng dụng **Windows Desktop Native (`.exe`)** và **Firmware Arduino Nano** cho mạch **Robot Giải Rubik 3x3 Cơ Cấu 4 Thanh Đẩy (`R, L, F, B`)**.

Hệ thống thực hiện khép kín 3 bước tự động:
1. **Nhận diện Ảnh 2D Scramble 3x3** (quét từ file ảnh `.png/.jpg/.bmp` hoặc chỉnh sửa trực tiếp trên lưới 2D 54 ô).
2. **Giải Rubik 3x3 (Kociemba Two-Phase IDA\*) & Biên dịch sang Lệnh 4 Thanh Đẩy `{R, L, F, B}`**: Tự động chuyển toàn bộ lời giải 6 mặt (`U, D, R, L, F, B`) về dạng chỉ dùng 4 thanh đẩy **`R, L, F, B`** kèm số nhịp đẩy **`1, 2, 3`** (`R, R2, R3, L, L2, L3, F, F2, F3, B, B2, B3`).
3. **Truyền tín hiệu qua cáp USB (`9600 bps`) & Mô phỏng Xung 4 Kênh**: Gửi trực tiếp chuỗi lệnh xuống **Arduino Nano** hoặc cổng COM ảo của **Proteus (`COMPIM`)**.

---

## 🔌 Sơ Đồ Chân Phần Cứng (Arduino Nano — Proteus & Mạch Thật)

| Linh kiện | Chân Arduino Nano | Chức năng & Cơ chế hoạt động |
| :--- | :--- | :--- |
| **Servo Mặt `R` (Right)** | **`D5`** (`servos[0]`) | Thanh đẩy mặt Phải: Nghỉ `0°` $\rightarrow$ Đẩy `75°` (`150ms`) $\rightarrow$ Thu về `0°` (`150ms`) |
| **Servo Mặt `L` (Left)** | **`D6`** (`servos[1]`) | Thanh đẩy mặt Trái: Nghỉ `0°` $\rightarrow$ Đẩy `75°` (`150ms`) $\rightarrow$ Thu về `0°` (`150ms`) |
| **Servo Mặt `F` (Front)** | **`D9`** (`servos[2]`) | Thanh đẩy mặt Trước: Nghỉ `0°` $\rightarrow$ Đẩy `75°` (`150ms`) $\rightarrow$ Thu về `0°` (`150ms`) |
| **Servo Mặt `B` (Back)** | **`D10`** (`servos[3]`) | Thanh đẩy mặt Sau: Nghỉ `0°` $\rightarrow$ Đẩy `75°` (`150ms`) $\rightarrow$ Thu về `0°` (`150ms`) |
| **Nút `START`** | **`D2`** (`INPUT_PULLUP`) | Bấm để chạy chuỗi kiểm tra mẫu: `R L2 F3 B` |
| **Nút `STOP` (Khẩn cấp)** | **`D3`** (`INPUT_PULLUP`) | Bấm để dừng khẩn cấp ngay lập tức & thu cả 4 thanh đẩy về `0°` |
| **Giao tiếp USB / UART** | **`D0 (RX) / D1 (TX)`** | `9600 bps (8N1)` — Nhận chuỗi lệnh giải từ App Desktop qua cáp USB |

---

## 📡 Quy Ước Tín Hiệu Điều Khiển (`R, L, F, B` — Nhịp `1, 2, 3`)

Vì mỗi thanh đẩy Servo chỉ đẩy mặt Rubik quay theo một chiều cố định ($90^\circ$ mỗi nhịp đẩy $75^\circ$), mọi bước xoay được mã hóa như sau:
- **`R`, `L`, `F`, `B`** (1 nhịp đẩy = $90^\circ$ CW, thời gian `300ms`).
- **`R2`, `L2`, `F2`, `B2`** (2 nhịp đẩy liên tiếp = $180^\circ$, thời gian `600ms`).
- **`R3`, `L3`, `F3`, `B3`** (hoặc `R'`, `L'`, `F'`, `B'`: 3 nhịp đẩy liên tiếp = $270^\circ \equiv 90^\circ$ ngược chiều kim đồng hồ, thời gian `900ms`).
- **Ví dụ khung truyền qua USB Serial (`9600 baud`)**:
  ```text
  L2 B L3 F3 B3 F B L2 B F3 L2 B3 B3 F3 L2 R3 B2 L3\n
  ```
- **Lệnh dừng khẩn cấp từ App Desktop qua USB**: Gửi ký tự `!` hoặc `STOP\n`.

---

## 🚀 Cách Sử Dụng Nhanh

### 1. Chạy trực tiếp App Desktop (`.exe`)
Không cần cài thêm thư viện hay bật server/trình duyệt. Bấm đúp trực tiếp vào file:
```text
RubikRobotDesktop.exe
```

### 2. Chạy kiểm thử tự động toàn luồng (Headless Self-Test)
```powershell
.\RubikRobotDesktop.exe --self-test
```

### 3. Biên dịch lại từ mã nguồn C# (nếu chỉnh sửa code)
Sử dụng trình biên dịch C# có sẵn của Windows (`.NET Framework 4.x`):
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:RubikRobotDesktop.exe /r:System.dll /r:System.Core.dll /r:System.Data.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll src\RubikCore.cs src\MainForm.cs src\Program.cs
```

---

## 📂 Cấu Trúc Thư Mục Dự Án

```text
RubikRobotDesktop/
├── RubikRobotDesktop.exe          # Ứng dụng Windows Desktop chạy trực tiếp
├── README.md                      # Tài liệu hướng dẫn dự án
├── firmware/
│   ├── RobotGiaiRubik.ino         # Mã nguồn Arduino Nano (4 Servo D5, D6, D9, D10 + Nút D2, D3)
│   └── RobotGiaiRubik.ino.hex     # File HEX nạp trực tiếp vào Arduino Nano trong Proteus
└── src/
    ├── RubikCore.cs               # Engine quét ảnh 2D, giải 3x3 Kociemba 2-Phase & biên dịch 4 thanh đẩy
    ├── MainForm.cs                # Giao diện Desktop 3 phân hệ, biểu đồ xung 4 kênh & USB SerialPort
    └── Program.cs                 # Entrypoint ứng dụng & bộ kiểm thử tự động --self-test
```
