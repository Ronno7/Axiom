"""Read-only source guard for the authored palette; run Unity's palette audit for serialized assets.

Usage: python Axiom/ArtSource/Palette/check_palette.py
Historical LookDev experiments and captured documentation images are intentionally not current game art.
Checks recursive USS declarations, UXML inline styles, common C# color literals, and declared ShaderLab
Color properties. This is a source guard, not a shader-language interpreter or a serialized-asset audit.
"""
from pathlib import Path
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent.parent


def style_issues(style, label, tokens):
    """Inspect declaration values only, so a hexadecimal-looking ID selector is not a color."""
    issues = []
    for declaration in style.split(";"):
        _, separator, value = declaration.partition(":")
        if not separator:
            continue
        if re.search(r"\brgba?\s*\(|#(?:[0-9a-f]{8}|[0-9a-f]{6}|[0-9a-f]{4}|[0-9a-f]{3})\b", value, re.I):
            issues.append(f"{label}: authored colors must reference Palette.uss")
        for token in re.findall(r"var\(\s*(--axiom-[\w-]+)", value):
            if token not in tokens:
                issues.append(f"{label}: unknown palette token {token}")
    return issues


def main():
    result = subprocess.run([sys.executable, str(HERE / "generate_palette.py"), "--check"], check=False)
    failures = []
    token_source = (PROJECT / "Assets/UI/Palette.uss").read_text(encoding="utf-8")
    tokens = set(re.findall(r"(--axiom-[\w-]+)\s*:", token_source))
    for path in (PROJECT / "Assets/UI").rglob("*.uss"):
        if path == PROJECT / "Assets/UI/Palette.uss":
            continue
        source = path.read_text(encoding="utf-8")
        source = re.sub(r"/\*.*?\*/", "", source, flags=re.S)
        for block in re.findall(r"\{([^{}]*)\}", source):
            failures.extend(style_issues(block, str(path.relative_to(PROJECT)), tokens))
    for path in (PROJECT / "Assets/UI").rglob("*.uxml"):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as error:
            failures.append(f"{path.relative_to(PROJECT)}: cannot inspect UXML: {error}")
            continue
        for element in root.iter():
            if "style" in element.attrib:
                label = f"{path.relative_to(PROJECT)}/{element.get('name', element.tag)}"
                failures.extend(style_issues(element.attrib["style"], label, tokens))
    palette = json.loads((HERE / "palette.json").read_text(encoding="utf-8"))
    shader_root = PROJECT / "Assets/Art/Shaders"
    # ShaderLab Color declarations have a distinct signature; ordinary numeric shader math is data.
    color_property = re.compile(r'^\s*(?:\[[^\]\r\n]*\]\s*)*([A-Za-z_]\w*)\s*'
                                r'\(\s*"(?:\\.|[^"\\])*"\s*,\s*Color\s*\)\s*=', re.M)
    for path in shader_root.rglob("*.shader"):
        source = path.read_text(encoding="utf-8")
        source = re.sub(r"/\*.*?\*/|//[^\n]*", "", source, flags=re.S)
        declared = palette.get("shader_defaults", {}).get(path.relative_to(shader_root).as_posix(), {})
        for name in color_property.findall(source):
            if name not in declared:
                failures.append(f"{path.relative_to(PROJECT)}/{name}: declare a palette shader default")
    for folder in ("Assets/Scripts", "Assets/Editor"):
        for path in (PROJECT / folder).rglob("*.cs"):
            if path.name == "AxiomPalette.cs":
                continue
            source = path.read_text(encoding="utf-8")
            # Named components/alpha from existing palette colors are legitimate; literal RGB is not.
            if re.search(r"new\s+Color(?:32)?\s*\(\s*[\d.-]", source):
                failures.append(f"{path.relative_to(PROJECT)}: literal RGB outside palette source")
            if re.search(r"Color\.(?:red|green|blue|cyan|magenta|yellow|gray|grey)\b", source):
                failures.append(f"{path.relative_to(PROJECT)}: built-in authored hue outside palette source")
            if re.search(r"(?:TryParseHtmlString|FromHtml)\s*\(\s*\"#", source):
                failures.append(f"{path.relative_to(PROJECT)}: literal HTML color outside palette source")
    if failures:
        print("Palette source violations:\n" + "\n".join(failures))
    else:
        print("Palette source guard passed: recursive UI styles, common C# color literals, and declared shader colors.")
    return 1 if failures or result.returncode else 0


if __name__ == "__main__":
    sys.exit(main())
