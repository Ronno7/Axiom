"""Generates wiring-cockpit.svg: the full wiring sheet for the cockpit controller (v2).

1  Overview: every part to its own row on the DFRobot Gravity IO shield (G = ground, V = +5 V, S = signal).
2  Each harness terminal by terminal, with every solder joint marked.
3  The NeoPixel chain: start circuit, board-to-board links, route across the panel and wire lengths.
4  Soldering summary, build order and rules.
Run: python make_wiring.py
"""
import math
from pathlib import Path

OUT = Path(__file__).resolve().parent / "wiring-cockpit.svg"
W = 1600
o = []
add = o.append

BLACK, RED, GRAY = "#1f1f1f", "#d62828", "#868e96"
BLUE, YELLOW = "#1971c2", "#f2b705"
SOLDER, SOLDER_EDGE = "#f08c00", "#7a4300"
SIG = {"A0": "#2b8a3e", "A1": "#1864ab", "D2": "#e8590c", "D3": "#c92a2a", "D4": "#7048e8",
       "D6": "#e64980", "D7": "#a61e4d", "D8": "#5c940d", "D9": "#f76707", "D10": "#0ca678"}
I2C = [("GND", BLACK), ("VCC", RED), ("SDA", BLUE), ("SCL", YELLOW)]


# ---------------- Drawing helpers ----------------
def t(x, y, s, cls="small", anchor="start", extra=""):
    s = s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    add(f'<text x="{x:g}" y="{y:g}" class="{cls}" text-anchor="{anchor}" {extra}>{s}</text>')


def line(pts, color, width=3, dash=False):
    p = " ".join(f"{x:g},{y:g}" for x, y in pts)
    d = ' stroke-dasharray="6 5"' if dash else ""
    add(f'<polyline class="halo" points="{p}"/><polyline points="{p}" fill="none" stroke="{color}" '
        f'stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"{d}/>')


def joint(x, y):
    """A solder joint."""
    add(f'<circle cx="{x:g}" cy="{y:g}" r="5.5" fill="{SOLDER}" stroke="{SOLDER_EDGE}" stroke-width="1.5"/>')


def sleeve(x, y, vertical=False, size=30):
    """Heat-shrink over a joint."""
    w, h = (12, size) if vertical else (size, 12)
    add(f'<rect x="{x - w / 2:g}" y="{y - h / 2:g}" width="{w}" height="{h}" rx="4" fill="#495057" opacity=".22"/>')


def screw(x, y):
    add(f'<circle cx="{x:g}" cy="{y:g}" r="7" fill="#dee2e6" stroke="#495057" stroke-width="1.5"/>'
        f'<path d="M{x - 4:g},{y:g} h8 M{x:g},{y - 4:g} v8" stroke="#495057" stroke-width="1.5"/>')


def plugmark(x, y):
    """Plug-in contact: no solder."""
    add(f'<rect x="{x - 5:g}" y="{y - 5:g}" width="10" height="10" rx="2" fill="#fff" stroke="#495057" stroke-width="2"/>')


def resistor(x1, y1, x2, y2, label, label_side=-1):
    """Resistor body centred on a straight lead from (x1, y1) to (x2, y2)."""
    line([(x1, y1), (x2, y2)], "#495057", 2)
    cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
    vertical = x1 == x2
    w, h = (12, 34) if vertical else (34, 12)
    add(f'<rect x="{cx - w / 2:g}" y="{cy - h / 2:g}" width="{w}" height="{h}" rx="3" fill="#e9dcc3" stroke="#8a7757"/>')
    if vertical:
        t(cx + 12 * (1 if label_side > 0 else -1), cy + 4, label, "tiny", "start" if label_side > 0 else "end")
    else:
        t(cx, cy + (20 if label_side > 0 else -11), label, "tiny", "middle")


def header(x, y0, row, pins="GVS", used=None, pitch=12):
    """Female plug on a shield header: housing with pins top to bottom. Returns the pin y values; wires leave at x + 16."""
    used = pins if used is None else used
    n = len(pins)
    add(f'<rect x="{x:g}" y="{y0 - 8:g}" width="16" height="{(n - 1) * pitch + 16:g}" rx="3" fill="#212529"/>')
    ys = []
    for i, p in enumerate(pins):
        py = y0 + i * pitch
        add(f'<rect x="{x + 5:g}" y="{py - 3:g}" width="6" height="6" fill="{"#ced4da" if p in used else "#495057"}"/>')
        t(x - 5, py + 4, p, "tinyb", "end")
        ys.append(py)
    t(x + 8, y0 - 14, row, "rowname", "middle")
    return ys


def pad(x, y, label, used=True, side=1):
    add(f'<rect x="{x - 6:g}" y="{y - 5:g}" width="12" height="10" rx="2" fill="{"#adb5bd" if used else "#e9ecef"}" stroke="#495057"/>')
    if label:
        t(x + 12 * side, y + 4, label, "tiny", "start" if side > 0 else "end")


def chip(xr, y, text, color):
    w = 8 + 6.9 * len(text)
    add(f'<rect x="{xr - w:g}" y="{y:g}" width="{w:g}" height="22" rx="11" fill="{color}"/>')
    t(xr - w / 2, y + 15, text, "chip", "middle")


def panel(x, y, w, h, title, chip_text, chip_color=SOLDER):
    add(f'<g transform="translate({x:g},{y:g})">')
    add(f'<rect width="{w}" height="{h}" rx="10" fill="#f8f9fa" stroke="#ced4da"/>')
    t(16, 28, title, "part")
    chip(w - 14, 11, chip_text, chip_color)


def notes(x, y, lines, step=16):
    for i, s in enumerate(lines):
        t(x, y + i * step, s, "tiny")


def section(y, number, title, sub):
    add(f'<rect x="40" y="{y:g}" width="{W - 80}" height="3" fill="#212529"/>')
    t(40, y + 34, f"{number}  {title}", "title2")
    t(40, y + 56, sub, "small")


