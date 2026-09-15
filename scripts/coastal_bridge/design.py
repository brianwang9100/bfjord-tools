"""Pure, bounded design math shared by the asset generator and input checks."""
from pathlib import Path
import hashlib
import json
import math
import re

VERSION = 'bridge-families-2'
DESIGN_TYPES = ('coastalArch', 'stoneViaduct', 'steelThroughTruss', 'timberTrestle')
DIMENSIONS = ('totalLength', 'archSpan', 'archRise', 'deckWidth', 'deckThickness',
              'archRibWidth', 'archRibDepth', 'spandrelSpacing', 'parapetHeight', 'abutmentLength')


def validate(recipe):
    recipe = dict(recipe)
    recipe.setdefault('designType', 'coastalArch')
    if recipe['designType'] not in DESIGN_TYPES:
        raise ValueError('Unknown structural designType')
    if recipe.get('schemaVersion') != 1 or recipe.get('railingStyle') != 'openConcrete':
        raise ValueError('Only schema 1, straight level openConcrete designs are supported')
    for key in ('id', 'materialsRevision'):
        if not isinstance(recipe.get(key), str) or not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,63}', recipe[key]):
            raise ValueError('Invalid stable identity: ' + key)
    if type(recipe.get('seed')) is not int or not 0 <= recipe['seed'] <= 2147483647:
        raise ValueError('Seed must be a bounded nonnegative integer')
    for key in DIMENSIONS:
        value = recipe.get(key)
        if type(value) not in (int, float) or not math.isfinite(value) or value <= 0:
            raise ValueError('Finite positive metres required: ' + key)
    r = recipe
    if not 40 <= r['totalLength'] <= 260 or not 20 <= r['archSpan'] <= 200:
        raise ValueError('Bridge size is outside the authored family')
    if r['archSpan'] + 2 * r['abutmentLength'] >= r['totalLength']:
        raise ValueError('The main arch and two abutments require distinct approach spans')
    if not 5 <= r['deckWidth'] <= 12 or not .3 <= r['deckThickness'] <= 1.2:
        raise ValueError('Deck section is outside the authored family')
    if not 4 <= r['archRise'] <= r['archSpan'] * .6:
        raise ValueError('Arch rise is outside the authored family')
    if not .4 <= r['archRibWidth'] < r['deckWidth'] * .2 or not .6 <= r['archRibDepth'] <= 3:
        raise ValueError('Invalid arch rib section')
    if not 4 <= r['spandrelSpacing'] <= r['archSpan'] / 3:
        raise ValueError('Spandrel spacing must leave several supported deck bays')
    if not .85 <= r['parapetHeight'] <= 1.5 or not 2 <= r['abutmentLength'] <= 10:
        raise ValueError('Parapet or abutment dimensions are outside the authored family')
    if r['designType'] != 'coastalArch':
        defaults = {'stoneViaduct': (5, 19, 8), 'steelThroughTruss': (8, 9, 8),
                    'timberTrestle': (12, 16, 8)}[r['designType']]
        for key, default in zip(('bayCount', 'pierHeight', 'trussHeight'), defaults):
            r.setdefault(key, default)
        if type(r['bayCount']) is not int or not 3 <= r['bayCount'] <= 24:
            raise ValueError('bayCount must be an integer from 3 to 24')
        for key, low, high in (('pierHeight', 6, 45), ('trussHeight', 5.5, 16)):
            value = r[key]
            if type(value) not in (float, int) or not math.isfinite(value) or not low <= value <= high:
                raise ValueError('Invalid structural dimension: ' + key)
        bay = (r['totalLength'] - 2 * r['abutmentLength']) / r['bayCount']
        limits = {'stoneViaduct': (8, 26), 'steelThroughTruss': (4, 14), 'timberTrestle': (3, 8)}[r['designType']]
        if not limits[0] <= bay <= limits[1]:
            raise ValueError('Bay spacing outside this structural family')
        if r['designType'] == 'stoneViaduct' and r['pierHeight'] < bay * .5 + 3:
            raise ValueError('Stone arch opening requires taller piers')
    return recipe


def load(path):
    return validate(json.loads(Path(path).read_text()))


def arch_top(recipe, station):
    t = station / (recipe['archSpan'] * .5)
    return -recipe['deckThickness'] - 1 - recipe['archRise'] * t * t


def arch_depth(recipe, station):
    t = abs(station) / (recipe['archSpan'] * .5)
    return recipe['archRibDepth'] * (1 + .85 * t * t)


def spandrel_stations(recipe):
    half = recipe['archSpan'] * .5
    count = max(2, math.ceil(half / recipe['spandrelSpacing']))
    spacing = half / count
    return [i * spacing for i in range(-count + 1, count)]


def recipe_hash(recipe, generator_source):
    encoded = json.dumps(recipe, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()
    return hashlib.sha256(encoded + b'\n' + VERSION.encode() + b'\n' + generator_source).hexdigest()
