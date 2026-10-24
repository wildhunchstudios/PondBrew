# GR-12B body-plan helpers (pilot, GR.34). Executed in the kit script's namespace (after its helpers), so Part, ellipsoid, cone,
# tube, taper_tube, torus, shell, rot, make_object, shape_key, slot_name, side_fins, simple_part, lure, gold_crown and
# creature_root are available. Unity coordinates throughout (X right, Y up, Z forward; a creature faces -Z).

def bp_catmull(points, n):
    """n points along a Catmull-Rom curve through points, evenly spaced by arc length."""
    P = [Vector(p) for p in points]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    segs = len(points) - 1
    fine = []
    m = max(400, n * 10)
    for i in range(m + 1):
        s = i / m * segs
        k = min(int(s), segs - 1); f = s - k
        p0, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        fine.append(.5 * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f * f * f))
    L = [0.0]
    for i in range(1, len(fine)):
        L.append(L[-1] + (fine[i] - fine[i - 1]).length)
    out, j = [], 0
    for i in range(n):
        target = L[-1] * i / (n - 1)
        while j < len(L) - 2 and L[j + 1] < target:
            j += 1
        span = L[j + 1] - L[j]
        f = 0 if span < 1e-9 else (target - L[j]) / span
        out.append(fine[j].lerp(fine[j + 1], min(1, max(0, f))))
    return out

class BPSpine:
    """A smooth spine through control points (head first), with frames that turn as little as they can (parallel transport).
    side is the creature's right (+X when the spine runs toward +Z), up its top."""
    def __init__(self, points, n=81, up=(0, 1, 0)):
        self.n = n
        self.pts = bp_catmull(points, n)
        self.sides, self.ups = [], []
        side = None
        for j in range(n):
            tan = (self.pts[min(j + 1, n - 1)] - self.pts[max(j - 1, 0)]).normalized()
            if side is None:
                side = Vector(up).cross(tan).normalized()
            side = (side - tan * side.dot(tan)).normalized()
            self.sides.append(side.copy()); self.ups.append(tan.cross(side).normalized())
    def frame(self, t):
        x = min(max(t, 0), 1) * (self.n - 1)
        k = min(int(x), self.n - 2); f = x - k
        c = self.pts[k].lerp(self.pts[k + 1], f)
        s = self.sides[k].lerp(self.sides[k + 1], f).normalized()
        up = self.ups[k].lerp(self.ups[k + 1], f).normalized()
        return c, s, up
    def tangent(self, t):
        a, b = self.frame(max(0, t - .01))[0], self.frame(min(1, t + .01))[0]
        return (b - a).normalized()

def bp_profile(keys, nose=.12, tail=.04):
    """A body profile from keys [(t, rx, ry_top, ry_bottom)], eased between keys, rounded off to a dome at the nose and a cap
    at the tail end. Returns profile(t) -> (rx, ryt, ryb)."""
    def lerp_keys(t):
        if t <= keys[0][0]:
            return keys[0][1:]
        for a, b in zip(keys, keys[1:]):
            if t <= b[0]:
                f = (t - a[0]) / (b[0] - a[0]); f = f * f * (3 - 2 * f)
                return tuple(a[i] + (b[i] - a[i]) * f for i in range(1, 4))
        return keys[-1][1:]
    def prof(t):
        rx, ryt, ryb = lerp_keys(t)
        d = 1.0
        if t < nose:
            d = math.sqrt(max(0, 1 - (1 - t / nose) ** 2))
        elif t > 1 - tail:
            d = math.sqrt(max(0, 1 - ((t - (1 - tail)) / tail) ** 2))
        return rx * d, ryt * d, ryb * d
    return prof

class BPBody:
    """What a spine loft made: the spine and profile (to place things on its surface) and each vertex's spine fraction, so
    the Puffed key can swell it from its spine."""
    def __init__(self, spine, prof):
        self.spine, self.prof = spine, prof
    def at(self, t, angle, k=1.0):
        """A point on the surface at spine fraction t and angle round it (0 the right side, 90 the top), k of the radius."""
        c, s, up = self.spine.frame(t)
        rx, ryt, ryb = self.prof(t)[:3]
        a = math.radians(angle); cs, sn = math.cos(a), math.sin(a)
        return c + s * (rx * cs * k) + up * ((ryt if sn > 0 else ryb) * sn * k)
    def normal(self, t, angle):
        p0 = self.at(t, angle, 1.0); c = self.spine.frame(t)[0]
        return (p0 - c).normalized()