# ---------------- Sheet ----------------
add("@@HEADER@@")
add("""<style>
.halo{fill:none;stroke:#fff;stroke-width:7;stroke-linecap:round;stroke-linejoin:round}
.small{font-size:12.5px;fill:#343a40}.tiny{font-size:11px;fill:#495057}.tinyb{font-size:10px;font-weight:700;fill:#495057}
.note{font-size:11px;fill:#868e96;font-style:italic}.part{font-size:15px;font-weight:700;fill:#212529}
.pin{font-size:14px;font-weight:700;fill:#f1f3f5}.rowname{font-size:11px;font-weight:700;fill:#212529}
.title{font-size:28px;font-weight:700;fill:#111}.title2{font-size:21px;font-weight:700;fill:#111}
.sec{font-size:15px;font-weight:700;fill:#212529}.chip{font-size:11.5px;font-weight:700;fill:#fff}
.map{font-size:10.5px;fill:#495057}.mapb{font-size:10.5px;font-weight:700;fill:#212529}
.th{font-size:12.5px;font-weight:700;fill:#212529}
</style>
<defs><marker id="arr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto">
<path d="M0,0 L10,5 L0,10 z" fill="#e64980"/></marker></defs>""")
add("@@BG@@")
t(40, 48, "Axiom cockpit controller: full wiring (v2)", "title")
t(40, 74, "Every part has its own cable to its own row on the Gravity shield, so nothing is soldered to the Arduino "
          "and any part can be unplugged. Cable colours: black = G (ground), red = V (+5 V), colour = S (signal).", "small")

# Legend
ly = 104
joint(50, ly)
t(62, ly + 4, "solder joint", "small")
sleeve(178, ly)
joint(178, ly)
t(200, ly + 4, "solder joint + heat-shrink (wire to wire)", "small")
plugmark(475, ly)
t(488, ly + 4, "plugs on, no solder", "small")
screw(625, ly)
t(640, ly + 4, "screw terminal", "small")
chip(870, ly - 11, "8 solder", SOLDER)
t(878, ly + 4, "joints in that harness", "small")
add(f'<rect x="1060" y="{ly - 6}" width="34" height="12" rx="3" fill="#e9dcc3" stroke="#8a7757"/>')
t(1102, ly + 4, "resistor (from the kit)", "small")
t(1290, ly + 4, "H1–H10 = harness, see section 2", "small")

# =====================================================================
# 1  Overview
# =====================================================================
S1 = 140
section(S1, "1", "Overview", "Which shield row each part plugs into. The orange dots are where the cable meets the part.")

LX, RX = 640, 920
PX, QX = 500, 1000
TOP = S1 + 80
add(f'<rect x="{LX}" y="{TOP}" width="{RX - LX}" height="640" rx="14" fill="#0f7291"/>')
t((LX + RX) / 2, TOP + 32, "ARDUINO UNO R3 (kit)", "pin", "middle", 'font-size="19"')
t((LX + RX) / 2, TOP + 52, "+ DFRobot Gravity IO Shield V7.1 on top", "pin", "middle", 'font-size="13"')
t((LX + RX) / 2, TOP + 610, "power jumper on 5V · nothing on SERVO_PWR", "tiny", "middle", 'style="fill:#d0ebff"')
t((LX + RX) / 2, TOP + 626, "D0/D1 = USB serial, keep free", "tiny", "middle", 'style="fill:#d0ebff"')


def row(side, y, name, desc, used="GVS"):
    x = LX - 22 if side == "L" else RX
    add(f'<rect x="{x}" y="{y - 16}" width="22" height="32" rx="3" fill="#212529"/>')
    for i, p in enumerate("GVS"):
        add(f'<rect x="{x + 7}" y="{y - 13 + i * 10}" width="8" height="6" fill="{"#ced4da" if p in used else "#495057"}"/>')
    tx = LX + 10 if side == "L" else RX - 10
    t(tx, y - 2, name, "pin", "start" if side == "L" else "end")
    t(tx, y + 13, desc, "tiny", "start" if side == "L" else "end", 'style="fill:#a5d8ff"')


def cable(side, y, x_part, conns, mark=joint):
    for p, color in conns:
        py = y - 10 + "GVS".index(p) * 10
        px = LX - 22 if side == "L" else RX + 22
        line([(x_part, py), (px, py)], color)
        mark(x_part + (10 if side == "L" else -10), py)


def ov_box(x, y, w, h, title, lines, chip_text, chip_color=SOLDER):
    add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="8" fill="#f8f9fa" stroke="#adb5bd"/>')
    t(x + 12, y + 22, title, "part")
    chip(x + w - 10, y + 8, chip_text, chip_color)
    for i, s in enumerate(lines):
        t(x + 12, y + 42 + i * 17, s, "small")


left = [("A0", TOP + 110, "stick X"), ("A1", TOP + 160, "stick Y"), ("D2", TOP + 210, "FIRE (stick top)"),
        ("D3", TOP + 300, "EMP"), ("D4", TOP + 380, "MENU"), ("D7", TOP + 470, "MASTER ARM"),
        ("D9", TOP + 550, "sonar TRIG"), ("D10", TOP + 600, "sonar ECHO")]
used_l = {"A0": "GVS", "A1": "GVS", "D2": "GS", "D3": "GS", "D4": "GS", "D7": "GVS", "D9": "GVS", "D10": "S"}
for name, y, desc in left:
    row("L", y, name, desc, used_l[name])
ly_ = {n: y for n, y, _ in left}

ov_box(40, TOP + 70, PX - 40, 180, "H1  Joystick JH-D400X-R4", [
    "X pot → A0, Y pot → A1", "Outer lugs → G and V, middle lug (wiper) → S",
    "Top button (2 blue wires) → D2 S and G", "Twist pot: unused"], "8 solder")
for n in ("A0", "A1"):
    cable("L", ly_[n], PX, [("G", BLACK), ("V", RED), ("S", SIG[n])])
cable("L", ly_["D2"], PX, [("G", BLACK), ("S", SIG["D2"])])
ov_box(40, TOP + 268, PX - 40, 64, "H2  EMP mushroom (APIELE 22 mm)", ["Its two contacts → G and S, either way round"],
       "screws", "#495057")
