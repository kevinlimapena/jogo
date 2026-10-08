"""Ícones simples das cartas do Ezequiel (128x128, PNG). Substituíveis por arte final com o mesmo nome."""
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = sys.argv[1] if len(sys.argv) > 1 else "out"
os.makedirs(OUT, exist_ok=True)
S = 512          # tela de desenho (supersample)
F = 128          # tamanho final
k = S / 100.0    # unidades 0..100

RAR = {
    "comum": (205, 205, 210), "raro": (77, 153, 255), "epico": (191, 89, 255),
    "lendario": (255, 153, 26), "evo": (255, 242, 178), "arma": (120, 200, 210), "praga": (200, 50, 45),
}

# ---------------------------------------------------------------- utilidades
def P(x, y): return (x * k, y * k)
def rot(pts, ang, cx, cy):
    a = math.radians(ang); c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]

class Art:
    def __init__(self):
        self.img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)
    def poly(self, pts, col, ang=0, c=(50, 50)):
        if ang: pts = rot(pts, ang, *c)
        self.d.polygon([P(x, y) for x, y in pts], fill=col)
    def circ(self, cx, cy, r, col):
        self.d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=col)
    def ell(self, cx, cy, rx, ry, col):
        self.d.ellipse([P(cx - rx, cy - ry), P(cx + rx, cy + ry)], fill=col)
    def ring(self, cx, cy, r, w, col):
        self.d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], outline=col, width=int(w * k))
    def line(self, pts, w, col, ang=0, c=(50, 50)):
        if ang: pts = rot(pts, ang, *c)
        q = [P(x, y) for x, y in pts]
        self.d.line(q, fill=col, width=int(w * k), joint="curve")
        for x, y in q: self.d.ellipse([x - w * k / 2, y - w * k / 2, x + w * k / 2, y + w * k / 2], fill=col)
    def rect(self, x0, y0, x1, y1, col, ang=0, c=(50, 50)):
        self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], col, ang, c)
    def rrect(self, x0, y0, x1, y1, r, col):
        self.d.rounded_rectangle([P(x0, y0), P(x1, y1)], radius=r * k, fill=col)
    def arc(self, cx, cy, r, a0, a1, w, col):
        self.d.arc([P(cx - r, cy - r), P(cx + r, cy + r)], a0, a1, fill=col, width=int(w * k))
    def erase_circ(self, cx, cy, r):
        m = Image.new("L", (S, S), 0); ImageDraw.Draw(m).ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=255)
        a = self.img.getchannel("A"); a = ImageChops.subtract(a, m); self.img.putalpha(a)

# ---------------------------------------------------------------- formas
GOLD = (255, 205, 70); GOLD2 = (255, 240, 160); STEEL = (215, 225, 240); STEEL2 = (150, 165, 190)
WOOD = (150, 95, 45); RED = (235, 55, 60); FIRE = (255, 120, 30); FIRE2 = (255, 225, 90)
BLUE = (70, 160, 255); WATER = (60, 140, 230); WHITE = (245, 245, 245); DARK = (40, 35, 45)
GREEN = (90, 210, 110); PURP = (190, 110, 255)

def teardrop(a, cx, cy, w, h, col, up=True, wobble=0.0):
    pts = []
    for i in range(60):
        t = i / 60 * 2 * math.pi
        x = w * math.sin(t) * (math.sin(t / 2) ** 1.0)
        y = -h * math.cos(t)
        if wobble: x *= 1 + wobble * math.sin(t * 5)
        pts.append((cx + x, cy + (y if up else -y)))
    a.poly(pts, col)

def flame(a, cx, cy, s=1.0):
    teardrop(a, cx, cy, 22 * s, 30 * s, FIRE, wobble=0.12)
    teardrop(a, cx, cy + 7 * s, 12 * s, 20 * s, FIRE2)

def heart(a, cx, cy, s, col):
    a.circ(cx - 9 * s, cy - 5 * s, 10.5 * s, col); a.circ(cx + 9 * s, cy - 5 * s, 10.5 * s, col)
    a.poly([(cx - 19.5 * s, cy - 2 * s), (cx + 19.5 * s, cy - 2 * s), (cx, cy + 20 * s)], col)

def star(a, cx, cy, n, r1, r2, col, ang=-90):
    pts = []
    for i in range(n * 2):
        r = r1 if i % 2 == 0 else r2
        t = math.radians(ang + i * 180 / n)
        pts.append((cx + r * math.cos(t), cy + r * math.sin(t)))
    a.poly(pts, col)

def bolt(a, cx, cy, s, col):
    pts = [(4, -30), (-14, 4), (-2, 4), (-8, 30), (14, -6), (2, -6), (10, -30)]
    a.poly([(cx + x * s, cy + y * s) for x, y in pts], col)

def shield(a, cx, cy, s, col, col2):
    pts = [(-22, -24), (22, -24), (22, 0)] + [(22 * math.cos(t), 6 + 22 * math.sin(t) * 1.1) for t in [i / 20 * math.pi for i in range(1, 20)]] + [(-22, 0)]
    a.poly([(cx + x * s, cy + y * s) for x, y in pts], col)
    pts2 = [(x * 0.62, y * 0.62 - 2) for x, y in pts]
    a.poly([(cx + x * s, cy + y * s) for x, y in pts2], col2)

