// Axiom controller firmware for Arduino Uno. Protocol v1: see ../README.md.
// Sends one packet every 20 ms:   AX,<x>,<y>,<mm>,<fire>,<stick>*HH
// Accepts buzzer commands:        BZ,<hz>,<ms>*HH
// Accepts LCD rows:               LT,<row>,<text>*HH
// HH is the XOR of every character before '*', as two hex digits.
//
// Optional 16x2 LCD with an I2C backpack (PCF8574): SDA to A4, SCL to A5. Its address is found automatically
// and no library is needed. Without one, everything else works the same.

#include <Wire.h>

// ---- Pins: change these to match your wiring. Nothing in Unity depends on them. ----
const uint8_t PIN_JOY_X  = A0;   // joystick VRx
const uint8_t PIN_JOY_Y  = A1;   // joystick VRy
const uint8_t PIN_JOY_SW = 2;    // joystick push switch to GND (pause); leave unconnected if absent
const uint8_t PIN_FIRE   = 3;    // fire button to GND
const uint8_t PIN_TRIG   = 9;    // HC-SR04 TRIG
const uint8_t PIN_ECHO   = 10;   // HC-SR04 ECHO
const uint8_t PIN_BUZZER = 8;    // passive buzzer (+) ; (-) to GND

// ---- Timing ----
const unsigned long SEND_INTERVAL_MS  = 20;     // 50 packets per second
const unsigned long SONAR_INTERVAL_MS = 60;     // HC-SR04 needs ~60 ms between pings
const unsigned long ECHO_TIMEOUT_US   = 25000;  // ~4 m; no echo in time -> -1
const unsigned long DEBOUNCE_MS       = 15;
const unsigned long LCD_SILENCE_MS    = 3000;   // no LT rows for this long -> standby screen
const unsigned long STANDBY_FRAME_MS  = 150;
const uint8_t LCD_CELLS_PER_LOOP      = 4;      // redraw budget, so the display never stalls input

// ---- LCD glyphs (CGRAM slots 0-7). The game refers to them by slot; keep the order. ----
const uint8_t GLYPHS[8][8] = {
  { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 },  // 0 bar, 1 column
  { 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18 },  // 1 bar, 2 columns
  { 0x1C, 0x1C, 0x1C, 0x1C, 0x1C, 0x1C, 0x1C, 0x1C },  // 2 bar, 3 columns
  { 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E },  // 3 bar, 4 columns
  { 0x1F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F },  // 4 recommended band (rails)
  { 0x1F, 0x1F, 0x1F, 0x1F, 0x0E, 0x04, 0x00, 0x00 },  // 5 shield segment
  { 0x1F, 0x11, 0x11, 0x11, 0x0A, 0x04, 0x00, 0x00 },  // 6 shield segment, lost
  { 0x04, 0x0E, 0x15, 0x15, 0x15, 0x0E, 0x04, 0x00 },  // 7 octahedron (animated on standby)
};
// Standby spin for slot 7: the front vertex swings right, turns away, returns from the left.
const uint8_t SPIN[4][8] = {
  { 0x04, 0x0E, 0x15, 0x15, 0x15, 0x0E, 0x04, 0x00 },
  { 0x04, 0x0E, 0x13, 0x13, 0x13, 0x0E, 0x04, 0x00 },
  { 0x04, 0x0A, 0x11, 0x11, 0x11, 0x0A, 0x04, 0x00 },
  { 0x04, 0x0E, 0x19, 0x19, 0x19, 0x0E, 0x04, 0x00 },
};

struct Button {
  uint8_t pin;
  bool stable;          // debounced state, true = pressed
  bool lastRaw;
  unsigned long changedAt;
};

Button fireButton  = { PIN_FIRE, false, false, 0 };
Button stickButton = { PIN_JOY_SW, false, false, 0 };

long distanceMm = -1;
unsigned long lastSend = 0;
unsigned long lastSonar = 0;
char command[48];  // an LT row with every cell escaped is 40 characters
uint8_t commandLength = 0;

// LCD state. lcdWant is what should be on screen; lcdShown is what the LCD has.
uint8_t lcdAddress = 0;          // 0 = no display found
uint8_t lcdWant[2][16], lcdShown[2][16];
uint8_t lcdCursorRow = 255, lcdCursorCol = 0;  // where the next data write lands; 255 = unknown
unsigned long lastLcdRow = 0;
bool gameDrivesLcd = false;
unsigned long lastStandbyFrame = 0;
uint8_t spinFrame = 0;

void setup() {
  pinMode(PIN_FIRE, INPUT_PULLUP);
  pinMode(PIN_JOY_SW, INPUT_PULLUP);
  pinMode(PIN_TRIG, OUTPUT);
  pinMode(PIN_ECHO, INPUT);
  pinMode(PIN_BUZZER, OUTPUT);
  digitalWrite(PIN_TRIG, LOW);
  Serial.begin(115200);
  lcdBegin();
  tone(PIN_BUZZER, 880, 60);  // power-on chirp
}

