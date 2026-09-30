// Axiom controller firmware for Arduino Uno. Protocol v1: see ../README.md.
// Sends one packet every 20 ms:   AX,<x>,<y>,<mm>,<fire>,<stick>*HH
// Accepts buzzer commands:        BZ,<hz>,<ms>*HH
// HH is the XOR of every character before '*', as two hex digits.

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
char command[32];
uint8_t commandLength = 0;

void setup() {
  pinMode(PIN_FIRE, INPUT_PULLUP);
  pinMode(PIN_JOY_SW, INPUT_PULLUP);
  pinMode(PIN_TRIG, OUTPUT);
  pinMode(PIN_ECHO, INPUT);
  pinMode(PIN_BUZZER, OUTPUT);
  digitalWrite(PIN_TRIG, LOW);
  Serial.begin(115200);
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

// Collects characters up to a newline, then handles "BZ,<hz>,<ms>*HH".
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
  }
}
