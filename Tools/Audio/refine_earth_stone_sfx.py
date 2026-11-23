"""Four replacement clips only. Keep the original 18 WAVs and generator intact.
Original procedural material, no recordings/API. Python + numpy, 44.1k PCM mono.
Earth: low transient / fracture / falling grit. Stone: dry, stepped masonry / latch.
"""
from pathlib import Path
import hashlib
import wave
import numpy as np

RATE = 44100
DEST = Path(__file__).resolve().parents[2] / "Assets/BRODUN/Audio/SFX"
CLIPS = {"earth_prepare_v2": .30, "earth_hit_v2": .50,
         "stone_assemble_v2": .44, "stone_lock_v2": .20}

def lowpass(x, width):
    return np.convolve(x, np.ones(width) / width, mode="same")

def generate(name, length):
    rng = np.random.default_rng(int.from_bytes(hashlib.sha256(name.encode()).digest()[:8], "little"))
    t = np.arange(round(length * RATE)) / RATE
    noise = rng.uniform(-1, 1, len(t))
    rough = lowpass(noise, 7) - lowpass(noise, 150)
    crunch = lowpass(noise, 3) - lowpass(noise, 31)
    x = np.zeros_like(t)

    if name == "earth_prepare_v2":
        # Low scrape pulling into the planted strike, no completion click.
        envelope = np.sin(np.pi * t / length) ** .8
        x = lowpass(noise, 240) * envelope * 6
        x += rough * envelope * (.15 + .25*t/length)
        x += np.sin(2*np.pi*(42*t + 8*t*t)) * envelope * .10
    elif name == "earth_hit_v2":
        # A single heavy front, then scattered, progressively quieter falling gravel.
        phase = 2*np.pi*(43*t + 37*(1-np.exp(-t*35))/35)
        x = np.sin(phase) * np.exp(-t*18) * 1.15
        x += lowpass(noise, 220) * np.exp(-t*11) * 4.3
        x += rough * np.exp(-t*38) * 2.4
        for start, weight in [(0, 1), (.014, .70), (.037, .45)]:
            age = np.maximum(0, t-start)
            x += crunch * (t>=start) * np.exp(-age*95) * weight
        for i in range(18):
            start = rng.uniform(.065, .34)
            age = np.maximum(0, t-start)
            envelope = (t>=start) * np.exp(-age*rng.uniform(70,140))
            x += (rough + .10*np.sin(2*np.pi*rng.uniform(320,1300)*age)) * envelope * (.55-start)
    elif name == "stone_assemble_v2":
        # Four small dry knocks, increasingly close together; no bass explosion.
        for index, start in enumerate([.012, .143, .255, .345]):
            age = np.maximum(0, t-start)
            envelope = (t>=start) * np.exp(-age*(57+index*4))
            click = crunch * .8 + rough * .6
            body = .16*np.sin(2*np.pi*(470+index*63)*age) + .08*np.sin(2*np.pi*(973+index*91)*age)
            x += (click+body) * envelope * (.7+index*.1)
            scrape = (t>=start) * np.exp(-((age-.035)/.022)**2)
            x += rough * scrape * .18
        x -= lowpass(x, 220)  # Remove the low rumble used by the attack.
    else:  # A concise two-part stone latch, not an impact/explosion.
        for start, weight, frequency in [(0, .65, 630), (.038, 1, 910)]:
            age = np.maximum(0, t-start)
            envelope = (t>=start)*np.exp(-age*78)
            x += (crunch*.8 + rough*.3 + .12*np.sin(2*np.pi*frequency*age)) * envelope * weight
        x -= lowpass(x, 220)

    # Earth alone gets a very short reflection; defense remains dry and precise.
    if name == "earth_hit_v2":
        dry = x.copy(); offset = round(.031*RATE)
        x[offset:] += dry[:-offset]*.08
    x -= x.mean()
    edge = round(.003*RATE)
    x[:edge] *= np.linspace(0,1,edge); x[-edge:] *= np.linspace(1,0,edge)
    x *= .78 / max(float(np.abs(x).max()), 1e-8)
    return np.rint(x*32767).astype("<i2")

if __name__ == "__main__":
    DEST.mkdir(parents=True, exist_ok=True)
    for name, length in CLIPS.items():
        path = DEST / (name+".wav")
        pcm = generate(name, length)
        with wave.open(str(path), "wb") as file:
            file.setnchannels(1); file.setsampwidth(2); file.setframerate(RATE)
            file.writeframes(pcm.tobytes())
        print(f"{name}: {length:.2f}s, peak={np.abs(pcm.astype(float)).max()/32767:.3f}")