void loop() {
  unsigned long now = millis();
  updateButton(fireButton, now);
  updateButton(stickButton, now);

  if (now - lastSonar >= SONAR_INTERVAL_MS) {
    lastSonar = now;
    distanceMm = readDistanceMm();
  }
  if (now - lastSend >= SEND_INTERVAL_MS) {
    lastSend = now;
    sendPacket();
  }
  readCommands();
  updateLcd(now);
}

// Buttons are wired to GND with pull-ups: LOW means pressed. A change must hold for
// DEBOUNCE_MS before it counts, which removes contact bounce.
void updateButton(Button &b, unsigned long now) {
  bool raw = digitalRead(b.pin) == LOW;
  if (raw != b.lastRaw) {
    b.lastRaw = raw;
    b.changedAt = now;
  } else if (raw != b.stable && now - b.changedAt >= DEBOUNCE_MS) {
    b.stable = raw;
  }
}

long readDistanceMm() {
  digitalWrite(PIN_TRIG, LOW);
  delayMicroseconds(2);
  digitalWrite(PIN_TRIG, HIGH);
  delayMicroseconds(10);
  digitalWrite(PIN_TRIG, LOW);
  unsigned long echoUs = pulseIn(PIN_ECHO, HIGH, ECHO_TIMEOUT_US);
  if (echoUs == 0) return -1;             // no echo: out of range or nothing in front
  return (long)(echoUs * 343UL / 2000UL); // speed of sound 343 m/s, there and back
}

uint8_t checksum(const char *body) {
  uint8_t sum = 0;
  while (*body) sum ^= (uint8_t)*body++;
  return sum;
}

void sendPacket() {
  char body[48];
  snprintf(body, sizeof(body), "AX,%d,%d,%ld,%d,%d",
           analogRead(PIN_JOY_X), analogRead(PIN_JOY_Y), distanceMm,
           fireButton.stable ? 1 : 0, stickButton.stable ? 1 : 0);
  char frame[56];
  snprintf(frame, sizeof(frame), "%s*%02X", body, checksum(body));
  Serial.println(frame);
}

// Collects characters up to a newline, then handles one command.
void readCommands() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\r') continue;
    if (c != '\n') {
      if (commandLength < sizeof(command) - 1) command[commandLength++] = c;
      continue;
    }
    command[commandLength] = '\0';
    handleCommand(command);
    commandLength = 0;
  }
}

void handleCommand(char *line) {
  char *star = strrchr(line, '*');
  if (star == NULL) return;
  *star = '\0';
  unsigned int expected = 0;
  if (sscanf(star + 1, "%2X", &expected) != 1 || checksum(line) != expected) return;
  unsigned int hz = 0, ms = 0;
  if (sscanf(line, "BZ,%u,%u", &hz, &ms) == 2 && hz >= 31 && ms > 0 && ms <= 2000) {
    tone(PIN_BUZZER, hz, ms);
  } else if (strncmp(line, "LT,", 3) == 0) {
    handleLcdRow(line + 3);
  }
}

// "<row>,<text>": one display row, padded with spaces. In the text, \0-\7 are the custom glyphs,
// \> and \< the display's built-in arrows, and \# a solid block.
void handleLcdRow(const char *p) {
  if ((p[0] != '0' && p[0] != '1') || p[1] != ',') return;
  uint8_t row = p[0] - '0';
  p += 2;
  uint8_t col = 0;
  while (*p && col < 16) {
    uint8_t cell = (uint8_t)*p++;
    if (cell == '\\' && *p) {
      char e = *p++;
      if (e >= '0' && e <= '7') cell = e - '0';
      else if (e == '>') cell = 0x7E;
      else if (e == '<') cell = 0x7F;
      else if (e == '#') cell = 0xFF;
      else cell = (uint8_t)e;
    }
    lcdWant[row][col++] = cell;
  }
  while (col < 16) lcdWant[row][col++] = ' ';
  lastLcdRow = millis();
  if (!gameDrivesLcd && lcdAddress) lcdGlyph(7, GLYPHS[7]);  // stop the standby spin
  gameDrivesLcd = true;
}

// ---- LCD ----

// PCF8574 backpack wiring: P0 = RS, P1 = RW, P2 = EN, P3 = backlight, P4-P7 = D4-D7.
const uint8_t LCD_RS = 0x01, LCD_EN = 0x04, LCD_LIGHT = 0x08;

