// Axiom cockpit controller (v2) firmware for Arduino Uno on a Gravity IO shield. Wiring: ../cockpit/README.md.
// Protocol v2 extends v1 (../README.md); the first six AX fields mean the same, so the game's v1 code still works.
// Sends one packet every 20 ms:   AX,<x>,<y>,<mm>,<fire>,<menu>,<twist>,<emp>,<arm>*HH
// Accepts buzzer commands:        BZ,<hz>,<ms>*HH
// Accepts LCD rows:               LT,<row>,<text>*HH
// Accepts rumble:                 RM,<level 0-255>,<ms>*HH   ms = 0 sets the steady level; ms > 0 is a pulse
// Accepts the EMP dome's lamp:    LA,<0 off | 1 on | 2 blink>*HH
// HH is the XOR of every character before '*', as two hex digits.
//
// The steady rumble stops RUMBLE_HOLD_MS after the last RM command, so the game resends it while it runs and
// a crashed or closed game never leaves the motor on. The light string on D6 only runs a standby test so far.

#include <Wire.h>
#include <Adafruit_NeoPixel.h>  // Library Manager: "Adafruit NeoPixel"

// ---- Pins (Gravity shield rows). Nothing in Unity depends on them. ----
const uint8_t PIN_JOY_X  = A0;   // stick left-right, right = high
const uint8_t PIN_JOY_Y  = A1;   // stick forward-back, forward = high
const uint8_t PIN_TWIST  = A2;   // stick twist, clockwise = high
const uint8_t PIN_FIRE   = 2;    // stick top button to GND
const uint8_t PIN_EMP    = 3;    // 60 mm dome, NO to GND
const uint8_t PIN_MENU   = 4;    // 30 mm button, NO to GND
const uint8_t PIN_LIGHTS = 6;    // WS2812B string DIN, through 330 Ohm
const uint8_t PIN_RUMBLE = 5;   // DFR0440 vibration module (PWM; Timer0, so beep() on Timer2 doesn't disturb it)
const uint8_t PIN_ARM    = 7;    // MASTER ARM: +5 V when armed, 10 kOhm pull-down when safe
const uint8_t PIN_BUZZER = 8;    // DFR0032 passive buzzer (PB0: beep()'s interrupts switch it directly)
const uint8_t PIN_TRIG   = 9;    // HC-SR04 TRIG
const uint8_t PIN_ECHO   = 10;   // HC-SR04 ECHO
const uint8_t PIN_LAMP   = 12;   // EMP dome's lamp (12 V part with its own resistor: a few mA at 5 V)

// ---- Timing ----
const unsigned long SEND_INTERVAL_MS  = 20;     // 50 packets per second
const unsigned long SONAR_INTERVAL_MS = 60;     // HC-SR04 needs ~60 ms between pings
const unsigned long ECHO_TIMEOUT_US   = 25000;  // ~4 m; no echo in time -> -1
const unsigned long DEBOUNCE_MS       = 15;
const unsigned long GAME_SILENCE_MS   = 3000;   // no commands for this long -> standby screen and self-test
const unsigned long RUMBLE_HOLD_MS    = 1500;   // steady rumble lasts this long after the last RM
const unsigned long STANDBY_FRAME_MS  = 150;
const uint8_t LCD_CELLS_PER_LOOP      = 4;      // redraw budget, so the display never stalls input
const uint8_t SELF_TEST_RUMBLE        = 180;    // standby: holding EMP runs the motor at this level
const uint8_t BUZZER_VOLUME           = 10;     // % of each cycle the buzzer is driven: 50 = full (as tone()), 0 = mute
const uint8_t LIGHT_COUNT             = 50;     // 50 to test a whole uncut string; 16 once it is cut
const uint8_t LIGHT_BRIGHTNESS        = 51;     // 20 % cap: keeps 16 lights inside the USB power budget
const unsigned long LIGHT_STEP_MS     = 40;     // standby test: the dot moves one module per step
const uint16_t LIGHT_BUDGET_MA        = 180;    // frames estimated above this are dimmed (USB power budget)
const unsigned long EMP_CHARGE_MS     = 200;    // standby EMP demo: charge, blast, afterglow
const unsigned long EMP_BLAST_MS      = 1000;
const unsigned long EMP_SHOW_MS       = 2200;

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
  bool activeLow;       // true: wired to GND with the internal pull-up
  bool stable;          // debounced state, true = pressed / armed
  bool lastRaw;
  unsigned long changedAt;
};

