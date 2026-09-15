"""Focused design-contract checks; no Blender or generated asset dependencies."""
import importlib.util
import math
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent


class BridgeDesignTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue((HERE / 'design.py').is_file(), 'Bridge design validation is not implemented')
        import design
        self.design = design

    def test_both_recipes_keep_deck_endpoints_and_usable_arch(self):
        for name in ('coastal-arch-180', 'coastal-arch-104'):
            recipe = self.design.load(HERE / 'recipes' / (name + '.json'))
            self.assertEqual(recipe['schemaVersion'], 1)
            self.assertGreater(recipe['totalLength'], recipe['archSpan'] + 2 * recipe['abutmentLength'])
            crown = self.design.arch_top(recipe, 0)
            spring = self.design.arch_top(recipe, recipe['archSpan'] / 2)
            self.assertAlmostEqual(crown - spring, recipe['archRise'])
            self.assertLess(crown, -recipe['deckThickness'])
            stations = self.design.spandrel_stations(recipe)
            self.assertEqual(stations, sorted(stations))
            self.assertTrue(all(abs(s) < recipe['archSpan'] / 2 for s in stations))
            self.assertGreater(len(stations), 3)

    def test_structure_defaults_and_invalid_choices(self):
        import json
        valid = json.loads((HERE / 'recipes/coastal-arch-180.json').read_text())
        self.assertEqual(self.design.validate(valid).get('designType'), 'coastalArch')
        for change in (dict(designType='suspension'), dict(designType='stoneViaduct', bayCount=0),
                       dict(designType='timberTrestle', pierHeight=math.nan),
                       dict(designType='steelThroughTruss', trussHeight=3)):
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.design.validate(dict(valid, **change))

    def test_authored_family_recipes_keep_usable_bay_spacing(self):
        for name, expected, limits in (
                ('stone-viaduct-110', 'stoneViaduct', (8, 26)),
                ('steel-through-truss-88', 'steelThroughTruss', (4, 14)),
                ('timber-trestle-80', 'timberTrestle', (3, 8))):
            recipe = self.design.load(HERE / 'recipes' / (name + '.json'))
            self.assertEqual(recipe['designType'], expected)
            bay = (recipe['totalLength'] - 2 * recipe['abutmentLength']) / recipe['bayCount']
            self.assertGreaterEqual(bay, limits[0])
            self.assertLessEqual(bay, limits[1])
            self.assertGreaterEqual(recipe['pierHeight'], 6)
            self.assertGreater(recipe['deckWidth'] - 1, 4)

    def test_bad_inputs_fail_before_generating_geometry(self):
        import json
        valid = json.loads((HERE / 'recipes/coastal-arch-180.json').read_text())
        changes = [dict(archSpan=190), dict(deckWidth=-8), dict(archRise=math.nan),
                   dict(archRibWidth=4), dict(railingStyle='solid'), dict(totalLength=0),
                   dict(id='../outside'), dict(deckThickness=math.inf)]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.design.validate(dict(valid, **change))


if __name__ == '__main__':
    unittest.main()
