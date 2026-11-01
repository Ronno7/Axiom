"""Temporary score for the shared entry: a sixteen-beat build at 102 BPM, then a short flight tail.

Writes Assets/Audio/Entry/entry_build.wav (44.1 kHz, 16-bit stereo). Pure standard library.
    python Axiom/ArtSource/Audio/entry_build.py

Original material in a dark "analog synths against an orchestra" idiom; a placeholder for real music.
Shape (beats from PLAY, matching Presentation/EntrySequence.cs; bars are four beats):
  bar 1  a resonant saw bass ostinato answers the press, filter closed; soft data clicks on the systems rows
  bar 2  a low saw arpeggio gathers, cellos join the ostinato, the filter opens while the passage draws itself
  ~4.5s  a breath under the threshold scan
  bar 3  arrival: timpani and low brass swell on Bb, high strings rise with vibrato; the arpeggio steps back
  bar 4  Gm, then A sus4 to A; a timpani roll and a reversed swell into control
  bar 5+ resolve to D minor on the control downbeat with a full hit, strings hold, the ostinato fades
"""
import math
import random
import struct
import wave
from array import array
from pathlib import Path

SR = 44100
BPM = 102.0
BEAT = 60.0 / BPM
LENGTH = 24 * BEAT + 2.8
N = int(LENGTH * SR)
OUT = Path(__file__).resolve().parents[2] / "Assets/Audio/Entry/entry_build.wav"
ARRIVAL, CONTROL, END = 8, 16, 24

random.seed(102)
L = array("d", bytes(8 * N))
R = array("d", bytes(8 * N))
SL = array("d", bytes(8 * N))  # reverb send
SR_ = array("d", bytes(8 * N))


