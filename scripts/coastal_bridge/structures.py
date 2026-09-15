"""Original structural bridge families; geometry uses semantic physical materials.

These are art-directed static assets, not civil-engineering calculations. Public
heritage references establish structural vocabulary; no third-party mesh is copied.
"""
import math
from mathutils import Vector


def framed_beam(geometry, a, b, width, depth, flange=.055, mat='BridgeMetal', detailed=True):
    """Fabricated I-section; retain its overall section at distant LOD."""
    if not detailed:
        geometry.beam(a, b, width, depth, mat)
        return
    tangent = (Vector(b) - Vector(a)).normalized()
    ref = Vector((1, 0, 0)) if abs(tangent.x) < .8 else Vector((0, 0, 1))
    u = (ref - tangent * tangent.dot(ref)).normalized()
    v = tangent.cross(u).normalized()
    geometry.beam(a, b, flange, depth - 2 * flange, mat)
    for sign in (-1, 1):
        offset = v * sign * (depth - flange) / 2
        geometry.beam(Vector(a) + offset, Vector(b) + offset, width, flange, mat)


def arch_sector(geometry, center, x0, x1, radius0, radius1, a0, a1, mat):
    points = []
    for x in (x0, x1):
        for angle, radius in ((a0, radius0), (a1, radius0), (a1, radius1), (a0, radius1)):
            points.append((x, center[0] + math.sin(angle) * radius, center[1] + math.cos(angle) * radius))
    geometry.polyhedron(points, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                                 (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], mat)


def stone_viaduct(r, lod, parts):
    body, detail = parts['Structure'], parts['Details']
    length, width, bays = r['totalLength'], r['deckWidth'], r['bayCount']
    start = -length / 2 + r['abutmentLength']
    step = (length - 2 * r['abutmentLength']) / bays
    pier = max(1.6, step * .15)
    radius = (step - pier) / 2
    spring = -r['deckThickness'] - 1.35 - radius
    base = -r['pierHeight']
    segments = (32, 20, 12)[lod]
    for i in range(bays):
        center = start + (i + .5) * step
        # Each closed prism fills the spandrel above the curved barrel intrados.
        for k in range(segments):
            a0, a1 = math.pi * k / segments, math.pi * (k + 1) / segments
            z0, z1 = center + math.cos(a0) * radius, center + math.cos(a1) * radius
            y0, y1 = spring + math.sin(a0) * radius, spring + math.sin(a1) * radius
            top = -r['deckThickness']
            vertices = [(x, y, z) for x in (-width / 2, width / 2)
                        for y, z in ((y0, z0), (y1, z1), (top, z1), (top, z0))]
            body.polyhedron(vertices, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                                      (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], 'BridgeStone')
        if lod < 2:
            # Proud individual voussoirs form an actual radial arch ring at each face.
            blocks = 25 if lod == 0 else 17
            for sign in (-1, 1):
                x = sign * width / 2
                for k in range(blocks):
                    arch_sector(detail, (spring, center), min(x, x + sign * .12), max(x, x + sign * .12),
                                radius, radius + .62, k * math.pi / blocks + .0015,
                                (k + 1) * math.pi / blocks - .0015, 'BridgeStone')
    for i in range(bays + 1):
        z = start + i * step
        body.box(-width / 2, width / 2, base + .75, -r['deckThickness'], z - pier / 2, z + pier / 2, 'BridgeStone')
        body.box(-width / 2 - .4, width / 2 + .4, base, base + .8, z - pier / 2 - .45, z + pier / 2 + .45, 'BridgeStone')
        if lod < 2:
            # Banded pier courses and projecting impost stones articulate each support.
            detail.box(-width / 2 - .17, width / 2 + .17, spring - .2, spring + .15,
                       z - pier / 2 - .14, z + pier / 2 + .14, 'BridgeStone')
            for y in range(math.ceil(base + 1), math.floor(spring)):
                for sign in (-1, 1):
                    x = sign * (width / 2 + .018)
                    detail.box(x - .024, x + .024, y, y + .82, z - pier / 2 + .018, z + pier / 2 - .018, 'BridgeStone')
    for sign in (-1, 1):
        end, inner = sign * length / 2, sign * (length / 2 - r['abutmentLength'])
        body.box(-width / 2, width / 2, base, -r['deckThickness'], min(end, inner), max(end, inner), 'BridgeStone')
        x = sign * (width / 2 - .17)
        parts['Parapets'].box(x - .2, x + .2, .12, r['parapetHeight'], -length / 2, length / 2, 'BridgeStone')
        parts['Parapets'].box(x - .27, x + .27, r['parapetHeight'], r['parapetHeight'] + .10,
                              -length / 2, length / 2, 'BridgeStone')
        detail.box(sign * width / 2 - .13, sign * width / 2 + .13, -.4, -.15,
                   -length / 2, length / 2, 'BridgeStone')