Button fireButton = { PIN_FIRE, true, false, false, 0 };
Button empButton  = { PIN_EMP,  true, false, false, 0 };
Button menuButton = { PIN_MENU, true, false, false, 0 };
Button armSwitch  = { PIN_ARM,  false, false, false, 0 };
Button *const buttons[] = { &fireButton, &empButton, &menuButton, &armSwitch };

Adafruit_NeoPixel lights(LIGHT_COUNT, PIN_LIGHTS, NEO_GRB + NEO_KHZ800);

long distanceMm = -1;
unsigned long lastSend = 0;
unsigned long lastSonar = 0;
unsigned long lastCommand = 0;   // any valid command from the game
bool gameActive = false;
char command[48];  // an LT row with every cell escaped is 40 characters
uint8_t commandLength = 0;

// Rumble: a steady level the game keeps alive, and short pulses on top of it.
uint8_t rumbleSteady = 0, rumblePulse = 0, rumbleShown = 0;
unsigned long rumbleSteadyAt = 0, rumblePulseUntil = 0;
uint8_t lampMode = 0;            // set by LA while the game runs; standby keeps the lamp lit
unsigned long empShowAt = 0;     // standby EMP demo start; 0 = not running
bool empBoomed = false;

// LCD state. lcdWant is what should be on screen; lcdShown is what the LCD has.
uint8_t lcdAddress = 0;          // 0 = no display found
uint8_t lcdWant[2][16], lcdShown[2][16];
uint8_t lcdCursorRow = 255, lcdCursorCol = 0;  // where the next data write lands; 255 = unknown
bool gameDrivesLcd = false;
unsigned long lastStandbyFrame = 0;
uint8_t spinFrame = 0;

void setup() {
  pinMode(PIN_FIRE, INPUT_PULLUP);
  pinMode(PIN_EMP, INPUT_PULLUP);
  pinMode(PIN_MENU, INPUT_PULLUP);
  pinMode(PIN_ARM, INPUT);         // external 10 kOhm pull-down
  pinMode(PIN_RUMBLE, OUTPUT);
  pinMode(PIN_LAMP, OUTPUT);
  pinMode(PIN_TRIG, OUTPUT);
  pinMode(PIN_ECHO, INPUT);
  pinMode(PIN_BUZZER, OUTPUT);
  digitalWrite(PIN_TRIG, LOW);
  analogWrite(PIN_RUMBLE, 0);
  lights.begin();
  lights.setBrightness(LIGHT_BRIGHTNESS);
  lights.show();                   // all off
  Serial.begin(115200);
  lcdBegin();
  beep(880, 60);  // power-on chirp
}

// One timestamp per pass: commands and the self-test are stamped with the same `now` the timeouts compare
// against, so an elapsed time can never be negative (an unsigned underflow would read as a long silence).
void loop() {
  unsigned long now = millis();
  for (Button *b : buttons) updateButton(*b, now);

  if (now - lastSonar >= SONAR_INTERVAL_MS) {
    lastSonar = now;
    distanceMm = readDistanceMm();
  }
  if (now - lastSend >= SEND_INTERVAL_MS) {
    lastSend = now;
    updateLights(now);
    sendPacket();
  }
  readCommands(now);
  gameActive = lastCommand != 0 && now - lastCommand <= GAME_SILENCE_MS;
  if (!gameActive) selfTest(now);
  updateRumble(now);
  bool lampOn = !gameActive || lampMode == 1 || (lampMode == 2 && now % 500 < 250);
  if (empShowAt) lampOn = now - empShowAt < EMP_CHARGE_MS && now % 40 < 20;  // flickers, then out until recharged
  digitalWrite(PIN_LAMP, lampOn ? HIGH : LOW);
  updateLcd(now);
}

