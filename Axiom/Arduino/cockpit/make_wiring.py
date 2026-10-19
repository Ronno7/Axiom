"""Generates wiring-cockpit.svg: the full wiring sheet for the cockpit controller (v2).

1  Overview: every part to its own row on the DFRobot Gravity IO shield (G = ground, V = +5 V, S = signal).
2  Each harness terminal by terminal, with every solder joint marked.
3  The light string: its start connection, how to fit it and where each light sits on the panel.
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
BLUE, YELLOW, GRAVITY_GREEN = "#1971c2", "#f2b705", "#2f9e44"
SOLDER, SOLDER_EDGE = "#f08c00", "#7a4300"
PLUGIN = "#495057"
SIG = {"A0": "#2b8a3e", "A1": "#1864ab", "A2": "#5f3dc4", "D2": "#e8590c", "D3": "#c92a2a", "D4": "#7048e8", "D5": "#0c8599",
       "D6": "#e64980", "D7": "#a61e4d", "D8": "#5c940d", "D9": "#f76707", "D10": "#0ca678", "D12": "#9c36b5"}
I2C = [("GND", BLACK), ("VCC", RED), ("SDA", BLUE), ("SCL", YELLOW)]
STICK_WIRE = "#adb5bd"   # factory leads (the joystick's twist and button wires, the light string)


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


def splice(x, y):
    sleeve(x, y)
    joint(x, y)


def plugmark(x, y):
    """Plug-in contact: no solder."""
    add(f'<rect x="{x - 5:g}" y="{y - 5:g}" width="10" height="10" rx="2" fill="#fff" stroke="#495057" stroke-width="2"/>')


def capped(x, y):
    """An unused wire end, insulated."""
    add(f'<rect x="{x - 4:g}" y="{y - 5:g}" width="10" height="10" rx="3" fill="#495057" opacity=".55"/>')


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


def gravity_module(x, y, w, h, name):
    """A DFRobot Gravity module with its PH2.0 socket on the left edge; returns the socket pin ys (G, V, S)."""
    add(f'<rect x="{x:g}" y="{y:g}" width="{w}" height="{h}" rx="6" fill="#1c7ed6"/>')
    t(x + 54, y + h / 2 + 5, name, "pin", "middle", 'font-size="12"')
    cy = y + h / 2
    add(f'<rect x="{x - 4:g}" y="{cy - 18:g}" width="12" height="36" rx="2" fill="#f1f3f5" stroke="#495057"/>')
    return [cy - 10, cy, cy + 10]


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
t(62, ly + 4, "solder joint (on a tab)", "small")
splice(232, ly)
t(254, ly + 4, "solder joint + heat-shrink (wire to wire)", "small")
plugmark(530, ly)
t(543, ly + 4, "plugs on, no solder", "small")
capped(690, ly)
t(704, ly + 4, "unused wire: heat-shrink the end", "small")
chip(990, ly - 11, "5 solder", SOLDER)
t(998, ly + 4, "joints in that harness", "small")
add(f'<rect x="1170" y="{ly - 6}" width="34" height="12" rx="3" fill="#e9dcc3" stroke="#8a7757"/>')
t(1212, ly + 4, "resistor (kit)", "small")
t(1330, ly + 4, "H1–H10: see section 2", "small")

# =====================================================================
# 1  Overview
# =====================================================================
S1 = 140
section(S1, "1", "Overview", "Which shield row each part plugs into. 24 solder joints in the whole build; "
                             "the buzzer, rumble, sonar, LCD and USB just plug in.")

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


left = [("A0", TOP + 100, "stick X (left–right)"), ("A1", TOP + 140, "stick Y (fwd–back)"),
        ("A2", TOP + 180, "stick twist"), ("D2", TOP + 220, "FIRE (stick top)"),
        ("D3", TOP + 296, "EMP"), ("D12", TOP + 330, "EMP lamp"), ("D4", TOP + 380, "MENU"), ("D7", TOP + 470, "MASTER ARM"),
        ("D9", TOP + 540, "sonar TRIG + power"), ("D10", TOP + 580, "sonar ECHO")]
used_l = {"A0": "GVS", "A1": "GVS", "A2": "GVS", "D2": "GS", "D3": "GS", "D12": "GS", "D4": "GS", "D7": "GVS", "D9": "GVS", "D10": "S"}
for name, y, desc in left:
    row("L", y, name, desc, used_l[name])
ly_ = {n: y for n, y, _ in left}

ov_box(40, TOP + 70, PX - 40, 180, "H1  Joystick JH-D400X-R4", [
    "X pot (front face): its 3 lugs → A0", "Y pot (left face): its 3 lugs → A1",
    "Twist leads red / white / black → A2", "Top button, 2 yellow leads → D2 G and S"], "11 solder")
cable("L", ly_["A0"], PX, [("G", BLACK), ("V", RED), ("S", SIG["A0"])])
cable("L", ly_["A1"], PX, [("G", BLACK), ("V", RED), ("S", SIG["A1"])])
cable("L", ly_["A2"], PX, [("G", BLACK), ("V", RED), ("S", SIG["A2"])], splice)
cable("L", ly_["D2"], PX, [("G", BLACK), ("S", SIG["D2"])], splice)
ov_box(40, TOP + 266, PX - 40, 82, "H2  EMP: 60 mm red dome (uxcell)", ["Microswitch COM → G, NO → S (D3)",
    "Lamp − → G, + → S (D12)"], "4 solder")
cable("L", ly_["D3"], PX, [("G", BLACK), ("S", SIG["D3"])])
cable("L", ly_["D12"], PX, [("G", BLACK), ("S", SIG["D12"])])
ov_box(40, TOP + 350, PX - 40, 64, "H3  MENU: 30 mm arcade button", ["Microswitch COM → G, NO → S"], "2 solder")
cable("L", ly_["D4"], PX, [("G", BLACK), ("S", SIG["D4"])])
ov_box(40, TOP + 432, PX - 40, 80, "H4  MASTER ARM (DaierTek flip-cover toggle)", [
    "+1 → V, +2 → S, − → G", "10 kΩ across +2 and −"], "3 solder")
cable("L", ly_["D7"], PX, [("G", BLACK), ("V", RED), ("S", SIG["D7"])])
ov_box(40, TOP + 530, PX - 40, 90, "H5  HC-SR04 throttle sonar (kit)", [
    "4 single jumpers: VCC → V, TRIG → S, GND → G (D9)", "ECHO → S (D10)"], "plug-in", PLUGIN)
cable("L", ly_["D9"], PX, [("G", BLACK), ("V", RED), ("S", SIG["D9"])], plugmark)
cable("L", ly_["D10"], PX, [("S", SIG["D10"])], plugmark)

# Right side
right = [("D6", TOP + 110, "light data"), ("D5", TOP + 300, "rumble (PWM)"), ("D8", TOP + 380, "buzzer")]
for name, y, desc in right:
    row("R", y, name, desc)
CHAIN = (["#f1f3f5"] * 8 + ["#51cf66", "#339af0", "#ffa94d", "#ff6b6b", "#ffd43b"] + ["#4dabf7"] * 3)
ov_box(QX, TOP + 70, W - 40 - QX, 190, "H7  Light string: 16 pre-wired LED modules", [
    "V → 5V, G → GND, S → 330 Ω → DIN of module 1",
    "470–1000 µF across 5V/GND at the same joins",
    "Modules 1 → 16 come wired together (section 3)"], "4 solder")
cable("R", TOP + 110, QX, [("G", BLACK), ("V", RED), ("S", SIG["D6"])], splice)
for i, c in enumerate(CHAIN):
    cx, cy = QX + 30 + i * 32, TOP + 210
    add(f'<circle cx="{cx}" cy="{cy}" r="10" fill="{c}" stroke="#495057" stroke-width="1.3"/>')
    t(cx, cy + 4, str(i + 1), "tinyb", "middle")
    if i < 15:
        add(f'<line x1="{cx + 10}" y1="{cy}" x2="{cx + 21}" y2="{cy}" stroke="#e64980" stroke-width="2" marker-end="url(#arr)"/>')
t(QX + 30, TOP + 240, "1–8 throttle (front → back) · 9–13 LINK SLOW FAST PROX EMP · 14–16 shield", "tiny")
ov_box(QX, TOP + 272, W - 40 - QX, 64, "H9  Rumble: Gravity vibration module (DFR0440)", [
    "Its Gravity cable + 3 male–female jumpers to the D5 row"], "plug-in", PLUGIN)
cable("R", TOP + 300, QX, [("G", BLACK), ("V", RED), ("S", SIG["D5"])], plugmark)
ov_box(QX, TOP + 352, W - 40 - QX, 64, "H6  Buzzer: Gravity digital buzzer (DFR0032)", [
    "Its Gravity cable plugs onto the D8 row"], "plug-in", PLUGIN)
cable("R", TOP + 380, QX, [("G", BLACK), ("V", RED), ("S", SIG["D8"])], plugmark)

y = TOP + 470
add(f'<rect x="{RX}" y="{y - 22}" width="22" height="44" rx="3" fill="#212529"/>')
for k in range(4):
    add(f'<rect x="{RX + 7}" y="{y - 18 + k * 10}" width="8" height="6" fill="#ced4da"/>')
t(RX - 10, y - 2, "I2C #1", "pin", "end")
t(RX - 10, y + 13, "GND · VCC (5V) · SDA · SCL", "tiny", "end", 'style="fill:#a5d8ff"')
for k, (_, c) in enumerate(I2C):
    line([(RX + 22, y - 15 + k * 10), (QX, y - 15 + k * 10)], c, 2.5)
    plugmark(QX - 10, y - 15 + k * 10)
ov_box(QX, TOP + 440, W - 40 - QX, 64, "H8  LCD1602 + I2C backpack (kit)", [
    "4-pin cable, used whole: GND, VCC, SDA, SCL match by label"], "plug-in", PLUGIN)
add(f'<rect x="{RX}" y="{TOP + 547}" width="30" height="26" rx="3" fill="#adb5bd" stroke="#495057"/>')
t(RX - 10, TOP + 565, "USB-B", "pin", "end")
line([(RX + 30, TOP + 560), (QX, TOP + 560)], GRAY, 6)
ov_box(QX, TOP + 530, W - 40 - QX, 60, "H10  Panel USB cable (StarTech USBPNLBFBM1)", ["Uno to a socket in the back wall"],
       "plug-in", PLUGIN)
t(QX + 12, TOP + 615, "Free: A3, D11, D13 and I2C #2.", "tiny")

# =====================================================================
# 2  Harness details
# =====================================================================
S2 = TOP + 680
section(S2, "2", "Harness by harness",
        "Where a 3-pin cable is cut in half, the plug goes on the shield row and the cut end is soldered to the part. "
        "Strip 5 mm, tin both ends, slide the heat-shrink on first.")
PW, PH = 490, 360
COLS = [40, 555, 1070]
R1, R2, R3 = S2 + 80, S2 + 80 + PH + 24, S2 + 80 + 2 * (PH + 24)
TX = 300   # where wires meet the part in most panels

# H1 joystick ------------------------------------------------------------
panel(COLS[0], R1, PW, PH, "H1  Joystick: X, Y and twist pots + top button", "11 solder")
# (shield pin, colour, what it goes to, wire-to-wire?) per row; lug 1 = leftmost, looking at the pot's face
stick = {"A0": [(BLACK, "X pot lug 1"), (RED, "X pot lug 3"), (SIG["A0"], "X pot lug 2 (wiper)")],
         "A1": [(BLACK, "Y pot lug 3"), (RED, "Y pot lug 1"), (SIG["A1"], "Y pot lug 2 (wiper)")],
         "A2": [(BLACK, "white lead"), (RED, "red lead"), (SIG["A2"], "black lead (wiper)")],
         "D2": [(BLACK, "yellow lead"), (None, None), (SIG["D2"], "yellow lead")]}
for k, (name, wires) in enumerate(stick.items()):
    ys = header(44, 70 + k * 58, name, used="GS" if name == "D2" else "GVS")
    for py, (c, lab) in zip(ys, wires):
        if c is None:
            continue
        line([(60, py), (TX - 40, py)], c, 2.5)
        if name in ("A2", "D2"):
            line([(TX - 40, py), (TX + 20, py)], STICK_WIRE, 3)
            splice(TX - 40, py)
        else:
            joint(TX - 40, py)
        t(TX + 28, py + 4, lab, "tiny")
notes(16, 292, [
    "Mount the stick with its leads toward the back of the box. Lug 1 is the leftmost,",
    "looking at the pot's face from outside. X pot: front face. Y pot: left face.",
    "Right, forward and clockwise all read higher, so nothing needs inverting.",
    "All three pots are 10 kΩ with a full 0–10 kΩ sweep (metered). The button pair is",
    "isolated from the pots. Measure the stick's flange before the panel is cut."], 15)
add("</g>")

# H2 EMP ------------------------------------------------------------------
panel(COLS[1], R1, PW, PH, "H2  EMP: 60 mm red dome button (D3, lamp D12)", "4 solder")
ys = header(44, 74, "D3", used="GS")
add(f'<rect x="{TX + 6}" y="54" width="110" height="64" rx="5" fill="#495057"/>')
t(TX + 61, 90, "microswitch", "pin", "middle", 'font-size="12"')
for py, c, lab in ((ys[0], BLACK, "COM"), (ys[2], SIG["D3"], "NO")):
    line([(60, py), (TX, py)], c, 2.5)
    joint(TX, py)
    t(TX - 10, py - 8, lab, "tinyb", "end")
pad(TX, 110, None, used=False)
t(TX - 10, 114, "NC (unused)", "tinyb", "end")
ls = header(44, 150, "D12", used="GS")
add(f'<rect x="{TX + 6}" y="138" width="110" height="38" rx="5" fill="#c92a2a"/>')
t(TX + 61, 161, "lamp", "pin", "middle", 'font-size="12"')
for py, c, lab in ((ls[0], BLACK, "−"), (ls[2], SIG["D12"], "+")):
    line([(60, py), (TX, py)], c, 2.5)
    joint(TX, py)
    t(TX - 10, py - 8, lab, "tinyb", "end")
notes(16, 214, [
    "uxcell 60 mm dome: momentary, normally-open microswitch. Hole 23.5 mm (cut 24).",
    "Tabs are 4.8 mm (3/16 in): solder to them, or crimp 4.8 mm female spade connectors instead.",
    "Find COM and NO with the meter: COM–NC reads 0 Ω at rest, COM–NO only when pressed.",
    "The lamp has its own resistor (12 V part), so at 5 V it draws a few mA and D12 drives it directly.",
    "Lamp dark? Swap its two tabs: it only lights one way round. Lit = EMP available.",
    "Below the panel: about 55 mm with the microswitch; the box has about 65 mm there. Measure it.",
    "Pressed = LOW (internal pull-up). The 22 mm APIELE mushroom becomes a spare."], 17)
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
notes(16, 206, [
    "Tabs are 4.8 mm (3/16 in): solder to them, or crimp 4.8 mm female spade connectors instead.",
    "Find COM and NO with the meter: COM–NC reads 0 Ω at rest, COM–NO only when pressed.",
    "The microswitch clips off the button: wire it first, then clip it back on.",
    "Hole: 28 mm (not 30). Pick any colour from the EG STARTS set."], 17)
add("</g>")

# H4 MASTER ARM ----------------------------------------------------------------
panel(COLS[0], R2, PW, PH, "H4  MASTER ARM: flip-cover toggle (D7)", "3 solder")
ys = header(44, 74, "D7")
targets = [(ys[0], 70, BLACK, "− (side tab)"), (ys[1], 120, RED, "+1 (power)"), (ys[2], 170, SIG["D7"], "+2 (load)")]
for py, ty_, c, lab in targets:
    pts = [(60, py), (150, py), (175, ty_), (TX, ty_)] if py != ty_ else [(60, py), (TX, ty_)]
    line(pts, c, 2.5)
    joint(TX, ty_)
    t(TX - 10, ty_ - 8, lab, "tinyb", "end")
add(f'<rect x="{TX + 6}" y="50" width="70" height="140" rx="6" fill="#495057"/>')
t(TX + 41, 210, "switch tabs", "tiny", "middle")
line([(TX, 70), (TX + 110, 70)], "#495057", 2)
line([(TX, 170), (TX + 110, 170)], "#495057", 2)
resistor(TX + 110, 70, TX + 110, 170, "10 kΩ", 1)
notes(16, 240, [
    "Solder the 10 kΩ straight across the − and +2 tabs, sharing their joints.",
    "Armed = HIGH (the switch feeds +5 V to +2). Safe = LOW (the 10 kΩ pulls it down).",
    "− is the tab on the side; +1 and +2 are the two pins underneath (DaierTek's diagram). LED lit = armed.",
    "DaierTek's LED is rated 12 V, so at 5 V it may be dim. Hole: 12 mm.",
    "You have two: keep the second as a spare."], 17)
add("</g>")

# H5 sonar -----------------------------------------------------------------------
panel(COLS[1], R2, PW, PH, "H5  HC-SR04 throttle sonar (D9, D10)", "plug-in", PLUGIN)
d9 = header(44, 74, "D9")
d10 = header(44, 134, "D10", used="S")
sensor = {"VCC": 80, "TRIG": 100, "ECHO": 120, "GND": 140}
SXX = TX + 60
wires = [(d9[0], "GND", BLACK), (d9[1], "VCC", RED), (d9[2], "TRIG", SIG["D9"]), (d10[2], "ECHO", SIG["D10"])]
for py, pin, c in wires:
    line([(60, py), (150, py), (230, sensor[pin]), (SXX, sensor[pin])], c, 2.5)
    plugmark(70, py)
    plugmark(SXX - 10, sensor[pin])
add(f'<rect x="{SXX}" y="70" width="14" height="80" rx="3" fill="#212529"/>')
for pin, y_ in sensor.items():
    t(SXX + 22, y_ + 4, pin, "tinyb")
add(f'<rect x="{SXX + 60}" y="66" width="44" height="88" rx="4" fill="#1c7ed6"/>')
add(f'<circle cx="{SXX + 82}" cy="90" r="14" fill="#dee2e6"/><circle cx="{SXX + 82}" cy="130" r="14" fill="#dee2e6"/>')
t(190, 182, "4 single female–female jumpers, 40 cm", "tiny", "middle")
notes(16, 220, [
    "No solder: one jumper per pin. The sensor is ~22 cm from the Uno; 40 cm jumpers leave slack.",
    "Pins left to right, facing the transducers: VCC  TRIG  ECHO  GND (printed on the board).",
    "The D10 row only uses S. A dab of hot glue on each plug once it all works.",
    "It sits at the front of the throttle tunnel, facing the sliding plate. Keep wires out of the tunnel:",
    "the sonar sees anything in front of the plate."], 17)
add("</g>")

# H6 buzzer ------------------------------------------------------------------------
panel(COLS[2], R2, PW, PH, "H6  Buzzer: DFRobot Gravity DFR0032 (D8)", "plug-in", PLUGIN)
ys = header(44, 90, "D8")
sock = gravity_module(TX, 64, 130, 76, "buzzer")
for py, sy, c in zip(ys, sock, (BLACK, RED, GRAVITY_GREEN)):
    line([(60, py), (130, py), (200, sy), (TX - 4, sy)], c, 2.5)
    plugmark(70, py)
add(f'<circle cx="{TX + 110}" cy="102" r="13" fill="#212529"/>')
t(170, 168, "Gravity cable (in the box)", "tiny", "middle")
notes(16, 206, [
    "No solder: its cable plugs onto the D8 row at one end and into the module at the other.",
    "Black → G, red → V, green → S. It has its own transistor, so the pin isn't overloaded.",
    "Passive buzzer: it plays the pitch the game sends (the proximity beeps rise properly).",
    "Mount it anywhere near the Uno with two M3 screws or double-sided tape.",
    "Replaces the kit's buzzer + S8050 circuit (5 joints)."], 17)
add("</g>")

# H8 LCD -------------------------------------------------------------------------
panel(COLS[0], R3, PW, PH, "H8  LCD1602 + I2C backpack (I2C #1)", "plug-in", PLUGIN)
ys = header(44, 80, "I2C #1", pins="1234", used="1234", pitch=16)
for py, (nm, c) in zip(ys, I2C):
    line([(60, py), (TX, py)], c, 2.5)
    plugmark(TX + 8, py)
    t(TX + 22, py + 4, nm, "tinyb")
add(f'<rect x="{TX + 60}" y="64" width="120" height="80" rx="4" fill="#2f9e44"/>')
add(f'<rect x="{TX + 72}" y="78" width="96" height="40" fill="#8ce99a"/>')
t(TX + 120, 136, "contrast screw", "tiny", "middle", 'style="fill:#fff"')
notes(16, 206, [
    "One 4-pin ACEBOTT cable used whole: no cutting, no solder.",
    "Match by the labels on the shield header and the backpack. If the orders differ, lift the",
    "little tab on one plug housing, pull the pins out and re-seat them in the right order.",
    "Blank or solid blocks: turn the contrast screw on the backpack.",
    "Address 0x27 or 0x3F: the sketch finds it."], 17)
add("</g>")

# H9 rumble ------------------------------------------------------------------------
panel(COLS[1], R3, PW, PH, "H9  Rumble: DFRobot Gravity DFR0440 (D5)", "plug-in", PLUGIN)
ys = header(44, 90, "D5")
sock = gravity_module(TX, 64, 130, 76, "vibration")
for py, sy, c in zip(ys, sock, (BLACK, RED, GRAVITY_GREEN)):
    line([(60, py), (130, py), (200, sy), (TX - 4, sy)], c, 2.5)
    plugmark(70, py)
add(f'<circle cx="{TX + 110}" cy="102" r="12" fill="#adb5bd" stroke="#495057" stroke-width="2"/>')
t(170, 168, "Gravity cable + 3 M–F jumpers, slack loop", "tiny", "middle")
notes(16, 206, [
    "No solder. Glue the module to the back of the throttle's reflector plate: it moves with the knob,",
    "and the sonar only sees the plate's front face. The cable leaves the tunnel at the back.",
    "Leave a loop of at least 120 mm so the plate slides its full 100 mm without tugging.",
    "Its 30 cm cable is too short: add 3 male–female jumpers (40 cm) and tape the joins together.",
    "D5 is a PWM pin: the sketch sets the strength (engine rumble rises with speed, a jolt on hits).",
    "Too weak through the stem? It can move into the knob later, with its cable down a hollow stem."], 17)
add("</g>")

# H10 USB -------------------------------------------------------------------------
panel(COLS[2], R3, PW, PH, "H10  USB and the shield", "plug-in", PLUGIN)
add('<rect x="40" y="70" width="160" height="100" rx="8" fill="#0f7291"/>')
t(120, 112, "Uno + shield", "pin", "middle", 'font-size="12"')
t(120, 130, "(turned sideways)", "tiny", "middle", 'style="fill:#d0ebff"')
add('<rect x="200" y="96" width="24" height="24" rx="2" fill="#adb5bd" stroke="#495057"/>')
line([(224, 108), (300, 108), (300, 120), (330, 120)], GRAY, 6)
add('<rect x="330" y="96" width="26" height="48" rx="3" fill="#495057"/>')
add('<rect x="356" y="60" width="16" height="120" fill="#c2a878"/>')
t(364, 194, "back wall", "tiny", "middle")
t(380, 124, "socket", "tiny")
notes(16, 230, [
    "Panel cable (StarTech USBPNLBFBM1, 30 cm): its B plug goes in the Uno, and the socket",
    "screws into the 6 mm back wall beside it. Measure its screw spacing before the wall is cut.",
    "The Uno lies sideways against the back wall so its plugs clear the MENU button's microswitch.",
    "Shield: press it onto the Uno, power jumper on 5V, nothing on SERVO_PWR.",
    "USB powers everything (500 mA fuse); see the budget in section 4."], 17)
add("</g>")

# =====================================================================
# 3  Light string
# =====================================================================
S3 = R3 + PH + 40
section(S3, "3", "Light string (H7)",
        "16 LED modules wired together at the factory. You solder only the start, then glue each module behind its hole.")
NY = S3 + 80
panel(40, NY, 720, 600, "Start of the string", "4 solder")
ys = header(44, 110, "D6")
SPL = 190
pin_y = [112, 160, 208]                       # GND, 5V, DIN at the first module
mods = [(430, 160), (560, 160), (690, 160)]
for py, ty_, c in zip(ys, pin_y, (BLACK, RED, SIG["D6"])):
    line([(60, py), (110, py), (140, ty_), (SPL, ty_)], c, 2.5)
resistor(SPL, pin_y[2], SPL + 60, pin_y[2], "330 Ω", 1)
for ty_ in pin_y:
    x_end = SPL + 60 if ty_ == pin_y[2] else SPL
    line([(x_end, ty_), (mods[0][0] - 40, ty_), (mods[0][0] - 20, 160 + (ty_ - 160) / 4)], STICK_WIRE, 3)
splice(SPL, pin_y[0])
splice(SPL, pin_y[1])
splice(SPL, pin_y[2])
splice(SPL + 60, pin_y[2])
# capacitor across 5V / GND at the joins
cx = SPL + 34
add(f'<line x1="{cx}" y1="{pin_y[0]}" x2="{cx}" y2="{pin_y[0] + 14}" stroke="#495057" stroke-width="2"/>'
    f'<path d="M{cx - 9},{pin_y[0] + 16} q9,5 18,0" fill="none" stroke="#212529" stroke-width="3"/>'
    f'<line x1="{cx - 9}" y1="{pin_y[1] - 16}" x2="{cx + 9}" y2="{pin_y[1] - 16}" stroke="#212529" stroke-width="3"/>'
    f'<line x1="{cx}" y1="{pin_y[1] - 16}" x2="{cx}" y2="{pin_y[1]}" stroke="#495057" stroke-width="2"/>')
t(cx + 13, pin_y[1] - 14, "+", "tinyb")
t(cx + 14, pin_y[0] + 22, "470–1000 µF, stripe (−) to GND", "tiny")
t(SPL + 150, 100, "string's input lead", "tiny", "middle")
for i, (mx, my) in enumerate(mods):
    add(f'<circle cx="{mx}" cy="{my}" r="22" fill="#212529"/><rect x="{mx - 8}" y="{my - 8}" width="16" height="16" '
        f'rx="2" fill="#fff3bf"/>')
    t(mx, my + 66, ("module 1 (THR 1)", "module 2", "module 3")[i], "tinyb", "middle")
    if i < 2:
        nx = mods[i + 1][0]
        for dy in (-6, 0, 6):
            add(f'<path d="M{mx + 22},{my + dy} C{mx + 60},{my + dy + 40} {nx - 60},{my + dy + 40} {nx - 22},{my + dy}" '
                f'fill="none" stroke="{STICK_WIRE}" stroke-width="2.5"/>')
t(495, 120, "factory wires, 9–12 cm", "tiny", "middle")
t(625, 120, "no solder", "tiny", "middle")
t(60, 278, "4 joints: the three input wires (G, V, and S through the 330 Ω). The capacitor's legs share the G and V joins.", "small")

ty = 316
t(16, ty, "How to fit it", "sec")
notes(16, ty + 22, [
    "1  Find the input end: the first module's pads read DIN (or its arrow points away from the input). Data only flows one way.",
    "2  Test the whole string on the bench before cutting: Adafruit's strandtest sketch, brightness 20, plugged on D6.",
    "3  Cut the wire between modules 16 and 17 and heat-shrink the 3 cut ends on both sides. The other 34 are spares.",
    "4  Glue each module LED-side against the underside of the panel, centred on its hole (hot glue round the edge).",
    "    Throttle bar: 10 mm modules on a 12.5 mm pitch, so they sit 2.5 mm apart. Order on the map →",
    "5  Fold the spare wire between modules into flat loops and tape or hot-glue them to the panel.",
    "    Keep them on the outside of the throttle tunnel: the sonar would see anything inside it.",
    "6  Never connect 5V and GND the wrong way round. Check the joins with the meter before plugging in.",
    "7  A dead module stops everything after it: cut it out and splice in a spare (6 joints, rare).",
    "8  The sketch caps brightness at 20 %: plenty behind the panel, and it keeps USB under its limit."], 17)
add("</g>")

# Panel map with the light route (compact preset: same numbers as make_cockpit.py)
PANEL_W, PANEL_D = 420.0, 245.0
THR_X, KNOB_W, SENSOR_FACE_Y, NEAR, TRAVEL = 72.0, 42.0, 44.0, 40.0, 100.0
CENTRE, BIG, MENU, STICK, ARM = 222.0, (222.0, 103.0), (222.0, 160.0), (345.0, 130.0), (140.0, 185.0)
LCD_Y, ANN_Y = 200.0, 230.0
STEM_MIN = SENSOR_FACE_Y + NEAR + 25.0
LED_X = THR_X + KNOB_W / 2 + 15.0
throttle = [(LED_X, STEM_MIN + TRAVEL / 16 + TRAVEL / 8 * i) for i in range(8)]
lights = [(CENTRE + d, ANN_Y) for d in (-50.0, -25.0, 0.0, 25.0, 50.0)]
shield = [(CENTRE + 48.0 + 13.0 * i, LCD_Y) for i in range(3)]
chain = throttle + lights + shield
UNO = (PANEL_W / 2, PANEL_D - 6.0 - 6.0 - 53.4 / 2)       # turned sideways against the back wall

panel(790, NY, W - 830, 600, "Where the modules sit: top view of the panel", "route", "#e64980")
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
add(f'<rect x="{ux - 34.3 * S}" y="{uy - 26.7 * S}" width="{68.6 * S}" height="{53.4 * S}" rx="4" fill="none" '
    f'stroke="#74c0fc" stroke-width="2" stroke-dasharray="5 4"/>')
# tunnel, slot and controls for orientation
tun_y1 = STEM_MIN + TRAVEL - 25.0 + 40.0 + 6.0                 # as make_cockpit.py
tx0, ty0 = m(THR_X - 30, tun_y1)
add(f'<rect x="{tx0}" y="{ty0}" width="{60 * S}" height="{(tun_y1 - 30.0) * S}" fill="none" stroke="#ffa94d" stroke-width="1.5" '
    f'stroke-dasharray="3 3"/>')
sx_, sy_ = m(THR_X, STEM_MIN + TRAVEL / 2)
add(f'<rect x="{sx_ - 4 * S}" y="{m(0, STEM_MIN + TRAVEL + 4)[1]}" width="{8 * S}" height="{(TRAVEL + 8) * S}" rx="6" fill="#868e96"/>')
t(tx0 - 6, sy_, "tunnel", "map", "end", 'style="fill:#ffa94d"')
for (cx_, cy_), r, lab in ((STICK, 20, "stick"), (BIG, 30, "EMP"), (MENU, 14, "MENU"), (ARM, 6, "ARM")):
    px_, py_ = m(cx_, cy_)
    add(f'<circle cx="{px_}" cy="{py_}" r="{r * S}" fill="#495057"/>')
    t(px_, py_ + 4, lab, "map", "middle", 'style="fill:#e9ecef"')
lx_, ly2 = m(CENTRE - 36, LCD_Y + 12.5)
add(f'<rect x="{lx_}" y="{ly2}" width="{72 * S}" height="{25 * S}" fill="#2f9e44"/>')
t(lx_ + 36 * S, ly2 + 12.5 * S + 4, "LCD", "map", "middle", 'style="fill:#fff"')
pts = [m(*UNO)] + [m(*p) for p in chain]
add('<polyline points="' + " ".join(f"{x:g},{y:g}" for x, y in pts) +
    '" fill="none" stroke="#e64980" stroke-width="2.5" stroke-dasharray="7 4" marker-mid="url(#arr)"/>')
for i, p in enumerate(chain):
    px_, py_ = m(*p)
    add(f'<circle cx="{px_}" cy="{py_}" r="{5 * S:g}" fill="{CHAIN[i]}" stroke="#212529"/>')
    off = (-12, 4, "end") if i < 8 else ((0, -11, "middle") if i < 13 else (0, 22, "middle"))
    t(px_ + off[0], py_ + off[1], str(i + 1), "mapb", off[2], 'style="fill:#fff"')
mx_, my_ = m(*UNO)
add(f'<circle cx="{mx_:g}" cy="{my_:g}" r="5" fill="#e64980"/>')
t(MX - 60, MY + PANEL_D * S + 34, "Dashed blue: Uno + shield on the floor below. Orange: throttle tunnel (keep wires out). "
                                  "Pink: data from D6 through modules 1–16.", "map")

LY = MY + PANEL_D * S + 72
hops = [math.dist(a, b) for a, b in zip(chain, chain[1:])]
longest = max(range(len(hops)), key=lambda i: hops[i])
t(16, LY, "Wire check", "sec")
notes(16, LY + 22, [
    f"Longest hop: {longest + 1} → {longest + 2}, {hops[longest]:.0f} mm centre to centre. The factory wire (9–12 cm) reaches it.",
    f"Throttle hops are {hops[0]:.1f} mm, so each one leaves a 7–10 cm loop to fold flat.",
    f"D6 to module 1: about {math.dist(UNO, chain[0]) + 40:.0f} mm (with the drop to the floor): the D6 cable half (15 cm)",
    "plus the string's input lead covers it; splice in extra wire only if yours is short."], 17)
add("</g>")

# =====================================================================
# 4  Soldering summary
# =====================================================================
S4 = NY + 600 + 40
section(S4, "4", "Soldering summary, build order and rules",
        "24 joints in all, every one on a tab or a wire-to-wire join. No small boards to solder.")
TY = S4 + 80
cols = [56, 400, 500, 1060]
for x, h in zip(cols, ("Harness", "Joints", "What you solder", "Difficulty")):
    t(x, TY, h, "th")
add(f'<line x1="40" y1="{TY + 8}" x2="{W - 40}" y2="{TY + 8}" stroke="#adb5bd"/>')
table = [
    ("H1  Joystick", "11", "6 pot lugs (X, Y) + 5 joins (twist, button)", "Medium: keep each lug on its pin"),
    ("H2  EMP 60 mm dome", "4", "COM and NO tabs + the 2 lamp tabs (or crimp spades)", "Easy"),
    ("H3  MENU button", "2", "COM and NO tabs (or crimp spades)", "Easy: good first practice"),
    ("H4  MASTER ARM", "3", "3 tabs, the 10 kΩ shares two of them", "Easy"),
    ("H5  Sonar", "0", "4 single jumpers", "None"),
    ("H6  Buzzer module", "0", "Gravity cable", "None"),
    ("H7  Light string", "4", "3 input wires + the 330 Ω; capacitor shares the joins", "Easy: get the input end and 5V/GND right"),
    ("H8  LCD", "0", "4-pin cable", "None"),
    ("H9  Rumble module", "0", "Gravity cable + 3 jumpers", "None"),
    ("H10  USB", "0", "Panel cable, 2 screws", "None"),
]
for i, rowv in enumerate(table):
    yy = TY + 28 + i * 21
    if i % 2 == 0:
        add(f'<rect x="40" y="{yy - 15}" width="{W - 80}" height="21" fill="#f1f3f5"/>')
    for x, v in zip(cols, rowv):
        t(x, yy, v, "small")
BY = TY + 28 + len(table) * 21 + 20
BH = 230
add(f'<rect x="40" y="{BY - 20}" width="{(W - 100) / 2}" height="{BH}" rx="8" fill="#e7f5ff" stroke="#a5d8ff"/>')
t(56, BY + 4, "Build order", "sec")
notes(56, BY + 28, [
    "1  Bench test every part on the shield first: jumpers and Gravity cables, nothing soldered.",
    "2  Light string: find the input end, test the whole string, then solder the start.",
    "3  Practise on the tabs: MENU, EMP, then MASTER ARM with its 10 kΩ.",
    "4  Joystick: solder the X and Y lugs, then splice the twist and button leads.",
    "5  Mount everything, plug each cable on its row (photo of the shield first),",
    "    then run the standby self-test on the LCD: stick, FIRE, distance.",
    "6  A dab of hot glue on each plug stops it working loose."], 19)
RX0 = 40 + (W - 100) / 2 + 20
add(f'<rect x="{RX0}" y="{BY - 20}" width="{(W - 100) / 2}" height="{BH}" rx="8" fill="#fff4e6" stroke="#f0c29a"/>')
t(RX0 + 16, BY + 4, "Rules", "sec")
notes(RX0 + 16, BY + 28, [
    "Power from USB (500 mA fuse): Uno 50 + LCD 25 + sonar 15 + lights ≤ 180 (16 × ~55 mA × 20 %)",
    "+ rumble ~80 + buzzer ~30 ≈ 380 mA. The 20 % brightness cap is what keeps it there.",
    "Buttons use the Uno's internal pull-ups: pressed = LOW. MASTER ARM is the exception: armed = HIGH.",
    "Lights: 330 Ω on data and 470–1000 µF at the start (Adafruit Überguide); 5 V data is fine.",
    "Sketch: update the lights right before each AX packet and have the game send right after it.",
    "    Updating 16 lights pauses serial input for ~0.5 ms, so bytes arriving then would be lost.",
    "Heat-shrink every wire-to-wire join; tape comes loose inside a box."], 19)

H = BY + BH
svg_head = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H:g}" width="{W}" height="{H:g}" '
            'font-family="Segoe UI, Helvetica, Arial, sans-serif">\n<title>Axiom cockpit controller wiring (v2)</title>')
o[o.index("@@HEADER@@")] = svg_head
o[o.index("@@BG@@")] = f'<rect width="{W}" height="{H:g}" fill="#fff"/>'
add("</svg>")
OUT.write_text("\n".join(x for x in o if x), encoding="utf-8")
print("wrote", OUT, f"{W} x {H:g}")