def bp_loft(part, body, material, t0=0.0, t1=1.0, rings=40, seg=36, belly=None, squarish=None, caps=(True, True)):
    """A closed loft along body's spine from t0 to t1 (a pole at each end where caps say so; an open end is closed flat).
    belly(t, sin, cos) -> slot or None paints faces. squarish(t) > 1 squares the section."""
    spine, prof = body.spine, body.prof
    verts, params = [], []
    ts = [t0 + (t1 - t0) * r / rings for r in range(rings + 1)]
    inner = ts[1:-1] if caps[0] and caps[1] else ts[1:] if not caps[0] and caps[1] else ts[:-1] if caps[0] else ts
    if caps[0]:
        verts.append(tuple(spine.frame(t0)[0])); params.append((t0, 0, 0))
    for t in inner:
        c, s, up = spine.frame(t)
        rx, ryt, ryb = prof(t)[:3]
        rx, ryt, ryb = max(rx, .002), max(ryt, .002), max(ryb, .002)
        sq = squarish(t) if squarish else 1.0
        for k in range(seg):
            a = math.tau * k / seg
            x, y = math.cos(a), math.sin(a)
            if sq != 1.0:
                m = max((abs(x) ** sq + abs(y) ** sq) ** (1 / sq), 1e-6); x, y = x / m, y / m
            verts.append(tuple(c + s * (rx * x) + up * ((ryt if y > 0 else ryb) * y)))
            params.append((t, math.sin(a), math.cos(a)))
    if caps[1]:
        verts.append(tuple(spine.frame(t1)[0])); params.append((t1, 0, 0))
    faces = []
    o = 1 if caps[0] else 0
    nr = len(inner)
    if caps[0]:
        for k in range(seg):
            faces.append((0, o + k, o + (k + 1) % seg))
    else:
        faces.append(tuple(o + k for k in range(seg - 1, -1, -1)))
    for r in range(nr - 1):
        a, b = o + r * seg, o + (r + 1) * seg
        for k in range(seg):
            j = (k + 1) % seg
            faces.append((a + k, b + k, b + j, a + j))
    last = o + (nr - 1) * seg
    if caps[1]:
        end = len(verts) - 1
        for k in range(seg):
            faces.append((last + k, end, last + (k + 1) % seg))
    else:
        faces.append(tuple(last + k for k in range(seg)))
    first_face = len(part.faces)
    rng = part.add(verts, faces, material)  # faces and params both index this loft's own vertices here
    if belly:
        for fi, f in enumerate(faces):
            t = sum(params[i][0] for i in f) / len(f)
            sn = sum(params[i][1] for i in f) / len(f); cs = sum(params[i][2] for i in f) / len(f)
            slot = belly(t, sn, cs)
            if slot:
                part.slots[first_face + fi] = part.slot(slot)
    return rng

def bp_puffed(obj, part, body, gain=.15):
    """The Puffed key for a spine body: every vertex of the part swells away from the nearest point of the spine, most at
    the middle of its length."""
    pts = body.spine.pts; n = len(pts)
    def puffed(i, v):
        v = Vector(v)
        best, bj = 1e9, 0
        for j in range(0, n, 2):
            d = (pts[j] - v).length_squared
            if d < best:
                best, bj = d, j
        t = bj / (n - 1)
        g = 1 + gain * math.sin(math.pi * min(1, max(0, t))) ** .8
        return pts[bj] + (v - pts[bj]) * g
    shape_key(obj, part, 'Puffed', puffed)

