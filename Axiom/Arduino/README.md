# Axiom controller (Arduino Uno)

Ultrasonic throttle, two-axis joystick, fire button and buzzer over USB serial.
Firmware: `AxiomController/AxiomController.ino`. Unity finds the controller on its own; keyboard
controls keep working whether or not it is connected.

## Wiring (default pins; change them at the top of the sketch)

![Wiring diagram](wiring.svg)

| Part | Pin | Notes |
| --- | --- | --- |
| Joystick VRx / VRy | A0 / A1 | +5V and GND to the module |
| Joystick SW (push) | D2 → GND | Pauses the game. Optional |
| Fire button | D3 → GND | Uses the internal pull-up; no resistor needed |
| HC-SR04 TRIG / ECHO | D9 / D10 | VCC 5V, GND |
| Buzzer (passive) | D8 → GND | An active buzzer also works, at a fixed pitch |

The throttle is the distance from the HC-SR04 to your hand or a sliding plate.
The default range is 60 mm (minimum throttle) to 300 mm (maximum).

## Upload

1. Arduino IDE → Board: **Arduino Uno**, select its port, upload the sketch.
2. Optional check: Serial Monitor at **115200** baud shows lines like `AX,512,508,176,0,0*0E`.
3. **Close the Serial Monitor before starting the game.** Only one program can use the port, and
   the game reports the port as "in use" until the monitor is closed.

## Protocol v1

ASCII lines, newline-terminated, 115200 baud. `HH` is the XOR of every character before `*`, as
two hex digits. Lines that fail the checksum, field count or ranges are ignored.

| Direction | Message | Fields |
| --- | --- | --- |
| Controller → PC, every 20 ms | `AX,x,y,mm,fire,stick*HH` | x, y: 0–1023 · mm: distance, or -1 for no echo · fire, stick: 0/1 (debounced) |
| PC → controller | `BZ,hz,ms*HH` | Buzzer tone (31 Hz and up, up to 2000 ms) |

## Behavior in the game

- **Connecting:** the game scans serial ports and connects to the first one that sends valid
  `AX` packets within 3 s. The Uno resets when the port opens, so the first connection takes about 2 s.
  The last good port is tried first.
- **Unplugging:** if packets stop for 1 s, the controller counts as disconnected. A run in progress
  pauses with a notice, and you can resume on the keyboard. Replugging reconnects automatically.
- **Sonar dropouts:** brief gaps hold the last throttle value. After 0.5 s without an echo, the HUD
  shows "THROTTLE SENSOR: NO SIGNAL", the buzzer sounds, and the throttle eases to cruise.
- **Buzzer:** cues for shield hits, entering a throttle zone, and a lost sensor, each paired with an
  on-screen cue. Turn it off with **Options → Controller buzzer**.

## Calibrating

**Options → Calibrate controller** (from the main menu or the pause menu). Press FIRE on the controller,
or the on-screen CAPTURE button, at each step:

1. Let go of the stick (records its resting centre).
2. Slide the throttle to its minimum.
3. Slide it to its maximum. Either end can be the nearer one; they must be at least 30 mm apart.
4. Check the live dot and bar, flip the invert switches if an axis runs backwards, then **Save**.

Saved values override the defaults in `ControllerCalibration.cs`; Cancel keeps the previous ones.