def bullet(a, cx, cy, s, col, ang=0):
    body = [(cx - 7 * s, cy - 4 * s), (cx + 7 * s, cy - 4 * s), (cx + 7 * s, cy + 18 * s), (cx - 7 * s, cy + 18 * s)]
    tip = [(cx + 7 * s * math.cos(t), cy - 4 * s - 14 * s * math.sin(t)) for t in [i / 20 * math.pi for i in range(21)]]
    a.poly(body, col, ang, (cx, cy)); a.poly(tip, col, ang, (cx, cy))
    a.rect(cx - 7 * s, cy + 12 * s, cx + 7 * s, cy + 18 * s, (180, 120, 40), ang, (cx, cy))

def eye(a, cx, cy, s, col, iris):
    pts = []
    for i in range(41):
        t = -1 + i / 20; pts.append((cx + 30 * s * t, cy - 16 * s * (1 - t * t)))
    for i in range(41):
        t = 1 - i / 20; pts.append((cx + 30 * s * t, cy + 16 * s * (1 - t * t)))
    a.poly(pts, col); a.circ(cx, cy, 11 * s, iris); a.circ(cx, cy, 5 * s, DARK); a.circ(cx + 3 * s, cy - 3 * s, 2.5 * s, WHITE)

def sword(a, cx, cy, ang, L=70, blade=STEEL, hilt=GOLD):
    b = [(cx - 4, cy - L * 0.5), (cx, cy - L * 0.5 - 7), (cx + 4, cy - L * 0.5), (cx + 4, cy + L * 0.22), (cx - 4, cy + L * 0.22)]
    a.poly(b, blade, ang, (cx, cy))
    a.line([(cx, cy - L * 0.45), (cx, cy + L * 0.18)], 1.2, STEEL2, ang, (cx, cy))
    a.rect(cx - 13, cy + L * 0.22, cx + 13, cy + L * 0.22 + 5, hilt, ang, (cx, cy))
    a.rect(cx - 3, cy + L * 0.22 + 5, cx + 3, cy + L * 0.5, WOOD, ang, (cx, cy))
    p = rot([(cx, cy + L * 0.5 + 2)], ang, cx, cy)[0]; a.circ(p[0], p[1], 4.5, hilt)

def coin(a, cx, cy, r, col=GOLD):
    a.circ(cx, cy, r, (200, 140, 30)); a.circ(cx, cy - 1, r - 2, col); a.ring(cx, cy - 1, r - 6, 2, (210, 150, 40))

def arrow(a, x0, y0, x1, y1, w, col, head=12):
    ang = math.atan2(y1 - y0, x1 - x0)
    bx, by = x1 - head * 0.8 * math.cos(ang), y1 - head * 0.8 * math.sin(ang)
    a.line([(x0, y0), (bx, by)], w, col)
    h = [(x1, y1), (bx + head * 0.6 * math.cos(ang + 1.6), by + head * 0.6 * math.sin(ang + 1.6)), (bx + head * 0.6 * math.cos(ang - 1.6), by + head * 0.6 * math.sin(ang - 1.6))]
    a.poly(h, col)

def hourglass(a, cx, cy, s, col, sand):
    a.rect(cx - 20 * s, cy - 30 * s, cx + 20 * s, cy - 25 * s, WOOD); a.rect(cx - 20 * s, cy + 25 * s, cx + 20 * s, cy + 30 * s, WOOD)
    a.poly([(cx - 16 * s, cy - 25 * s), (cx + 16 * s, cy - 25 * s), (cx + 2 * s, cy), (cx + 16 * s, cy + 25 * s), (cx - 16 * s, cy + 25 * s), (cx - 2 * s, cy)], col)
    a.poly([(cx - 9 * s, cy - 14 * s), (cx + 9 * s, cy - 14 * s), (cx, cy - 2 * s)], sand)
    a.poly([(cx - 13 * s, cy + 24 * s), (cx + 13 * s, cy + 24 * s), (cx, cy + 12 * s)], sand)

def cloud(a, cx, cy, s, col):
    for dx, dy, r in [(-14, 4, 11), (0, -3, 14), (14, 3, 11), (-5, 7, 10), (7, 8, 10)]:
        a.circ(cx + dx * s, cy + dy * s, r * s, col)

def trumpet(a, cx, cy, s, col, ang=-20):
    pts = [(-30, -3), (8, -3), (30, -15), (30, 15), (8, 3), (-30, 3)]
    a.poly([(cx + x * s, cy + y * s) for x, y in pts], col, ang, (cx, cy))
    a.rect(cx - 34 * s, cy - 5 * s, cx - 28 * s, cy + 5 * s, col, ang, (cx, cy))
    a.rect(cx - 10 * s, cy - 9 * s, cx - 4 * s, cy + 3 * s, col, ang, (cx, cy))

def bread(a, cx, cy, s):
    a.ell(cx, cy, 30 * s, 17 * s, (200, 135, 60)); a.ell(cx, cy - 3 * s, 27 * s, 13 * s, (235, 175, 90))
    for dx in (-12, 0, 12): a.line([(cx + dx * s - 4 * s, cy - 9 * s), (cx + dx * s + 4 * s, cy + 3 * s)], 2.5 * s, (170, 110, 45))

def wing(a, cx, cy, s, col, flip=1):
    for i in range(5):
        a.ell(cx + flip * (i * 6 - 8) * s, cy + (i * 5 - 10) * s, 20 * s - i * 2.5 * s, 6 * s, col)
    a.ell(cx - flip * 10 * s, cy - 10 * s, 10 * s, 9 * s, col)

def pistol(a, cx, cy, s, col, ang=0, long=1.0):
    pts = [(-24, -10), (24 * long, -10), (24 * long, -1), (-4, -1), (-2, 4), (-10, 22), (-20, 22), (-15, 2), (-24, 2)]
    a.poly([(cx + x * s, cy + y * s) for x, y in pts], col, ang, (cx, cy))
    a.rect(cx - 6 * s, cy - 1 * s, cx - 1 * s, cy + 7 * s, DARK, ang, (cx, cy))