def bp_plate(part, outline, thick, material, rim=None, rim_from=.72, normal=None, curl=0.0):
    """A soft fin membrane: a pillowy slab over a closed outline (roughly planar, star-shaped about its centre), thickest
    in the middle and thin at the edge, with an optional band of the rim slot round its edge."""
    P = [Vector(p) for p in outline]
    c = sum(P, Vector()) / len(P)
    if normal is None:
        nrm = Vector((0, 0, 0))
        for i in range(len(P)):
            a, b = P[i] - c, P[(i + 1) % len(P)] - c
            nrm += a.cross(b)
        nrm = nrm.normalized()
    else:
        nrm = Vector(normal).normalized()
    n = len(P)
    rings = (0.0, .45, rim_from, .9, 1.0)
    heights = (1.0, .92, .62, .42, .2)
    verts = []
    for side in (1, -1):
        verts.append(tuple(c + nrm * (thick * .5 * side)))
        for f, h in zip(rings[1:], heights[1:]):
            for p in P:
                q = c + (p - c) * f
                bend = nrm * (curl * f * f)
                verts.append(tuple(q + bend + nrm * (thick * .5 * h * side)))
    per = 1 + n * (len(rings) - 1)
    faces, slots = [], []
    for side in (0, 1):
        o = side * per
        for i in range(n):
            j = (i + 1) % n
            f = (o, o + 1 + i, o + 1 + j)
            faces.append(f if side == 0 else f[::-1]); slots.append(0)
        for r in range(len(rings) - 2):
            for i in range(n):
                j = (i + 1) % n
                a, b = o + 1 + r * n, o + 1 + (r + 1) * n
                f = (a + i, b + i, b + j, a + j)
                faces.append(f if side == 0 else f[::-1]); slots.append(1 if r >= 1 else 0)
    lo, hi = 1 + (len(rings) - 2) * n, per + 1 + (len(rings) - 2) * n
    for i in range(n):
        j = (i + 1) % n
        faces.append((lo + i, lo + j, hi + j, hi + i)); slots.append(1)
    first = len(part.faces)
    rng = part.add(verts, faces, material)
    if rim:
        for fi, s in enumerate(slots):
            if s:
                part.slots[first + fi] = part.slot(rim)
    return rng

def bp_outline(points, n=40, closed=True):
    """A smooth closed outline through points (Catmull-Rom, evenly spaced)."""
    pts = list(points) + [points[0], points[1]] if closed else list(points)
    curve = bp_catmull(pts[:len(points) + 1], n + 1)
    return [tuple(p) for p in curve[:-1]]

def bp_spots(part, body, sid, items, slot='Accent'):
    """Little round lights on the body's surface: items [(t, angle, radius)], both sides mirrored when angle isn't 90/-90."""
    for t, angle, r in items:
        for a in ((angle, 180 - angle) if abs(abs(angle) - 90) > 1 else (angle,)):
            p = body.at(t, a, .985)
            ellipsoid(part, tuple(p), (r, r, r), slot_name(sid, slot), (0, 0, 0), 12, 8)

def bp_face(root, sid, eye_c, eye_r, eye_yaw, mouth, eye_pitch=0, pupil_k=.6, blush=None, lid_slot='Body', smile=.025):
    """The house eyes (glossy whites, pupils, glints and lids with Blink and Happy) placed where the animal has them, a mouth
    of its own built by mouth(part) -> (centre, half width) into Lips (with Happy and Surprised), and blush only if given
    (blush=(centre, radii, yaw))."""
    S = lambda slot: slot_name(sid, slot)
    eyes, pupils, glints, lids, bl = Part('Eyes'), Part('Pupils'), Part('Glints'), Part('Lids'), Part('Blush')
    lid_builders = []
    for side in (-1, 1):
        c = (side * eye_c[0], eye_c[1], eye_c[2])
        eul = (eye_pitch, -side * eye_yaw, 0)
        ellipsoid(eyes, c, eye_r, 'DK_EyeWhite', eul, 24, 14)
        fwd = rot((0, 0, -1), eul)
        pr = (eye_r[0] * pupil_k, eye_r[1] * pupil_k * 1.06, eye_r[2] * .5)
        pc = Vector(c) + fwd * (eye_r[2] * .72) + Vector((0, -.004, 0))
        ellipsoid(pupils, pc, pr, 'DK_Pupil', eul, 20, 12)
        upv = rot((0, 1, 0), eul); sidev = rot((1, 0, 0), eul)
        gc = Vector(c) + fwd * (eye_r[2] * 1.15) + upv * (eye_r[1] * .29) - sidev * (side * (.006 + eye_r[0] * .16))
        ellipsoid(glints, gc, (eye_r[0] * .22, eye_r[1] * .2, eye_r[2] * .17), 'DK_Glint', eul, 12, 8)
        lid_r = (eye_r[0] * 1.07, eye_r[1] * 1.06, eye_r[2] * 1.08)
        idx, build = shell(lids, c, lid_r, S(lid_slot), eul, math.radians(24), .010, 24, 8)
        lid_builders.append((idx, build))
        if blush:
            bc, br, by = blush
            ellipsoid(bl, (side * bc[0], bc[1], bc[2]), br, S('Blush'), (0, -side * by, 0), 16, 8)
    if not blush:
        # The rig keeps a Blush part: a speck hidden inside the head, for animals that don't blush.
        ellipsoid(bl, (0, eye_c[1], eye_c[2] + .05), (.004, .004, .004), S('Blush'), (0, 0, 0), 6, 4)
    for p in (eyes, pupils, glints, bl):
        make_object(p, root)
    lids_obj = make_object(lids, root)
    for name, angle in (('Blink', 104), ('Happy', 58)):
        moved = {}
        for idx, build in lid_builders:
            for i, v in zip(idx, build(math.radians(angle))):
                moved[i] = v
        shape_key(lids_obj, lids, name, lambda i, v, moved=moved: moved[i])
    lips = Part('Lips')
    lc, hw = mouth(lips)
    lc = Vector(lc)
    lips_obj = make_object(lips, root)
    def happy(i, v):
        d = Vector(v) - lc
        return lc + Vector((d.x * 1.1, d.y * .85 + smile * min(1.5, (d.x / hw) ** 2) - .004, d.z))
    def surprised(i, v):
        d = Vector(v) - lc
        return lc + Vector((d.x * .95, d.y * 1.5, d.z * 1.05))
    shape_key(lips_obj, lips, 'Happy', happy)
    shape_key(lips_obj, lips, 'Surprised', surprised)