def hz(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def b(beat):
    return beat * BEAT


def poly_blep(t, dt):
    if t < dt:
        t /= dt
        return t + t - t * t - 1.0
    if t > 1.0 - dt:
        t = (t - 1.0) / dt
        return t * t + t + t + 1.0
    return 0.0


def add(i, v, pan, send):
    if 0 <= i < N:
        gl = math.cos((pan + 1) * math.pi / 4)
        gr = math.sin((pan + 1) * math.pi / 4)
        L[i] += v * gl
        R[i] += v * gr
        if send:
            SL[i] += v * gl * send
            SR_[i] += v * gr * send


def voice(start, dur, freq, vel, cutoff, env_cut=0.0, env_time=0.09, attack=0.005, decay=0.0, release=0.1,
          voices=1, detune=0.0, q=0.9, pan=0.0, send=0.0, vibrato=0.0, vib_rate=5.2, vib_delay=0.4, cut_end=None):
    """Band-limited saw ensemble through a state-variable low-pass, with optional vibrato and filter glide."""
    i0 = int(start * SR)
    total = int((dur + release) * SR)
    spread = [0.0] if voices == 1 else [detune * (2 * k / (voices - 1) - 1) for k in range(voices)]
    phases = [random.random() for _ in spread]
    low = band = 0.0
    for n in range(total):
        t = n / SR
        if t < attack:
            amp = t / attack
        elif t < dur:
            amp = math.exp(-(t - attack) / decay) if decay > 0 else 1.0
        else:
            held = math.exp(-(dur - attack) / decay) if decay > 0 else 1.0
            amp = held * max(0.0, 1 - (t - dur) / release)
        wobble = 1.0
        if vibrato and t > vib_delay:
            depth = min(1.0, (t - vib_delay) / 0.6) * vibrato
            wobble = 2 ** (depth * math.sin(2 * math.pi * vib_rate * t) / 1200)
        x = 0.0
        for k, cents in enumerate(spread):
            dt = freq * wobble * 2 ** (cents / 1200) / SR
            p = phases[k]
            x += 2 * p - 1 - poly_blep(p, dt)
            p += dt
            phases[k] = p - 1.0 if p >= 1.0 else p
        x /= len(spread)
        base = cutoff if cut_end is None else cutoff + (cut_end - cutoff) * min(1.0, t / max(dur, 1e-3))
        fc = min(base + env_cut * math.exp(-t / env_time), SR / 6.5)
        f = 2 * math.sin(math.pi * fc / SR)
        low += f * band
        high = x - low - q * band
        band += f * high
        add(i0 + n, low * amp * vel, pan, send)


def noise(start, dur, vel, cut0, cut1, rising, pan=0.0, send=0.0, highpass=False):
    i0 = int(start * SR)
    total = int(dur * SR)
    low = band = 0.0
    for n in range(total):
        u = n / max(1, total - 1)
        amp = u ** 2.4 if rising else (1 - u) ** 3
        fc = cut0 + (cut1 - cut0) * u
        f = 2 * math.sin(math.pi * min(fc, SR / 6.5) / SR)
        x = random.uniform(-1, 1)
        low += f * band
        high = x - low - 0.7 * band
        band += f * high
        add(i0 + n, (high if highpass else low) * amp * vel, pan, send)


def timpani(start, note, vel):
    """A tuned drum: a falling sine body and a short felt-mallet thump."""
    i0 = int(start * SR)
    f0 = hz(note)
    phase = 0.0
    for n in range(int(2.4 * SR)):
        t = n / SR
        f = f0 * (1 + 0.12 * math.exp(-t / 0.05))
        phase += 2 * math.pi * f / SR
        body = (math.sin(phase) + 0.35 * math.sin(phase * 1.5) * math.exp(-t / 0.3)) * math.exp(-t / 0.9)
        add(i0 + n, body * vel, 0.0, 0.25)
    noise(start, 0.08, vel * 0.25, 900, 300, False, 0.0, 0.1)


def click(start, vel, pan):
    """A short digital tick for a printed systems row."""
    i0 = int(start * SR)
    f = random.choice([1760, 2093, 2637])
    for n in range(int(0.012 * SR)):
        t = n / SR
        add(i0 + n, (1.0 if math.sin(2 * math.pi * f * t) > 0 else -1.0) * math.exp(-t / 0.003) * vel, pan, 0.2)


HARMONY = [(0, 26), (ARRIVAL, 22), (12, 19), (14, 21), (CONTROL, 26)]   # D, Bb, G, A, D (bass roots)
CHORD = {0: [50, 53, 57, 60, 64], ARRIVAL: [46, 50, 53, 57, 62], 12: [43, 46, 50, 55, 58],
         14: [45, 50, 52, 57, 62], 15: [45, 49, 52, 57, 61], CONTROL: [50, 53, 57, 62, 65]}


def at(table, beat):
    pairs = table if isinstance(table, list) else sorted(table.items())
    value = pairs[0][1]
    for start, v in pairs:
        if beat >= start:
            value = v
    return value


# 1. Resonant saw bass ostinato: eighths, octave jumps, the filter opening through bars 1-2.
for k in range(END * 2):
    beat = k / 2
    root = at(HARMONY, beat)
    jump = 12 if k % 8 in (3, 6) else 0
    opening = min(1.0, beat / ARRIVAL)
    fade = 1.0 if beat < CONTROL + 2 else max(0.0, 1 - (beat - CONTROL - 2) / 6)
    vel = (0.16 + 0.14 * opening if beat < ARRIVAL else 0.3) * fade
    voice(b(beat), BEAT / 2 * 0.85, hz(root + 12 + jump), vel, 120 + 520 * opening, env_cut=700, env_time=0.07,
          decay=0.18, release=0.05, voices=2, detune=6, q=0.45, send=0.05)

# 2. Low saw arpeggio in sixteenths: dark, gathering, stepping back on arrival.
pattern = [0, 2, 1, 3, 2, 4, 3, 1]
for k in range(4 * 1, (END - 3) * 4):
    beat = k / 4
    if ARRIVAL <= beat < ARRIVAL + 1.5:
        continue
    tones = at(CHORD, beat)
    note = tones[pattern[k % 8]]
    if beat < ARRIVAL:
        build = (beat - 1) / (ARRIVAL - 1)
        vel, cut = 0.035 + 0.075 * build, 500 + 1500 * build
    elif beat < CONTROL:
        vel, cut = 0.08, 1700 + 500 * (beat - 12) / 4 if beat >= 12 else 1500
    else:
        vel, cut = 0.07 * max(0.0, 1 - (beat - CONTROL) / 5), 1500
    voice(b(beat), BEAT / 4 * 0.8, hz(note), vel, cut, env_cut=900, env_time=0.05, decay=0.08,
          release=0.04, pan=-0.3 if k % 2 else 0.3, send=0.3)

# 3. Cellos: an eighth-note ostinato on root and fifth from bar 2, building into the arrival.
for k in range(4 * 2, END * 2 - 4):
    beat = k / 2
    root = at(HARMONY, beat) + 24
    note = root + (7 if k % 2 else 0)
    grow = min(1.0, (beat - 4) / 4)
    fade = 1.0 if beat < CONTROL + 2 else max(0.0, 1 - (beat - CONTROL - 2) / 5)
    voice(b(beat), BEAT / 2 * 0.7, hz(note), 0.06 * grow * fade, 900, env_cut=500, attack=0.02, decay=0.25,
          release=0.12, voices=3, detune=10, q=1.0, pan=-0.4, send=0.35)

# 4. Low brass: swell on the arrival, then Gm, A sus4 -> A, and the control resolution.
BRASS = [(ARRIVAL, 4, [34, 41, 46, 53]), (12, 2, [31, 38, 43, 50]), (14, 1, [33, 40, 45, 50]),
         (15, 1, [33, 40, 45, 49]), (CONTROL, 6, [26, 33, 38, 45, 50])]
for start, length, notes in BRASS:
    for j, note in enumerate(notes):
        voice(b(start), b(length), hz(note), 0.07, 380, env_cut=0, attack=0.35 if start == ARRIVAL else 0.12,
              release=1.4, voices=3, detune=7, q=1.1, pan=(j - 1.5) * 0.3, send=0.45,
              cut_end=1500 if start in (ARRIVAL, CONTROL) else 1100)

# 5. High strings: rise with vibrato on arrival, hold over control.
STRINGS = [(ARRIVAL, 4, [74, 77]), (12, 2, [74, 79]), (14, 2, [76, 81]), (CONTROL, 7, [74, 77, 81])]
for start, length, notes in STRINGS:
    for j, note in enumerate(notes):
        voice(b(start), b(length), hz(note), 0.028, 2600, attack=1.0 if start == ARRIVAL else 0.25,
              release=2.0, voices=4, detune=12, q=1.2, pan=(j - 1) * 0.5, send=0.6, vibrato=9, vib_delay=0.5)

# 6. Percussion: timpani on the arrival and control downbeats, a roll into control.
timpani(b(ARRIVAL), 38, 0.55)
timpani(b(12), 31, 0.3)
for k in range(8):
    timpani(b(CONTROL - 1) + k * BEAT / 8, 33, 0.08 + 0.03 * k)
timpani(b(CONTROL), 38, 0.6)

# 7. Swells: a reversed breath into the threshold and a riser into control.
noise(b(ARRIVAL) - 0.6, 0.6, 0.09, 700, 6000, True, 0.0, 0.3, highpass=True)
noise(b(CONTROL) - b(2), b(2), 0.08, 500, 7000, True, 0.0, 0.3)

# 8. Data clicks on the systems readout's beats.
for row_beat in (0.2, 1, 2, 3, 4, 5, 6, 8.6, 11.5, 12, 13):
    for j in range(3):
        click(b(row_beat) + j * 0.03, 0.02, 0.0)

# 9. The breath: thin the mix just before the arrival downbeat.
duck_start, duck_end = int((b(ARRIVAL) - 0.18) * SR), int(b(ARRIVAL) * SR)
for i in range(duck_start, duck_end):
    g = 1 - 0.75 * math.sin(math.pi * 0.5 * (i - duck_start) / (duck_end - duck_start))
    L[i] *= g
    R[i] *= g


def comb(src, delay, feedback, damp):
    out = array("d", bytes(8 * N))
    buf = [0.0] * delay
    idx = 0
    store = 0.0
    for i in range(N):
        y = buf[idx]
        store = y * (1 - damp) + store * damp
        buf[idx] = src[i] + store * feedback
        idx = idx + 1 if idx + 1 < delay else 0
        out[i] = y
    return out


def allpass(src, delay, gain=0.5):
    out = array("d", bytes(8 * N))
    buf = [0.0] * delay
    idx = 0
    for i in range(N):
        y = buf[idx]
        buf[idx] = src[i] + y * gain
        idx = idx + 1 if idx + 1 < delay else 0
        out[i] = y - src[i]
    return out


def reverb(send, spread):
    """A larger hall than before: longer combs and more feedback."""
    total = array("d", bytes(8 * N))
    for d in (2063, 1931, 1789, 1657):
        c = comb(send, d + spread, 0.86, 0.35)
        for i in range(N):
            total[i] += c[i] * 0.25
    for d in (556, 441, 341):
        total = allpass(total, d + spread)
    return total


wet_l, wet_r = reverb(SL, 0), reverb(SR_, 29)
peak = 0.0
mix = []
for i in range(N):
    fade = 1.0 if i < N - SR else (N - i) / SR
    l = math.tanh((L[i] + 0.45 * wet_l[i]) * 1.2) * fade
    r = math.tanh((R[i] + 0.45 * wet_r[i]) * 1.2) * fade
    mix.append((l, r))
    peak = max(peak, abs(l), abs(r))
gain = 0.89 / peak if peak > 0 else 1.0
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(b"".join(struct.pack("<hh", int(l * gain * 32767), int(r * gain * 32767)) for l, r in mix))
print(f"Wrote {OUT} ({LENGTH:.1f} s, peak gain {gain:.2f})")
