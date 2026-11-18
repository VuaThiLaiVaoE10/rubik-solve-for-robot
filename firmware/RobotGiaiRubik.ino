/*
 * ============================================================================
 * ROBOT GIAI RUBIK 4 THANH DAY (R, L, F, B) - ARDUINO NANO (ATmega328P)
 * ----------------------------------------------------------------------------
 * Mã nguồn dịch ngược 100% từ file RobotGiaiRubik.ino.hex + nâng cấp USB Stop:
 * - Chân Servo 4 thanh đẩy:
 *     + Mặt R (Right) : Chân D5  (servos[0])
 *     + Mặt L (Left)  : Chân D6  (servos[1])
 *     + Mặt F (Front) : Chân D9  (servos[2])
 *     + Mặt B (Back)  : Chân D10 (servos[3])
 * - Chân Nút nhấn vật lý:
 *     + Nút START     : Chân D2 (INPUT_PULLUP) -> Chạy chuỗi kiểm tra "R L2 F3 B"
 *     + Nút STOP      : Chân D3 (INPUT_PULLUP) -> Dừng khẩn cấp & thu 4 thanh đẩy về 0°
 * - Giao tiếp USB / UART:
 *     + Baudrate: 9600 bps (8N1)
 *     + Cú pháp nhận: Chuỗi lệnh cách nhau bởi dấu cách hoặc dấu phẩy (kết thúc bằng '\n')
 *       Ví dụ: "R L2 F3 B" hoặc "R, L2, F', B"
 *       (Trong đó: R = đẩy 1 nhịp 90°, R2 = đẩy 2 nhịp 180°, R3 hoặc R' = đẩy 3 nhịp 270°)
 * - Cơ cấu mỗi nhịp đẩy:
 *     + Đẩy Servo lên góc 75° -> đợi 150ms -> Thu Servo về góc 0° -> đợi 150ms
 * ============================================================================
 */

#include <Servo.h>

const int PIN_BTN_START = 2; // D2
const int PIN_BTN_STOP  = 3; // D3
const int servoPins[4]  = {5, 6, 9, 10}; // D5=R, D6=L, D9=F, D10=B

const int GOC_NGHI = 0;      // 0 độ (thu thanh đẩy về)
const int GOC_DAY  = 75;     // 75 độ (0x4B trong file .hex - đẩy mặt Rubik xoay 90°)
const int THOI_GIAN_GIU = 150; // 150ms (0x96 trong file .hex)

Servo servos[4];
volatile bool dungKhanCap = false;

void duaVeViTriGoc() {
  for (int i = 0; i < 4; i++) {
    servos[i].write(GOC_NGHI);
  }
}

bool kiemTraDungKhanCap() {
  // Kiểm tra nút cứng D3 (Active LOW) hoặc ký tự '!' gửi từ App Desktop qua cáp USB
  if (digitalRead(PIN_BTN_STOP) == LOW) {
    dungKhanCap = true;
  }
  while (Serial.available() > 0 && Serial.peek() == '!') {
    Serial.read();
    dungKhanCap = true;
  }
  if (dungKhanCap) {
    duaVeViTriGoc();
    Serial.println("[!] DA DUNG KHAN CAP!");
    return true;
  }
  return false;
}

void dayMat(int index, char tenMat, int soNhip) {
  for (int nhip = 1; nhip <= soNhip; nhip++) {
    if (kiemTraDungKhanCap()) return;

    Serial.print("Dang day mat ");
    Serial.print(tenMat);
    Serial.print(" - Nhip ");
    Serial.println(nhip);

    servos[index].write(GOC_DAY);
    delay(THOI_GIAN_GIU);

    if (kiemTraDungKhanCap()) return;

    servos[index].write(GOC_NGHI);
    delay(THOI_GIAN_GIU);
  }
}

void thucThiLenhDon(String token) {
  token.trim();
  if (token.length() == 0 || dungKhanCap) return;
  token.toUpperCase();

  if (token == "STOP" || token == "!") {
    dungKhanCap = true;
    kiemTraDungKhanCap();
    return;
  }

  char mat = token.charAt(0);
  int soNhip = 1;

  if (token.length() >= 2) {
    char hauTo = token.charAt(1);
    if (hauTo == '2') {
      soNhip = 2;
    } else if (hauTo == '3' || hauTo == '\'') {
      soNhip = 3;
    }
  }

  if (mat == 'R') {
    dayMat(0, 'R', soNhip);
  } else if (mat == 'L') {
    dayMat(1, 'L', soNhip);
  } else if (mat == 'F') {
    dayMat(2, 'F', soNhip);
  } else if (mat == 'B') {
    dayMat(3, 'B', soNhip);
  }
}

void xuLyChuoiLenh(String chuoi) {
  chuoi.trim();
  if (chuoi.length() == 0) return;

  dungKhanCap = false;
  int start = 0;
  int n = chuoi.length();

  for (int i = 0; i <= n; i++) {
    if (i == n || chuoi.charAt(i) == ' ' || chuoi.charAt(i) == ',') {
      if (i > start) {
        String token = chuoi.substring(start, i);
        thucThiLenhDon(token);
        if (dungKhanCap) return;
      }
      start = i + 1;
    }
  }

  if (!dungKhanCap) {
    Serial.println("--> HOAN THANH CHUOI LENH!");
  }
}

void setup() {
  Serial.begin(9600);
  pinMode(PIN_BTN_START, INPUT_PULLUP);
  pinMode(PIN_BTN_STOP, INPUT_PULLUP);

  for (int i = 0; i < 4; i++) {
    servos[i].attach(servoPins[i]);
  }
  duaVeViTriGoc();

  Serial.println("=== ROBOT GIAI RUBIK 4 THANH DAY SAN SANG ===");
  Serial.println("Nhap lenh don (VD: R, R2, R3) hoac chuoi (VD: R L2 F3 B):");
}

void loop() {
  // 1. Nút START (D2): bấm để chạy chuỗi kiểm tra "R L2 F3 B"
  if (digitalRead(PIN_BTN_START) == LOW) {
    delay(50);
    if (digitalRead(PIN_BTN_START) == LOW) {
      Serial.println("\n[NUT START] Chay chuoi kiem tra: R L2 F3 B");
      xuLyChuoiLenh("R L2 F3 B");
      while (digitalRead(PIN_BTN_START) == LOW) {
        delay(10);
      }
    }
  }

  // 2. Nhận chuỗi lệnh giải trực tiếp từ App Desktop qua cáp USB (Serial 9600)
  if (Serial.available() > 0) {
    String chuoi = Serial.readStringUntil('\n');
    xuLyChuoiLenh(chuoi);
  }
}
