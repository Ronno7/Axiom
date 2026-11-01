"""Boost burst sound: a filtered saw swell over a rising noise rush, about 1.6 s.

Writes Assets/Audio/SFX/boost_burst.wav (44.1 kHz, 16-bit mono). Pure standard library.
    python Axiom/ArtSource/Audio/boost_burst.py
"""
import math
import random
import struct
import wave
from pathlib import Path

SR = 44100
LENGTH = 1.6
N = int(LENGTH * SR)
OUT = Path(__file__).resolve().parents[2] / "Assets/Audio/SFX/boost_burst.wav"
random.seed(70)


def envelope(t):
    attack = min(1.0, t / 0.04)
    return attack * math.exp(-t / 0.55)


out = [0.0] * N
# A low saw pair (D1 and A1) whose filter sweeps open and closes again.
low = band = 0.0
phases = [0.0, 0.0, 0.0]
freqs = [36.7, 55.0, 73.4]
for n in range(N):
    t = n / SR
    x = 0.0
    for k, f in enumerate(freqs):
        f *= 1 + 0.25 * math.exp(-t / 0.08)  # a short downward pitch drop on the hit
        phases[k] = (phases[k] + f / SR) % 1.0
        x += 2 * phases[k] - 1
    x /= len(freqs)
    fc = 120 + 2600 * math.exp(-t / 0.25) * min(1.0, t / 0.03)
    f = 2 * math.sin(math.pi * fc / SR)
    low += f * band
    band += f * (x - low - 0.6 * band)
    out[n] += low * envelope(t) * 0.8

# A noise rush rising in pitch, high-passed, like air tearing past.
low = band = 0.0
for n in range(N):
    t = n / SR
    fc = 900 + 5000 * min(1.0, t / 0.35)
    f = 2 * math.sin(math.pi * min(fc, SR / 6.5) / SR)
    x = random.uniform(-1, 1)
    low += f * band
    high = x - low - 0.8 * band
    band += f * high
    out[n] += high * envelope(t) * 0.25

peak = max(abs(v) for v in out)
fade = int(0.05 * SR)
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    frames = []
    for n, v in enumerate(out):
        g = min(1.0, (N - n) / fade)
        frames.append(struct.pack("<h", int(math.tanh(v / peak * 1.3) / math.tanh(1.3) * 0.8 * g * 32767)))
    w.writeframes(b"".join(frames))
print(f"Wrote {OUT}")