def wing2(a):
    for i in range(6):
        lay = Art(); L = 40 - i * 4
        lay.ell(30 + L, 50, L, 7, WHITE if i % 2 == 0 else (215, 225, 240))
        r = lay.img.rotate(28 + i * 16, center=P(30, 50), resample=Image.BICUBIC)
        a.img.alpha_composite(r, (0, int(16 * k)))
    a.ell(30, 66, 11, 10, WHITE)

def drop(a, cx, cy, s, col):
    teardrop(a, cx, cy, 18 * s, 26 * s, col)

def crack(a, pts, w=2.2): a.line(pts, w, DARK)

def cardshape(a, cx, cy, ang, col):
    a.rrect(cx - 13, cy - 18, cx + 13, cy + 18, 3, col)  # sem rotação (desenhado em camada)

def fan_cards(a):
    for ang, col in [(-22, (77, 153, 255)), (0, (191, 89, 255)), (22, (255, 153, 26))]:
        lay = Art(); lay.rrect(37, 26, 63, 66, 3, col); lay.rrect(40, 29, 60, 63, 2, (30, 28, 40)); star(lay, 50, 46, 4, 7, 2.5, WHITE)
        r = lay.img.rotate(-ang, center=P(50, 80), resample=Image.BICUBIC)
        a.img.alpha_composite(r)

def waves(a):
    for x0, x1 in [(14, 36), (64, 86)]:
        pts = [(x0, 86), (x1, 86), (x1, 22)]
        for i in range(11):
            t = i / 10; pts.append((x1 - (x1 - x0) * t, 22 + 4 * math.sin(t * 9)))
        a.poly(pts, WATER)
        a.line([(x0 + 2, 25), (x1 - 2, 25)], 3, (180, 225, 255))
    a.poly([(38, 88), (62, 88), (55, 30), (45, 30)], (225, 195, 130))

def staff(a, col=WOOD, bloom=False):
    a.line([(50, 88), (50, 26)], 6, col)
    a.arc(42, 26, 8, 180, 360, 6, col); a.line([(34, 26), (34, 32)], 6, col)
    if bloom:
        for dx, dy in [(8, 46), (-8, 60), (8, 70), (-8, 38)]:
            a.circ(50 + dx, dy, 5, (255, 170, 200)); a.circ(50 + dx, dy, 2, GOLD)
        a.ell(56, 52, 6, 3, GREEN); a.ell(44, 64, 6, 3, GREEN)

def hammer(a, col=STEEL, ang=-30):
    a.rect(47, 30, 53, 88, WOOD, ang)
    a.rect(30, 16, 70, 36, col, ang); a.rect(30, 16, 70, 21, (240, 245, 255), ang)

def spear(a, ang=-35, L=1.0):
    a.rect(48, 26, 52, 92, WOOD, ang)
    a.poly([(50, 4), (58, 22), (50, 30), (42, 22)], STEEL, ang); a.rect(45, 28, 55, 32, GOLD, ang)

def bow(a, triple=False):
    a.arc(66, 50, 40, 125, 235, 5, WOOD)
    a.line([(43, 17), (43, 83)], 1.6, WHITE)
    offs = [-10, 0, 10] if triple else [0]
    for dy in offs: arrow(a, 40, 50 + dy, 88, 50 + dy * 0.4, 2.5, STEEL, 12)

def scroll(a):
    a.rect(26, 26, 74, 74, (240, 225, 185)); a.ell(26, 50, 6, 26, (215, 195, 150)); a.ell(74, 50, 6, 26, (215, 195, 150))
    for i, y in enumerate([36, 50, 64]):
        a.circ(42 + i * 8, y, 6, RED); a.circ(42 + i * 8, y, 2.5, (150, 20, 25))

def cross(a, col=GOLD):
    a.rect(45, 14, 55, 88, col); a.rect(28, 30, 72, 40, col)

def moon(a, cx, cy, r, col):
    a.circ(cx, cy, r, col); a.erase_circ(cx + r * 0.45, cy - r * 0.25, r * 0.85)

def clock(a):
    a.circ(50, 50, 30, GOLD); a.circ(50, 50, 25, (250, 245, 225))
    for i in range(12):
        t = i / 12 * 2 * math.pi; a.circ(50 + 20 * math.sin(t), 50 - 20 * math.cos(t), 1.6, DARK)
    a.line([(50, 50), (50, 33)], 3, DARK); a.line([(50, 50), (62, 56)], 3, DARK); a.circ(50, 50, 3, RED)

def horn(a):
    pts = []
    for i in range(40):
        t = i / 39; ang = t * 1.6 * math.pi; r = 34 * (1 - t * 0.7)
        pts.append((50 + r * math.cos(ang), 50 + r * math.sin(ang)))
    a.line(pts, 15, (230, 215, 180)); a.line(pts, 4, (200, 180, 140))

def drone(a):
    a.line([(28, 40), (72, 40)], 4, STEEL2)
    for x in (24, 76): a.ell(x, 36, 13, 3.5, (200, 210, 230))
    a.ell(50, 52, 18, 11, (90, 110, 140)); a.ell(50, 49, 14, 7, STEEL); a.circ(50, 56, 5, RED); a.circ(51, 55, 2, WHITE)

def ghost(a):
    pts = [(28, 50)] + [(50 + 22 * math.cos(t), 48 - 26 * math.sin(t)) for t in [math.pi - i / 20 * math.pi for i in range(21)]] + [(72, 80)]
    for i in range(7): pts.append((72 - i * 22 / 3, 80 if i % 2 == 0 else 72))
    a.poly(pts, (225, 235, 255)); a.ell(42, 46, 4, 6, DARK); a.ell(58, 46, 4, 6, DARK)