cable("L", ly_["D3"], PX, [("G", BLACK), ("S", SIG["D3"])], screw)
ov_box(40, TOP + 350, PX - 40, 64, "H3  MENU: 30 mm arcade button", ["Microswitch COM → G, NO → S"], "2 solder")
cable("L", ly_["D4"], PX, [("G", BLACK), ("S", SIG["D4"])])
ov_box(40, TOP + 432, PX - 40, 80, "H4  MASTER ARM (flip-cover toggle)", [
    "+ → V, ACC → S, GND → G", "10 kΩ across ACC and GND"], "3 solder")
cable("L", ly_["D7"], PX, [("G", BLACK), ("V", RED), ("S", SIG["D7"])])
ov_box(40, TOP + 530, PX - 40, 90, "H5  HC-SR04 throttle sonar (kit)", [
    "VCC → V, TRIG → S, GND → G on the D9 row", "ECHO → S on the D10 row"], "4 solder")
cable("L", ly_["D9"], PX, [("G", BLACK), ("V", RED), ("S", SIG["D9"])])
cable("L", ly_["D10"], PX, [("S", SIG["D10"])])

# Right side
row("R", TOP + 110, "D6", "NeoPixel data")
row("R", TOP + 300, "D8", "buzzer")
ov_box(QX, TOP + 70, W - 40 - QX, 190, "H7  NeoPixel chain: 16 × Adafruit 1312", [
    "V → +5V, G → GND, S → 330 Ω → DIN of board 1", "1000 µF across +5V/GND at board 1",
    "Each board's OUT → the next board's IN (see section 3)"], "94 solder")
cable("R", TOP + 110, QX, [("G", BLACK), ("V", RED), ("S", SIG["D6"])])
CHAIN = (["#f1f3f5"] * 8 + ["#51cf66", "#339af0", "#ffa94d", "#ff6b6b", "#ffd43b"] + ["#4dabf7"] * 3)
for i, c in enumerate(CHAIN):
    cx, cy = QX + 30 + i * 32, TOP + 210
    add(f'<circle cx="{cx}" cy="{cy}" r="10" fill="{c}" stroke="#495057" stroke-width="1.3"/>')
    t(cx, cy + 4, str(i + 1), "tinyb", "middle")
    if i < 15:
        add(f'<line x1="{cx + 10}" y1="{cy}" x2="{cx + 21}" y2="{cy}" stroke="#e64980" stroke-width="2" marker-end="url(#arr)"/>')
t(QX + 30, TOP + 240, "1–8 throttle (front → back) · 9–13 LINK SLOW FAST PROX EMP · 14–16 shield", "tiny")
ov_box(QX, TOP + 272, W - 40 - QX, 64, "H6  Buzzer (kit) through an S8050 (kit)", [
    "S → 1 kΩ → base · V → buzzer + · buzzer − → collector · emitter → G"], "5 solder")
cable("R", TOP + 300, QX, [("G", BLACK), ("V", RED), ("S", SIG["D8"])])

for y, label in ((TOP + 400, "I2C #1"), (TOP + 480, "I2C #2")):
    add(f'<rect x="{RX}" y="{y - 22}" width="22" height="44" rx="3" fill="#212529"/>')
    for k in range(4):
        add(f'<rect x="{RX + 7}" y="{y - 18 + k * 10}" width="8" height="6" fill="#ced4da"/>')
    t(RX - 10, y - 2, label, "pin", "end")
    t(RX - 10, y + 13, "GND · VCC (5V) · SDA · SCL", "tiny", "end", 'style="fill:#a5d8ff"')
    for k, (_, c) in enumerate(I2C):
        line([(RX + 22, y - 15 + k * 10), (QX, y - 15 + k * 10)], c, 2.5)
        plugmark(QX - 10, y - 15 + k * 10)
ov_box(QX, TOP + 362, W - 40 - QX, 64, "H8  LCD1602 + I2C backpack (kit)", [
    "4-pin cable, used whole: GND, VCC, SDA, SCL match by label"], "plug-in", "#495057")
ov_box(QX, TOP + 444, W - 40 - QX, 80, "H9  Haptics: DRV2605L + vibration disc", [
    "STEMMA QT cable to I2C #2 (plug-in)", "Motor + / − pads → disc in the throttle knob"], "4 solder")
add(f'<rect x="{RX}" y="{TOP + 552}" width="30" height="26" rx="3" fill="#adb5bd" stroke="#495057"/>')
t(RX - 10, TOP + 570, "USB-B", "pin", "end")
line([(RX + 30, TOP + 565), (QX, TOP + 565)], GRAY, 6)
ov_box(QX, TOP + 540, W - 40 - QX, 50, "H10  Panel USB cable (Adafruit 907) to the back wall", [], "plug-in", "#495057")

# =====================================================================
# 2  Harness details
# =====================================================================
S2 = TOP + 680
section(S2, "2", "Harness by harness",
        "Cut each 3-pin cable in half: the plug goes on the shield row, the cut end is soldered to the part. "
        "Strip 5 mm, tin both ends, slide the heat-shrink on first.")
PW, PH = 490, 320
COLS = [40, 555, 1070]
R1, R2, R3 = S2 + 80, S2 + 80 + PH + 24, S2 + 80 + 2 * (PH + 24)
TX = 300   # where wires meet the part in most panels

# H1 joystick ------------------------------------------------------------
panel(COLS[0], R1, PW, PH, "H1  Joystick JH-D400X-R4", "8 solder")
rows = [("A0", 74, "X pot", SIG["A0"]), ("A1", 140, "Y pot", SIG["A1"])]
for name, y0, pot, sc in rows:
    ys = header(44, y0, name)
    for py, c, lab in zip(ys, (BLACK, RED, sc), ("outer lug", "other outer lug", "middle lug (wiper)")):
        line([(60, py), (TX, py)], c, 2.5)
        joint(TX, py)
        t(TX + 12, py + 4, f"{pot}: {lab}", "tiny")
