"""Generate Axiom's palette API, USS tokens and exact vector/raster reference board.

Run from any directory:
    python Axiom/ArtSource/Palette/generate_palette.py
    python Axiom/ArtSource/Palette/generate_palette.py --check

palette.json is the source of truth. Pillow is optional for PNG export; SVG and code need only
Python's standard library. --check never writes files. No Unity settings or .meta files are touched.
"""

from __future__ import annotations

import argparse
import html
import io
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent.parent
WIDTH, HEIGHT = 1800, 1780


def rgb(value: str) -> tuple[int, int, int]:
    return tuple(int(value[i:i + 2], 16) for i in (1, 3, 5))


def load_palette() -> dict:
    data = json.loads((HERE / "palette.json").read_text(encoding="utf-8"))
    names = set()
    for color in data["colors"] + data["technical_colors"]:
        if color["name"] in names or not re.fullmatch(r"[A-Z][A-Za-z0-9]*", color["name"]):
            raise ValueError("Palette names must be unique C# identifiers: " + color["name"])
        if not re.fullmatch(r"#[0-9A-F]{6}", color["hex"]):
            raise ValueError("Use uppercase six-digit sRGB hex: " + color["hex"])
        names.add(color["name"])
    for variant in data["alpha_variants"]:
        if variant["source"] not in names or not 0 <= variant["alpha"] <= 1:
            raise ValueError("Invalid alpha variant: " + variant["name"])
    return data


def csharp(data: dict) -> str:
    lines = [
        "// Generated from ArtSource/Palette/palette.json. Run generate_palette.py; do not edit here.",
        "using UnityEngine;", "using UnityEngine.Rendering;", "", "namespace GeometricFlight.Presentation", "{",
        "    /// <summary>Axiom's authored sRGB palette. Alpha and emission are separate from hue.</summary>",
        "    public static class AxiomPalette", "    {",
    ]
    for color in data["colors"] + data["technical_colors"]:
        r, g, b = rgb(color["hex"])
        lines.append(f'        /// <summary>{color["hex"]}: {color["role"]}.</summary>')
        lines.append(f'        public static readonly Color {color["name"]} = new Color32({r}, {g}, {b}, 255);')
    lines += [
        "        public static readonly Color Clear = new Color32(0, 0, 0, 0);", "",
        "        /// <summary>Preserves the supplied RGB, including HDR values; changes alpha only.</summary>",
        "        public static Color WithAlpha(Color color, float alpha)", "        {",
        "            color.a = alpha;", "            return color;", "        }", "",
        "        /// <summary>For raw shader-color inputs: converts authored sRGB once, then scales RGB only.",
        "        /// UI colors and Unity properties that already convert sRGB must use the tokens directly.</summary>",
        "        public static Color ShaderColor(Color srgb, float intensity = 1f, float alpha = 1f)",
        "        {",
        "            Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;",
        "            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, alpha);",
        "        }", "",
        "        /// <summary>Value for Material.SetVector, accounting for the property's GPU upload encoding.",
        "        /// Plain Color and Gamma properties store sRGB; HDR Color and raw vectors store linear.",
        "        /// This differs from MaterialPropertyBlock.SetVector, which always takes raw shader values.</summary>",
        "        public static Color MaterialColor(Material material, string property, Color srgb, float intensity = 1f, float alpha = 1f)",
        "        {",
        "            Color color = ShaderColor(srgb, intensity, alpha);",
        "            int index = material.shader.FindPropertyIndex(property);",
        "            if (index < 0 || QualitySettings.activeColorSpace != ColorSpace.Linear) return color;",
        "            ShaderPropertyFlags flags = material.shader.GetPropertyFlags(index);",
        "            bool gammaUpload = (flags & ShaderPropertyFlags.Gamma) != 0",
        "                || (material.shader.GetPropertyType(index) == ShaderPropertyType.Color",
        "                    && (flags & ShaderPropertyFlags.HDR) == 0);",
        "            return gammaUpload ? WithAlpha(color.gamma, alpha) : color;",
        "        }", "    }", "}", "",
    ]
    return "\n".join(lines)


