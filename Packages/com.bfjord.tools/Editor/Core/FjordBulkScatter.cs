using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bwork.FjordCoast.Editor
{
    /// <summary>Pure deterministic foliage planning. The caller owns terrain queries, assets, and scene changes.</summary>
    public static class FjordBulkScatter
    {
        public sealed class Species
        {
            public string id, prefabKey;
            public float weight, radius, minimumScale, maximumScale;
            public float groundOffsetMeters;
        }

        public sealed class Recipe
        {
            public string id;
            public Rect area;
            public int seed, maximumCount;
            public float densityPerHectare, minimumSpacing;
            public float minimumHeight, maximumHeight, minimumSlopeDegrees, maximumSlopeDegrees;
            public Species[] species;
            public int clusterCount;
            public float clusterRadius;
        }

        public readonly struct Placement
        {
            public readonly string speciesId, prefabKey;
            public readonly Vector3 position;
            public readonly float rotationDegrees, scale, supportRadius;

            public Placement(Species species, Vector3 position, float rotation, float scale)
            {
                speciesId = species.id;
                prefabKey = species.prefabKey;
                this.position = position;
                rotationDegrees = rotation;
                this.scale = scale;
                supportRadius = species.radius * scale;
            }
        }

        public sealed class Result
        {
            public string recipeId;
            public int targetCount, considered;
            public int rejectedGround, rejectedHeight, rejectedSlope, rejectedExclusion, rejectedSpacing;
            public Placement[] placements = Array.Empty<Placement>();
        }

        /// <param name="ground">Returns world Y for an XZ coordinate, or null when there is no owned surface.</param>
        /// <param name="normal">Returns the world-space surface normal at a supported point.</param>
        /// <param name="excludes">Returns true when the full circular footprint is reserved.</param>
        public static Result Plan(
            Recipe recipe,
            Func<Vector2, float?> ground,
            Func<Vector3, Vector3> normal,
            Func<Vector3, float, bool> excludes,
            IReadOnlyList<Placement> neighbors = null,
            Func<Species, Vector3, float, Vector3?> fitSupport = null)
        {
            Validate(recipe, ground, normal, excludes);
            if (neighbors != null && (neighbors.Count > 100000 || neighbors.Any(p => !Finite(p.position) || !Finite(p.supportRadius) || p.supportRadius <= 0 || p.supportRadius > 1000)))
                throw new ArgumentException("Neighbor footprints must be finite, positive and bounded.");
            double desired = (double)recipe.area.width * recipe.area.height * recipe.densityPerHectare / 10000d;
            int target = desired >= recipe.maximumCount
                ? recipe.maximumCount : Mathf.RoundToInt((float)desired);
            var result = new Result { recipeId = recipe.id, targetCount = target };
            if (target == 0) return result;

            float totalWeight = recipe.species.Sum(value => value.weight);
            float maximumRadius = Mathf.Max(recipe.species.Max(value => value.radius * value.maximumScale),
                neighbors == null || neighbors.Count == 0 ? 0 : neighbors.Max(p => p.supportRadius));
            float maximumInteraction = Mathf.Max(recipe.minimumSpacing, maximumRadius * 2);
            float cellSize = maximumInteraction / 1.414214f;
            var grid = new Dictionary<Vector2Int, List<int>>();
            var placements = new List<Placement>(target);
            var blockers = neighbors == null ? new List<Placement>() : new List<Placement>(neighbors);
            for (int i = 0; i < blockers.Count; i++)
            {
                var key = Cell(blockers[i].position, cellSize);
                if (!grid.TryGetValue(key, out var bucket)) grid.Add(key, bucket = new List<int>());
                bucket.Add(i);
            }
            var random = new RandomSequence(recipe.seed, recipe.id);
            var clusterRandom = new RandomSequence(recipe.seed, recipe.id + "-clusters");
            var centers = new Vector2[recipe.clusterCount];
            for (int i = 0; i < centers.Length; i++) centers[i] = new Vector2(
                Mathf.Lerp(recipe.area.xMin, recipe.area.xMax, clusterRandom.NextFloat()),
                Mathf.Lerp(recipe.area.yMin, recipe.area.yMax, clusterRandom.NextFloat()));
            int attemptLimit = Math.Min(100000, Math.Max(256, target * 64));

            while (result.considered < attemptLimit && placements.Count < target)
            {
                result.considered++;
                var xz = new Vector2(
                    Mathf.Lerp(recipe.area.xMin, recipe.area.xMax, random.NextFloat()),
                    Mathf.Lerp(recipe.area.yMin, recipe.area.yMax, random.NextFloat()));
                if (centers.Length > 0)
                {
                    int cluster = Mathf.Min(centers.Length - 1, (int)(random.NextFloat() * centers.Length));
                    float angle = random.NextFloat() * Mathf.PI * 2;
                    float distance = Mathf.Sqrt(random.NextFloat()) * recipe.clusterRadius;
                    // Different elliptical patch orientations break the visual rhythm of circular groves.
                    var offset = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * .62f);
                    float rotation = cluster * 2.399963f;
                    xz = centers[cluster] + new Vector2(offset.x * Mathf.Cos(rotation) - offset.y * Mathf.Sin(rotation),
                        offset.x * Mathf.Sin(rotation) + offset.y * Mathf.Cos(rotation));
                    if (!recipe.area.Contains(xz)) { result.rejectedGround++; continue; }
                }
                var species = SelectSpecies(recipe.species, totalWeight, random.NextFloat());
                float scale = Mathf.Lerp(species.minimumScale, species.maximumScale, random.NextFloat());
                float radius = species.radius * scale;
                float? height = ground(xz);
                if (!height.HasValue || !Finite(height.Value)) { result.rejectedGround++; continue; }
                var point = new Vector3(xz.x, height.Value, xz.y);
                if (point.y < recipe.minimumHeight || point.y > recipe.maximumHeight)
                { result.rejectedHeight++; continue; }

                var surfaceNormal = normal(point);
                if (!Finite(surfaceNormal) || surfaceNormal.sqrMagnitude < 1e-8f)
                { result.rejectedSlope++; continue; }
                float slope = Vector3.Angle(Vector3.up, surfaceNormal);
                if (!Finite(slope) || slope < recipe.minimumSlopeDegrees || slope > recipe.maximumSlopeDegrees)
                { result.rejectedSlope++; continue; }
                if (excludes(point, radius)) { result.rejectedExclusion++; continue; }
                if (!HasSpacing(point, radius, maximumRadius, recipe.minimumSpacing, cellSize, grid, blockers))
                { result.rejectedSpacing++; continue; }

                var planted = point + Vector3.up * species.groundOffsetMeters * scale;
                if (fitSupport != null)
                {
                    var fit = fitSupport(species, planted, scale);
                    if (!fit.HasValue || !Finite(fit.Value)) { result.rejectedGround++; continue; }
                    if (fit.Value.x != planted.x || fit.Value.z != planted.z)
                        throw new InvalidOperationException("Support fitting must preserve the accepted horizontal footprint.");
                    planted = fit.Value;
                }
                var placement = new Placement(species, planted, random.NextFloat() * 360f, scale);
                int index = blockers.Count;
                placements.Add(placement);
                blockers.Add(placement);
                var key = Cell(point, cellSize);
                if (!grid.TryGetValue(key, out var bucket)) grid.Add(key, bucket = new List<int>());
                bucket.Add(index);
            }
            result.placements = placements.ToArray();
            return result;
        }

        static bool HasSpacing(Vector3 point, float radius, float maximumRadius, float minimumSpacing,
            float cellSize, Dictionary<Vector2Int, List<int>> grid, List<Placement> placements)
        {
            var center = Cell(point, cellSize);
            int range = Mathf.CeilToInt(Mathf.Max(minimumSpacing, radius + maximumRadius) / cellSize);
            for (int z = center.y - range; z <= center.y + range; z++)
            for (int x = center.x - range; x <= center.x + range; x++)
            {
                if (!grid.TryGetValue(new Vector2Int(x, z), out var bucket)) continue;
                foreach (int index in bucket)
                {
                    var other = placements[index];
                    float required = Mathf.Max(minimumSpacing, radius + other.supportRadius);
                    float dx = point.x - other.position.x, dz = point.z - other.position.z;
                    if (dx * dx + dz * dz < required * required) return false;
                }
            }
            return true;
        }

        static Species SelectSpecies(Species[] species, float totalWeight, float sample)
        {
            float remaining = sample * totalWeight;
            foreach (var value in species)
            {
                remaining -= value.weight;
                if (remaining <= 0) return value;
            }
            return species[species.Length - 1];
        }

        static Vector2Int Cell(Vector3 point, float size) =>
            new Vector2Int(Mathf.FloorToInt(point.x / size), Mathf.FloorToInt(point.z / size));

        static void Validate(Recipe recipe, Func<Vector2, float?> ground,
            Func<Vector3, Vector3> normal, Func<Vector3, float, bool> excludes)
        {
            if (recipe == null || ground == null || normal == null || excludes == null)
                throw new ArgumentNullException("Recipe and all three caller-owned queries are required.");
            if (string.IsNullOrWhiteSpace(recipe.id) || !Finite(recipe.area.xMin) || !Finite(recipe.area.yMin) ||
                !Finite(recipe.area.xMax) || !Finite(recipe.area.yMax) || !Finite(recipe.area.width) ||
                !Finite(recipe.area.height) || recipe.area.width <= 0 || recipe.area.height <= 0)
                throw new ArgumentException("Recipe ID and finite positive rectangular area are required.");
            if (!Finite(recipe.densityPerHectare) || recipe.densityPerHectare < 0 ||
                !Finite(recipe.minimumSpacing) || recipe.minimumSpacing < .25f ||
                recipe.maximumCount < 0 || recipe.maximumCount > 2000)
                throw new ArgumentException("Density, spacing, or maximum count is outside the planner bounds.");
            if (!Finite(recipe.minimumHeight) || !Finite(recipe.maximumHeight) || recipe.minimumHeight > recipe.maximumHeight ||
                !Finite(recipe.minimumSlopeDegrees) || !Finite(recipe.maximumSlopeDegrees) ||
                recipe.minimumSlopeDegrees < 0 || recipe.maximumSlopeDegrees > 90 ||
                recipe.minimumSlopeDegrees > recipe.maximumSlopeDegrees)
                throw new ArgumentException("Height or slope bounds are invalid.");
            if (recipe.species == null || recipe.species.Length == 0 || recipe.species.Length > 32 ||
                recipe.species.Any(value => value == null || string.IsNullOrWhiteSpace(value.id) ||
                    string.IsNullOrWhiteSpace(value.prefabKey) || !Finite(value.weight) || value.weight <= 0 || value.weight > 1000000 ||
                    !Finite(value.radius) || value.radius <= 0 || value.radius > 100 ||
                    !Finite(value.minimumScale) || !Finite(value.maximumScale) || value.minimumScale <= 0 ||
                    value.minimumScale > value.maximumScale || value.maximumScale > 10))
                throw new ArgumentException("One to 32 valid weighted species descriptors are required.");
            if (recipe.species.Select(value => value.id).Distinct(StringComparer.Ordinal).Count() != recipe.species.Length)
                throw new ArgumentException("Species IDs must be unique within a recipe.");
            if (recipe.clusterCount < 0 || recipe.clusterCount > 128 || !Finite(recipe.clusterRadius) ||
                recipe.clusterCount > 0 && (recipe.clusterRadius < 1 || recipe.clusterRadius > 200) ||
                recipe.species.Any(s => !Finite(s.groundOffsetMeters) || s.groundOffsetMeters < -1 || s.groundOffsetMeters > .2f))
                throw new ArgumentException("Cluster count/radius or planting offset is outside the bounded recipe range.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        struct RandomSequence
        {
            uint state;
            public RandomSequence(int seed, string id)
            {
                state = unchecked((uint)seed) ^ 2166136261u;
                foreach (char value in id) { state ^= value; state *= 16777619u; }
                if (state == 0) state = 0x9e3779b9u;
            }
            public float NextFloat()
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (state >> 8) * (1f / 16777216f);
            }
        }
    }
}
