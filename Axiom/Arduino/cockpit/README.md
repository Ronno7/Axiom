# Axiom cockpit controller (v2)

One sloped box (compact preset 420 × 245 mm): throttle lever with 8 lights on the left, analog stick
with FIRE on top on the right, LCD, shield lights and LINK/SLOW/FAST/PROX/EMP warning lights in the
middle, a red mushroom EMP button, a MENU button (pause in flight, back in menus) and a flip-cover
MASTER ARM switch.

- 3D model: [axiom_cockpit.blend](axiom_cockpit.blend) (built by [make_cockpit.py](make_cockpit.py); `--roomy` for 500 × 280)
- Wiring: [wiring-cockpit.svg](wiring-cockpit.svg) (built by [make_wiring.py](make_wiring.py)): overview, every harness
  terminal by terminal, the NeoPixel route with wire lengths, and a soldering summary (about 120 joints, 94 of them NeoPixels)

![Wiring](wiring-cockpit.svg)

## Shopping list

Prices are approximate (September 2026). Quantities include a few spares where parts are easy to lose or damage.

**Controls**

| Qty | Part | Link | ≈ | Notes |
| --- | --- | --- | --- | --- |
| 1 | JH-D400X-R4 joystick, 10 kΩ, **with button** | [Amazon](https://www.amazon.com/4-axis-Joystick-Potentiometer-JH-D400X-R4-Button/dp/B015IJANPO) | $25 | FIRE is the top button |
| 1 | APIELE 22 mm red mushroom push button, **momentary**, SPST | [Amazon](https://www.amazon.com/dp/B09KZK8SXQ) | $10 | EMP. Not an E-stop: those latch |
| 1 | 30 mm Happ-style arcade buttons, 6-pack | [EG STARTS, Amazon](https://www.amazon.com/EG-STARTS-American-Standard-Switchable/dp/B07GBRD4HJ) | $12 | MENU + spares; 28 mm hole, 4.8 mm tabs |
| 1 | Illuminated toggle with missile cover | [SparkFun COM-11310](https://www.sparkfun.com/toggle-switch-and-cover-illuminated-red.html) or [DaierTek 2-pack, Amazon](https://www.amazon.com/dp/B07T6XJF1T) | $5–12 | MASTER ARM. Same 3-pin, 12 mm hole switch. DaierTek's LED is rated 12 V, so it may glow dimly at 5 V |

**Lights and haptics**

| Qty | Part | Link | ≈ | Notes |
| --- | --- | --- | --- | --- |
| 4 | Breadboard-friendly RGB Smart NeoPixel, 5-pack | [Adafruit 1312](https://www.adafruit.com/product/1312) | $32 | 16 used + 4 spares. Labelled pads, capacitor on board; glued behind 5 mm holes |
| 1 | 1000 µF electrolytic capacitor, 10 V or higher | [Amazon search](https://www.amazon.com/s?k=1000uf+electrolytic+capacitor+16v) | $2 | Across the NeoPixel power |
| 1 | DRV2605L haptic driver | [Adafruit 2305](https://www.adafruit.com/product/2305) | $8 | Engine rumble |
| 2 | Vibrating mini motor disc | [Adafruit 1201](https://www.adafruit.com/product/1201) | $4 | One in the throttle knob, one spare. The driver runs one disc only |
| 1 | STEMMA QT to female-header cable | [Adafruit 4397](https://www.adafruit.com/product/4397) | $1 | DRV2605L to the shield |

**Wiring**

| Qty | Part | Link | ≈ | Notes |
| --- | --- | --- | --- | --- |
| 1 | DFRobot Gravity IO Expansion Shield V7.1 (DFR0265) | [DFRobot](https://www.dfrobot.com/product-1009.html) · [The Pi Hut](https://thepihut.com/products/gravity-io-expansion-shield-for-arduino-v7-1) | $5–9 | GND / 5V / signal for every pin |
| 1 | Panel-mount USB-B cable | [Adafruit 907](https://www.adafruit.com/product/907) | $4 | Fits the 6 mm back wall |
| 1 | Female–female Dupont cables: 14 × 3-pin, 5 × 4-pin, 30 cm | [ACEBOTT, Amazon](https://www.amazon.com/dp/B0DTKDKF5C) | $8 | 3-pin: cut in half, one plug per shield row (10 used). 4-pin: LCD to the shield whole; if the pin order differs, lift the housing tabs and swap pins |
| 1 | 22 AWG silicone wire kit, 6 colours | [TUOFENG, Amazon](https://www.amazon.com/Electric-Flexible-Silicone-different-Electronic/dp/B07G2JWYDW) | $15 | Flexible enough for the throttle knob |
| 1 | Heat-shrink kit, 2:1 | [Amazon](https://www.amazon.com/580PCS-Heat-Shrink-Tubings-Assortment/dp/B089FMGCZS) | $9 | |

**Tools** (skip what you have)

| Qty | Part | Link | ≈ | Notes |
| --- | --- | --- | --- | --- |
| 1 | Soldering iron: Pinecil V2 | [Amazon](https://www.amazon.com/PINECIL-Smart-Mini-Portable-Soldering/dp/B096X6SG13) | $26 | Needs a USB-C PD charger (45 W+); or a 60 W station kit |
| 1 | Rosin-core solder 0.8 mm + flux pen | [Amazon search](https://www.amazon.com/s?k=rosin+core+solder+0.8mm) | $10 | |
| 1 | Multimeter | [Amazon search](https://www.amazon.com/s?k=digital+multimeter) | $15 | For the checks below |

**Enclosure** (order after the cardboard mock-up)

| Qty | Part | Link | ≈ | Notes |
| --- | --- | --- | --- | --- |
| 1 | Top panel, 3 mm black acrylic, laser cut | [SendCutSend](https://sendcutsend.com/materials/acrylic/) | $40–60 | From the cut file (to come) |
| 1 | Body, 6 mm Baltic birch plywood, laser cut | [SendCutSend](https://sendcutsend.com/materials/baltic-birch-plywood/) | $40–60 | Sides, front, back, bottom |
| 1 | Rubber feet, wood glue, M3 screws, adhesive leatherette for the palm rest | Any | $15 | |

**Total:** about $150 in parts and wiring, $50 in tools, and $90–130 for the enclosure.

**From the SunFounder kit:** Uno R3, HC-SR04, LCD1602 (I2C), active buzzer, S8050 transistor,
resistors (330 Ω, 1 kΩ, 10 kΩ), breadboard and jumpers for bench tests.

## Wiring checked against the manufacturers

| Part | Our wiring | Source | Result |
| --- | --- | --- | --- |
| Arduino Uno R3 | A4/A5 = I2C; pins ≤ 20 mA; USB ≤ 500 mA | [Arduino store](https://store-usa.arduino.cc/products/arduino-uno-rev3), [Wikipedia](https://en.wikipedia.org/wiki/Arduino_Uno) | OK, **the buzzer needed a transistor** (below) |
| Gravity IO shield V7.1 | Every part on its own G/V/S row, I2C on the two 4-pin headers | [DFRobot wiki](https://wiki.dfrobot.com/IO_Expansion_Shield_for_Arduino_V7_SKU_DFR0265) | OK. **Power jumper on 5V**; I2C headers are fixed 5 V; SERVO_PWR only feeds D0–D13, leave it empty |
| HC-SR04 | VCC/TRIG/GND on D9, ECHO on D10 | [Datasheet](https://cdn.sparkfun.com/datasheets/Sensors/Proximity/HCSR04.pdf) | OK: 5 V part, 5 V echo |
| JH-D400X-R4 | X/Y wipers to A0/A1, outer lugs to 5V/GND; button (2 blue) to D2 | [JH-D202X datasheet](https://uelectronics.com/wp-content/uploads/2019/10/D202X-R4.pdf), seller specs | OK. Z pot is red/black/white, unused. No lug diagram published: **meter check** |
| LCD1602 I2C backpack | GND/VCC/SDA/SCL to I2C #1 | Kit module (PCF8574) | OK: address 0x27 or 0x3F |
| NeoPixel 1312 | 330 Ω on data, 1000 µF at the chain start, boards chained IN → OUT | [Adafruit 1312](https://www.adafruit.com/product/1312), [board schematic](https://github.com/adafruit/Adafruit_Breadboard_NeoPixel_PCB), [Überguide](https://learn.adafruit.com/adafruit-neopixel-uberguide/best-practices) | OK. SK6812 (GRB), up to 36 mA each; 1 µF capacitor already on each board |
| DRV2605L | STEMMA QT to I2C #2, motor +/− to the disc | [Adafruit guide](https://learn.adafruit.com/adafruit-drv2605-haptic-controller-breakout/pinouts) | OK: 3–5 V, 0x5A, own 10 kΩ pull-ups |
| Vibration disc 1201 | Motor +/− on the DRV2605L | [Adafruit 1201](https://www.adafruit.com/product/1201) | OK: 2–5 V, 100 mA at 5 V |
| MASTER ARM (COM-11310) | + to 5V, accessory to D7, ground to GND, 10 kΩ accessory→ground | [SparkFun](https://www.sparkfun.com/toggle-switch-and-cover-illuminated-red.html), [Core Electronics forum](https://forum.core-electronics.com.au/t/toggle-switch-and-cover-illuminated-red-com-11310/12826) | OK. Ground is the terminal nearest the top. **Reads HIGH when armed**, unlike the buttons |
| EMP mushroom (APIELE) | Its two NO terminals to D3 and GND | [Listing](https://www.amazon.com/dp/B09KZK8SXQ): momentary SPST | OK |
| 30 mm buttons | Microswitch COM to GND, NO to the pin | [EG STARTS listing](https://www.amazon.com/EG-STARTS-American-Standard-Switchable/dp/B07GBRD4HJ) | OK. **28 mm hole**, not 30 (model updated) |
| Buzzer (kit, active) | D8 → 1 kΩ → S8050 base; buzzer between 5V and collector; emitter to GND | Uno pin limit 20 mA | **Changed**: the buzzer draws ~30 mA, too much for a pin |
| Panel USB (907) | Uno to the back wall | [Adafruit 907](https://www.adafruit.com/product/907) | OK: for 3–6.35 mm walls; ours is 6 mm |

**Power from USB (500 mA fuse):** Uno 50 + LCD 25 + HC-SR04 15 + NeoPixels 145 (16 × 36 mA at a 25 %
brightness cap) + haptic 100 + buzzer 30 ≈ 365 mA. The sketch must cap brightness: all 16 at full
white would draw 576 mA on their own.

## Check when the parts arrive

1. **NeoPixels:** on the breadboard, run a colour test on one board (pads are labelled) before
   soldering the chain.
2. **Joystick lugs:** the two outer lugs of each pot read about 10 kΩ; the middle one is the wiper.
3. **Mushroom button:** it springs back when released (momentary); the meter shows it closes when pressed.
   If it opens instead (normally closed), it still works: the sketch inverts it.
4. **MASTER ARM:** confirm which terminal is ground with the meter (the LED conducts to it).
5. **S8050:** with the flat face toward you, the legs are usually E, B, C. Check the marking on yours.
6. **Before ordering the panel:** measure the joystick's panel hole and screw spacing, and the button
   and switch bodies. The model's openings are from listings (stick 40 mm hole, 32.5 mm screw square).