ys = header(44, 206, "D2", used="GS")
for py, c in ((ys[0], BLACK), (ys[2], SIG["D2"])):
    line([(60, py), (TX, py)], c, 2.5)
    sleeve(TX - 30, py)
    joint(TX - 30, py)
    line([(TX - 24, py), (TX, py)], "#4dabf7", 2.5)
    t(TX + 12, py + 4, "button: blue wire", "tiny")
notes(16, 256, [
    "Meter check: the two outer lugs read about 10 kΩ end to end; the middle one is the wiper.",
    "Axis runs backwards? Swap that pot's outer lugs, or flip it in calibration.",
    "Twist pot (red/black/white wires): unused. Insulate the ends with heat-shrink.",
    "Hole: per the stick's flange. Measure before the panel is cut."])
add("</g>")

# H2 EMP -------------------------------------------------------------------
panel(COLS[1], R1, PW, PH, "H2  EMP: red mushroom button (D3)", "0–2 solder", "#495057")
ys = header(44, 90, "D3", used="GS")
add(f'<rect x="{TX}" y="60" width="120" height="80" rx="6" fill="#343a40"/>')
t(TX + 60, 94, "contact", "pin", "middle", 'font-size="12"')
t(TX + 60, 110, "block", "pin", "middle", 'font-size="12"')
for py, c in ((ys[0], BLACK), (ys[2], SIG["D3"])):
    line([(60, py), (TX - 8, py)], c, 2.5)
    screw(TX, py)
t(TX + 60, 160, "either contact to G, the other to S", "tiny", "middle")
notes(16, 200, [
    "Screw terminals: strip 6 mm, twist and tin the end, clamp it under the screw. No solder on the switch.",
    "Solder tabs instead? Then it is 2 joints, each with heat-shrink.",
    "Meter check: the contacts close when pressed and it springs back.",
    "If it opens when pressed instead (normally closed), it still works: the sketch inverts it.",
    "Momentary, not an E-stop (those latch). Hole: 22 mm."])
add("</g>")

# H3 MENU ------------------------------------------------------------------
panel(COLS[2], R1, PW, PH, "H3  MENU: 30 mm arcade button (D4)", "2 solder")
ys = header(44, 90, "D4", used="GS")
add(f'<rect x="{TX + 6}" y="62" width="110" height="84" rx="5" fill="#495057"/>')
t(TX + 61, 108, "microswitch", "pin", "middle", 'font-size="12"')
for py, c, lab in ((ys[0], BLACK, "COM"), (ys[2], SIG["D4"], "NO")):
    line([(60, py), (TX, py)], c, 2.5)
    joint(TX, py)
    t(TX - 10, py - 8, lab, "tinyb", "end")
pad(TX, 138, None, used=False)
t(TX - 10, 142, "NC (unused)", "tinyb", "end")
notes(16, 200, [
    "Tabs are 4.8 mm (3/16 in): solder to them, or crimp 4.8 mm female spade connectors instead.",
    "COM, NO and NC are moulded on the switch body next to each tab.",
    "The microswitch clips off the button: wire it first, then clip it back on.",
    "Hole: 28 mm (not 30)."])
add("</g>")

# H4 MASTER ARM ----------------------------------------------------------------
panel(COLS[0], R2, PW, PH, "H4  MASTER ARM: flip-cover toggle (D7)", "3 solder")
ys = header(44, 74, "D7")
targets = [(ys[0], 70, BLACK, "GND"), (ys[1], 120, RED, "+ (power)"), (ys[2], 170, SIG["D7"], "ACC (lamp/load)")]
for py, ty, c, lab in targets:
    pts = [(60, py), (150, py), (175, ty), (TX, ty)] if py != ty else [(60, py), (TX, ty)]
    line(pts, c, 2.5)
    joint(TX, ty)
    t(TX - 10, ty - 8, lab, "tinyb", "end")
add(f'<rect x="{TX + 6}" y="50" width="70" height="140" rx="6" fill="#495057"/>')
t(TX + 41, 210, "switch tabs", "tiny", "middle")
line([(TX, 70), (TX + 110, 70)], "#495057", 2)
line([(TX, 170), (TX + 110, 170)], "#495057", 2)
resistor(TX + 110, 70, TX + 110, 170, "10 kΩ", 1)
notes(16, 236, [
    "Solder the 10 kΩ straight across the GND and ACC tabs, sharing their joints.",
    "Armed = HIGH (the switch feeds +5 V to ACC). Safe = LOW (the 10 kΩ pulls it down).",
    "Tab positions vary: find GND with the meter (the lamp conducts to it). LED lit = armed.",
    "Hole: 12 mm. DaierTek's LED is rated 12 V, so at 5 V it may be dim."])
add("</g>")

# H5 sonar -----------------------------------------------------------------------
panel(COLS[1], R2, PW, PH, "H5  HC-SR04 throttle sonar (D9, D10)", "4 solder")
d9 = header(44, 74, "D9")
d10 = header(44, 134, "D10", used="S")
SX = 210
sensor = {"VCC": 80, "TRIG": 100, "ECHO": 120, "GND": 140}
wires = [(d9[0], "GND", BLACK), (d9[1], "VCC", RED), (d9[2], "TRIG", SIG["D9"]), (d10[2], "ECHO", SIG["D10"])]
for py, pin, c in wires:
    line([(60, py), (SX, py)], c, 2.5)
    line([(SX, py), (SX + 40, py), (SX + 110, sensor[pin]), (TX + 60, sensor[pin])], c, 2.5)
    sleeve(SX, py)
    joint(SX, py)
add(f'<rect x="{TX + 60}" y="70" width="14" height="80" rx="3" fill="#212529"/>')
for pin, y in sensor.items():
    plugmark(TX + 80, y)
    t(TX + 94, y + 4, pin, "tinyb")
