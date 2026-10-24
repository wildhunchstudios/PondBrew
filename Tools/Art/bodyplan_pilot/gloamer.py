# GR-12B pilot: the Gloamer's eight as animals of their own. Each keeps the rig's names (Body/Puffed, the face parts, Lips
# with Happy and Surprised, Tail, Left and Right fin, Crest and Lantern pivots) and the family's signature, a glowing lure
# and lights.

def bp_belly(S, below=-.5, t_from=.06, t_to=.97):
    return lambda t, sn, cs: S('Belly') if sn < below and t_from < t < t_to else None

# ---------------------------------------------------------------- Moonmouth: an anglerfish
def gp_moonmouth():
    sid = 'moonmouth'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaMoonmouth')
    spine = BPSpine([(0, 0, -.40), (0, 0, -.12), (0, .03, .18), (0, .06, .40)])
    body = BPBody(spine, bp_profile([(0, .30, .26, .30), (.25, .37, .35, .35), (.5, .25, .24, .23), (.74, .085, .1, .085),
                                     (1, .05, .06, .05)], nose=.14, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.45, .1))
    # The jutting lower jaw: a heavy chin under the gape.
    ellipsoid(B, (0, -.17, -.345), (.2, .075, .075), S('Belly'), (-10, 0, 0), 28, 14)
    bp_spots(B, body, sid, [(.32, -12, .024), (.42, -14, .022), (.52, -16, .02), (.62, -18, .017), (.38, -40, .018), (.5, -42, .016)])
    for side in (1, -1):
        tube(B, [tuple(body.at(t, (-28 if side > 0 else -152) + 6 * (t - .05) / .2, 1.0)) for t in (.03, .08, .13, .18, .23)], .012, 'DK_Mouth', 10)
    bp_puffed(make_object(B, root), B, body, .14)
    eye = body.at(.13, 58, .84)
    bp_face(root, sid, tuple(eye), (.085, .1, .075), 30,
            mouth_gape((0, -.09, -.405), .2, .115, S('Belly'), sid,
                       upper=[(-.7, .04, .014), (-.35, .05, .016), (0, .05, .016), (.35, .05, .016), (.7, .04, .014)],
                       lower=[(-.8, .045, .015), (-.5, .06, .018), (-.15, .065, .019), (.15, .065, .019), (.5, .06, .018), (.8, .045, .015)],
                       tilt=16, rim_r=.032, depth=.016), eye_pitch=14)
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.27, -.04, .0), (.36, -.08, .05), (.42, -.12, .13), (.36, -.17, .15), (.29, -.13, .07)], 24)],
                 .03, S('Fin'))
    bp_fins(root, (.27, -.08, .03), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .07, .37), (0, .12, .44), (0, .1, .5), (0, .03, .52), (0, -.04, .5), (0, -.07, .44), (0, -.03, .37)], 30),
                 .03, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .05, .37), tail)
    def crest(part):
        a, b = body.at(.56, 90, .9), body.at(.72, 90, .9)
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .09, .03))), tuple(a + Vector((0, .11, .09))), tuple(b + Vector((0, .07, .02))), tuple(b)], 24),
                 .025, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.6, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.09, 90, .96), .3, .12, .085, .022, 150)
    return root