// Sends one 4-bit nibble (the high half of 'value'); the falling edge of EN latches it.
void lcdNibble(uint8_t value, uint8_t mode) {
  uint8_t data = (value & 0xF0) | mode | LCD_LIGHT;
  Wire.beginTransmission(lcdAddress);
  Wire.write(data | LCD_EN);
  Wire.write(data);
  Wire.endTransmission();
}

// One byte as two nibbles in a single I2C transmission (~0.5 ms at 100 kHz).
void lcdSend(uint8_t value, uint8_t mode) {
  uint8_t high = (value & 0xF0) | mode | LCD_LIGHT;
  uint8_t low = (uint8_t)(value << 4) | mode | LCD_LIGHT;
  Wire.beginTransmission(lcdAddress);
  Wire.write(high | LCD_EN);
  Wire.write(high);
  Wire.write(low | LCD_EN);
  Wire.write(low);
  Wire.endTransmission();
}

void lcdGlyph(uint8_t slot, const uint8_t rows[8]) {
  lcdSend(0x40 | (slot << 3), 0);  // set the CGRAM address
  for (uint8_t i = 0; i < 8; i++) lcdSend(rows[i], LCD_RS);
  lcdCursorRow = 255;              // the address counter now points into CGRAM
}

void lcdBegin() {
  Wire.begin();
  Wire.setWireTimeout(3000, true);  // a loose I2C wire must not freeze the controller
  for (uint8_t a = 0x20; a <= 0x3F && !lcdAddress; a++) {
    if (a == 0x28) a = 0x38;        // PCF8574 is 0x20-0x27, PCF8574A is 0x38-0x3F
    Wire.beginTransmission(a);
    if (Wire.endTransmission() == 0) lcdAddress = a;
  }
  if (!lcdAddress) return;

  // Reset into 4-bit mode (HD44780 datasheet, "initializing by instruction").
  delay(50);
  lcdNibble(0x30, 0); delayMicroseconds(4500);
  lcdNibble(0x30, 0); delayMicroseconds(4500);
  lcdNibble(0x30, 0); delayMicroseconds(150);
  lcdNibble(0x20, 0);
  lcdSend(0x28, 0);   // 4-bit, two lines, 5x8 font
  lcdSend(0x0C, 0);   // display on, no cursor
  lcdSend(0x06, 0);   // write left to right
  lcdSend(0x01, 0);   // clear
  delay(2);
  for (uint8_t i = 0; i < 8; i++) lcdGlyph(i, GLYPHS[i]);
  memset(lcdWant, ' ', sizeof(lcdWant));
  memset(lcdShown, ' ', sizeof(lcdShown));
}

// Writes up to LCD_CELLS_PER_LOOP changed cells, moving the cursor only when it must.
void lcdFlush() {
  uint8_t budget = LCD_CELLS_PER_LOOP;
  for (uint8_t r = 0; r < 2; r++) {
    for (uint8_t c = 0; c < 16; c++) {
      if (lcdWant[r][c] == lcdShown[r][c]) continue;
      if (lcdCursorRow != r || lcdCursorCol != c) lcdSend(0x80 | (r ? 0x40 : 0) | c, 0);
      lcdSend(lcdWant[r][c], LCD_RS);
      lcdShown[r][c] = lcdWant[r][c];
      lcdCursorRow = r;
      lcdCursorCol = c + 1;
      if (--budget == 0) return;
    }
  }
}

void lcdText(uint8_t row, const char *text) {
  for (uint8_t c = 0; c < 16; c++) lcdWant[row][c] = *text ? (uint8_t)*text++ : ' ';
}

// Without the game, the display shows a spinning octahedron and a live self-test of every input,
// so the controller can be checked (and the throttle track aligned) with nothing but USB power.
void drawStandby(unsigned long now) {
  if (now - lastStandbyFrame < STANDBY_FRAME_MS) return;
  lastStandbyFrame = now;
  spinFrame = (spinFrame + 1) % 4;
  lcdGlyph(7, SPIN[spinFrame]);

  lcdText(0, "  AXIOM  STANDBY");
  lcdWant[0][0] = 7;
  char text[17];
  int x = (int)(analogRead(PIN_JOY_X) * 99L / 1023);
  int y = (int)(analogRead(PIN_JOY_Y) * 99L / 1023);
  char button = fireButton.stable ? 'F' : stickButton.stable ? 'P' : ' ';
  if (distanceMm < 0) snprintf(text, sizeof(text), " ---mm X%02d Y%02d %c", x, y, button);
  else snprintf(text, sizeof(text), "%4ldmm X%02d Y%02d %c", min(distanceMm, 9999L), x, y, button);
  lcdText(1, text);
}

void updateLcd(unsigned long now) {
  if (!lcdAddress) return;
  if (gameDrivesLcd && now - lastLcdRow > LCD_SILENCE_MS) gameDrivesLcd = false;
  if (!gameDrivesLcd) drawStandby(now);
  lcdFlush();
}
