"""Generates Assets/app.ico (purple diamond on transparent, PNG-compressed entries)."""
import struct, zlib, os

def make_png(size):
    c = size / 2.0
    r = size * 0.42
    rows = []
    for y in range(size):
        row = bytearray([0])  # filter 0
        for x in range(size):
            dx = abs(x + 0.5 - c)
            dy = abs(y + 0.5 - c)
            d = dx + dy
            if d <= r:
                a = min(1.0, (r - d) * size * 0.35)
                t = (x + y) / (2.0 * size)
                R = int(0xC0 + (0x8B - 0xC0) * t)
                G = int(0x84 + (0x5C - 0x84) * t)
                B = int(0xFC + (0xF6 - 0xFC) * t)
                row += bytes((R, G, B, int(a * 255)))
            else:
                row += bytes((0, 0, 0, 0))
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) +
            chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))

def make_ico(sizes):
    images = [(s, make_png(s)) for s in sizes]
    out = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b""
    data = b""
    for s, png in images:
        entries += struct.pack("<BBBBHHII",
                               0 if s >= 256 else s, 0 if s >= 256 else s,
                               0, 0, 1, 32, len(png), offset)
        data += png
        offset += len(png)
    return out + entries + data

path = os.path.join(os.path.dirname(__file__), "app.ico")
with open(path, "wb") as f:
    f.write(make_ico([16, 24, 32, 48, 64, 256]))
print("wrote", path)