# ---------------------------------------------------------------- Gloomfin: an ocean sunfish
def gp_gloomfin():
    sid = 'gloomfin'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaGloomfin')
    spine = BPSpine([(0, 0, -.30), (0, 0, -.05), (0, 0, .24)])
    body = BPBody(spine, bp_profile([(0, .09, .2, .2), (.3, .14, .31, .31), (.65, .13, .3, .3), (1, .1, .25, .25)], nose=.18, tail=.1))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.55, .08))
    bp_spots(B, body, sid, [(.35, 10, .02), (.47, 25, .018), (.58, 5, .02), (.68, 30, .016), (.5, -20, .018), (.75, -5, .016)])
    bp_puffed(make_object(B, root), B, body, .12)
    eye = body.at(.13, 26, .86)
    bp_face(root, sid, tuple(eye), (.075, .088, .068), 52, mouth_o((0, -.045, -.302), .026, S('Belly')))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.11, .02, -.17), (.15, .04, -.14), (.18, .03, -.09), (.15, -.0, -.09), (.11, -.0, -.13)], 20)],
                 .022, S('Body'), rim=S('Fin'))
    bp_fins(root, (.11, .01, -.15), pec)
    def clavus(part):
        # The sunfish's tail: a frilly scalloped rim round its back.
        c = Vector((0, 0, .06))
        for k in range(9):
            th = math.radians(-64 + 128 * k / 8)
            p = c + Vector((0, .24 * math.sin(th), .2 * math.cos(th) + .02))
            ellipsoid(part, tuple(p), (.022, .062, .055), S('Fin'), (-math.degrees(th), 0, 0), 18, 10)
    simple_part(root, 'Tail pivot', (0, 0, .26), clavus)
    fin_outline = [(0, .26, -.0), (0, .42, .06), (0, .6, .16), (0, .68, .25), (0, .62, .3), (0, .44, .25), (0, .26, .22)]
    def crest(part):
        bp_plate(part, bp_outline(fin_outline, 32), .035, S('Fin'), rim=S('Accent'), rim_from=.8)
    simple_part(root, 'Crest pivot', (0, .3, .1), crest)
    def keel(part):
        bp_plate(part, bp_outline([(0, -y, z) for _, y, z in fin_outline][::-1], 32), .035, S('Fin'), rim=S('Accent'), rim_from=.8)
    simple_part(root, 'Keel pivot', (0, -.3, .1), keel)
    bp_lure(root, sid, body.at(.07, 90, .96), .16, .1, .05, .016, 140)
    return root

# ---------------------------------------------------------------- Shadeback: a coelacanth
def gp_shadeback():
    sid = 'shadeback'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaShadeback')
    spine = BPSpine([(0, 0, -.40), (0, .01, -.1), (0, 0, .2), (0, 0, .36)])
    body = BPBody(spine, bp_profile([(0, .15, .15, .16), (.25, .21, .23, .22), (.55, .19, .21, .18), (.82, .085, .1, .08),
                                     (1, .05, .06, .05)], nose=.2, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.55, .12, .9))
    bp_spots(B, body, sid, [(.3, 20, .03), (.38, 46, .022), (.45, 4, .028), (.52, 34, .025), (.6, -12, .024), (.66, 22, .02),
                            (.72, 48, .018), (.4, -28, .02), (.78, 6, .016)])
    # Fleshy lobed fins: the pelvic pair, the second dorsal and the anal, each a lobe and a little fan.
    def lobed(part, base, out, length, rad, fan):
        base, out = Vector(base), Vector(out).normalized()
        tip = base + out * length
        cone(part, tuple(base - out * .015), tuple(tip), rad, S('Body'), 14, 6, .35)
        back = Vector((0, 0, 1))
        a = (back - out * back.dot(out)).normalized()
        pts = [tip - out * .01 + a * -.012, tip + out * fan * .55 + a * -.02, tip + out * fan + a * .02, tip + out * fan * .8 + a * fan * .55,
               tip + out * fan * .25 + a * fan * .55, tip + a * .03]
        bp_plate(part, bp_outline([tuple(p) for p in pts], 26), .016, S('Fin'))
    lobed(B, body.at(.74, 90, .92), (0, 1, .5), .05, .022, .08)
    lobed(B, body.at(.74, -90, .92), (0, -1, .5), .05, .022, .08)
    for side in (1, -1):
        lobed(B, body.at(.56, -62 if side > 0 else -118, .92), (side * .6, -.8, .55), .045, .02, .075)
    bp_puffed(make_object(B, root), B, body, .14)
    eye = body.at(.16, 22, .84)
    bp_face(root, sid, tuple(eye), (.08, .09, .07), 44, mouth_grin(body, .035, .14, -22, .011), eye_pitch=4)
    def pec(part, m):
        cone(part, m((.18, -.08, -.1)), m((.26, -.13, -.03)), .03, S('Body'), 14, 6, .35)
        bp_plate(part, [m(p) for p in bp_outline([(.25, -.12, -.04), (.31, -.15, .0), (.35, -.2, .07), (.3, -.2, .1), (.25, -.15, .02)], 24)],
                 .018, S('Fin'))
    bp_fins(root, (.2, -.09, -.08), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .04, .34), (0, .15, .4), (0, .18, .5), (0, .07, .5), (0, .02, .43)], 24), .022, S('Fin'))
        bp_plate(part, bp_outline([(0, -.04, .34), (0, -.02, .43), (0, -.07, .5), (0, -.18, .5), (0, -.15, .4)], 24), .022, S('Fin'))
        ellipsoid(part, (0, 0, .46), (.024, .032, .085), S('Body'), (0, 0, 0), 14, 8)
        ellipsoid(part, (0, 0, .555), (.012, .05, .04), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, 0, .33), tail)
    def crest(part):
        a, b = body.at(.34, 90, .9), body.at(.46, 90, .9)
        top = [tuple(a), tuple(a + Vector((0, .1, .0))), tuple(a + Vector((0, .15, .05))), tuple(b + Vector((0, .13, .03))),
               tuple(b + Vector((0, .05, .03))), tuple(b)]
        bp_plate(part, bp_outline(top, 28), .022, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.4, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.14, 90, .96), .2, .15, .055, .018, 120)
    return root