def glass(a):
    a.poly([(50, 14), (78, 46), (50, 88), (22, 46)], (170, 230, 255)); a.poly([(50, 14), (64, 46), (50, 88), (36, 46)], (215, 245, 255))
    crack(a, [(40, 30), (52, 44), (46, 54), (58, 70)])

def burst(a, cx, cy, r, col=FIRE, col2=FIRE2, n=10):
    star(a, cx, cy, n, r, r * 0.5, col); star(a, cx, cy, n, r * 0.6, r * 0.3, col2, -90 + 180 / n)

def ripples(a, col):
    for r in (14, 24, 34): a.ring(50, 50, r, 3.5, col)

def pillar(a):
    a.rect(38, 38, 62, 90, (230, 225, 210)); a.rect(34, 34, 66, 40, (200, 190, 170)); a.rect(34, 86, 66, 92, (200, 190, 170))
    for x in (43, 50, 57): a.line([(x, 42), (x, 84)], 1.5, (190, 180, 160))
    flame(a, 50, 18, 0.65)

def stone(a, col=(170, 165, 155)):
    a.poly([(30, 40), (46, 26), (66, 30), (74, 50), (62, 70), (38, 72), (26, 56)], col)
    a.poly([(36, 42), (48, 32), (62, 36), (60, 44), (44, 48)], (205, 200, 190))

def sling(a):
    a.line([(30, 20), (44, 62)], 3.2, WOOD); a.line([(70, 20), (56, 62)], 3.2, WOOD)
    a.ell(50, 66, 12, 7, (140, 90, 50)); a.circ(50, 62, 8, (175, 170, 160))

def medkit(a):
    a.rrect(24, 32, 76, 78, 6, WHITE); a.rect(42, 25, 58, 33, (200, 200, 200))
    a.rect(45, 40, 55, 70, RED); a.rect(35, 50, 65, 60, RED)

def crosshair(a, col=RED):
    a.ring(50, 50, 24, 5, col)
    for p in [((50, 14), (50, 34)), ((50, 66), (50, 86)), ((14, 50), (34, 50)), ((66, 50), (86, 50))]: a.line(list(p), 5, col)
    a.circ(50, 50, 5, col)

def clover(a):
    for dx, dy in [(0, -12), (12, 0), (0, 12), (-12, 0)]: heart(a, 50 + dx, 46 + dy, 0.62, GREEN)
    a.line([(50, 52), (60, 84)], 3.5, (60, 150, 70))

def chevrons(a, n, col, vertical=False):
    for i in range(n):
        if vertical:
            y = 62 - i * 20; a.line([(30, y + 10), (50, y - 6), (70, y + 10)], 7, col)
        else:
            x = 30 + i * 18; a.line([(x, 30), (x + 18, 50), (x, 70)], 7, col)

