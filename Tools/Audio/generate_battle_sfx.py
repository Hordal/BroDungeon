"""Reproducible, original synthesis; no external recordings, services, or game RNG.
Run with Python + numpy. Outputs only the named battle WAVs beside existing assets.
These are layered prototype sounds, not recorded/commercial sound-library material.
"""
from pathlib import Path
import hashlib
import wave
import numpy as np

RATE = 44100
ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "Assets/BRODUN/Audio/SFX"
SOUNDS = [
    ("earth_prepare", .38, "rumble"), ("earth_hit", .58, "earth"),
    ("stone_assemble", .50, "stones"), ("stone_lock", .36, "lock"),
    ("wind_release", .30, "swoosh"), ("wind_hit", .29, "cut"),
    ("wind_read", .64, "breeze"), ("card_draw", .25, "paper"),
    ("enemy_claw", .27, "claw"), ("enemy_heavy", .49, "heavy"),
    ("enemy_magic", .41, "magic"), ("enemy_venom", .39, "venom"),
    ("enemy_fire", .44, "fire"), ("enemy_spark", .29, "spark"),
    ("enemy_block", .43, "metal"), ("enemy_buff", .57, "buff"),
    ("player_hurt", .24, "body"), ("enemy_hurt", .22, "body_low"),
]

def smooth(signal, samples):
    return np.convolve(signal, np.ones(samples) / samples, mode="same")

def synthesize(name, seconds, kind):
    rng = np.random.default_rng(int.from_bytes(hashlib.sha256(name.encode()).digest()[:8], "little"))
    t = np.arange(round(seconds * RATE)) / RATE
    n = rng.uniform(-1, 1, len(t))
    air = smooth(n, 3) - smooth(n, 39)
    grit = smooth(n, 5) - smooth(n, 110)
    bass = smooth(n, 160) * 5
    # A pitched low transient sits under filtered, non-periodic impacts.
    thud = np.sin(2 * np.pi * (68 * t + 10 * (1 - np.exp(-t * 40)))) * np.exp(-t * 21)
    x = np.zeros_like(t)

    def grains(count, limit, weight, decay):
        nonlocal x
        for index in range(count):
            start = rng.uniform(0, min(limit, seconds * .8))
            age = np.maximum(0, t - start)
            envelope = (t >= start) * np.exp(-age * decay)
            resonance = np.sin(2 * np.pi * rng.uniform(180, 1100) * age) * .12
            x += (grit * rng.uniform(.6, 1.2) + resonance) * envelope * weight / np.sqrt(count)

    if kind in ("earth", "heavy", "lock"):
        x = .9 * thud + grit * np.exp(-t * 26) * 1.7 + bass * np.exp(-t * 8) * .9
        grains(17 if kind == "earth" else 9, .23, .9, 42)
        if kind == "lock":
            x += (np.sin(t * 2 * np.pi * 147) + .35 * np.sin(t * 2 * np.pi * 379)) * np.exp(-t * 24) * .22
    elif kind in ("rumble", "stones"):
        envelope = np.sin(np.minimum(1, t / seconds) * np.pi) ** .7
        x = bass * envelope * 1.7 + grit * envelope * .23
        grains(14, seconds * .65, .7 if kind == "stones" else .35, 70)
    elif kind in ("swoosh", "cut", "claw"):
        peak = .12 if kind == "swoosh" else .045
        sweep = np.exp(-((t - peak) / (.064 if kind == "swoosh" else .03)) ** 2)
        x = air * sweep * 2.5 + grit * sweep * .6
        if kind != "swoosh":
            x += thud * .3 + grit * np.exp(-t * 35) * 1.2
            grains(5, .05, .3, 95)
    elif kind in ("breeze", "paper"):
        envelope = np.sin(np.pi * t / seconds) ** 1.2
        flutter = .5 + .5 * np.sin(2 * np.pi * (19 * t + 12 * t * t)) ** 2
        x = air * envelope * flutter * (1.1 if kind == "paper" else .5)
        if kind == "breeze":
            x += (np.sin(2*np.pi*741*t) + .4*np.sin(2*np.pi*1123*t)) * envelope * .045
    elif kind in ("magic", "buff", "metal"):
        envelope = np.exp(-t * (10 if kind == "metal" else 6))
        for frequency, strength in [(311,.18), (641,.10), (1097,.07), (1771,.025)]:
            x += np.sin(2*np.pi*(frequency*t + (24*t*t if kind == "buff" else 0))) * strength * envelope
        x += air * np.exp(-t * 28) * .6 + bass * envelope * .3
        if kind == "buff":
            x *= np.minimum(1, t/.15)
            x += air * np.sin(np.pi*t/seconds) * .18
    elif kind == "venom":
        x = smooth(n, 24) * np.exp(-t * 7) * 1.9 + grit * np.exp(-t * 32) * .9
        grains(8, .17, .55, 85)
        x += np.sin(2*np.pi*(230*t - 100*t*t)) * np.exp(-t*16) * .2
    elif kind == "fire":
        x = bass * np.exp(-t*8) * 2 + grit * np.exp(-t*10) * 1.6 + .55*thud
        grains(15, .25, .8, 100)
    elif kind == "spark":
        gate = np.exp(-t*18) * (.2 + .8 * (np.sin(t*237) > .2))
        x = air * gate * 2 + np.sin(2*np.pi*(1700*t-1000*t*t)) * gate * .12
        grains(7, .14, .5, 130)
    else:  # Short non-vocal body impacts; no extra combat effect implied.
        x = thud * (.85 if kind == "body_low" else .6) + grit * np.exp(-t*40) * 1.5
        grains(3, .025, .25, 100)

    # Quiet, early room reflections; normalize AFTER summing to preserve headroom.
    dry = x.copy()
    for delay, gain in [(.027, .12), (.053, .065), (.081, .03)]:
        offset = round(delay * RATE)
        if offset < len(x): x[offset:] += dry[:-offset] * gain
    edge = min(round(.006 * RATE), len(x)//2)
    x[:edge] *= np.linspace(0, 1, edge)
    x[-edge:] *= np.linspace(1, 0, edge)
    x -= np.mean(x)
    # Taper again after DC removal so the exact file endpoints are silent.
    x[:edge] *= np.linspace(0, 1, edge); x[-edge:] *= np.linspace(1, 0, edge)
    peak = float(np.max(np.abs(x)))
    x *= .78 / max(peak, 1e-6)
    return np.rint(x * 32767).astype("<i2")

if __name__ == "__main__":
    DEST.mkdir(parents=True, exist_ok=True)
    for name, length, kind in SOUNDS:
        pcm = synthesize(name, length, kind)
        with wave.open(str(DEST / (name + ".wav")), "wb") as output:
            output.setnchannels(1); output.setsampwidth(2); output.setframerate(RATE)
            output.writeframes(pcm.tobytes())
        print(f"{name}: {len(pcm)/RATE:.2f}s, peak={np.abs(pcm.astype(float)).max()/32767:.3f}")
