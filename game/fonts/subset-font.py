"""Rebuild the small game UI fallback from the pinned official Noto CJK font."""

from pathlib import Path
import hashlib
import sys

from fontTools import subset
from fontTools.ttLib import TTFont


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / ".engine-cache/font-source/NotoSansCJKsc-Regular.otf"
SOURCE_SHA256 = "2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b"
OUTPUT = Path(__file__).with_name("HeightFallCjk-Regular.otf")
SHOP_UI_TEXT = "金币不足装备栏已满购买出售商店"


def main() -> None:
    source = Path(sys.argv[1]) if len(sys.argv) > 1 else SOURCE
    if hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_SHA256:
        raise ValueError("The source font does not match the pinned Noto CJK SC Regular 2.004.")

    files = sorted((ROOT / "game/scripts").rglob("*.cs"))
    files += sorted((ROOT / "src/HeightFall.Sim").rglob("*.cs"))
    # Include comments as well as labels/names, leaving room for UI text already
    # described by the source. ASCII keeps using Godot's original primary font.
    characters = sorted({ord(c) for path in files
                         for c in path.read_text(encoding="utf-8-sig") if ord(c) >= 128}
                        | {ord(c) for c in SHOP_UI_TEXT})

    font = TTFont(source, recalcTimestamp=False)
    missing = set(characters) - font.getBestCmap().keys()
    if missing:
        raise ValueError("Characters missing from Noto CJK: " +
                         ", ".join(f"U+{code:04X}" for code in sorted(missing)))

    options = subset.Options()
    options.hinting = False
    options.recalc_timestamp = False
    options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
    options.name_languages = [0x409]
    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=characters)
    subsetter.subset(font)

    # Give the modified subset its own family/PostScript names. Copyright and
    # license name records are retained from the official source.
    names = {1: "HeightFall CJK", 2: "Regular", 3: "HeightFall CJK Regular 2.004 Subset",
             4: "HeightFall CJK Regular", 6: "HeightFallCjk-Regular"}
    for record in font["name"].names:
        if record.nameID in names:
            record.string = names[record.nameID].encode(record.getEncoding())
    cff = font["CFF "].cff
    cff.fontNames[0] = "HeightFallCjk-Regular"
    cff.topDictIndex[0].FamilyName = "HeightFall CJK"
    cff.topDictIndex[0].FullName = "HeightFall CJK Regular"
    font.save(OUTPUT)
    font.close()

    size = OUTPUT.stat().st_size
    if size >= 300_000:
        raise ValueError(f"Subset exceeds the 300 KB budget: {size} bytes")
    print(f"{len(characters)} Unicode characters; {size} bytes; SHA256 " +
          hashlib.sha256(OUTPUT.read_bytes()).hexdigest())


if __name__ == "__main__":
    main()
