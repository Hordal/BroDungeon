# PHASE 3 presentation resources

Only the resources actually used by Boot/Title/CharacterSelect/DungeonMap were imported.
The original WebPrototype files are unchanged. No new Unity package is needed.

- Backgrounds/title_background.png: WebPrototype/Assets/BRODUNArt/Backgrounds/title_background.png
- Backgrounds/area01_forgotten_corridor.png: same filename in WebPrototype/Assets/BRODUNArt/Backgrounds
- Characters/warrior.png, mage.png, priest.png: each character's Frames/<id>/idle_00.png in WebPrototype/Assets/BRODUNArt
- UI/*.png: matching PNG names from WebPrototype/Assets/BRODUNArt/UI (17 images)
- Point filtering, mipmaps off, uncompressed, single Sprite: only these 22 imported PNGs.
- No monster frame import, no Animator, no audio, no generated combat effects.

## Fonts (bundled, not dependent on the player's Windows installation)

- NotoSansCJKkr-Regular.otf: https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/Korean
  - Raw source: https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf
  - Original copyright metadata is embedded in the unmodified font. SIL OFL 1.1 is included in Fonts/OFL.txt.
- Silkscreen-Regular.ttf: https://github.com/google/fonts/tree/main/ofl/silkscreen
  - Raw source: https://raw.githubusercontent.com/google/fonts/main/ofl/silkscreen/Silkscreen-Regular.ttf
  - Copyright 2001 The Silkscreen Project Authors. SIL OFL 1.1 and copyright notice: Fonts/OFL_Silkscreen.txt.

The preliminary variable NotoSansKR font was removed from this change; the static regular font improves Unity legacy Text readability.
Silkscreen is used only for the BRODUN logo. Korean UI uses Noto Sans CJK KR.