// A change must hold for DEBOUNCE_MS before it counts, which removes contact bounce.
void updateButton(Button &b, unsigned long now) {
  bool raw = (digitalRead(b.pin) == LOW) == b.activeLow;
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
  snprintf(body, sizeof(body), "AX,%d,%d,%ld,%d,%d,%d,%d,%d",
           analogRead(PIN_JOY_X), analogRead(PIN_JOY_Y), distanceMm,
           fireButton.stable ? 1 : 0, menuButton.stable ? 1 : 0,
           analogRead(PIN_TWIST), empButton.stable ? 1 : 0, armSwitch.stable ? 1 : 0);
  char frame[56];
  snprintf(frame, sizeof(frame), "%s*%02X", body, checksum(body));
  Serial.println(frame);
}

// Collects characters up to a newline, then handles one command.
void readCommands(unsigned long now) {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\r') continue;
    if (c != '\n') {
      if (commandLength < sizeof(command) - 1) command[commandLength++] = c;
      continue;
    }
    command[commandLength] = '\0';
    handleCommand(command, now);
    commandLength = 0;
  }
}

void handleCommand(char *line, unsigned long now) {
  char *star = strrchr(line, '*');
  if (star == NULL) return;
  *star = '\0';
  unsigned int expected = 0;
  if (sscanf(star + 1, "%2X", &expected) != 1 || checksum(line) != expected) return;
  unsigned int a = 0, b = 0;
  if (sscanf(line, "BZ,%u,%u", &a, &b) == 2) {
    if (a >= 31 && b > 0 && b <= 2000) beep(a, b);
  } else if (sscanf(line, "RM,%u,%u", &a, &b) == 2) {
    if (a > 255 || b > 2000) return;
    if (b == 0) {
      rumbleSteady = a;
      rumbleSteadyAt = now;
    } else {
      rumblePulse = a;
      rumblePulseUntil = now + b;
    }
  } else if (sscanf(line, "LA,%u", &a) == 1) {
    if (a > 2) return;
    lampMode = a;
  } else if (strncmp(line, "LT,", 3) == 0) {
    handleLcdRow(line + 3);
  } else {
    return;
  }
  lastCommand = now;
}

// Without the game: every button press clicks, MASTER ARM chirps up or down, EMP fires the light-show demo,
// holding EMP runs the motor, and the EMP lamp stays lit (see loop).
// The game's own rumble always wins, because selfTest only runs while the game is silent.
void selfTest(unsigned long now) {
  static bool was[4] = { false, false, false, false };
  for (uint8_t i = 0; i < 4; i++) {
    bool on = buttons[i]->stable;
    if (on != was[i]) {
      if (buttons[i] == &armSwitch) beep(on ? 1320 : 660, 80);
      else if (on && buttons[i] == &empButton) {
        empShowAt = now;
        empBoomed = false;
      } else if (on) beep(1760, 15);
      was[i] = on;
    }
  }
  rumblePulse = max(empButton.stable ? SELF_TEST_RUMBLE : 0, empShowRumble(now));
  rumblePulseUntil = now + 50;
}

// The EMP demo's rumble: builds during the charge, full during the blast, then fades.
uint8_t empShowRumble(unsigned long now) {
  if (!empShowAt) return 0;
  unsigned long t = now - empShowAt;
  if (t < EMP_CHARGE_MS) return t;
  if (t < 700) return 255;
  if (t < 1200) return 255 - (t - 700) / 2;
  return 0;
}