def steel_through_truss(r, lod, parts):
    steel, detail, foundations = parts['Structure'], parts['Details'], parts['Supports']
    length, width, count, high = r['totalLength'], r['deckWidth'], r['bayCount'], r['trussHeight']
    end = length / 2 - r['abutmentLength']
    step = 2 * end / count
    low, x = -r['deckThickness'] - .35, width / 2 + .35
    nodes = [-end + i * step for i in range(count + 1)]
    def member(a, b, w=.32, d=.4):
        framed_beam(steel, a, b, w, d, detailed=lod < 2)
    for side in (-1, 1):
        xx = side * x
        # Sloping end posts and long top/bottom chords create a Pratt through-truss silhouette.
        member((xx, low, -end), (xx, high, nodes[1]), .48, .60)
        member((xx, high, nodes[-2]), (xx, low, end), .48, .60)
        member((xx, high, nodes[1]), (xx, high, nodes[-2]), .48, .60)
        member((xx, low, -end), (xx, low, end), .45, .55)
        for i, z in enumerate(nodes[1:-1], 1):
            member((xx, low, z), (xx, high, z), .32, .42)
            if i < count - 1:
                a, b = ((xx, high, z), (xx, low, nodes[i + 1])) if i < count / 2 else ((xx, low, z), (xx, high, nodes[i + 1]))
                member(a, b, .25, .3)
            if lod < 2:
                for y in (low, high):
                    detail.box(xx - .28, xx + .28, y - .35, y + .35, z - .38, z + .38, 'BridgeMetal')
                    if lod == 0:
                        for dz in (-.25, .25):
                            for dy in (-.23, .23):
                                detail.box(xx - .30, xx + .30, y + dy - .035, y + dy + .035,
                                           z + dz - .035, z + dz + .035, 'BridgeMetal')
    for i, z in enumerate(nodes):
        member((-x, low, z), (x, low, z), .34, .60)
        if 0 < i < count:
            member((-x, high, z), (x, high, z), .3, .38)
            if lod < 2 and i < count - 1:
                member((-x, high, z), (x, high, nodes[i + 1]), .12, .14)
                member((x, high, z), (-x, high, nodes[i + 1]), .12, .14)
        if lod < 2 and i < count:
            member((-x, low - .25, z), (x, low - .25, nodes[i + 1]), .13, .15)
        if i in (1, count - 1):
            # Portal knee braces stay above the usable riding envelope.
            for side in (-1, 1):
                member((side * x, high - 1.7, z), (side * (x - 1.7), high, z), .19, .23)
    for side in (-1, 1):
        z = side * end
        foundations.box(-x - .55, x + .55, -r['pierHeight'], low - .2, z - 1.3, z + 1.3)
        foundations.box(-x - .8, x + .8, -r['pierHeight'] - .7, -r['pierHeight'], z - 1.65, z + 1.65)
        for xx in (-x, x):
            detail.box(xx - .48, xx + .48, low - .3, low - .1, z - .5, z + .5, 'BridgeMetal')
    for xx in (-width * .3, 0, width * .3):
        member((xx, low, -length / 2), (xx, low, length / 2), .26, .46)
    railing(r, lod, parts, 'BridgeMetal')