# ---------------------------------------------------------------- Hushray: a stingray
def gp_hushray():
    sid = 'hushray'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaHushray')
    spine = BPSpine([(0, 0, -.30), (0, .01, 0), (0, .02, .22)])
    body = BPBody(spine, bp_profile([(0, .14, .06, .035), (.3, .22, .095, .045), (.7, .17, .08, .04), (1, .07, .045, .03)], nose=.25, tail=.12))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=lambda t, sn, cs: S('Belly') if sn < -.2 else None)
    for side in (1, -1):
        ellipsoid(B, (side * .075, .07, -.14), (.06, .045, .06), S('Body'), (0, 0, 0), 18, 10)  # eye mounds
        ellipsoid(B, (side * .085, .085, -.06), (.022, .008, .016), 'DK_Mouth', (0, 0, 0), 12, 6)  # spiracles
    bp_spots(B, body, sid, [(.5, 90, .016), (.62, 90, .014), (.74, 90, .012)])
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, (.075, .1, -.15), (.06, .07, .055), 18,
            mouth_arc((0, -.022, -.285), .1, .02, .009, .05), eye_pitch=42, pupil_k=.62)
    def wing(part, m):
        out = bp_outline([(.12, .01, -.25), (.28, .005, -.2), (.42, 0, -.08), (.46, 0, .02), (.4, 0, .12), (.26, .01, .17),
                          (.12, .02, .14), (.1, .03, -.06)], 40)
        bp_plate(part, [m(p) for p in out], .075, S('Fin'), rim=S('Accent'), rim_from=.84, normal=(0, 1, 0))
        for p in ((.24, .04, -.12), (.31, .035, -.05), (.36, .03, .03), (.27, .04, .03), (.2, .045, .07)):
            ellipsoid(part, m(p), (.015, .01, .015), S('Accent'), (0, 0, 0), 10, 6)
    bp_fins(root, (.16, .0, -.04), wing)
    def tail(part):
        taper_tube(part, [(0, .015, .2), (0, .025, .34), (.02, .035, .48), (-.01, .035, .62), (0, .03, .74)], .032, .007, S('Fin'), 12)
        cone(part, (0, .04, .33), (0, .07, .42), .012, S('Accent'), 10, 4, .1)
    simple_part(root, 'Tail pivot', (0, .01, .2), tail)
    def crest(part):
        bp_plate(part, bp_outline([(0, .05, .12), (0, .1, .17), (0, .09, .21), (0, .05, .2)], 16), .018, S('Fin'))
    simple_part(root, 'Crest pivot', (0, .06, .16), crest)
    bp_lure(root, sid, (0, .095, -.2), .15, .12, .045, .014, 130)
    return root

