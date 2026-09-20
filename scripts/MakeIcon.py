"""Rebuild amule-modern.ico from the PNG master in src/Desktop/Assets."""
import io
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
assets = root / "src" / "Desktop" / "Assets"
png_path = assets / "amule-modern.png"
ico_path = assets / "amule-modern.ico"
img = Image.open(png_path).convert("RGBA")
master = img.resize((512, 512), Image.Resampling.LANCZOS)
sizes = [16, 24, 32, 48, 64, 128, 256]
blobs = []
for size in sizes:
    buf = io.BytesIO()
    master.resize((size, size), Image.Resampling.LANCZOS).save(buf, format="PNG")
    blobs.append(buf.getvalue())
count = len(sizes)
offset = 6 + 16 * count
entries = []
for size, blob in zip(sizes, blobs):
    w = 0 if size >= 256 else size
    entries.append((w, w, len(blob), offset, blob))
    offset += len(blob)
data = bytearray()
data += (0).to_bytes(2, "little") + (1).to_bytes(2, "little") + count.to_bytes(2, "little")
for w, h, nbytes, ofs, _ in entries:
    data += bytes([w, h, 0, 0])
    data += (1).to_bytes(2, "little") + (32).to_bytes(2, "little")
    data += nbytes.to_bytes(4, "little") + ofs.to_bytes(4, "little")
for _, _, _, _, blob in entries:
    data += blob
ico_path.write_bytes(data)
print(ico_path, ico_path.stat().st_size)