def timber_trestle(r, lod, parts):
    timber, detail, supports = parts['Structure'], parts['Details'], parts['Supports']
    length, width, count = r['totalLength'], r['deckWidth'], r['bayCount']
    end = length / 2 - r['abutmentLength']; step = end * 2 / count
    top = -r['deckThickness'] - .6; base = -r['pierHeight']
    stations = [-end + i * step for i in range(count + 1)]
    for i, z in enumerate(stations):
        timber.box(-width / 2 - .25, width / 2 + .25, top - .45, top + .05, z - .3, z + .3, 'BridgeTimber')
        timber.box(-width / 2 - 1.05, width / 2 + 1.05, base + .3, base + .7, z - .3, z + .3, 'BridgeTimber')
        supports.box(-width / 2 - 1.3, width / 2 + 1.3, base - .4, base + .3, z - .9, z + .9)
        for fraction in (-1, -1 / 3, 1 / 3, 1):
            upper = fraction * (width / 2 - .3); lower = fraction * (width / 2 + .7)
            timber.beam((lower, base + .5, z), (upper, top, z), .36, .40, 'BridgeTimber')
            if lod == 0:
                for y, xx in ((top - .4, upper), (base + .7, lower)):
                    detail.box(xx - .21, xx + .21, y - .19, y + .19, z - .225, z - .205, 'BridgeMetal')
                    for dx in (-.12, .12):
                        detail.box(xx + dx - .035, xx + dx + .035, y - .035, y + .035, z - .25, z - .22, 'BridgeMetal')
        levels = max(2, math.ceil((top - base) / 6))
        for j in range(levels):
            y0 = base + .7 + j * (top - base - .8) / levels
            y1 = base + .7 + (j + 1) * (top - base - .8) / levels
            x0 = width / 2 + .7 - (y0 - base) / (top - base)
            x1 = width / 2 + .7 - (y1 - base) / (top - base)
            timber.beam((-x0, y0, z - .26), (x1, y1, z - .26), .21, .23, 'BridgeTimber')
            timber.beam((x0, y0, z + .26), (-x1, y1, z + .26), .21, .23, 'BridgeTimber')
            if j > 0:
                timber.box(-x0, x0, y0 - .14, y0 + .14, z - .26, z + .26, 'BridgeTimber')
        if i < count:
            for side in (-1, 1):
                xx = side * (width / 2 - .28)
                timber.beam((xx, top - .5, z), (xx, base + .8, z + step), .23, .25, 'BridgeTimber')
                if lod < 2:
                    timber.beam((xx, base + .8, z), (xx, top - .5, z + step), .23, .25, 'BridgeTimber')
    for xx in (-width * .38, -width * .13, width * .13, width * .38):
        timber.box(xx - .18, xx + .18, top, -r['deckThickness'], -length / 2, length / 2, 'BridgeTimber')
    if lod < 2:
        for i in range(math.ceil(length / .65)):
            z = -length / 2 + i * .65
            timber.box(-width / 2, width / 2, -r['deckThickness'], -r['deckThickness'] + .2,
                       z, min(z + .61, length / 2), 'BridgeTimber')
    railing(r, lod, parts, 'BridgeTimber')


def railing(r, lod, parts, mat):
    length, width, h = r['totalLength'], r['deckWidth'], r['parapetHeight']
    rails, details = parts['Parapets'], parts['Details']
    for side in (-1, 1):
        x = side * (width / 2 - .18)
        for y in (.52, h - .07):
            rails.box(x - .10, x + .10, y - .09, y + .09, -length / 2, length / 2, mat)
        bays = math.ceil(length / 2.5)
        for i in range(bays + 1):
            z = -length / 2 + i * length / bays
            rails.box(x - .11, x + .11, .15, h + .035,
                      max(-length / 2, z - .11), min(length / 2, z + .11), mat)
            if lod == 0:
                for y in (.52, h - .07):
                    details.box(x - .135, x + .135, y - .03, y + .03,
                                max(-length / 2, z - .032), min(length / 2, z + .032), 'BridgeMetal')


BUILDERS = {'stoneViaduct': stone_viaduct, 'steelThroughTruss': steel_through_truss, 'timberTrestle': timber_trestle}


def build_lod(r, lod, Geometry, bpy):
    collection = bpy.data.collections.new('Editable_LOD' + str(lod))
    bpy.context.scene.collection.children.link(collection)
    parts = {name: Geometry() for name in ('Deck', 'Structure', 'Supports', 'Parapets', 'Details', 'RoadPaint')}
    width, length = r['deckWidth'], r['totalLength']
    parts['Deck'].box(-width / 2, width / 2, -r['deckThickness'], -.045, -length / 2, length / 2,
                      'BridgeTimber' if r['designType'] == 'timberTrestle' else 'BridgeConcrete')
    parts['Deck'].box(-width / 2 + .5, width / 2 - .5, -.045, 0, -length / 2, length / 2, 'BridgeAsphalt')
    for side in (-1, 1):
        x = side * (width / 2 - .25)
        parts['Deck'].box(x - .25, x + .25, 0, .18, -length / 2, length / 2,
                          'BridgeTimber' if r['designType'] == 'timberTrestle' else 'BridgeStone' if r['designType'] == 'stoneViaduct' else 'BridgeConcrete')
        for x, w, mat in ((side * .09, .045, 'BridgeRoadPaintAmber'),
                          (side * (width / 2 - .7), .05, 'BridgeRoadPaintWhite')):
            parts['RoadPaint'].box(x - w, x + w, .001, .004, -length / 2, length / 2, mat)
    BUILDERS[r['designType']](r, lod, parts)
    if lod < 2:
        for z in (-length / 2 + r['abutmentLength'], length / 2 - r['abutmentLength']):
            parts['Details'].box(-width / 2 + .5, width / 2 - .5, .006, .012, z - .025, z + .025, 'BridgeMetal')
    objects = []
    for name, geometry in parts.items():
        radius = 0 if name in ('RoadPaint', 'Details') else .008 if r['designType'] == 'steelThroughTruss' else .018
        obj = geometry.object('Bridge' + name + '_LOD' + str(lod), collection, radius if lod == 0 else 0)
        if obj:
            objects.append(obj)
    return collection, objects