# ---------------------------------------------------------------- Duskdrifter: an oarfish
def gp_duskdrifter():
    sid = 'duskdrifter'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaDuskdrifter')
    spine = BPSpine([(0, 0, -.44), (.012, .005, -.22), (-.02, .01, .02), (.025, 0, .26), (-.02, -.01, .47), (.015, 0, .62)], 121)
    body = BPBody(spine, bp_profile([(0, .058, .11, .1), (.12, .066, .13, .115), (.5, .056, .115, .095), (.85, .036, .075, .058),
                                     (1, .016, .04, .03)], nose=.08, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), 0, 1.0, 80, 28, belly=bp_belly(S, -.55, .05))
    lo, hi, sd = [], [], []
    for k in range(48):
        t = .07 + .9 * k / 47
        c, s, up = spine.frame(t)
        base = body.at(t, 90, .9)
        h = .075 * (1 - .4 * t) * math.sin(math.pi / 2 * min(1, (t - .07) / .08)) ** .5
        lo.append(tuple(base)); hi.append(tuple(base + up * max(h, .006))); sd.append(tuple(s))
    bp_strip(B, lo, hi, .02, S('Fin'), rim=S('Accent'), side_dirs=sd)
    bp_spots(B, body, sid, [(t, -30, .013) for t in (.16, .24, .32, .4, .48, .56, .64, .72, .8)])
    bp_puffed(make_object(B, root), B, body, .1)
    def tail(part):
        # The oarfish has hardly a tail fin: a little tuft at the end of its ribbon.
        end, tan = spine.frame(1.0)[0], spine.tangent(.98)
        bp_plate(part, bp_outline([tuple(end - tan * .03 + Vector((0, .03, 0))), tuple(end + tan * .05 + Vector((0, .045, 0))),
                                   tuple(end + tan * .07), tuple(end + tan * .05 - Vector((0, .04, 0))), tuple(end - tan * .03 - Vector((0, .025, 0)))], 20),
                 .016, S('Fin'), rim=S('Accent'))
    simple_part(root, 'Tail pivot', tuple(spine.frame(.97)[0]), tail)
    eye = body.at(.065, 18, .82)
    bp_face(root, sid, tuple(eye), (.05, .06, .046), 56, mouth_arc((0, -.03, -.437), .03, .01, .007, .015), eye_pitch=4)
    def oars(part, m):
        pts = [(.03, -.11, -.32), (.06, -.17, -.22), (.08, -.2, -.06), (.09, -.21, .1)]
        taper_tube(part, [m(p) for p in pts], .009, .006, S('Fin'), 10)
        ellipsoid(part, m((.09, -.212, .14)), (.014, .03, .05), S('Accent'), (0, 0, 0), 14, 8)
    bp_fins(root, (.03, -.1, -.33), oars)
    def crest(part):
        for k, (t, lean, length) in enumerate(((.05, 22, .2), (.065, 34, .26), (.08, 46, .3), (.095, 56, .27), (.11, 66, .22), (.125, 74, .17))):
            base = body.at(t, 90, .9)
            pts = [tuple(base + rot((0, length * s, 0), (lean * s, 0, 0)) + Vector((0, 0, .02 * s * s))) for s in (0, .25, .5, .75, 1)]
            taper_tube(part, pts, .012, .007, S('Fin'), 10)
            ellipsoid(part, pts[-1], (.014, .014, .014), S('Accent'), (0, 0, 0), 10, 6)
    simple_part(root, 'Crest pivot', tuple(body.at(.08, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.04, 90, .96), .14, .1, .045, .014, 130, 'flame')
    return root

# ---------------------------------------------------------------- Murkmaw: a viperfish
def gp_murkmaw():
    sid = 'murkmaw'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaMurkmaw')
    spine = BPSpine([(0, 0, -.40), (0, .02, -.15), (0, 0, .15), (0, .02, .42)])
    body = BPBody(spine, bp_profile([(0, .1, .1, .15), (.18, .17, .18, .23), (.4, .15, .16, .15), (.75, .08, .09, .07),
                                     (1, .035, .045, .035)], nose=.14, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.55, .14))
    bp_spots(B, body, sid, [(t, -58, .016) for t in (.24, .32, .4, .48, .56, .64, .72)] + [(t, -78, .013) for t in (.28, .36, .44, .52, .6)])
    bp_puffed(make_object(B, root), B, body, .13)
    eye = body.at(.12, 38, .84)
    fang = (0, 1, -.5)
    bp_face(root, sid, tuple(eye), (.075, .086, .066), 38,
            mouth_grin(body, .03, .17, -14, .013, teeth=[(.4, .17, .02, fang), (.6, .17, .02, fang), (.27, .05, .011, True),
                                                         (.73, .05, .011, True), (.33, .045, .01, False), (.67, .045, .01, False)]))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.15, -.08, -.18), (.22, -.1, -.13), (.25, -.14, -.05), (.2, -.15, -.06), (.15, -.12, -.12)], 20)],
                 .02, S('Fin'))
    bp_fins(root, (.15, -.1, -.15), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .02, .39), (0, .1, .45), (0, .16, .56), (0, .1, .55), (0, .02, .47)], 20), .02, S('Fin'))
        bp_plate(part, bp_outline([(0, -.02, .39), (0, -.02, .47), (0, -.1, .55), (0, -.16, .56), (0, -.1, .45)], 20), .02, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .02, .39), tail)
    def crest(part):
        a, b = body.at(.62, 90, .9), body.at(.72, 90, .9)
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .07, .02))), tuple(b + Vector((0, .06, .03))), tuple(b)], 16), .02, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.66, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.3, 90, .96), .26, .24, .05, .012, 162)
    return root