add(f'<rect x="{TX + 130}" y="66" width="44" height="88" rx="4" fill="#1c7ed6"/>')
add(f'<circle cx="{TX + 152}" cy="90" r="14" fill="#dee2e6"/><circle cx="{TX + 152}" cy="130" r="14" fill="#dee2e6"/>')
t(SX, 182, "splices", "tiny", "middle")
t(TX + 67, 170, "4-pin half", "tiny", "middle")
notes(16, 220, [
    "The sensor end is half of a 4-pin cable, plugged onto the sensor: no solder on the sensor itself.",
    "Splice its 4 wires to the D9 half (G, V, S) and the S wire of the D10 half.",
    "Pins left to right, facing the transducers: VCC  TRIG  ECHO  GND (printed on the board).",
    "It sits at the front of the throttle tunnel, facing the sliding plate.",
    "The D10 plug only uses S: its G and V wires stay unconnected (heat-shrink the ends)."])
add("</g>")

# H6 buzzer ------------------------------------------------------------------------
panel(COLS[2], R2, PW, PH, "H6  Buzzer through an S8050 (D8)", "5 solder")
ys = header(44, 74, "D8")
qx, qy = 330, 172                      # transistor centre
add(f'<circle cx="{qx}" cy="{qy}" r="22" fill="#fff" stroke="#212529" stroke-width="2"/>')
add(f'<path d="M{qx - 8},{qy - 12} v24 M{qx - 8},{qy - 5} L{qx + 8},{qy - 16} M{qx - 8},{qy + 5} L{qx + 8},{qy + 16}" '
    f'stroke="#212529" stroke-width="2" fill="none"/>')
t(qx + 30, qy + 4, "S8050", "tinyb")
t(qx - 30, qy - 6, "B", "tinyb", "end")
t(qx + 10, qy - 26, "C", "tinyb")
t(qx + 10, qy + 34, "E", "tinyb")
# buzzer
add(f'<rect x="{qx - 18}" y="86" width="36" height="34" rx="17" fill="#212529"/>')
t(qx - 24, 108, "buzzer", "tinyb", "end")
t(qx - 24, 84, "+", "tinyb", "end")
# G: over the top and down the right side to the emitter
line([(60, ys[0]), (72, ys[0]), (72, 44), (460, 44), (460, 250), (qx, 250), (qx, qy + 16)], BLACK, 2.5)
# V to buzzer +
line([(60, ys[1]), (100, ys[1]), (100, 62), (qx, 62), (qx, 86)], RED, 2.5)
# S through 1 kΩ to the base
line([(60, ys[2]), (190, ys[2]), (190, qy)], SIG["D8"], 2.5)
resistor(190, qy, qx - 8, qy, "1 kΩ", -1)
# buzzer − to collector
line([(qx, 120), (qx, qy - 16)], "#495057", 2.5)
for x, y, v in ((qx, 72, True), (qx, 140, True), (qx, 228, True), (198, qy, False), (qx - 34, qy, False)):
    sleeve(x, y, v)
    joint(x, y)
# TO-92 pictogram
px_, py_ = 425, 120
add(f'<path d="M{px_ - 18},{py_} a18,18 0 0 1 36,0 z" fill="#212529"/>')
for i, lab in enumerate("EBC"):
    lx = px_ - 12 + i * 12
    add(f'<rect x="{lx - 1.5}" y="{py_}" width="3" height="18" fill="#adb5bd"/>')
    t(lx, py_ + 30, lab, "tinyb", "middle")
t(px_, py_ - 24, "flat face", "tiny", "middle")
notes(16, 272, [
    "Build it in the air, heat-shrink each joint, then one bigger sleeve over the lot.",
    "Check your S8050's legs (usually E B C, flat face toward you). Buzzer + is the longer leg."])
add("</g>")

# H8 LCD -------------------------------------------------------------------------
panel(COLS[0], R3, PW, PH, "H8  LCD1602 + I2C backpack (I2C #1)", "plug-in", "#495057")
ys = header(44, 80, "I2C #1", pins="1234", used="1234", pitch=16)
for i, (py, (nm, c)) in enumerate(zip(ys, I2C)):
    line([(60, py), (TX, py)], c, 2.5)
    plugmark(TX + 8, py)
    t(TX + 22, py + 4, nm, "tinyb")
add(f'<rect x="{TX + 60}" y="64" width="120" height="80" rx="4" fill="#2f9e44"/>')
add(f'<rect x="{TX + 72}" y="78" width="96" height="40" fill="#8ce99a"/>')
t(TX + 120, 136, "contrast screw", "tiny", "middle", 'style="fill:#fff"')
notes(16, 200, [
    "One 4-pin cable used whole: no cutting, no solder.",
    "Match by the labels on the shield header and the backpack. If the orders differ, lift the",
    "little tab on one plug housing, pull the pins out and re-seat them in the right order.",
    "Blank or solid blocks: turn the contrast screw on the backpack.",
    "Address 0x27 or 0x3F: the sketch finds it."])
add("</g>")

# H9 haptics ------------------------------------------------------------------------
panel(COLS[1], R3, PW, PH, "H9  Haptics: DRV2605L + vibration disc", "4 solder")
ys = header(44, 70, "I2C #2", pins="1234", used="1234", pitch=16)
bx = 230
add(f'<rect x="{bx}" y="54" width="110" height="96" rx="6" fill="#1c7ed6"/>')
t(bx + 55, 104, "DRV2605L", "pin", "middle", 'font-size="12"')
for py, (nm, c) in zip(ys, I2C):
    line([(60, py), (bx - 12, py)], c, 2.5)
    plugmark(70, py)
t(bx - 6, 46, "STEMMA QT", "tiny", "end")
add(f'<rect x="{bx - 12}" y="62" width="12" height="60" rx="2" fill="#212529"/>')
mp = [(84, RED, "Motor +"), (120, BLUE, "Motor −")]
for (y, c, lab), xx in zip(mp, (425, 455)):
    joint(bx + 110, y)
    t(bx + 104, y + 4, lab, "tinyb", "end", 'style="fill:#fff"')
    line([(bx + 116, y), (xx, y), (xx, 222)], c, 2.5)
    sleeve(xx, 180, True, 26)
    joint(xx, 180)
