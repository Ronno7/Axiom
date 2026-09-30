#!/usr/bin/env python3
"""Axiom controller enclosure drawings.

Generates, for each build style:
  blueprint-<mode>.svg  assembly drawing
  templates-<mode>.svg  1:1 cutting templates (print at 100% on US Letter)

Modes:
  case       uxcell / generic 250 x 190 x 80 mm ABS instrument case (recommended), foam-board internals
  cardboard  the all-cardboard 204 x 258 x 68 mm box

Every size is in millimetres. Measure your case when it arrives, edit its preset below, and rerun:
  python make_drawings.py            (both modes)
  python make_drawings.py case       (one mode)

Interior coordinates: x from the inside of the LEFT wall, y from the inside of the FRONT
(towards the back), z up from the floor. Outer positions add the shell thickness T.
"""
import math
import sys
from pathlib import Path

PRESETS = {
    "case": dict(
        TITLE="ABS INSTRUMENT CASE EDITION",
        SUBTITLE="250 × 190 × 80 mm ABS instrument case (removable front/rear panels), internals from 4–5 mm foam board, hot glued. "
                 "Interior is an estimate: measure yours and update the preset.",
        T=3.0, SHEET=4.0, W_IN=184.0, D_IN=244.0, H_IN=74.0,
        TUN_W=50.0, D_FAR=150.0, STEM_LEN=105.0,
        TOWER_X=128.0, TOWER_Y=28.0, TOWER_H=56.0,
        BUTTON_X=158.0, BUTTON_Z=37.0,
        BREADBOARD=(58.0, 73.0, 55.0, 165.0), UNO_X=117.0, GRILLE=(86.0, 222.0),
        LID="TOP SHELL", FRONT="FRONT PANEL", BACK="REAR PANEL", SHELL="#cfd4d8", SHELL_EDGE="#7b848c",
        CLOSURE="Top shell held by the case's own screws",
        STEPS=["Measure the interior; update the case preset and rerun this script. Print templates-case.svg at 100%.",
               "Cut openings with the Dremel (low–medium speed so ABS doesn't melt): top-shell slot and stick hole, front-panel square, rear-panel USB hole.",
               "Build the tunnel (divider + sensor wall) and sled from foam board; hot glue the tunnel to the case floor. Tape-line it.",
               "Glue HC-SR04 behind the window, tower + joystick, trigger button; place Uno and breadboard.",
               "Wire (see wiring.svg), close the case, push on the knob. Calibrate in game: Options → Calibrate."],
    ),
    "cardboard": dict(
        TITLE="CARDBOARD EDITION",
        SUBTITLE="Desk console in 4 mm corrugated cardboard or foam board, hot glued.",
        T=4.0, SHEET=4.0, W_IN=196.0, D_IN=250.0, H_IN=60.0,
        TUN_W=56.0, D_FAR=170.0, STEM_LEN=90.0,
        TOWER_X=144.0, TOWER_Y=32.0, TOWER_H=42.0,
        BUTTON_X=176.0, BUTTON_Z=30.0,
        BREADBOARD=(66.0, 78.0, 55.0, 165.0), UNO_X=138.0, GRILLE=(94.0, 225.0),
        LID="LID", FRONT="FRONT WALL", BACK="BACK WALL", SHELL="#e9dcc3", SHELL_EDGE="#8a7757",
        CLOSURE="Lid hinge: packing tape along this top edge",
        STEPS=["Cut floor, walls, divider, sensor wall, lid; print templates-cardboard.svg at 100% for the openings.",
               "Glue walls to the floor, then the divider and sensor wall. Tape-line the tunnel.",
               "Build the sled; check it slides full length with the stem through a test slot.",
               "Glue HC-SR04 behind the window, tower + joystick, trigger button; place Uno and breadboard.",
               "Wire (see wiring.svg), fit the lid, push on the knob. Calibrate in game: Options → Calibrate."],
    ),
}

# Shared part sizes (researched; see README.md)
SENSOR_WIN_W, SENSOR_WIN_H = 17.0, 43.0   # window for the transducers (sensor mounted on its side)
CAN_LEN = 12.0                 # HC-SR04 transducer can length
D_NEAR = 50.0                  # plate distance from the transducer faces at the near end of travel
SLED_LEN, RUNNER_H = 50.0, 15.0
STEM_W, STEM_FROM_PLATE = 10.0, 25.0
SLOT_W = 8.0
TOWER_SIDE = 40.0
STICK_HOLE = 30.0              # lets the cap dip into the lid at full tilt
JOY_PCB = (34.0, 26.0)
BUTTON_CUT, BUTTON_CAP = 6.5, 18.0
UNO_W, UNO_L, USB_OUT = 53.4, 68.6, 6.2
USB_HOLE_W, USB_HOLE_H = 18.0, 16.0
LED_D = 5.0                    # runway LEDs beside the slot (5 mm LEDs)