# ---------------------------------------------------------------- Nightlantern: a vampire squid
def gp_nightlantern():
    sid = 'nightlantern'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaNightlantern')
    spine = BPSpine([(0, -.08, -.1), (0, .1, -.02), (0, .3, .12)], 61, up=(0, 0, -1))
    body = BPBody(spine, bp_profile([(0, .18, .18, .18), (.35, .23, .23, .23), (.75, .19, .19, .19), (1, .1, .1, .1)], nose=.16, tail=.22))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=lambda t, sn, cs: S('Belly') if sn < -.6 and t < .5 else None)
    for side in (1, -1):
        ellipsoid(B, (side * .2, .2, .08), (.028, .028, .028), S('Accent'), (0, 0, 0), 14, 8)  # lights at the fin bases
    bp_puffed(make_object(B, root), B, body, .12)
    eye = body.at(.32, 58, .82)
    bp_face(root, sid, tuple(eye), (.098, .11, .082), 26, mouth_arc((0, -.055, -.235), .045, .015, .009, .03), eye_pitch=-4)
    def ears(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.17, .24, .02), (.26, .3, .06), (.32, .3, .12), (.27, .24, .14), (.18, .2, .1)], 24)],
                 .03, S('Fin'))
    bp_fins(root, (.18, .23, .07), ears)
    def cloak(part):
        c = Vector((0, -.12, -.1))
        tips = []
        for k in range(8):
            phi = math.tau * (k + .5) / 8
            rad = Vector((math.sin(phi), 0, -math.cos(phi)))
            pts = [c + rad * .05, c + rad * .15 + Vector((0, -.08, 0)), c + rad * .24 + Vector((0, -.13, 0)),
                   c + rad * .3 + Vector((0, -.12, 0)), c + rad * .33 + Vector((0, -.08, 0))]
            taper_tube(part, [tuple(p) for p in pts], .028, .011, S('Body'), 10)
            ellipsoid(part, tuple(pts[-1] + Vector((0, .006, 0))), (.022, .022, .022), S('Accent'), (0, 0, 0), 12, 8)
            for s in (.35, .6):  # soft spines (cirri) along each arm
                q = c + rad * (.05 + .25 * s) + Vector((0, -.1 * s - .02, 0))
                cone(part, tuple(q), tuple(q + Vector((0, -.04, 0)) + rad * .01), .008, S('Fin'), 8, 3, .1)
            tips.append([c + rad * .07, c + rad * .17 + Vector((0, -.09, 0)), c + rad * .25 + Vector((0, -.125, 0))])
        for k in range(8):
            a, b = tips[k], tips[(k + 1) % 8]
            web = [tuple(c + Vector((0, -.02, 0))), tuple(a[1]), tuple(a[2]), tuple((a[2] + b[2]) / 2 + Vector((0, .03, 0))), tuple(b[2]), tuple(b[1])]
            bp_plate(part, web, .014, S('Fin'))
    simple_part(root, 'Tail pivot', (0, -.12, -.1), cloak)
    def crest(part):
        top = body.at(.97, 90, .5)
        cone(part, tuple(top), tuple(top + Vector((0, .06, .05))), .05, S('Body'), 14, 5, .4)
    simple_part(root, 'Crest pivot', tuple(body.at(1.0, 90, 0)), crest)
    bp_lure(root, sid, body.at(.72, 90, .96), .12, .14, .05, .013, 125, 'fizz')
    return root