add('<circle cx="440" cy="236" r="16" fill="#adb5bd" stroke="#495057" stroke-width="2"/>')
t(416, 240, "disc", "tiny", "end")
t(bx + 30, 180, "silicone wire, 150–200 mm", "tiny")
t(bx + 30, 196, "with a slack loop", "tiny")
notes(16, 268, [
    "STEMMA QT cable (Adafruit 4397): red VIN, black GND, blue SDA, yellow SCL onto I2C #2.",
    "Disc leads are thin: splice near the disc, heat-shrink, hot-glue for strain relief.",
    "Leave a loop of at least 120 mm so the knob slides its full 100 mm."])
add("</g>")

# H10 USB -------------------------------------------------------------------------
panel(COLS[2], R3, PW, PH, "H10  USB and the shield", "plug-in", "#495057")
add('<rect x="40" y="70" width="140" height="100" rx="8" fill="#0f7291"/>')
t(110, 112, "Uno + shield", "pin", "middle", 'font-size="12"')
add('<rect x="28" y="96" width="24" height="24" rx="2" fill="#adb5bd" stroke="#495057"/>')
line([(28, 108), (10, 108), (10, 200), (300, 200), (300, 120), (330, 120)], GRAY, 6)
add('<rect x="330" y="96" width="26" height="48" rx="3" fill="#495057"/>')
add('<rect x="356" y="60" width="16" height="120" fill="#c2a878"/>')
t(364, 194, "back wall", "tiny", "middle")
t(372, 124, "socket", "tiny")
notes(16, 230, [
    "Panel cable (Adafruit 907): its B plug goes in the Uno, the socket mounts in the 6 mm back wall",
    "with 2 × M3 screws 29 mm apart. Your PC cable plugs into that socket.",
    "Shield: press it onto the Uno, power jumper on 5V, nothing on SERVO_PWR.",
    "USB powers everything (500 mA fuse); see the budget in section 4."])
add("</g>")

# =====================================================================
# 3  NeoPixel chain
# =====================================================================
S3 = R3 + PH + 40
section(S3, "3", "NeoPixel chain (H7)",
        "16 boards, one data line. This is the hard part: about 94 small joints. Build and test it in three groups.")
NY = S3 + 80
panel(40, NY, 720, 600, "Start of the chain and the board-to-board link", "94 solder")
# start circuit
ys = header(44, 110, "D6")
SC = 12                               # px per mm for the board drawings
BW, BH = 12.7 * SC, 10.2 * SC
b1x, b2x = 310, 530
by = 160                               # board centre y
pady = [by - 40, by, by + 40]         # GND, +5V, DATA
names_in, names_out = ("GND", "+5V", "DIN"), ("GND", "+5V", "DOUT")


def board(x, label):
    add(f'<rect x="{x:g}" y="{by - BH / 2:g}" width="{BW:g}" height="{BH:g}" rx="6" fill="#212529"/>')
    add(f'<rect x="{x + BW / 2 - 18:g}" y="{by - 18:g}" width="36" height="36" rx="4" fill="#f8f9fa" stroke="#adb5bd"/>')
    add(f'<circle cx="{x + BW / 2:g}" cy="{by:g}" r="11" fill="#fff3bf"/>')
    t(x + BW / 2, by + BH / 2 + 18, label, "tinyb", "middle")
    for i in range(3):
        for px_ in (x + 12, x + BW - 12):
            add(f'<circle cx="{px_:g}" cy="{pady[i]:g}" r="6" fill="#ced4da" stroke="#868e96"/>')


board(b1x, "board 1 (THR 1)")
board(b2x, "board 2 (THR 2)")
for i, nm in enumerate(names_in):
    t(b1x + 22, pady[i] + 4, nm, "tinyb", extra='style="fill:#fff"')
    t(b2x + 22, pady[i] + 4, nm, "tinyb", extra='style="fill:#fff"')
for i, nm in enumerate(names_out):
    t(b1x + BW - 22, pady[i] + 4, nm, "tinyb", "end", 'style="fill:#fff"')
t(b1x + 12, by - BH / 2 - 8, "IN", "tinyb", "middle")
t(b1x + BW - 12, by - BH / 2 - 8, "OUT", "tinyb", "middle")
t(b2x + 12, by - BH / 2 - 8, "IN", "tinyb", "middle")
t(b2x + BW - 12, by - BH / 2 - 8, "OUT", "tinyb", "middle")
# shield → board 1
line([(60, ys[0]), (120, ys[0]), (150, pady[0]), (b1x + 12, pady[0])], BLACK, 2.5)
line([(60, ys[1]), (110, ys[1]), (150, pady[1]), (b1x + 12, pady[1])], RED, 2.5)
line([(60, ys[2]), (100, ys[2]), (140, pady[2]), (170, pady[2])], SIG["D6"], 2.5)
resistor(170, pady[2], 235, pady[2], "330 Ω", 1)
line([(235, pady[2]), (b1x + 12, pady[2])], SIG["D6"], 2.5)
sleeve(170, pady[2])
joint(170, pady[2])
# 1000 µF across +5V / GND at board 1
cx = 272
add(f'<line x1="{cx}" y1="{pady[0]}" x2="{cx}" y2="{pady[0] + 12}" stroke="#495057" stroke-width="2"/>'
    f'<path d="M{cx - 9},{pady[0] + 14} q9,5 18,0" fill="none" stroke="#212529" stroke-width="3"/>'
    f'<line x1="{cx - 9}" y1="{pady[1] - 12}" x2="{cx + 9}" y2="{pady[1] - 12}" stroke="#212529" stroke-width="3"/>'
    f'<line x1="{cx}" y1="{pady[1] - 12}" x2="{cx}" y2="{pady[1]}" stroke="#495057" stroke-width="2"/>')
t(cx + 13, pady[1] - 10, "+", "tinyb")
t(cx - 12, pady[0] + 14, "1000 µF", "tiny", "end")
t(cx - 12, pady[0] + 27, "stripe (−) to GND", "tiny", "end")
for i in range(3):
    joint(b1x + 12, pady[i])
# link board 1 → board 2
for i, c in enumerate((BLACK, RED, SIG["D6"])):
    line([(b1x + BW - 12, pady[i]), (b2x + 12, pady[i])], c, 2.5)
    joint(b1x + BW - 12, pady[i])
    joint(b2x + 12, pady[i])
