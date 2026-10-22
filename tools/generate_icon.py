from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets" / "zflow.ico"
OUT.parent.mkdir(parents=True, exist_ok=True)

S = 3
N = 512 * S
img = Image.new("RGBA", (N, N), (10, 9, 17, 255))
px = img.load()
for y in range(N):
    for x in range(N):
        t = (x / N * 0.35 + y / N * 0.65)
        px[x, y] = (int(11 + 15 * t), int(9 + 2 * (1 - t)), int(19 + 13 * t), 255)

glow = Image.new("RGBA", (N, N), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse((58*S, 50*S, 454*S, 455*S), fill=(255, 42, 132, 100))
glow = glow.filter(ImageFilter.GaussianBlur(38*S))
img = Image.alpha_composite(img, glow)
d = ImageDraw.Draw(img)
d.rounded_rectangle((12*S, 12*S, 500*S, 500*S), radius=100*S,
                     fill=(18, 14, 27, 255), outline=(255, 65, 139, 255), width=5*S)
d.rounded_rectangle((25*S, 25*S, 487*S, 487*S), radius=85*S,
                     outline=(89, 39, 83, 255), width=3*S)

# Original shield/network mark; no third-party brand artwork.
shield = [(256,65),(310,93),(378,111),(414,146),(428,205),(414,280),
          (373,348),(319,399),(256,447),(193,399),(139,348),(98,280),
          (84,205),(98,146),(134,111),(202,93)]
d.polygon([(x*S,y*S) for x,y in shield], fill=(34,14,37,255),
          outline=(255,67,136,255), width=7*S)
inner = [(256,90),(306,114),(361,129),(390,157),(402,207),(389,271),
         (354,327),(311,369),(256,411),(201,369),(158,327),(123,271),
         (110,207),(122,157),(151,129),(206,114)]
d.polygon([(x*S,y*S) for x,y in inner], fill=(20,14,29,255),
          outline=(255,177,212,255), width=4*S)

# Split line and bright network nodes.
nodes = [(173,196),(237,231),(286,183),(344,229),(194,303),(258,335),(324,298)]
edges = [(0,1),(1,2),(2,3),(1,4),(4,5),(5,6),(3,6)]
for a,b in edges:
    ax, ay = nodes[a]
    bx, by = nodes[b]
    d.line((ax*S, ay*S, bx*S, by*S), fill=(255,75,147,255), width=5*S)
for i,(x,y) in enumerate(nodes):
    r = 10 if i in (1,2,5) else 7
    d.ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),
              fill=(255,231,245,255), outline=(255,74,147,255), width=4*S)

# The interrupted center bar stands for custom packet segmentation.
d.rounded_rectangle((189*S, 245*S, 239*S, 263*S), radius=8*S, fill=(255,69,140,255))
d.rounded_rectangle((273*S, 245*S, 323*S, 263*S), radius=8*S, fill=(255,184,67,255))
d.polygon([(246*S,238*S),(266*S,238*S),(257*S,253*S),(271*S,253*S),
           (247*S,278*S),(253*S,260*S),(240*S,260*S)], fill=(255,235,247,255))

for x,y,r in [(72,145,8),(429,127,6),(439,360,8),(89,385,5),(365,74,4)]:
    d.polygon([(x*S,(y-r)*S),((x+r//2)*S,y*S),(x*S,(y+r)*S),((x-r//2)*S,y*S)],
              fill=(255,90,157,255))

img = img.resize((512, 512), Image.Resampling.LANCZOS)
mask = Image.new("L", (512, 512), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, 511, 511), radius=100, fill=255)
img.putalpha(mask)
img.save(OUT, format="ICO", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print(f"Created icon: {OUT}")