def configure(mode):
    """Loads a preset and computes the derived geometry as module globals used by the drawings."""
    g = globals()
    g.update(PRESETS[mode])
    g["MODE"] = mode
    g["W"], g["D"], g["H"] = W_IN + 2 * T, D_IN + 2 * T, H_IN + 2 * T
    # The sensor wall sits just far enough back for the sled to reach D_FAR with 2 mm to spare.
    g["SENSOR_WALL_Y"] = SLED_LEN + D_FAR + CAN_LEN - SHEET + 2
    g["STICK_X"], g["STICK_Y"] = TOWER_X + TOWER_SIDE / 2, TOWER_Y + TOWER_SIDE / 2
    g["UNO"] = (UNO_X, D_IN - USB_OUT - UNO_L, UNO_W, UNO_L)
    g["USB_X"] = UNO_X + 37.8   # jack centre along the board edge (check against your board)
    g["TUN_CX"], g["TUN_CZ"] = TUN_W / 2, H_IN / 2
    g["FACE_Y"] = SENSOR_WALL_Y + SHEET - CAN_LEN
    g["PLATE_NEAR"], g["PLATE_FAR"] = FACE_Y - D_NEAR, FACE_Y - D_FAR
    g["STEM_NEAR"], g["STEM_FAR"] = PLATE_NEAR - STEM_FROM_PLATE, PLATE_FAR - STEM_FROM_PLATE
    g["SLOT_Y0"], g["SLOT_Y1"] = STEM_FAR - STEM_W / 2, STEM_NEAR + STEM_W / 2
    g["SLED_W"] = TUN_W - 2
    g["PLATE_H"] = H_IN - 4
    g["TRAVEL"] = D_FAR - D_NEAR
    # Runway LEDs: just past the divider (clear of the 40 mm knob), one at the middle of each fifth of
    # the lever's travel, LED 1 nearest the sensor.
    g["LED_X"] = TUN_W + SHEET + 5
    g["LED_YS"] = [STEM_NEAR - (i + 0.5) * (STEM_NEAR - STEM_FAR) / 5 for i in range(5)]


BLUE, RED, ORANGE = "#1c6fd1", "#d62828", "#e8590c"


def f(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


# =====================================================================
# Blueprint (screen/print sheet, 2 px per mm in each view)
# =====================================================================
class View:
    """Maps millimetres to page pixels. Plan views put +y (towards the back) up the page."""

    def __init__(self, ox, oy, scale=2.0, y_up=True):
        self.ox, self.oy, self.s, self.y_up = ox, oy, scale, y_up

    def p(self, x, y):
        return self.ox + x * self.s, (self.oy - y * self.s) if self.y_up else (self.oy + y * self.s)

    def rect(self, x, y, w, h, cls="", extra=""):
        x0, y0 = self.p(x, y + h if self.y_up else y)
        return f'<rect x="{f(x0)}" y="{f(y0)}" width="{f(w * self.s)}" height="{f(h * self.s)}" class="{cls}" {extra}/>'

    def circle(self, x, y, r, cls="", extra=""):
        cx, cy = self.p(x, y)
        return f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r * self.s)}" class="{cls}" {extra}/>'

    def line(self, x1, y1, x2, y2, cls="", extra=""):
        a, b = self.p(x1, y1), self.p(x2, y2)
        return f'<line x1="{f(a[0])}" y1="{f(a[1])}" x2="{f(b[0])}" y2="{f(b[1])}" class="{cls}" {extra}/>'

    def poly(self, pts, cls="", extra=""):
        s = " ".join(f"{f(a)},{f(b)}" for a, b in (self.p(x, y) for x, y in pts))
        return f'<polygon points="{s}" class="{cls}" {extra}/>'

    def text(self, x, y, s, cls="lbl", anchor="start", dx=0, dy=0):
        px, py = self.p(x, y)
        return f'<text x="{f(px + dx)}" y="{f(py + dy)}" class="{cls}" text-anchor="{anchor}">{s}</text>'

    def dim_h(self, x1, x2, y, label, off=10):
        """Horizontal dimension; off is in px, positive = below (page)."""
        a, b = self.p(x1, y), self.p(x2, y)
        yy = a[1] + off
        return (f'<line x1="{f(a[0])}" y1="{f(a[1])}" x2="{f(a[0])}" y2="{f(yy + 4 * (1 if off > 0 else -1))}" class="ext"/>'
                f'<line x1="{f(b[0])}" y1="{f(b[1])}" x2="{f(b[0])}" y2="{f(yy + 4 * (1 if off > 0 else -1))}" class="ext"/>'
                f'<line x1="{f(a[0])}" y1="{f(yy)}" x2="{f(b[0])}" y2="{f(yy)}" class="dim" marker-start="url(#a)" marker-end="url(#a)"/>'
                f'<text x="{f((a[0] + b[0]) / 2)}" y="{f(yy - 4)}" class="dimt" text-anchor="middle">{label}</text>')

    def dim_v(self, x, y1, y2, label, off=-10):
        a, b = self.p(x, y1), self.p(x, y2)
        xx = a[0] + off
        tip = xx + (4 if off > 0 else -4)
        return (f'<line x1="{f(a[0])}" y1="{f(a[1])}" x2="{f(tip)}" y2="{f(a[1])}" class="ext"/>'
                f'<line x1="{f(b[0])}" y1="{f(b[1])}" x2="{f(tip)}" y2="{f(b[1])}" class="ext"/>'
                f'<line x1="{f(xx)}" y1="{f(a[1])}" x2="{f(xx)}" y2="{f(b[1])}" class="dim" marker-start="url(#a)" marker-end="url(#a)"/>'
                f'<text transform="translate({f(xx - 4)},{f((a[1] + b[1]) / 2)}) rotate(-90)" class="dimt" text-anchor="middle">{label}</text>')


def callout(x1, y1, x2, y2, label, anchor="start"):
    return (f'<polyline points="{f(x1)},{f(y1)} {f(x2)},{f(y2)}" class="lead"/>'
            f'<circle cx="{f(x1)}" cy="{f(y1)}" r="2" fill="#333"/>'
            f'<text x="{f(x2 + (4 if anchor == "start" else -4))}" y="{f(y2 + 4)}" class="lbl" text-anchor="{anchor}">{label}</text>')