# ---------------------------------------------------------------- moldura
def plate(rar):
    c = RAR[rar]
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for i in range(60, 0, -1):
        t = i / 60.0
        col = tuple(int(10 + c[j] * 0.34 * (1 - t) ** 1.2 + 14 * (1 - t)) for j in range(3)) + (255,)
        r = S * 0.72 * t
        gd.ellipse([S / 2 - r, S * 0.44 - r, S / 2 + r, S * 0.44 + r], fill=col)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([6, 6, S - 6, S - 6], radius=int(S * 0.17), fill=255)
    bg = Image.new("RGBA", (S, S), (10, 9, 14, 255)); bg.alpha_composite(grad)
    img.paste(bg, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([6, 6, S - 6, S - 6], radius=int(S * 0.17), outline=c + (255,), width=int(S * 0.035))
    d.rounded_rectangle([int(S * 0.06), int(S * 0.06), S - int(S * 0.06), S - int(S * 0.06)], radius=int(S * 0.13), outline=tuple(min(255, v + 40) for v in c) + (90,), width=3)
    return img

def finish(art, rar):
    base = plate(rar)
    a = art.img.getchannel("A")
    outline = a.filter(ImageFilter.MaxFilter(17))
    sh = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sh.putalpha(outline.filter(ImageFilter.GaussianBlur(10)).point(lambda v: int(v * 0.55)))
    base.alpha_composite(sh, (6, 10))
    ol = Image.new("RGBA", (S, S), (22, 16, 12, 255)); ol.putalpha(outline)
    base.alpha_composite(ol)
    base.alpha_composite(art.img)
    return base.resize((F, F), Image.LANCZOS)

# ---------------------------------------------------------------- cartas
I = {}
def icon(cid, rar):
    def deco(fn): I[cid] = (rar, fn); return fn
    return deco

@icon("dano", "comum")
def _(a): bullet(a, 50, 46, 1.9, GOLD)
@icon("cadencia", "comum")
def _(a): chevrons(a, 3, FIRE2)
@icon("polvora", "comum")
def _(a): burst(a, 30, 62, 15); arrow(a, 34, 58, 84, 22, 6, GOLD, 18)
@icon("calibre", "comum")
def _(a): a.circ(50, 50, 26, (210, 160, 60)); a.circ(44, 43, 9, GOLD2)
@icon("coracao", "comum")
def _(a): heart(a, 46, 52, 1.35, RED); a.rect(66, 18, 74, 42, WHITE); a.rect(58, 26, 82, 34, WHITE)
@icon("agil", "comum")
def _(a): wing2(a)
@icon("sorte", "comum")
def _(a): clover(a)
@icon("crit", "comum")
def _(a): eye(a, 50, 50, 1.25, WHITE, (230, 170, 40))
@icon("ganancia", "comum")
def _(a): coin(a, 36, 62, 16); coin(a, 64, 62, 16); coin(a, 50, 38, 17)
@icon("fantasma", "comum")
def _(a): ghost(a)
@icon("pao", "comum")
def _(a): bread(a, 50, 54, 1.25)
@icon("dizimo", "comum")
def _(a): coin(a, 50, 58, 24); arrow(a, 50, 30, 50, 8, 6, GREEN, 14)

@icon("multi", "raro")
def _(a): [bullet(a, x, 46 + abs(x - 50) * 0.3, 1.0, GOLD, ang) for x, ang in ((26, -24), (50, 0), (74, 24))]
@icon("perfura", "raro")
def _(a): a.circ(38, 50, 12, (200, 80, 80)); a.circ(62, 50, 12, (200, 80, 80)); arrow(a, 12, 50, 90, 50, 5, STEEL, 16)
@icon("explosivo", "raro")
def _(a): burst(a, 50, 50, 34, FIRE, FIRE2, 12)
@icon("homing", "raro")
def _(a): crosshair(a, (255, 90, 90)); a.arc(50, 70, 32, 200, 300, 5, GOLD)
@icon("escudo", "raro")
def _(a): shield(a, 50, 48, 1.3, (90, 200, 255), (200, 240, 255))
@icon("pulo2", "raro")
def _(a): chevrons(a, 2, WHITE, vertical=True); a.line([(30, 84), (70, 84)], 4, (150, 150, 160))
@icon("vampiro", "raro")
def _(a): drop(a, 50, 52, 1.35, (200, 20, 40)); a.ell(43, 46, 4, 7, (255, 120, 130))
@icon("fatal", "raro")
def _(a):
    a.poly([(50, 10), (58, 24), (56, 66), (44, 66), (42, 24)], STEEL); a.rect(32, 64, 68, 70, GOLD); a.rect(46, 70, 54, 88, WOOD)
@icon("kit", "raro")
def _(a): medkit(a)
@icon("funda", "raro")
def _(a): sling(a)
@icon("mana", "raro")
def _(a):
    a.poly([(30, 56), (70, 56), (64, 88), (36, 88)], (180, 120, 70)); a.rect(28, 52, 72, 58, (150, 95, 50))
    for x, y in [(36, 18), (52, 12), (66, 22), (44, 32), (60, 38), (50, 46)]: star(a, x, y, 4, 5, 2, WHITE)
@icon("pisao", "raro")
def _(a): arrow(a, 50, 10, 50, 58, 8, GOLD, 22); a.arc(50, 82, 30, 200, 340, 5, WHITE); a.arc(50, 82, 20, 210, 330, 4, WHITE)
@icon("setimo", "raro")
def _(a): moon(a, 44, 50, 28, (240, 230, 170)); star(a, 72, 30, 5, 9, 4, WHITE)

@icon("corrente", "epico")
def _(a): bolt(a, 50, 50, 1.25, (130, 210, 255)); bolt(a, 50, 50, 0.75, WHITE)
@icon("drone", "epico")
def _(a): drone(a)
@icon("reacao", "epico")
def _(a): burst(a, 30, 66, 16); burst(a, 52, 46, 18); burst(a, 72, 26, 14)
@icon("represalia", "epico")
def _(a): ripples(a, (255, 120, 90)); a.circ(50, 50, 7, WHITE)
@icon("adrenalina", "epico")
def _(a): heart(a, 50, 52, 1.5, RED); bolt(a, 50, 48, 0.75, FIRE2)
@icon("vidro", "epico")
def _(a): glass(a)
@icon("pacto", "epico")
def _(a): drop(a, 42, 54, 1.2, (170, 10, 30)); a.line([(56, 20), (80, 44)], 5, STEEL); a.line([(80, 20), (56, 44)], 5, STEEL)
@icon("laminas", "epico")
def _(a):
    for th in (0, 120, 240):
        a.poly([(50, 50), (44, 40), (50, 10), (58, 22), (56, 44)], FIRE, th)
        a.poly([(50, 46), (48, 38), (51, 18), (54, 30), (53, 44)], FIRE2, th)
    a.circ(50, 50, 9, (255, 120, 40)); a.circ(50, 50, 4, FIRE2)
@icon("jerico", "epico")
def _(a): trumpet(a, 52, 54, 1.2, GOLD); [a.line([(80, y), (92, y - 6)], 3, GOLD2) for y in (34, 46)]
@icon("fogoceu", "epico")
def _(a): cloud(a, 50, 22, 1.1, (190, 190, 210)); flame(a, 50, 66, 0.8)
@icon("ira", "epico")
def _(a): flame(a, 50, 54, 1.25); a.line([(38, 50), (47, 55)], 4, DARK); a.line([(62, 50), (53, 55)], 4, DARK)

@icon("marvermelho", "lendario")
def _(a): waves(a)
@icon("vara", "lendario")
def _(a): staff(a, WOOD); a.line([(50, 46), (58, 52), (44, 60), (56, 68), (48, 74)], 3.5, GREEN)
@icon("tempo", "lendario")
def _(a): clock(a)
@icon("ariete", "lendario")
def _(a): horn(a)
@icon("destino", "lendario")
def _(a): fan_cards(a)
@icon("arsenal", "lendario")
def _(a): sword(a, 50, 50, 40, 68); sword(a, 50, 50, -40, 68); star(a, 50, 24, 5, 9, 4, GOLD)

# armas
@icon("arma_espada", "comum")
def _(a): sword(a, 50, 50, 35, 78)
@icon("arma_lanca", "raro")
def _(a): spear(a)
@icon("arma_martelo", "raro")
def _(a): hammer(a)
@icon("arma_pistola", "comum")
def _(a): pistol(a, 52, 48, 1.4, STEEL2)
@icon("arma_escopeta", "raro")
def _(a): pistol(a, 42, 52, 1.15, (140, 100, 70), long=1.45); [a.circ(86, 40 + d, 3, FIRE2) for d in (-8, 0, 8)]
@icon("arma_metralhadora", "raro")
def _(a): pistol(a, 40, 54, 1.1, (110, 120, 130), long=1.6); [a.rect(46, 30 + i * 0, 50, 42, GOLD) for i in range(1)]; [a.circ(86, 36 + i * 7, 2.5, FIRE2) for i in range(3)]
@icon("arma_duplas", "raro")
def _(a): pistol(a, 40, 40, 0.95, STEEL2, -15); pistol(a, 60, 62, 0.95, (130, 230, 210), -15)
@icon("arma_bazuca", "epico")
def _(a):
    a.rect(14, 42, 78, 56, (90, 110, 80), -12); a.rect(36, 56, 46, 70, (70, 85, 60), -12)
    a.poly([(78, 40), (92, 46), (80, 56)], RED, -12); burst(a, 18, 54, 9)
@icon("arma_arco", "raro")
def _(a): bow(a)
@icon("arma_railgun", "epico")
def _(a): a.rect(12, 44, 70, 56, (80, 90, 120)); a.rect(14, 46, 66, 50, (120, 220, 255)); a.line([(70, 50), (94, 50)], 6, (160, 230, 255)); a.rect(26, 56, 34, 70, (60, 70, 95))

# evoluções
@icon("evo_espada", "evo")
def _(a): flame(a, 50, 38, 0.95); sword(a, 50, 54, 0, 70, FIRE2, GOLD)
@icon("evo_martelo", "evo")
def _(a): hammer(a, (190, 170, 140), -25); a.poly([(64, 70), (90, 62), (66, 78)], STEEL)
@icon("evo_lanca", "evo")
def _(a): spear(a, -45); star(a, 22, 22, 4, 10, 3, GOLD2)
@icon("evo_arco", "evo")
def _(a): bow(a, triple=True)
@icon("evo_trombeta", "evo")
def _(a): trumpet(a, 50, 56, 1.3, GOLD); [a.line([(50, 50), (50 + 44 * math.cos(t), 50 + 44 * math.sin(t))], 2, GOLD2) for t in [-2.6, -2.0, -1.2, -0.6]]
@icon("evo_pedra", "evo")
def _(a): stone(a); star(a, 70, 28, 5, 12, 5, GOLD)
@icon("evo_pao", "evo")
def _(a): star(a, 50, 48, 12, 38, 26, (255, 230, 140)); bread(a, 50, 54, 1.15)
@icon("evo_coluna", "evo")
def _(a): pillar(a)
@icon("evo_cruz", "evo")
def _(a): star(a, 50, 46, 16, 40, 24, (255, 235, 160)); cross(a)
@icon("evo_selos", "evo")
def _(a): scroll(a)
@icon("evo_vara", "evo")
def _(a): staff(a, WOOD, bloom=True)

# cartas que mudam as regras
@icon("diluvio", "lendario")
def _(a):
    a.poly([(16, 52), (84, 52), (74, 70), (26, 70)], (150, 95, 45)); a.rect(30, 36, 70, 52, (190, 130, 60)); a.poly([(26, 36), (74, 36), (50, 24)], (120, 70, 35))
    a.rect(46, 40, 54, 48, DARK)
    for x in range(10, 95, 16): a.arc(x, 82, 9, 200, 340, 4, WATER)
@icon("jaco", "lendario")
def _(a):
    a.poly([(40, 90), (46, 90), (58, 10), (52, 10)], GOLD2); a.poly([(58, 90), (64, 90), (76, 10), (70, 10)], GOLD2)
    for i in range(7):
        y = 82 - i * 11; a.line([(45 - (82 - y) * 0.15 + 2, y), (63 - (82 - y) * 0.15 + 2, y)], 3, GOLD)
    star(a, 72, 12, 8, 10, 4, WHITE)
@icon("gideao", "lendario")
def _(a):
    a.poly([(34, 50), (66, 50), (62, 84), (38, 84)], (185, 110, 60)); a.ell(50, 50, 16, 5, (150, 85, 45))
    flame(a, 50, 32, 0.7); a.line([(22, 30), (32, 20)], 4, GOLD2); a.line([(78, 30), (68, 20)], 4, GOLD2)
@icon("babel", "lendario")
def _(a):
    for i, (w, y) in enumerate([(30, 80), (25, 66), (20, 52), (15, 38), (10, 24)]):
        a.rect(50 - w, y - 12, 50 + w, y, (215 - i * 8, 175 - i * 8, 120)); a.rect(50 - w, y - 3, 50 + w, y, (170, 130, 85))
    bolt(a, 76, 24, 0.5, (190, 140, 255))
@icon("jose", "epico")
def _(a): moon(a, 40, 46, 22, (240, 230, 170)); a.circ(72, 30, 9, GOLD); [star(a, x, y, 5, 5, 2, WHITE) for x, y in [(66, 58), (78, 70), (58, 76), (82, 50)]]
@icon("fartura", "epico")
def _(a):
    for dx in (-14, 0, 14):
        a.line([(50 + dx * 0.4, 88), (50 + dx, 40)], 3, (200, 160, 60))
        for k in range(4): a.ell(50 + dx + (k % 2 * 2 - 1) * 4, 42 + k * 7, 4, 6, GOLD)
    a.rect(36, 66, 64, 72, (150, 95, 45))
@icon("lo", "epico")
def _(a):
    a.circ(50, 24, 9, (240, 240, 235)); a.poly([(38, 34), (62, 34), (70, 88), (30, 88)], (235, 235, 228))
    a.poly([(42, 40), (50, 36), (46, 86), (36, 86)], (210, 210, 205)); arrow(a, 86, 30, 70, 30, 4, (255, 120, 90), 10)

# pragas
@icon("praga_ras", "praga")
def _(a):
    a.ell(50, 58, 26, 18, (70, 160, 60)); a.circ(36, 40, 9, (70, 160, 60)); a.circ(64, 40, 9, (70, 160, 60))
    a.circ(36, 40, 4, DARK); a.circ(64, 40, 4, DARK); a.line([(40, 64), (60, 64)], 3, (40, 100, 35))
    a.ell(26, 76, 10, 5, (60, 140, 50)); a.ell(74, 76, 10, 5, (60, 140, 50))
@icon("praga_gafanhotos", "praga")
def _(a):
    a.ell(48, 52, 26, 9, (150, 140, 60)); a.circ(76, 48, 8, (130, 120, 50)); a.circ(79, 45, 2.5, DARK)
    a.line([(40, 50), (24, 30), (16, 70)], 3, (110, 100, 40)); a.ell(44, 40, 20, 6, (200, 200, 150, 200)); a.line([(56, 58), (66, 78)], 3, (110, 100, 40))
@icon("idolo", "praga")
def _(a):
    a.rect(26, 74, 74, 86, (120, 80, 40)); a.ell(48, 54, 24, 13, GOLD); a.circ(72, 42, 10, GOLD)
    a.poly([(66, 32), (62, 22), (70, 30)], GOLD2); a.poly([(78, 32), (82, 22), (74, 30)], GOLD2)
    for x in (32, 42, 56, 64): a.rect(x - 2, 62, x + 2, 74, (230, 170, 50))

@icon("harpa", "raro")
def _(a):
    a.poly([(30, 86), (40, 86), (40, 20), (30, 26)], WOOD)
    a.arc(56, 52, 30, 270, 360, 6, GOLD); a.arc(56, 52, 30, 0, 75, 6, GOLD)
    a.line([(34, 84), (64, 84), (82, 66)], 6, WOOD)
    for i in range(5):
        x = 44 + i * 7; a.line([(x, 82), (x, 26 + i * 4)], 1.6, (240, 235, 210))
    star(a, 78, 18, 4, 8, 3, WHITE)

# cartas novas (variedade)
@icon("lampada", "comum")
def _(a):
    a.ell(48, 64, 26, 11, (200, 140, 60)); a.poly([(70, 60), (88, 52), (74, 68)], (200, 140, 60)); a.arc(28, 62, 9, 90, 270, 4, (170, 110, 45))
    flame(a, 72, 40, 0.7); a.rect(22, 84, 78, 88, (120, 80, 40))
@icon("sal", "comum")
def _(a):
    a.poly([(30, 50), (70, 50), (64, 86), (36, 86)], (190, 190, 200)); a.ell(50, 50, 20, 6, WHITE)
    for x, y in ((40, 34), (52, 26), (60, 38), (46, 18)): a.rect(x - 3, y - 3, x + 3, y + 3, WHITE, 45, (x, y))
@icon("cinto", "comum")
def _(a):
    a.rect(14, 42, 86, 58, (150, 95, 45)); a.rect(14, 46, 86, 54, (120, 75, 35))
    a.rrect(38, 36, 62, 64, 4, GOLD); a.rrect(43, 41, 57, 59, 3, (120, 75, 35)); a.rect(48, 44, 52, 56, GOLD2)
@icon("espigas", "comum")
def _(a):
    for dx, ang in ((-16, -15), (0, 0), (16, 15)):
        a.line([(50 + dx * 0.4, 90), (50 + dx, 30)], 3, (190, 150, 60))
        for k in range(5): a.ell(50 + dx + (k % 2 * 2 - 1) * 4, 20 + k * 7, 4, 6, GOLD)
@icon("talento", "comum")
def _(a):
    a.poly([(30, 46), (70, 46), (78, 86), (22, 86)], (150, 95, 45)); a.rect(40, 36, 60, 46, (120, 75, 35))
    coin(a, 38, 28, 11); coin(a, 60, 22, 11); a.line([(50, 58), (50, 76)], 4, GOLD); a.line([(42, 64), (58, 64)], 4, GOLD)
@icon("cajado_pastor", "comum")
def _(a):
    a.line([(40, 92), (54, 30)], 6, WOOD); a.arc(66, 30, 12, 180, 360, 6, WOOD); a.line([(78, 30), (76, 40)], 6, WOOD)
@icon("sopro", "raro")
def _(a):
    for i in range(3):
        a.arc(30 + i * 4, 50, 14 + i * 9, 300, 420, 4, (200, 235, 255))
    heart(a, 68, 52, 0.9, RED)
@icon("aguia", "raro")
def _(a):
    wing(a, 38, 50, 0.95, (230, 220, 200), 1); wing(a, 62, 50, 0.95, (230, 220, 200), -1)
    a.ell(50, 54, 7, 16, (120, 80, 40)); a.circ(50, 34, 7, WHITE); a.poly([(48, 36), (52, 36), (50, 44)], GOLD)
@icon("ouro_ofir", "raro")
def _(a):
    a.poly([(16, 70), (84, 70), (74, 84), (26, 84)], WOOD); a.line([(50, 70), (50, 20)], 3, WOOD); a.poly([(52, 22), (78, 56), (52, 56)], WHITE)
    coin(a, 32, 60, 9); coin(a, 44, 58, 9)
@icon("chifre_oleo", "raro")
def _(a):
    a.poly([(24, 78), (34, 84), (76, 34), (66, 26)], (225, 200, 150)); a.ell(71, 30, 9, 6, GOLD)
    drop(a, 30, 34, 1.0, GOLD); drop(a, 20, 52, 0.7, GOLD)
@icon("brasas", "raro")
def _(a):
    a.line([(18, 30), (52, 54)], 4, STEEL2); a.ell(56, 58, 12, 7, STEEL2)
    for x, y in ((50, 70), (64, 74), (58, 64)): a.circ(x, y, 7, FIRE)
    a.circ(57, 68, 4, FIRE2); flame(a, 58, 46, 0.55)
@icon("orvalho", "raro")
def _(a):
    a.ell(50, 70, 34, 12, (235, 230, 215)); a.ell(50, 66, 28, 8, (250, 245, 235))
    drop(a, 38, 40, 1.0, WATER); drop(a, 60, 30, 1.1, WATER); drop(a, 52, 54, 0.8, BLUE)
@icon("relampago", "raro")
def _(a):
    a.poly([(10, 86), (40, 40), (60, 60), (90, 86)], (140, 120, 100)); cloud(a, 50, 24, 1.1, (120, 120, 140))
    bolt(a, 36, 50, 0.8, FIRE2); bolt(a, 64, 46, 0.8, FIRE2)
@icon("anjo_guarda", "epico")
def _(a):
    wing(a, 34, 52, 0.85, WHITE, 1); wing(a, 66, 52, 0.85, WHITE, -1)
    a.poly([(42, 44), (58, 44), (66, 88), (34, 88)], (240, 235, 220)); a.circ(50, 34, 9, (240, 210, 170)); a.ring(50, 20, 10, 3, GOLD)
@icon("forca_sansao", "epico")
def _(a):
    a.poly([(20, 74), (30, 82), (82, 34), (74, 24)], (235, 230, 215)); a.ell(80, 26, 10, 8, (235, 230, 215))
    for x, y in ((30, 72), (42, 62), (54, 52)): a.rect(x - 2, y - 8, x + 2, y + 2, (190, 180, 160))
    burst(a, 76, 24, 12, FIRE2, WHITE, 8)
@icon("granizo", "epico")
def _(a):
    cloud(a, 50, 26, 1.3, (90, 80, 100))
    for x, y in ((28, 54), (50, 64), (72, 52), (38, 80), (64, 82)):
        a.circ(x, y, 7, (220, 235, 255)); flame(a, x, y - 6, 0.35)
@icon("rede_pedro", "epico")
def _(a):
    a.poly([(16, 30), (84, 30), (72, 86), (28, 86)], (190, 170, 120, 90))
    for i in range(6): a.line([(16 + i * 13.6, 30), (28 + i * 8.8, 86)], 2, (190, 170, 120))
    for j in range(5): y = 30 + j * 14; a.line([(16 + j * 2.4, y), (84 - j * 2.4, y)], 2, (190, 170, 120))
    a.ell(40, 64, 10, 4, STEEL); a.ell(58, 52, 10, 4, STEEL); a.ell(54, 74, 10, 4, STEEL)
@icon("porcao", "epico")
def _(a):
    a.poly([(16, 50), (48, 50), (44, 84), (20, 84)], (190, 140, 80)); a.poly([(52, 50), (84, 50), (80, 84), (56, 84)], (190, 140, 80))
    flame(a, 32, 34, 0.7); flame(a, 68, 30, 0.95)
@icon("querubins", "lendario")
def _(a):
    a.ring(50, 50, 32, 6, GOLD); a.ring(50, 50, 20, 5, GOLD2)
    for i in range(8):
        t = i * math.pi / 4; eye(a, 50 + 32 * math.cos(t), 50 + 32 * math.sin(t), 0.22, WHITE, BLUE)
    eye(a, 50, 50, 0.4, WHITE, (230, 170, 40))
@icon("manto", "lendario")
def _(a):
    a.poly([(36, 20), (64, 20), (82, 88), (18, 88)], (120, 85, 55)); a.poly([(44, 20), (56, 20), (52, 88), (46, 88)], (95, 65, 40))
    flame(a, 26, 70, 0.6); flame(a, 74, 70, 0.6); flame(a, 50, 18, 0.5)
@icon("leao", "lendario")
def _(a):
    for i in range(12):
        t = i * math.pi / 6; a.circ(50 + 26 * math.cos(t), 50 + 26 * math.sin(t), 12, (200, 120, 40))
    a.circ(50, 52, 24, (235, 175, 70)); a.circ(41, 46, 4, DARK); a.circ(59, 46, 4, DARK)
    a.poly([(44, 58), (56, 58), (50, 66)], DARK); a.poly([(36, 24), (42, 12), (50, 22), (58, 12), (64, 24)], GOLD)

# genéricos (fallback por raridade, se faltar o ícone de alguma carta)
for r in ("comum", "raro", "epico", "lendario"):
    I["_" + r] = (r, lambda a: star(a, 50, 50, 4, 30, 10, WHITE))

def render(cid):
    rar, fn = I[cid]
    a = Art(); fn(a)
    return finish(a, rar)

if __name__ == "__main__":
    for cid in I:
        render(cid).save(os.path.join(OUT, cid + ".png"), optimize=True)
    print(len(I), "icones")