# ---------------------------------------------------------------- Eclipse Leviathan: a mosasaur, its lure an eclipse
def gp_eclipseleviathan():
    sid = 'eclipseleviathan'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaEclipseleviathan')
    spine = BPSpine([(0, .04, -.56), (0, .03, -.3), (0, 0, -.05), (0, 0, .2), (0, .03, .46)], 101)
    body = BPBody(spine, bp_profile([(0, .04, .03, .035), (.08, .075, .062, .062), (.19, .12, .115, .1), (.26, .11, .11, .105),
                                     (.42, .18, .18, .19), (.7, .125, .135, .12), (.9, .055, .07, .05), (1, .03, .04, .03)],
                                    nose=.05, tail=.03))
    B = Part('Body')
    bp_loft(B, body, S('Body'), rings=60, belly=bp_belly(S, -.5, .03))
    for side in (1, -1):  # brow ridges over the eyes
        p = body.at(.17, 52 if side > 0 else 128, 1.0)
        ellipsoid(B, tuple(p), (.05, .022, .065), S('Body'), (0, 0, 0), 16, 8)
    def rear(m):
        return [m(p) for p in bp_outline([(.12, -.12, .16), (.2, -.17, .2), (.26, -.21, .26), (.22, -.2, .29), (.13, -.13, .25)], 22)]
    for side in (1, -1):
        bp_plate(B, rear(lambda p, side=side: (side * p[0], p[1], p[2])), .03, S('Fin'), rim=S('Accent'), rim_from=.82)
    bp_spots(B, body, sid, [(t, -22, .022) for t in (.36, .45, .54, .63, .72)] + [(t, -46, .017) for t in (.4, .5, .6, .68)])
    bp_puffed(make_object(B, root), B, body, .12)
    eye = body.at(.165, 40, .84)
    down, up_ = (0, -1, -.15), (0, 1, -.15)
    teeth = [(w, .026, .009, down) for w in (.12, .2, .28, .36, .64, .72, .8, .88)] + [(w, .022, .008, up_) for w in (.16, .24, .32, .68, .76, .84)]
    bp_face(root, sid, tuple(eye), (.068, .078, .06), 36, mouth_grin(body, .012, .2, -6, .009, teeth=teeth), eye_pitch=8)
    def flipper(part, m):
        out = bp_outline([(.13, -.08, -.18), (.24, -.14, -.15), (.34, -.21, -.06), (.37, -.23, .03), (.3, -.19, .05), (.14, -.1, -.06)], 28)
        bp_plate(part, [m(p) for p in out], .035, S('Fin'), rim=S('Accent'), rim_from=.82)
    bp_fins(root, (.14, -.09, -.13), flipper)
    def tail(part):
        bp_plate(part, bp_outline([(0, .02, .4), (0, -.06, .47), (0, -.18, .56), (0, -.2, .62), (0, -.09, .58), (0, .02, .5)], 26),
                 .03, S('Fin'), rim=S('Accent'), rim_from=.82)
        bp_plate(part, bp_outline([(0, .05, .43), (0, .1, .5), (0, .12, .56), (0, .06, .53), (0, .03, .48)], 18), .025, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .03, .42), tail)
    def crown(part):
        top = body.at(.21, 90, 1.0)
        gold_crown(part, (0, top.y + .004, top.z), .075, 5, .07, 'DK_Gold', -6)
        for t in (.34, .42, .5, .58, .66):  # a low ridge of scutes down the back
            p = body.at(t, 90, .96)
            cone(part, tuple(p), tuple(p + Vector((0, .045, .02))), .022, S('Fin'), 10, 4, .2)
    simple_part(root, 'Crest pivot', tuple(body.at(.3, 90, 1.0)), crown)
    base = body.at(.1, 90, .96)
    lantern = Part('Lantern')
    pts = [tuple(base + Vector((0, .16 * math.sin(math.radians(a)), -.16 * (1 - math.cos(math.radians(a)))))) for a in range(0, 121, 10)]
    tube(lantern, pts, .018, S('Body'), 10)
    head = Vector(pts[-1]) + Vector((0, .0, -.07))
    ellipsoid(lantern, tuple(head), (.064, .066, .064), S('Fin'), (0, 0, 0), 24, 14)
    torus(lantern, head + Vector((0, 0, .01)), .084, .016, S('Accent'), (1, 1, 1), 36, 10)
    for k in range(8):
        a = math.tau * k / 8
        d = Vector((math.cos(a), math.sin(a), 0))
        cone(lantern, tuple(head + d * .095), tuple(head + d * .12), .013, S('Accent'), 8, 3, .1)
    make_object(lantern, root, tuple(base), 'Lantern pivot')
    return root

PILOT = [('moonmouth', 'DioramaMoonmouth', gp_moonmouth), ('gloomfin', 'DioramaGloomfin', gp_gloomfin),
         ('shadeback', 'DioramaShadeback', gp_shadeback), ('hushray', 'DioramaHushray', gp_hushray),
         ('duskdrifter', 'DioramaDuskdrifter', gp_duskdrifter), ('murkmaw', 'DioramaMurkmaw', gp_murkmaw),
         ('nightlantern', 'DioramaNightlantern', gp_nightlantern), ('eclipseleviathan', 'DioramaEclipseleviathan', gp_eclipseleviathan)]