def blueprint():
    out = []
    add = out.append
    add('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1760 1720" width="1760" height="1720" '
        'font-family="Segoe UI, Helvetica, Arial, sans-serif">')
    add("""<title>Axiom controller enclosure blueprint (MODENAME)</title>
<defs><marker id="a" viewBox="0 0 10 10" refX="5" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
<path d="M0,1 L9,5 L0,9" fill="none" stroke="#444" stroke-width="1.4"/></marker>
<pattern id="hatch" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
<line x1="0" y1="0" x2="0" y2="6" stroke="#b9a98f" stroke-width="2"/></pattern></defs>
<style>
.sheet{fill:SHELLFILL;stroke:SHELLEDGE;stroke-width:1.2}
.cut{fill:url(#hatch);stroke:#8a7757;stroke-width:1.2}
.void{fill:#fbf8f2;stroke:#8a7757;stroke-width:1}
.hole{fill:#ffffff;stroke:#222;stroke-width:1.4}
.lidcut{fill:none;stroke:#222;stroke-width:1.4;stroke-dasharray:6 4}
.ghost{fill:none;stroke:#999;stroke-width:1;stroke-dasharray:4 3}
.pcbg{fill:#2f7d4f;stroke:#1d5233;stroke-width:1}
.pcbb{fill:#1c5fa8;stroke:#123f70;stroke-width:1}
.uno{fill:#0f7291;stroke:#0a4f66;stroke-width:1}
.bb{fill:#f4f4f0;stroke:#bdbdb4;stroke-width:1}
.metal{fill:#c9ced3;stroke:#7d868f;stroke-width:1}
.dark{fill:#2b2b2b;stroke:#111;stroke-width:1}
.plate{fill:#f7f7f7;stroke:#333;stroke-width:1.6}
.sled{fill:#d7c7a6;stroke:#6e5d3e;stroke-width:1.2}
.beam{fill:rgba(28,111,209,0.12);stroke:#1c6fd1;stroke-width:1;stroke-dasharray:5 3}
.ext{stroke:#777;stroke-width:0.8}
.dim{stroke:#444;stroke-width:1}
.dimt{font-size:12px;fill:#222}
.lead{fill:none;stroke:#333;stroke-width:1}
.lbl{font-size:13px;fill:#222}
.lbls{font-size:11px;fill:#555}
.lblw{font-size:11px;fill:#fff;font-weight:700}
.title{font-size:17px;font-weight:700;fill:#111}
.sub{font-size:12px;fill:#555}
.tbl{font-size:12.5px;fill:#222}
.tblh{font-size:13px;font-weight:700;fill:#111}
</style>
<rect width="1760" height="1720" fill="#ffffff"/>""")
    add(f'<text x="40" y="44" font-size="26" font-weight="800" fill="#111">AXIOM CONTROLLER · ENCLOSURE BLUEPRINT · {TITLE}</text>')
    add(f'<text x="40" y="68" class="sub">{SUBTITLE} All dimensions in mm. Outer {f(W)} W × {f(D)} D × {f(H)} H. '
        f'Generated by make_drawings.py ({MODE} preset).</text>')

    # ---------------- 1. Interior plan ----------------
    v = View(250, 700)
    add('<text x="40" y="110" class="title">1 · INTERIOR LAYOUT (lid removed)</text>')
    add('<text x="40" y="128" class="sub">Front of the console at the bottom. Dashed = lid openings above.</text>')
    add(v.rect(-T, -T, W, D, "sheet"))
    add(v.rect(0, 0, W_IN, D_IN, "void"))
    # tunnel and walls
    add(v.rect(TUN_W, 0, SHEET, SENSOR_WALL_Y, "cut"))                      # divider
    add(v.rect(0, SENSOR_WALL_Y, TUN_W, SHEET, "cut"))                      # sensor wall
    add(v.rect(0, 0, TUN_W, SENSOR_WALL_Y, "void", 'fill="#f3ecde"'))    # tunnel floor tint
    # sensor behind the wall (on its side): cans through the window, PCB flat on the back face
    add(v.rect(TUN_CX - 8, FACE_Y, 16, CAN_LEN - SHEET, "metal"))
    add(v.rect(TUN_CX - 10, SENSOR_WALL_Y + SHEET, 20, 1.6, "pcbb"))
    add(v.rect(TUN_CX + 10, SENSOR_WALL_Y + SHEET - 1, 25, 3, "dark"))       # header + sockets, pointing +x
    # sled at mid travel, ghosts at both ends
    mid = (PLATE_NEAR + PLATE_FAR) / 2
    for py, cls in ((PLATE_NEAR, "ghost"), (PLATE_FAR, "ghost"), (mid, "sled")):
        add(v.rect(1, py - SLED_LEN, SLED_W, SLED_LEN, cls))
        if cls == "sled":
            add(v.rect(1, py - T, SLED_W, T, "plate"))
            add(v.rect(TUN_CX - STEM_W / 2, py - STEM_FROM_PLATE - 2, STEM_W, 4, "dark"))
    add(v.poly([(TUN_CX - 8, FACE_Y), (TUN_CX + 8, FACE_Y), (TUN_CX + 8 + (FACE_Y - mid) * math.tan(math.radians(7.5)), mid),
                (TUN_CX - 8 - (FACE_Y - mid) * math.tan(math.radians(7.5)), mid)], "beam"))
    add(v.rect(TUN_CX - SLOT_W / 2, SLOT_Y0, SLOT_W, SLOT_Y1 - SLOT_Y0, "lidcut"))
    for ly in LED_YS:
        add(v.circle(LED_X, ly, LED_D / 2, "lidcut"))
    # electronics
    bx, by, bw, bl = BREADBOARD
    add(v.rect(bx, by, bw, bl, "bb"))
    for xx in (bx + 4, bx + 8, bx + bw - 8, bx + bw - 4):
        add(v.line(xx, by + 4, xx, by + bl - 4, "", f'stroke="{RED if xx in (bx + 4, bx + bw - 4) else BLUE}" stroke-width="1"'))
    add(v.rect(bx + bw / 2 - 1.5, by + 3, 3, bl - 6, "", 'fill="#dcdcd4"'))
    add(v.text(bx + bw / 2, by + bl / 2, "BREADBOARD", "lbls", "middle"))
    add(v.circle(bx + bw / 2 + 12, by + bl - 22, 6, "dark"))
    ux, uy, uw, ul = UNO
    add(v.rect(ux, uy, uw, ul, "uno"))
    add(v.rect(USB_X - 6, uy + ul - 4, 12, USB_OUT + 4, "metal"))
    add(v.text(ux + uw / 2, uy + ul / 2 - 4, "UNO", "lblw", "middle"))
    # joystick tower
    add(v.rect(TOWER_X, TOWER_Y, TOWER_SIDE, TOWER_SIDE, "sled"))
    add(v.rect(STICK_X - JOY_PCB[0] / 2, STICK_Y - JOY_PCB[1] / 2, JOY_PCB[0], JOY_PCB[1], "pcbg"))
    add(v.rect(STICK_X - JOY_PCB[0] / 2 - 8, STICK_Y - 6, 8, 12, "dark"))
    add(v.circle(STICK_X, STICK_Y, 12, "dark"))
    add(v.circle(STICK_X, STICK_Y, STICK_HOLE / 2, "lidcut"))
    # trigger behind the front wall
    add(v.rect(BUTTON_X - 3, 0, 6, 4, "dark"))
    add(v.rect(BUTTON_X - BUTTON_CAP / 2, -T - 3, BUTTON_CAP, 3, "plate"))
    # grille and USB
    add(v.circle(GRILLE[0], GRILLE[1], 9, "lidcut"))
    add(v.rect(USB_X - USB_HOLE_W / 2, D_IN, USB_HOLE_W, T, "hole"))
    # callouts
    X = lambda x: v.p(x, 0)[0]
    Y = lambda y: v.p(0, y)[1]
    add(callout(X(TUN_CX), Y(FACE_Y + 6), X(-T) - 10, Y(D_IN + 26), "HC-SR04 glued behind sensor wall", "end"))
    add(callout(X(TUN_CX), Y(mid - 20), X(-T) - 10, Y(mid - 20), "Sled + reflector plate", "end"))
    add(callout(X(TUN_CX), Y(STEM_FAR), X(-T) - 10, Y(STEM_FAR - 10), "Lid slot (lever travel)", "end"))
    add(callout(X(6), Y(SENSOR_WALL_Y - 40), X(-T) - 10, Y(SENSOR_WALL_Y - 40), "Throttle tunnel (tape-lined)", "end"))
    add(callout(X(STICK_X + 12), Y(STICK_Y + 10), X(W_IN + T) + 16, Y(STICK_Y + 44), "Joystick on tower (h " + f(TOWER_H) + ")"))
    add(callout(X(BUTTON_X), Y(-T - 1), X(W_IN + T) + 16, Y(10), "Trigger: 6×6 button + cap"))
    add(callout(X(USB_X), Y(D_IN + 2), X(W_IN + T) + 16, Y(D_IN + 20), f"USB hole in {BACK.lower()}"))
    add(callout(X(ux + uw / 2), Y(uy + 20), X(W_IN + T) + 16, Y(uy + 20), "Uno (USB to the back)"))
    add(callout(X(bx + bw / 2 + 12), Y(by + bl - 22), X(W_IN + T) + 16, Y(by + bl - 60), "Buzzer (under lid grille)"))
    add(v.dim_h(-T, W_IN + T, -T, f"{f(W)} outer", 34))
    add(v.dim_h(0, TUN_W, 0, f"{f(TUN_W)} tunnel", 18))
    add(v.dim_v(-T, -T, D_IN + T, f"{f(D)} outer", -34))

    # ---------------- 2. Lid ----------------
    L = View(990, 700)
    add(f'<text x="990" y="110" class="title">2 · {LID} CUTOUTS</text>')
    add(f'<text x="990" y="128" class="sub">Nominal, from the outer LEFT and FRONT edges. Confirm by marking through from inside with parts in place.</text>')
    add(L.rect(0, 0, W, D, "sheet"))
    sx, sy = TUN_CX + T, 0
    add(L.rect(sx - SLOT_W / 2, SLOT_Y0 + T, SLOT_W, SLOT_Y1 - SLOT_Y0, "hole"))
    add(L.circle(STICK_X + T, STICK_Y + T, STICK_HOLE / 2, "hole"))
    for i in range(7):
        a = i * math.pi / 3
        gx, gy = (GRILLE[0] + T, GRILLE[1] + T) if i == 6 else (GRILLE[0] + T + 7 * math.cos(a), GRILLE[1] + T + 7 * math.sin(a))
        add(L.circle(gx, gy, 1.5, "hole"))
    for i, ly in enumerate(LED_YS):
        add(L.circle(LED_X + T, ly + T, LED_D / 2, "hole"))
        add(L.text(LED_X + T + 5, ly + T - 1.5, str(i + 1), "lbls"))
    add(L.text(LED_X + T + 12, (LED_YS[0] + LED_YS[-1]) / 2 + T + 4, f"runway LEDs 5× Ø{f(LED_D)}", "lbls"))
    add(L.text(LED_X + T + 12, (LED_YS[0] + LED_YS[-1]) / 2 + T - 4,
               f"{f(LED_X + T)} from left, pitch {f(LED_YS[0] - LED_YS[1])}", "lbls"))
    add(L.line(sx, SLOT_Y0 + T, sx, -2, "", 'stroke="#aaa" stroke-dasharray="3 3"'))
    add(L.dim_h(0, sx, 0, f(sx), 20))
    add(L.dim_h(0, STICK_X + T, 0, f(STICK_X + T), 40))
    add(L.dim_v(W, 0, SLOT_Y0 + T, f(SLOT_Y0 + T), 18))
    add(L.dim_v(W, 0, STICK_Y + T, f(STICK_Y + T), 44))
    add(L.dim_v(sx - SLOT_W / 2, SLOT_Y0 + T, SLOT_Y1 + T, f"slot {f(SLOT_Y1 - SLOT_Y0)} × {f(SLOT_W)}", -10))
    add(L.text(STICK_X + T, STICK_Y + T + STICK_HOLE / 2 + 4, f"Ø{f(STICK_HOLE)}", "lbl", "middle"))
    add(L.text(GRILLE[0] + T, GRILLE[1] + T - 16, "sound grille 7× Ø3", "lbls", "middle"))
    add(L.text(W / 2, D - 8, "BACK EDGE", "lbls", "middle"))
    add(L.text(W / 2, 6, "FRONT EDGE", "lbls", "middle"))
    add(L.text(W / 2, D / 2 + 30, "Throttle knob push-fits on the stem", "lbls", "middle"))
    add(L.text(W / 2, D / 2 + 22, "(not glued) so the lid can lift off.", "lbls", "middle"))

    # ---------------- 3. Section through the tunnel ----------------
    S = View(120, 1070)
    add('<text x="40" y="800" class="title">3 · SECTION A–A THROUGH THE THROTTLE</text>')
    add('<text x="40" y="818" class="sub">Viewed from the left; front of the console on the left. Sled at mid travel, dashed at both ends.</text>')
    # page x = y (front→back), page y = z
    Sz = lambda y, z: S.p(y + T, z + T)

    class SV:  # section helpers: (y, z) in interior coords
        @staticmethod
        def rect(y, z, dy, dz, cls, extra=""):
            return S.rect(y + T, z + T, dy, dz, cls, extra)

        @staticmethod
        def line(y1, z1, y2, z2, cls="", extra=""):
            return S.line(y1 + T, z1 + T, y2 + T, z2 + T, cls, extra)

        @staticmethod
        def poly(pts, cls, extra=""):
            return S.poly([(a + T, b + T) for a, b in pts], cls, extra)

    add(S.rect(0, 0, D, T, "cut"))                                   # floor
    add(S.rect(0, H - T, TUN_CX, T, "cut"))                          # lid pieces either side of the slot
    add(SV.rect(SLOT_Y1, H_IN, D_IN - SLOT_Y1 + T, T, "cut"))
    add(SV.rect(-T, H_IN, SLOT_Y0 + T, T, "cut"))
    add(S.rect(0, T, T, H_IN, "cut"))                                # front wall
    add(S.rect(D - T, T, T, H_IN, "cut"))                            # back wall
    win_z0 = TUN_CZ - SENSOR_WIN_H / 2
    add(SV.rect(SENSOR_WALL_Y, 0, SHEET, win_z0, "cut"))                 # sensor wall below/above the window
    add(SV.rect(SENSOR_WALL_Y, win_z0 + SENSOR_WIN_H, SHEET, H_IN - win_z0 - SENSOR_WIN_H, "cut"))
    add(SV.rect(SENSOR_WALL_Y + SHEET, TUN_CZ - 22.5, 1.6, 45, "pcbb"))  # PCB on its side
    for cz in (TUN_CZ - 13, TUN_CZ + 13):
        add(SV.rect(FACE_Y, cz - 8, CAN_LEN, 16, "metal"))
    add(SV.poly([(FACE_Y, TUN_CZ - 16), (FACE_Y, TUN_CZ + 16), (mid, TUN_CZ + 16 + (FACE_Y - mid) * math.tan(math.radians(7.5))),
                 (mid, TUN_CZ - 16 - (FACE_Y - mid) * math.tan(math.radians(7.5)))], "beam"))
    for py in (PLATE_NEAR, PLATE_FAR):
        add(SV.rect(py - SLED_LEN, 0, SLED_LEN, PLATE_H, "ghost"))
    add(SV.rect(mid - SLED_LEN, 0, SLED_LEN, RUNNER_H, "sled"))       # base + runners (side view)
    add(SV.rect(mid - T, 0, T, PLATE_H, "plate"))                      # reflector plate
    add(SV.poly([(mid - T, RUNNER_H), (mid - T, RUNNER_H + 28), (mid - T - 28, RUNNER_H)], "sled"))  # gusset
    stem_y = mid - STEM_FROM_PLATE
    add(SV.rect(stem_y - STEM_W / 2, RUNNER_H, STEM_W, STEM_LEN - RUNNER_H, "sled"))
    add(SV.rect(stem_y - 15, H_IN - 4, 30, 3, "sled"))                  # collar under the lid
    add(SV.rect(stem_y - 20, H_IN + T + 14, 40, 14, "dark"))            # knob
    add(S.text(stem_y + T, H + 34, "KNOB (push-fit)", "lbls", "middle"))
    Sx = lambda y: S.p(y + T, 0)[0]
    Sy = lambda z: S.p(0, z + T)[1]
    add(callout(Sx(FACE_Y + 6), Sy(TUN_CZ + 13), Sx(FACE_Y - 10), Sy(H_IN + 44), "HC-SR04 hidden behind the sensor wall"))
    add(callout(Sx(mid - 2), Sy(PLATE_H - 6), Sx(mid + 20), Sy(H_IN + 34), "Reflector plate: flat, square to the sensor"))
    add(callout(Sx(stem_y - 15), Sy(H_IN - 3), Sx(stem_y - 60), Sy(H_IN + 34), "Collar rides under lid", "end"))
    add(callout(Sx(mid - 30), Sy(RUNNER_H / 2), Sx(mid - 60), Sy(-40), "Sled runs on the tunnel floor", "end"))
    add(SV.line(FACE_Y, -10, FACE_Y, -2, "ext"))
    add(S.dim_h(PLATE_NEAR + T, FACE_Y + T, 0, f"near {f(D_NEAR)}", 34))
    add(S.dim_h(PLATE_FAR + T, FACE_Y + T, 0, f"far {f(D_FAR)} (travel {f(TRAVEL)})", 58))
    add(S.dim_v(D, T, H - T, f"{f(H_IN)} inside", 30))
    add(S.dim_v(D + 30, 0, H, f"{f(H)} outer", 30))

    # ---------------- 4. Front and back views ----------------
    F = View(800, 1020)
    add('<text x="800" y="800" class="title">4 · FRONT VIEW</text>')
    add(F.rect(0, 0, W, H, "sheet"))
    add(F.rect(BUTTON_X + T - BUTTON_CUT / 2, BUTTON_Z + T - BUTTON_CUT / 2, BUTTON_CUT, BUTTON_CUT, "hole"))
    add(F.circle(BUTTON_X + T, BUTTON_Z + T, BUTTON_CAP / 2, "ghost"))
    add(F.rect(STICK_X + T - 13, H, 26, 12, "dark"))                  # stick cap above the lid
    add(F.rect(TUN_CX + T - STEM_W / 2, H, STEM_W, 14, "sled"))       # throttle stem above the lid
    add(F.rect(TUN_CX + T - 20, H + 14, 40, 14, "dark"))              # throttle knob
    add(F.dim_h(0, BUTTON_X + T, 0, f(BUTTON_X + T), 22))
    add(F.dim_v(W, 0, BUTTON_Z + T, f(BUTTON_Z + T), 20))
    add(F.text(BUTTON_X + T + 12, BUTTON_Z + T + 6, f"trigger: {f(BUTTON_CUT)} square, Ø{f(BUTTON_CAP)} cap", "lbls"))

    B = View(1300, 1020)
    add('<text x="1300" y="800" class="title">5 · BACK VIEW</text>')
    add(B.rect(0, 0, W, H, "sheet"))
    ux_out = W - (USB_X + T)   # seen from behind, left/right swap
    add(B.rect(ux_out - USB_HOLE_W / 2, T, USB_HOLE_W, USB_HOLE_H, "hole"))
    add(B.dim_h(0, ux_out, 0, f"{f(ux_out)} from this edge", 22))
    add(B.text(ux_out + USB_HOLE_W / 2 + 6, T + 10, f"USB notch {f(USB_HOLE_W)} × {f(USB_HOLE_H)} from the floor", "lbls"))
    add(B.text(0, H + 6, CLOSURE, "lbls"))

    # ---------------- 6. Sled detail ----------------
    add('<text x="1000" y="1210" class="title">6 · THROTTLE SLED (the moving part)</text>')
    Q = View(1030, 1380)
    add(Q.rect(0, 0, SLED_W, SLED_LEN, "sled"))                     # base (top view)
    add(Q.rect(0, SLED_LEN - T, SLED_W, T, "plate"))
    add(Q.rect(0, 0, 3, SLED_LEN - T, "sheet"))
    add(Q.rect(SLED_W - 3, 0, 3, SLED_LEN - T, "sheet"))
    add(Q.rect(SLED_W / 2 - STEM_W / 2, SLED_LEN - T - STEM_FROM_PLATE - 2, STEM_W, 4, "dark"))
    add(Q.dim_h(0, SLED_W, 0, f(SLED_W), 18))
    add(Q.dim_v(0, 0, SLED_LEN, f(SLED_LEN), -14))
    add(Q.text(SLED_W / 2, SLED_LEN + 6, "top: base + runners, plate at the back", "lbls", "middle"))
    R2 = View(1200, 1380)
    add(R2.rect(0, 0, SLED_W, PLATE_H, "plate"))
    add(R2.rect(SLED_W / 2 - STEM_W / 2, 0, STEM_W, 0, "dark"))
    add(R2.dim_h(0, SLED_W, 0, f(SLED_W), 18))
    add(R2.dim_v(SLED_W, 0, PLATE_H, f(PLATE_H), 16))
    add(R2.text(SLED_W / 2, PLATE_H / 2, "REFLECTOR", "lbls", "middle"))
    add(R2.text(SLED_W / 2, PLATE_H / 2 - 7, "smooth face", "lbls", "middle"))
    add(R2.text(SLED_W / 2, PLATE_H + 6, "back: plate fills the tunnel", "lbls", "middle"))
    notes = ["Base " + f(SLED_W) + " × " + f(SLED_LEN) + "; runners 2× " + f(SLED_LEN) + " × " + f(RUNNER_H) + " glued on edge along the sides.",
             "Plate " + f(SLED_W) + " × " + f(PLATE_H) + ": smooth card or plastic, braced square with 2 triangular gussets.",
             "Stem " + f(STEM_W) + " × " + f(STEM_LEN) + " (3 layers laminated) " + f(STEM_FROM_PLATE) + " in front of the plate.",
             "Collar 30 × 20 glued to the stem just under the lid stops the sled lifting or tilting.",
             "Line the tunnel floor and walls with packing tape: it slides smoothly and quietly."]
    for i, n in enumerate(notes):
        add(f'<text x="1000" y="{1440 + i * 18}" class="tbl">• {n}</text>')

    # ---------------- 7. Tables ----------------
    def table(x, y, title, rows, widths):
        add(f'<text x="{x}" y="{y}" class="title">{title}</text>')
        yy = y + 22
        for r, row in enumerate(rows):
            if r == 0:
                add(f'<rect x="{x - 6}" y="{yy - 15}" width="{sum(widths) + 12}" height="21" fill="#efe7d6"/>')
            xx = x
            for c, cell in enumerate(row):
                add(f'<text x="{xx}" y="{yy}" class="{"tblh" if r == 0 else "tbl"}">{cell}</text>')
                xx += widths[c]
            yy += 20
        return yy

    y0 = 1210
    table(40, y0, "7 · PARTS (researched sizes — measure yours, clones vary)",
          [["Part", "Size (mm)", "Mounting"],
           ["Arduino Uno R3", "68.6 × 53.4, USB-B sticks out 6.2", "Corral of card strips on the floor, no glue on the PCB"],
           ["Breadboard, 830 point", "165 × 55 × 8.5–10", "Its own adhesive backing"],
           ["HC-SR04", "45 × 20 × 15, cans Ø16", "Hot glue PCB edges behind the sensor wall, on its side"],
           ["KY-023 joystick", "34 × 26 × 32 (with cap)", "Hot glue PCB to the tower top; shim for clearance"],
           ["6×6 tactile button", "6 × 6 × ~5", "Body in the front-wall square, glued from inside"],
           ["Active buzzer", "Ø12 × 9.5", "Stays on the breadboard, under the grille"]],
          [170, 230, 420])
    table(40, y0 + 175, "8 · WIRE BUDGET (kit: 10 male–female, 65 male–male)",
          [["Link", "Female ends needed", "Note"],
           ["Joystick → breadboard", "5", "M–F; SW (pause) is optional"],
           ["HC-SR04 → breadboard", "4", "M–F, sockets point into the electronics bay"],
           ["Trigger button → breadboard", "2", "Only 1 M–F left: wrap the GND leg round an M–M pin and glue, or skip SW"],
           ["Everything else", "0", "M–M; extend reach by hopping through a spare breadboard row"]],
          [230, 170, 420])
    table(40, y0 + 310, "9 · BUILD ORDER",
          [["#", "Step", ""]] + [[str(i + 1), step, ""] for i, step in enumerate(STEPS)],
          [30, 790, 0])
    add("</svg>")
    return "\n".join(out).replace("SHELLFILL", SHELL).replace("SHELLEDGE", SHELL_EDGE).replace("MODENAME", MODE)