// ---- Lights ----
// Standby test: one dot runs from the input end to the far end in red, then green, then blue, so every module
// shows each colour once a lap, and a dead module leaves everything after it dark. Pressing EMP plays the demo
// show instead. While the game runs, the lights are off (no game command yet). Runs just before an AX packet:
// show() blocks interrupts for ~30 us per module, so it should never overlap the game's commands.
const uint16_t STEP_OFF = 0xFFFE, STEP_SHOW = 0xFFFD, STEP_NONE = 0xFFFF;

void updateLights(unsigned long now) {
  static uint16_t shownStep = STEP_NONE;
  if (empShowAt && (gameActive || now - empShowAt >= EMP_SHOW_MS)) empShowAt = 0;
  uint16_t step = empShowAt ? STEP_SHOW
                : gameActive ? STEP_OFF
                : (uint16_t)((now / LIGHT_STEP_MS) % (LIGHT_COUNT * 3U));
  if (step == shownStep && step != STEP_SHOW) return;
  shownStep = step;
  lights.clear();
  if (step == STEP_SHOW) {
    drawEmpShow(now - empShowAt);
  } else if (step != STEP_OFF) {
    static const uint32_t COLOURS[3] = { 0xFF0000, 0x00FF00, 0x0000FF };
    lights.setPixelColor(step % LIGHT_COUNT, COLOURS[step / LIGHT_COUNT]);
  }
  limitLightPower();
  lights.show();
}

// One frame of the EMP demo, t ms after the press, with its sound.
//   Charge: the first modules flicker brighter while the pitch rises.
//   Blast: a white front races down the string with a fading blue wake; a low boom.
//   Afterglow: blue-white sparks with crackles, fading out.
void drawEmpShow(unsigned long t) {
  if (t < EMP_CHARGE_MS) {
    uint8_t v = t * 255 / EMP_CHARGE_MS;
    for (uint8_t i = 0; i < 4 && i < LIGHT_COUNT; i++) {
      if (random(3)) lights.setPixelColor(i, v / 2, v / 2, v);
    }
    beep(500 + t * 6, 25);
  } else if (t < EMP_BLAST_MS) {
    if (!empBoomed) {
      beep(90, 350);
      empBoomed = true;
    }
    int16_t front = (t - EMP_CHARGE_MS) * (LIGHT_COUNT + 12L) / (EMP_BLAST_MS - EMP_CHARGE_MS);
    for (int16_t i = 0; i < LIGHT_COUNT && i <= front; i++) {
      int16_t d = front - i;  // modules behind the front
      if (d == 0) lights.setPixelColor(i, 255, 255, 255);
      else if (d < 12) lights.setPixelColor(i, 0, d < 4 ? 200 - d * 50 : 0, 255 - d * 18);
      else lights.setPixelColor(i, 20, 0, 40);  // faint violet trail
    }
  } else {
    uint8_t fade = 255 - (t - EMP_BLAST_MS) * 255 / (EMP_SHOW_MS - EMP_BLAST_MS);
    for (uint8_t i = 0; i < LIGHT_COUNT; i++) {
      if (random(6) == 0) lights.setPixelColor(i, fade / 2, fade / 2, fade);
    }
    if (random(4) == 0) beep(2500 + random(2000), 3);
  }
}

// Estimates the string's current (~18 mA per colour channel at full) and dims the frame if it is over
// LIGHT_BUDGET_MA, so no pattern can overload USB power. The buffer already includes the brightness cap.
void limitLightPower() {
  uint8_t *p = lights.getPixels();
  uint32_t sum = 0;
  for (uint16_t i = 0; i < LIGHT_COUNT * 3U; i++) sum += p[i];
  uint32_t ma = sum * 18 / 255;
  if (ma <= LIGHT_BUDGET_MA) return;
  for (uint16_t i = 0; i < LIGHT_COUNT * 3U; i++) p[i] = (uint32_t)p[i] * LIGHT_BUDGET_MA / ma;
}