def uss(data: dict) -> str:
    colors = {color["name"]: color["hex"] for color in data["colors"] + data["technical_colors"]}
    lines = [
        "/* Generated from ArtSource/Palette/palette.json. Run generate_palette.py; do not edit here. */",
        ":root {",
    ]
    for name, value in colors.items():
        lines.append(f"    --axiom-{name.lower()}: {value};")
    for variant in data["alpha_variants"]:
        r, g, b = rgb(colors[variant["source"]])
        lines.append(f'    --axiom-{variant["name"]}: rgba({r}, {g}, {b}, {variant["alpha"]:g});')
    lines += ["    --axiom-transparent: rgba(0, 0, 0, 0);", "}", ""]
    return "\n".join(lines)


def luminance(value: str) -> float:
    channels = [channel / 255 for channel in rgb(value)]
    linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in channels]
    return sum(v * weight for v, weight in zip(linear, (0.2126, 0.7152, 0.0722)))


def contrast(a: str, b: str) -> float:
    low, high = sorted((luminance(a), luminance(b)))
    return (high + 0.05) / (low + 0.05)


def shader_exports(data: dict) -> dict[Path, bytes]:
    """Only canonical color defaults are generated; all shader logic remains hand-authored."""
    colors = {c["name"]: c["hex"] for c in data["colors"] + data["technical_colors"]}
    exports = {}
    for filename, properties in data.get("shader_defaults", {}).items():
        path = PROJECT / "Assets/Art/Shaders" / filename
        source = path.read_text(encoding="utf-8")
        for name, spec in properties.items():
            values = [c / 255 for c in rgb(colors[spec["source"]])]
            values = [(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
                      * spec.get("intensity", 1) for c in values]
            values.append(spec.get("alpha", 1))
            pattern = re.compile(r"((?:\[[^\]\n]+\]\s*)*" + re.escape(name) + r'\s*\("[^"\n]*",\s*Color\)\s*=\s*)\([^\n)]*\)')
            def replace_default(match):
                # ShaderLab plain Color defaults use sRGB storage; HDR defaults use raw linear.
                # Gamma overrides also request sRGB upload. Match the material-property contract.
                encoded = values[:]
                if "[HDR]" not in match.group(1) or "[Gamma]" in match.group(1):
                    encoded[:3] = [12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055
                                   for c in values[:3]]
                return match.group(1) + "(" + ", ".join(f"{v:.9g}" for v in encoded) + ")"
            source, count = pattern.subn(replace_default, source)
            if count != 1:
                raise ValueError(f"Expected one color default for {filename}:{name}, found {count}")
        exports[path] = source.encode("utf-8")
    return exports


class Board:
    """The same primitive list generates SVG and PNG, keeping swatches and labels in sync."""

    def __init__(self, colors: dict[str, str]):
        self.colors = colors
        self.items = []

    def rect(self, x, y, w, h, color, stroke=None, width=1):
        self.items.append(("rect", (x, y, w, h, self.colors.get(color, color),
                                    self.colors.get(stroke, stroke), width)))

    def line(self, x1, y1, x2, y2, color, width=1):
        self.items.append(("line", (x1, y1, x2, y2, self.colors.get(color, color), width)))

    def polygon(self, points, color, stroke=None, width=1):
        self.items.append(("polygon", (points, self.colors.get(color, color),
                                       self.colors.get(stroke, stroke), width)))

    def text(self, x, y, value, size=20, color="Text", bold=False):
        self.items.append(("text", (x, y, value, size, self.colors.get(color, color), bold)))

    def svg(self) -> str:
        output = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" '
                  f'viewBox="0 0 {WIDTH} {HEIGHT}" role="img" aria-labelledby="title description">',
                  '<title id="title">Axiom art and interface palette</title>',
                  '<desc id="description">Twelve exact sRGB swatches with semantic roles, interface examples, '
                  'contrast values and derivation rules. Generated from palette.json.</desc>']
        for kind, args in self.items:
            if kind == "rect":
                x, y, w, h, color, stroke, width = args
                output.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{color}" '
                              f'stroke="{stroke or "none"}" stroke-width="{width}"/>')
            elif kind == "line":
                x1, y1, x2, y2, color, width = args
                output.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" '
                              f'stroke="{color}" stroke-width="{width}"/>')
            elif kind == "polygon":
                points, color, stroke, width = args
                coords = " ".join(f"{x},{y}" for x, y in points)
                output.append(f'<polygon points="{coords}" fill="{color}" stroke="{stroke or "none"}" '
                              f'stroke-width="{width}"/>')
            else:
                x, y, value, size, color, bold = args
                output.append(f'<text x="{x}" y="{y}" font-family="Segoe UI, Arial, sans-serif" '
                              f'font-size="{size}" font-weight="{700 if bold else 400}" '
                              f'fill="{color}">{html.escape(value)}</text>')
        output.append("</svg>\n")
        return "\n".join(output)

    def png(self) -> bytes | None:
        try:
            from PIL import Image, ImageDraw, ImageFont
        except ImportError:
            return None
        scale = 2
        image = Image.new("RGB", (WIDTH * scale, HEIGHT * scale), self.colors["Void"])
        draw = ImageDraw.Draw(image)
        fonts = {}

        def font(size, bold):
            key = (size, bold)
            if key not in fonts:
                candidates = [
                    Path("C:/Windows/Fonts") / ("segoeuib.ttf" if bold else "segoeui.ttf"),
                    Path("/usr/share/fonts/truetype/dejavu") / ("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"),
                ]
                path = next((candidate for candidate in candidates if candidate.exists()), None)
                if path is None:
                    raise RuntimeError("PNG export requires Segoe UI or DejaVu Sans; SVG remains portable.")
                fonts[key] = ImageFont.truetype(str(path), size * scale)
            return fonts[key]

        for kind, args in self.items:
            if kind == "rect":
                x, y, w, h, color, stroke, width = args
                draw.rectangle((x * scale, y * scale, (x + w) * scale - 1, (y + h) * scale - 1),
                               fill=color, outline=stroke, width=max(1, round(width * scale)))
            elif kind == "line":
                x1, y1, x2, y2, color, width = args
                draw.line((x1 * scale, y1 * scale, x2 * scale, y2 * scale), fill=color,
                          width=max(1, round(width * scale)))
            elif kind == "polygon":
                points, color, stroke, width = args
                coords = [(x * scale, y * scale) for x, y in points]
                draw.polygon(coords, fill=color)
                if stroke:
                    draw.line(coords + coords[:1], fill=stroke, width=max(1, round(width * scale)))
            else:
                x, y, value, size, color, bold = args
                draw.text((x * scale, y * scale), value, font=font(size, bold), fill=color, anchor="ls")
        image = image.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS)
        buffer = io.BytesIO()
        image.save(buffer, format="PNG", optimize=True)
        return buffer.getvalue()


