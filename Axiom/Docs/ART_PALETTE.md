# Art palette

Twelve colours for the world, effects and interface. Mostly dark, with light where it counts. The player runs cool,
Programs run warm.

![Axiom's twelve colour palette](images/art-palette.png)

| Colour | sRGB | Use |
| --- | --- | --- |
| Void | `#060A0C` | Sky, fog, deep recesses |
| Structure | `#0C141F` | Architecture and craft bodies |
| Panel | `#102931` | UI panels and secondary planes |
| Edge | `#3A5362` | Quiet outlines and inactive tracks |
| Muted | `#8198A6` | Secondary text |
| Text | `#B7C9D1` | Body text and neutral outlines |
| Light | `#E6FFFF` | Title, focus, important values, the player's white core |
| Cyan | `#6FC3DF` | Player systems, shield, cool energy |
| Orange | `#DF740C` | Programs, hostile emission, selection accent |
| Ember | `#FF410D` | Faults, damage, attacks |
| Gold | `#FFE64D` | Saved for a boss |
| Hot | `#FFF2D9` | Impact cores and warning text |

## Rules

- Keep most of the image in Void, Structure and Panel. If every edge glows, nothing has scale.
- Colour never works alone. Focus and fault, full and empty, SAFE and SPENT also differ by shape or word.
- Warm doesn't mean hostile. Targets and attacks also get their own shape, motion or brackets.
- No green. Ready is Light or Cyan.
- Gold stays rare, and nothing blends toward it.
- Effects can fade, darken and glow, but they don't bring new colours.

## Changing a colour

Everything comes from [`palette.json`](../ArtSource/Palette/palette.json). `generate_palette.py` writes the C#, the USS,
the shader defaults and the board above. `check_palette.py` confirms they still match.
