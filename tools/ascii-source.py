"""Keeps the C# sources pure ASCII: any non-ASCII character becomes a \\uXXXX / \\UXXXXXXXX escape.

Icon-font glyphs (private-use characters) are invisible in most editors, so escapes keep the
code readable and diff-friendly.  Usage:  python tools/ascii-source.py
"""
import pathlib

root = pathlib.Path(__file__).resolve().parent.parent
changed = 0
for path in [p for d in ("src", "tests") for p in (root / d).rglob("*.cs")]:
    if "obj" in path.parts or "bin" in path.parts:
        continue
    raw = path.read_bytes()
    text = raw.decode("utf-8-sig")
    out = "".join(
        ch if ord(ch) < 128 else ("\\u%04X" % ord(ch) if ord(ch) <= 0xFFFF else "\\U%08X" % ord(ch))
        for ch in text
    )
    data = out.encode("ascii")
    if data != raw:
        path.write_bytes(data)
        changed += 1
        print("escaped", path.relative_to(root))
print(f"{changed} file(s) changed")