// ---- Buzzer ----
// Like tone(), but the buzzer is driven for only BUZZER_VOLUME % of each cycle, which makes it quieter at the
// same pitch. Timer2 runs once per cycle: COMPA switches the buzzer on, COMPB switches it off. 62 Hz minimum.
volatile uint32_t beepCycles = 0;

void beep(unsigned int hz, unsigned int ms) {
  if (BUZZER_VOLUME == 0) return;
  hz = max(hz, 62u);
  static const uint16_t PRESCALE[] = { 1, 8, 32, 64, 128, 256, 1024 };
  uint32_t ticks = F_CPU / hz;
  uint8_t sel = 0;
  while (ticks / PRESCALE[sel] > 256) sel++;
  uint8_t top = ticks / PRESCALE[sel] - 1;
  uint8_t onTicks = max(1UL, (top + 1UL) * min(BUZZER_VOLUME, 50) / 100);
  noInterrupts();
  TCCR2A = _BV(WGM21);   // CTC: count to OCR2A, then restart
  TCCR2B = sel + 1;      // CS22..CS20 = 1..7 selects the prescaler above
  OCR2A = top;
  OCR2B = onTicks;
  TCNT2 = 0;
  beepCycles = (uint32_t)hz * ms / 1000 + 1;
  TIMSK2 = _BV(OCIE2A) | _BV(OCIE2B);
  interrupts();
}

ISR(TIMER2_COMPA_vect) {
  if (beepCycles == 0) {
    TIMSK2 = 0;
    return;
  }
  beepCycles--;
  PORTB |= _BV(PORTB0);
}

ISR(TIMER2_COMPB_vect) {
  PORTB &= ~_BV(PORTB0);
}

void updateRumble(unsigned long now) {
  if (rumbleSteady && now - rumbleSteadyAt > RUMBLE_HOLD_MS) rumbleSteady = 0;
  uint8_t level = rumbleSteady;
  if ((long)(rumblePulseUntil - now) > 0 && rumblePulse > level) level = rumblePulse;
  if (level != rumbleShown) {
    analogWrite(PIN_RUMBLE, level);
    rumbleShown = level;
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

// Without the game, the display shows a spinning octahedron and a live self-test of every input:
//   row 0: octahedron, AXIOM, and F E M A when FIRE, EMP, MENU are pressed and MASTER ARM is armed
//   row 1: distance, then stick X, Y and twist (0-99)
void drawStandby(unsigned long now) {
  if (now - lastStandbyFrame < STANDBY_FRAME_MS) return;
  lastStandbyFrame = now;
  spinFrame = (spinFrame + 1) % 4;
  lcdGlyph(7, SPIN[spinFrame]);

  char text[17];
  snprintf(text, sizeof(text), "  AXIOM     %c%c%c%c",
           fireButton.stable ? 'F' : '.', empButton.stable ? 'E' : '.',
           menuButton.stable ? 'M' : '.', armSwitch.stable ? 'A' : '.');
  lcdText(0, text);
  lcdWant[0][0] = 7;
  int x = (int)(analogRead(PIN_JOY_X) * 99L / 1023);
  int y = (int)(analogRead(PIN_JOY_Y) * 99L / 1023);
  int twist = (int)(analogRead(PIN_TWIST) * 99L / 1023);
  if (distanceMm < 0) snprintf(text, sizeof(text), "---mm X%02dY%02dT%02d", x, y, twist);
  else snprintf(text, sizeof(text), "%4ldmm X%02dY%02dT%02d", min(distanceMm, 9999L), x, y, twist);
  lcdText(1, text);
}

void updateLcd(unsigned long now) {
  if (!lcdAddress) return;
  if (gameDrivesLcd && !gameActive) gameDrivesLcd = false;
  if (!gameDrivesLcd) drawStandby(now);
  lcdFlush();
}