# ---------------------------------------------------------------- mouths (each builds into the Lips part)
def mouth_grin(body, t_mid, t_corner, a_corner, r=.012, sid=None, teeth=None, lip=None, k=1.0, depth=.0):
    """A smile drawn along the snout: a dark line from one corner (at spine fraction t_corner, angle a_corner on the right)
    under the snout (t_mid at the bottom) to the other. teeth: [(where along the line 0..1, length, radius, up?)]."""
    def build(part):
        pts = []
        for k_ in range(15):
            s = k_ / 14
            ang = (-180 - a_corner) + (a_corner - (-180 - a_corner)) * s
            w = abs(s - .5) * 2
            t = t_mid + (t_corner - t_mid) * w ** 1.6
            pts.append(body.at(t, ang, k + depth))
        tube(part, [tuple(p) for p in pts], r, 'DK_Mouth', 10)
        if lip:
            tube(part, [tuple(p + Vector((0, r * 1.2, 0))) for p in pts[2:-2]], r * .8, lip, 8)
        for where, length, rad, up in (teeth or ()):
            i = min(14, max(0, int(round(where * 14))))
            base = pts[i]
            d = Vector((0, 1 if up else -1, 0)) if isinstance(up, bool) else Vector(up).normalized()
            tip = base + d * length
            cone(part, tuple(base - d * rad * .5), tuple(tip), rad, 'DK_EyeWhite', 10, 4, .15)
        mid = pts[7]; width = (pts[0] - pts[14]).length / 2
        return tuple(mid), max(width, .02)
    return build

def mouth_gape(center, rx, ry, rim, sid, upper=(), lower=(), tilt=0, rim_r=.03, depth=.06):
    """A wide open mouth on the front of the face: a dark hollow, a rim of lips round it, and rounded teeth along the
    upper and lower edges [(across -1..1, length, radius)], pointing in."""
    def build(part):
        c = Vector(center)
        ellipsoid(part, tuple(c + rot((0, 0, depth * .4), (tilt, 0, 0))), (rx * .92, ry * .9, depth), 'DK_Mouth', (tilt, 0, 0), 28, 14)
        torus(part, c, 1.0, rim_r, rim, (rx, ry, 1), 40, 12, (tilt, 0, 0))
        for teeth, sign in ((upper, 1), (lower, -1)):
            for x, length, rad in teeth:
                a = math.acos(max(-1, min(1, x)))
                p = c + rot((rx * math.cos(a), sign * ry * math.sin(a) * .97, 0), (tilt, 0, 0))
                tip = p + rot((0, -sign * length, -.006), (tilt, 0, 0))
                cone(part, tuple(p), tuple(tip), rad, 'DK_EyeWhite', 10, 4, .2)
        return tuple(c), rx
    return build