for i, c in enumerate((BLACK, RED, SIG["D6"])):
    line([(b2x + BW - 12, pady[i]), (b2x + BW + 22, pady[i])], c, 2.5, dash=True)
t(714, pady[2] + 24, "to 3 …", "tiny", "end")
t(60, 268, "Board 1: 4 joints (the capacitor legs share the GND and +5V pads). Every link after it:", "small")
t(60, 286, "3 wires, OUT → next IN, 6 joints. 15 links × 6 + 4 = 94.", "small")

ty = 322
t(16, ty, "How to build it", "sec")
notes(16, ty + 22, [
    "1  Each board is 10.2 × 12.7 mm with 3 holes at each end (0.1 in apart). The pad names are printed on the board:",
    "    always follow the print. The drawing's pad order is only an example.",
    "2  Build three groups, each on a carrier strip (card or 3 mm ply) with 5 mm holes at the panel's spacing:",
    "    throttle bar (8 boards, 12.5 mm apart), warning lights (5, 25 mm apart), shield (3, 13 mm apart).",
    "    Lay the boards LED-down over the holes and glue them, then wire across the backs.",
    "3  Throttle bar: the boards are 10.2 mm wide on a 12.5 mm pitch, so turn the pad ends sideways (across the bar).",
    "4  Use the wire lengths on the right. Twist the three wires of each link together; it keeps them tidy.",
    "5  Test each group before gluing it in: Adafruit's strandtest sketch, brightness 20, on the bench.",
    "6  Never swap +5V and GND, even for a moment: it kills the board. Check with the meter before powering.",
    "7  A board that stays dark stops everything after it: check that board's DIN and the joint before it."], 17)
add("</g>")

# Panel map with the chain route (compact preset: same numbers as make_cockpit.py)
PANEL_W, PANEL_D = 420.0, 245.0
THR_X, KNOB_W, SENSOR_FACE_Y, NEAR, TRAVEL = 72.0, 42.0, 44.0, 40.0, 100.0
CENTRE, BIG, MENU, STICK, ARM = 222.0, (222.0, 100.0), (222.0, 160.0), (345.0, 130.0), (140.0, 185.0)
LCD_Y, ANN_Y = 200.0, 230.0
STEM_MIN = SENSOR_FACE_Y + NEAR + 25.0
LED_X = THR_X + KNOB_W / 2 + 15.0
throttle = [(LED_X, STEM_MIN + TRAVEL / 16 + TRAVEL / 8 * i) for i in range(8)]
lights = [(CENTRE + d, ANN_Y) for d in (-50.0, -25.0, 0.0, 25.0, 50.0)]
shield = [(CENTRE + 48.0 + 13.0 * i, LCD_Y) for i in range(3)]
chain = throttle + lights + shield
UNO = (PANEL_W / 2, PANEL_D - 6.0 - 40.0)

panel(790, NY, W - 830, 600, "Where the boards sit: top view of the panel", "route", "#e64980")
S = 1.35
MX, MY = 100, 60


def m(x, y):
    """Panel mm (x from the left, y from the front) to sheet px; the front edge is at the bottom."""
    return MX + x * S, MY + (PANEL_D - y) * S


x0, y0 = m(0, PANEL_D)
add(f'<rect x="{x0}" y="{y0}" width="{PANEL_W * S}" height="{PANEL_D * S}" rx="8" fill="#343a40"/>')
t(x0 + PANEL_W * S / 2, y0 - 8, "back", "map", "middle")
t(x0 + PANEL_W * S / 2, y0 + PANEL_D * S + 16, "front (your side)", "map", "middle")
ux, uy = m(*UNO)
add(f'<rect x="{ux - 27 * S}" y="{uy - 34 * S}" width="{53.4 * S}" height="{68.6 * S}" rx="4" fill="none" '
    f'stroke="#74c0fc" stroke-width="2" stroke-dasharray="5 4"/>')
# controls for orientation
sx_, sy_ = m(THR_X, STEM_MIN + TRAVEL / 2)
add(f'<rect x="{sx_ - 4 * S}" y="{m(0, STEM_MIN + TRAVEL + 4)[1]}" width="{8 * S}" height="{(TRAVEL + 8) * S}" rx="6" fill="#868e96"/>')
t(sx_ - 10, sy_, "throttle slot", "map", "end", 'style="fill:#ced4da"')
for (cx_, cy_), r, lab in ((STICK, 20, "stick"), (BIG, 20, "EMP"), (MENU, 14, "MENU"), (ARM, 6, "ARM")):
    px_, py_ = m(cx_, cy_)
    add(f'<circle cx="{px_}" cy="{py_}" r="{r * S}" fill="#495057"/>')
    t(px_, py_ + 4, lab, "map", "middle", 'style="fill:#e9ecef"')
lx_, ly2 = m(CENTRE - 36, LCD_Y + 12.5)
add(f'<rect x="{lx_}" y="{ly2}" width="{72 * S}" height="{25 * S}" fill="#2f9e44"/>')
t(lx_ + 36 * S, ly2 + 12.5 * S + 4, "LCD", "map", "middle", 'style="fill:#fff"')
# route
pts = [m(*UNO)] + [m(*p) for p in chain]
add('<polyline points="' + " ".join(f"{x:g},{y:g}" for x, y in pts) +
    '" fill="none" stroke="#e64980" stroke-width="2.5" stroke-dasharray="7 4" marker-mid="url(#arr)"/>')
for i, p in enumerate(chain):
    px_, py_ = m(*p)
    add(f'<rect x="{px_ - 7}" y="{py_ - 7}" width="14" height="14" rx="3" fill="{CHAIN[i]}" stroke="#212529"/>')
    off = (-14, 4, "end") if i < 8 else ((0, -12, "middle") if i < 13 else (0, 22, "middle"))
    t(px_ + off[0], py_ + off[1], str(i + 1), "mapb", off[2], 'style="fill:#fff"')