# =====================================================================
# Templates (1:1, US Letter). Units are millimetres.
# =====================================================================
def templates():
    PW, PH = 215.9, 279.4
    o = []
    add = o.append
    add(f'<svg xmlns="http://www.w3.org/2000/svg" width="{PW}mm" height="{PH}mm" viewBox="0 0 {PW} {PH}" '
        'font-family="Segoe UI, Helvetica, Arial, sans-serif">')
    add("""<title>Axiom controller cutting templates (1:1)</title>
<style>
.c{fill:none;stroke:#000;stroke-width:0.35}
.cx{stroke:#666;stroke-width:0.2}
.t{font-size:3.2px;fill:#111}
.s{font-size:2.6px;fill:#444}
.h{font-size:4.6px;font-weight:700;fill:#111}
.part{fill:#f3ecde;stroke:#000;stroke-width:0.35}
</style>""")
    add(f'<rect width="{PW}" height="{PH}" fill="#fff"/>')
    add('<text x="10" y="14" class="h">AXIOM CONTROLLER — CUTTING TEMPLATES (print at 100% / actual size)</text>')
    add('<text x="10" y="19.5" class="s">Check the scale bar first. Cut out, place on the sheet at the stated position, trace or cut through.</text>')
    add('<line x1="150" y1="27" x2="200" y2="27" stroke="#000" stroke-width="0.6"/>'
        '<line x1="150" y1="25" x2="150" y2="29" stroke="#000" stroke-width="0.4"/>'
        '<line x1="200" y1="25" x2="200" y2="29" stroke="#000" stroke-width="0.4"/>'
        '<text x="175" y="24" class="s" text-anchor="middle">scale check: must measure 50 mm</text>')

    def cross(x, y, r):
        add(f'<line x1="{f(x - r)}" y1="{f(y)}" x2="{f(x + r)}" y2="{f(y)}" class="cx"/>'
            f'<line x1="{f(x)}" y1="{f(y - r)}" x2="{f(x)}" y2="{f(y + r)}" class="cx"/>')

    def label(x, y, lines):
        for i, s in enumerate(lines):
            add(f'<text x="{f(x)}" y="{f(y + i * 4.2)}" class="{"t" if i == 0 else "s"}">{s}</text>')

    # Lid: joystick hole
    cx, cy = 30, 48
    add(f'<circle cx="{cx}" cy="{cy}" r="{f(STICK_HOLE / 2)}" class="c"/>')
    cross(cx, cy, STICK_HOLE / 2 + 4)
    label(10, 72, [f"{LID} · joystick hole Ø{f(STICK_HOLE)}", f"centre {f(STICK_X + T)} from left edge,",
                   f"{f(STICK_Y + T)} from front edge"])

    # Lid: sound grille
    gx, gy = 80, 48
    for i in range(7):
        a = i * math.pi / 3
        px, py = (gx, gy) if i == 6 else (gx + 7 * math.cos(a), gy + 7 * math.sin(a))
        add(f'<circle cx="{f(px)}" cy="{f(py)}" r="1.5" class="c"/>')
    cross(gx, gy, 12)
    label(62, 72, [f"{LID} · sound grille 7× Ø3", f"centre {f(GRILLE[0] + T)} from left,", f"{f(GRILLE[1] + T)} from front (over the buzzer)"])

    # Sensor wall window
    wx, wy = 118, 30
    add(f'<rect x="{wx}" y="{wy}" width="{f(SENSOR_WIN_W)}" height="{f(SENSOR_WIN_H)}" rx="3" class="c"/>')
    cross(wx + SENSOR_WIN_W / 2, wy + SENSOR_WIN_H / 2, 14)
    label(108, 82, [f"SENSOR WALL · window {f(SENSOR_WIN_W)} × {f(SENSOR_WIN_H)}", f"centred: {f(TUN_CX)} from either side,",
                    f"{f(TUN_CZ)} up from the wall's bottom edge", "Check against your sensor's cans"])

    # USB notch
    nx, ny = 160, 30
    add(f'<path d="M{nx},{ny + USB_HOLE_H} L{nx},{ny} L{nx + USB_HOLE_W},{ny} L{nx + USB_HOLE_W},{ny + USB_HOLE_H}" class="c"/>')
    add(f'<line x1="{nx - 4}" y1="{ny + USB_HOLE_H}" x2="{nx + USB_HOLE_W + 4}" y2="{ny + USB_HOLE_H}" class="cx" stroke-dasharray="1 1"/>')
    label(152, 56, [f"{BACK} · USB notch {f(USB_HOLE_W)} × {f(USB_HOLE_H)}", "open to the wall's bottom edge (dotted)",
                    f"centre ≈ {f(USB_X + T)} from the LEFT (inside)", "confirm with the Uno in place"])

    # Trigger
    bx, by = 30, 108
    add(f'<rect x="{f(bx - BUTTON_CUT / 2)}" y="{f(by - BUTTON_CUT / 2)}" width="{f(BUTTON_CUT)}" height="{f(BUTTON_CUT)}" class="c"/>')
    cross(bx, by, 8)
    add(f'<circle cx="{bx + 55}" cy="{by}" r="{f(BUTTON_CAP / 2)}" class="part"/>')
    label(10, 124, [f"{FRONT} · trigger square {f(BUTTON_CUT)}", f"centre {f(BUTTON_X + T)} from left edge,",
                    f"{f(BUTTON_Z)} up from the wall's bottom edge"])
    label(72, 124, [f"TRIGGER CAP Ø{f(BUTTON_CAP)}", "2–3 layers, glue to the actuator only"])

    # Lid slot (long) along the right side of the page
    sx, sy = 196, 96
    add(f'<rect x="{f(sx - SLOT_W / 2)}" y="{sy}" width="{f(SLOT_W)}" height="{f(SLOT_Y1 - SLOT_Y0)}" rx="{f(SLOT_W / 2)}" class="c"/>')
    add(f'<line x1="{sx}" y1="{sy - 4}" x2="{sx}" y2="{f(sy + SLOT_Y1 - SLOT_Y0 + 4)}" class="cx"/>')
    add(f'<text transform="translate({f(sx - 13)},{f(sy + (SLOT_Y1 - SLOT_Y0) / 2)}) rotate(-90)" class="t" text-anchor="middle">'
        f'{LID} · throttle slot {f(SLOT_Y1 - SLOT_Y0)} × {f(SLOT_W)}</text>')
    add(f'<text transform="translate({f(sx - 8.5)},{f(sy + (SLOT_Y1 - SLOT_Y0) / 2)}) rotate(-90)" class="s" text-anchor="middle">'
        f'centre line {f(TUN_CX + T)} from left edge; runs {f(SLOT_Y0 + T)} → {f(SLOT_Y1 + T)} from the front edge</text>')

    # Sled parts
    add('<text x="10" y="142" class="t" font-weight="700">THROTTLE SLED PARTS (cut from the same sheet)</text>')

    def part(x, y, w, h, name, qty=""):
        add(f'<rect x="{f(x)}" y="{f(y)}" width="{f(w)}" height="{f(h)}" class="part"/>')
        add(f'<text x="{f(x + w / 2)}" y="{f(y + h / 2)}" class="t" text-anchor="middle">{name}</text>')
        add(f'<text x="{f(x + w / 2)}" y="{f(y + h / 2 + 3.6)}" class="s" text-anchor="middle">{f(w)} × {f(h)} {qty}</text>')

    part(10, 148, SLED_W, SLED_LEN, "BASE")
    part(70, 148, SLED_W, PLATE_H, "REFLECTOR PLATE", "(smooth face)")
    part(130, 148, SLED_LEN, RUNNER_H, "RUNNER", "×2")
    part(130, 167, 30, 20, "COLLAR")
    add(f'<polygon points="130,192 160,192 130,222" class="part"/><text x="131.5" y="197" class="s">GUSSET ×2</text>'
        '<text x="131.5" y="201" class="s">30 × 30</text>')
    row = 148 + max(SLED_LEN, PLATE_H) + 6   # below the tallest sled part
    part(10, row, STEM_LEN, STEM_W, "STEM", "×3 laminated")
    k = row + 16
    add(f'<rect x="10" y="{f(k)}" width="40" height="14" class="part"/>'
        f'<rect x="25" y="{f(k + 2)}" width="10" height="4" fill="#fff" stroke="#000" stroke-width="0.3"/>'
        f'<text x="30" y="{f(k + 11)}" class="t" text-anchor="middle">KNOB ×3 stacked</text>'
        f'<text x="54" y="{f(k + 6)}" class="s">40 × 14, slot 10 × 4 fits</text><text x="54" y="{f(k + 10)}" class="s">the stem (push-fit)</text>')
    label(10, k + 22, ["Sled assembly: runners on edge along the base sides; plate glued square at the back end",
                    f"with the gussets; stem centred {f(STEM_FROM_PLATE)} in front of the plate; collar at the stem height",
                    f"that sits just under the lid (about {f(H_IN - RUNNER_H - 2)} above the base). Knob push-fits, no glue."])
    add("</svg>")
    return "\n".join(o)


if __name__ == "__main__":
    here = Path(__file__).parent
    for mode in (sys.argv[1:] or list(PRESETS)):
        configure(mode)
        (here / f"blueprint-{mode}.svg").write_text(blueprint(), encoding="utf-8")
        (here / f"templates-{mode}.svg").write_text(templates(), encoding="utf-8")
        print(f"{mode}: outer {f(W)} x {f(D)} x {f(H)} mm, interior {f(W_IN)} x {f(D_IN)} x {f(H_IN)}; "
              f"travel {f(TRAVEL)} mm ({f(D_NEAR)}-{f(D_FAR)} from the sensor), sensor wall y={f(SENSOR_WALL_Y)}, "
              f"stick at ({f(STICK_X + T)}, {f(STICK_Y + T)}) from outer left/front")