def mouth_o(center, r, rim, euler=(0, 0, 0)):
    """A small round pursed mouth (a sunfish's), a ring of lips with a dark middle."""
    def build(part):
        c = Vector(center)
        torus(part, c, r, r * .55, rim, (1, 1, 1), 28, 10, euler)
        ellipsoid(part, tuple(c + rot((0, 0, .004), euler)), (r * .8, r * .8, .01), 'DK_Mouth', euler, 14, 8)
        return tuple(c), r * 1.4
    return build

def bp_lure(root, sid, base, rise, reach, bulb, stalk=.02, bend=110, kind='round'):
    lure(root, sid, tuple(base), rise, reach, bulb, stalk, bend, kind)

def bp_fins(root, pivot, build):
    """The Left and Right fin pivots, mirrored: build(part, m) draws the right fin (+X) through m(point), which mirrors x for
    the left one, and returns nothing."""
    for side, label in ((-1, 'Left'), (1, 'Right')):
        part = Part(label + ' fin')
        build(part, lambda p, side=side: (side * p[0], p[1], p[2]))
        make_object(part, root, (side * pivot[0], pivot[1], pivot[2]), label + ' fin pivot')

def bp_strip(part, lower, upper, thick, material, rim=None, seg=12, side_dirs=None):
    """A fin strip between two edges (lists of points, front to back): a flat tube whose section is a thin lens from the
    lower edge to the upper, rounded at both ends; rim paints its upper edge."""
    n = len(lower)
    verts, faces, slots = [], [], []
    for i in range(n):
        lo, hi = Vector(lower[i]), Vector(upper[i])
        c = (lo + hi) / 2; up = hi - lo
        h = max(up.length / 2, 1e-4); up = up.normalized()
        tan = (Vector(lower[min(i + 1, n - 1)]) - Vector(lower[max(i - 1, 0)])).normalized()
        sd = Vector(side_dirs[i]) if side_dirs else tan.cross(up).normalized()
        end = 1.0 if 0 < i < n - 1 else .35
        for k in range(seg):
            a = math.tau * k / seg
            verts.append(tuple(c + up * (h * math.sin(a)) + sd * (thick * .5 * math.cos(a) * end)))
    for r in range(n - 1):
        for k in range(seg):
            j = (k + 1) % seg
            faces.append((r * seg + k, r * seg + j, (r + 1) * seg + j, (r + 1) * seg + k))
            slots.append(math.sin(math.tau * (k + .5) / seg) > .55)
    faces.append(tuple(range(seg - 1, -1, -1))); slots.append(False)
    faces.append(tuple((n - 1) * seg + k for k in range(seg))); slots.append(False)
    first = len(part.faces)
    rng = part.add(verts, faces, material)
    if rim:
        for fi, s in enumerate(slots):
            if s:
                part.slots[first + fi] = part.slot(rim)
    return rng

def mouth_arc(center, width, droop, r=.01, back=.02, right=(1, 0, 0), up=(0, 1, 0), teeth=(), lip=None):
    """A smile on a face that looks forward: a dark line curving down from its corners (droop) to its middle, its corners
    set back by back to follow the face. teeth: [(across -1..1, length, radius, (dx, dy, dz))]."""
    def build(part):
        c, rt, upv = Vector(center), Vector(right), Vector(up)
        fwd = rt.cross(upv).normalized()  # toward the back of the head (+Z for a creature facing -Z)
        pts = []
        for k in range(13):
            x = -1 + 2 * k / 12
            pts.append(c + rt * (width * x) + upv * (droop * (x * x - 1)) + fwd * (back * x * x))
        tube(part, [tuple(p) for p in pts], r, 'DK_Mouth', 10)
        if lip:
            tube(part, [tuple(p - upv * r * 1.3) for p in pts[2:-2]], r * .8, lip, 8)
        for x, length, rad, d in teeth:
            base = c + rt * (width * x) + upv * (droop * (x * x - 1)) + fwd * (back * x * x)
            d = Vector(d).normalized()
            cone(part, tuple(base - d * rad * .4), tuple(base + d * length), rad, 'DK_EyeWhite', 10, 4, .15)
        return tuple(c + upv * (-droop * .5)), width
    return build