mx_, my_ = m(*UNO)
add(f'<circle cx="{mx_:g}" cy="{my_:g}" r="5" fill="#e64980"/>')
t(MX, MY + PANEL_D * S + 34, "Dashed blue: the Uno + shield on the floor below. Pink: the data route from D6, through boards 1 to 16.", "map")

# cut list
LY = MY + PANEL_D * S + 72
t(16, LY, "Wire per link (centre to centre + 30 mm slack, rounded up to 5 mm)", "sec")


def cut(a, b):
    return 5 * math.ceil((math.dist(a, b) + 30) / 5)


links = [("D6 shield row → 1", cut(UNO, chain[0]) + 80, "silicone wire, spliced onto the D6 cable half")]
links.append(("1 → 2 … 7 → 8 (throttle)", cut(chain[0], chain[1]), "7 links × 3 wires"))
links.append(("8 → 9 (throttle → LINK)", cut(chain[7], chain[8]), ""))
links.append(("9 → 10 … 12 → 13 (lights)", cut(chain[8], chain[9]), "4 links × 3 wires"))
links.append(("13 → 14 (EMP → shield 1)", cut(chain[12], chain[13]), ""))
links.append(("14 → 15, 15 → 16 (shield)", cut(chain[13], chain[14]), "2 links × 3 wires"))
for i, (a, mm, note) in enumerate(links):
    yy = LY + 22 + i * 17
    t(16, yy, a, "tiny")
    t(250, yy, f"{mm} mm", "tinyb")
    t(310, yy, note, "tiny")
add("</g>")

# =====================================================================
# 4  Soldering summary
# =====================================================================
S4 = NY + 600 + 40
section(S4, "4", "Soldering summary, build order and rules", "About 120 joints in all; 94 of them are the NeoPixel chain.")
TY = S4 + 80
cols = [56, 330, 430, 1000]
for x, h in zip(cols, ("Harness", "Joints", "What you solder", "Difficulty")):
    t(x, TY, h, "th")
add(f'<line x1="40" y1="{TY + 8}" x2="{W - 40}" y2="{TY + 8}" stroke="#adb5bd"/>')
table = [
    ("H1  Joystick", "8", "6 pot lugs, 2 splices to the button wires", "Medium: identify the lugs with the meter first"),
    ("H2  EMP mushroom", "0–2", "Screw terminals (solder only if it has tabs)", "Easy"),
    ("H3  MENU button", "2", "COM and NO tabs (or crimp spades)", "Easy: good first practice"),
    ("H4  MASTER ARM", "3", "3 tabs, the 10 kΩ shares two of them", "Easy"),
    ("H5  Sonar", "4", "Wire-to-wire splices; nothing on the sensor", "Easy"),
    ("H6  Buzzer + S8050", "5", "Small circuit in the air, heat-shrunk", "Medium: get the transistor legs right"),
    ("H7  NeoPixel chain", "94", "16 small boards, 0.1 in holes, 3 wires per link", "Hard: slow and fiddly, test as you go"),
    ("H8  LCD", "0", "Plug-in cable", "None"),
    ("H9  Haptics", "4", "2 motor pads, 2 splices on thin disc leads", "Medium: thin leads, moving knob"),
    ("H10  USB", "0", "Plug-in cable, 2 screws", "None"),
]
for i, rowv in enumerate(table):
    yy = TY + 28 + i * 21
    if i % 2 == 0:
        add(f'<rect x="40" y="{yy - 15}" width="{W - 80}" height="21" fill="#f1f3f5"/>')
    for x, v in zip(cols, rowv):
        t(x, yy, v, "small")
BY = TY + 28 + len(table) * 21 + 20
add(f'<rect x="40" y="{BY - 20}" width="{(W - 100) / 2}" height="250" rx="8" fill="#e7f5ff" stroke="#a5d8ff"/>')
t(56, BY + 4, "Build order", "sec")
notes(56, BY + 28, [
    "1  Bench test every part first: Uno + shield + kit jumpers, before anything is soldered.",
    "2  Practise on the easy harnesses: H3, H4, H5, then H2 and H6.",
    "3  NeoPixel groups on their carrier strips, tested one group at a time.",
    "4  H1 joystick and H9 haptics once the panel and knob are in hand.",
    "5  Mount everything, plug each cable on its row (photo of the shield first),",
    "    then run the standby self-test on the LCD: stick, FIRE, distance.",
    "6  A dab of hot glue on each plug stops it working loose."], 19)
RX0 = 40 + (W - 100) / 2 + 20
add(f'<rect x="{RX0}" y="{BY - 20}" width="{(W - 100) / 2}" height="250" rx="8" fill="#fff4e6" stroke="#f0c29a"/>')
t(RX0 + 16, BY + 4, "Rules", "sec")
notes(RX0 + 16, BY + 28, [
    "Power from USB (500 mA fuse): Uno 50 + LCD 25 + sonar 15 + NeoPixels 145 + haptic 100",
    "+ buzzer 30 ≈ 365 mA. The sketch caps NeoPixel brightness at 25 % (full white = 576 mA alone).",
    "Buttons use the Uno's internal pull-ups: pressed = LOW. MASTER ARM is the exception: armed = HIGH.",
    "NeoPixels (Adafruit Überguide): 330 Ω on data, 1000 µF at the chain start, 5 V data is fine.",
    "I2C: LCD at 0x27/0x3F, DRV2605L at 0x5A: no clash. Both boards' pull-ups together are fine.",
    "Heat-shrink every wire-to-wire joint; tape is not enough inside a moving box.",
    "Spare pins: A2, A3, D5, D11, D12, D13 (the stick's twist axis can use A2 later)."], 19)

H = BY + 250
svg_head = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H:g}" width="{W}" height="{H:g}" '
            'font-family="Segoe UI, Helvetica, Arial, sans-serif">\n<title>Axiom cockpit controller wiring (v2)</title>')
o[o.index("@@HEADER@@")] = svg_head
o[o.index("@@BG@@")] = f'<rect width="{W}" height="{H:g}" fill="#fff"/>'
add("</svg>")
OUT.write_text("\n".join(x for x in o if x), encoding="utf-8")
print("wrote", OUT, f"{W} x {H:g}")