def palette_board(data: dict) -> Board:
    colors = {entry["name"]: entry["hex"] for entry in data["colors"] + data["technical_colors"]}
    board = Board(colors)
    board.rect(0, 0, WIDTH, HEIGHT, "Void")
    board.rect(0, 0, 12, HEIGHT, "Orange")
    board.text(72, 90, "AXIOM", 60, "Light", True)
    board.text(74, 130, "ART + INTERFACE / PALETTE 01", 21, "Cyan")
    board.text(1250, 80, "12 SWATCHES / ONE WORLD", 20, "Light", True)
    board.text(1250, 114, "Authored sRGB · exact source values", 19, "Muted")
    board.text(74, 176, "Dark mass. Selective light. A cool player signal against warm system energy.", 22)
    board.line(72, 200, 1728, 200, "Edge")

    card_w, card_h, gap = 396, 208, 24
    for index, entry in enumerate(data["colors"]):
        x = 72 + (index % 4) * (card_w + gap)
        y = 228 + (index // 4) * (card_h + gap)
        board.rect(x, y, card_w, card_h, "Structure", "Edge")
        board.rect(x + 1, y + 1, card_w - 2, 100, entry["name"])
        board.text(x + 20, y + 140, entry["name"].upper(), 24, "Light", True)
        board.text(x + 244, y + 140, entry["hex"], 21, "Cyan")
        board.text(x + 20, y + 179, entry["role"], 17)

    board.text(72, 972, "INTERFACE LANGUAGE", 25, "Light", True)
    board.text(1050, 972, "Color supports shape, text and motion.", 21, "Muted")
    for x in (72, 632, 1192):
        board.rect(x, 1000, 536, 240, "Panel")
    board.text(98, 1037, "SELECTED", 16, "Muted", True)
    board.polygon([(126, 1063), (524, 1063), (541, 1101), (511, 1139), (108, 1139)], "Light")
    board.line(98, 1077, 85, 1126, "Orange", 6)
    board.text(147, 1114, "PLAY", 32, "Void", True)
    board.text(98, 1200, "Inversion + warm focus marker", 19)

    board.text(658, 1037, "SYSTEM STATE", 16, "Muted", True)
    board.text(659, 1093, "READY", 30, "Cyan", True)
    board.text(931, 1093, "SPENT", 30, "Muted", True)
    for index in range(4):
        board.rect(660 + index * 44, 1120, 32, 10, "Cyan")
        board.rect(934 + index * 44, 1120, 32, 10, "Panel", "Edge", 2)
    board.text(658, 1200, "State words + filled / empty geometry", 19)

    board.text(1218, 1037, "CRITICAL", 16, "Muted", True)
    board.polygon([(1246, 1063), (1274, 1113), (1218, 1113)], "Ember")
    board.text(1238, 1103, "!", 28, "Void", True)
    board.text(1296, 1103, "HULL CRITICAL", 29, "Hot", True)
    board.line(1218, 1140, 1702, 1140, "Ember", 3)
    board.text(1218, 1200, "Hot text + Ember fault shape", 19)

    board.text(72, 1301, "RULES THAT HOLD THE WORLD TOGETHER", 25, "Light", True)
    columns = [(72, "VALUE + MATERIAL"), (632, "READABLE CONTRAST"), (1192, "CONTROLLED DERIVATION")]
    for x, title in columns:
        board.line(x, 1328, x + 536, 1328, "Edge")
        board.text(x, 1370, title, 19, "Cyan", True)
    lines = [
        "Most surfaces stay dark and substantial.",
        "Light describes selected seams and openings.",
        "Orange is system / Program energy.",
        "Threats also need a clear shape or telegraph.",
        "Gold stays rare: bosses and authority.",
        "The menu band uses restrained Orange.",
    ]
    for index, value in enumerate(lines):
        board.text(72, 1410 + index * 34, value, 19)
    for index, token in enumerate(("Muted", "Text", "Cyan", "Orange", "Hot")):
        board.text(632, 1410 + index * 34, token, 19, token)
        board.text(830, 1410 + index * 34, f'{contrast(colors[token], colors["Panel"]):.2f}:1 on Panel', 19)
    board.text(632, 1580, "Small critical text uses Hot, not Ember.", 19)
    rules = [
        "Alpha, shade and emission are separate.",
        "Convert sRGB once at the shader boundary.",
        "Scale RGB for HDR; keep alpha independent.",
        "Use explicit ramps; reserve Gold identity.",
        "No arbitrary hue shifts or new accent colors.",
        "Lit pixels remain continuous, not quantized.",
    ]
    for index, value in enumerate(rules):
        board.text(1192, 1410 + index * 34, value, 19)

    board.line(72, 1640, 1728, 1640, "Edge")
    board.rect(72, 1672, 28, 28, "Black", "Edge")
    board.rect(110, 1672, 28, 28, "White")
    board.text(156, 1694, "Black / White / Clear are technical mask and blending values.", 19, "Muted")
    board.text(72, 1737, "Axiom's project palette · not certified film production colors · generated from ArtSource/Palette/palette.json", 18, "Muted")
    return board


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Validate generated exports without writing.")
    args = parser.parse_args()
    data = load_palette()
    board = palette_board(data)
    exports = {
        PROJECT / "Assets/Scripts/Presentation/AxiomPalette.cs": csharp(data).encode("utf-8"),
        PROJECT / "Assets/UI/Palette.uss": uss(data).encode("utf-8"),
        PROJECT / "Docs/images/art-palette.svg": board.svg().encode("utf-8"),
    }
    exports.update(shader_exports(data))
    png = board.png()
    if png is not None:
        exports[PROJECT / "Docs/images/art-palette.png"] = png
    else:
        print("Pillow unavailable: PNG export/check skipped; SVG and code remain available.")
    mismatches = []
    for path, content in exports.items():
        if args.check:
            existing = path.read_bytes() if path.exists() else None
            if path.suffix != ".png" and existing is not None:
                existing = existing.replace(b"\r\n", b"\n")
            if existing != content:
                mismatches.append(str(path.relative_to(PROJECT)))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
    if mismatches:
        print("Palette exports differ; run generate_palette.py:")
        print("\n".join("  " + item for item in mismatches))
        return 1
    print(f'{"Checked" if args.check else "Generated"} {len(exports)} palette exports from {len(data["colors"])} authored swatches.')
    return 0


if __name__ == "__main__":
    sys.exit(main())
