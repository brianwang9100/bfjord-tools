using System;

namespace Bwork.FjordCoast.TerrainAuthoring
{
    public enum TerrainEditKind
    {
        RaiseLower,
        Flatten,
        Smooth,
        Stamp,
        ThermalErosion
    }

    public enum TerrainRegionShape
    {
        Ellipse,
        Box
    }

    public enum TerrainStampBlendMode
    {
        Add,
        Replace,
        Maximum,
        Minimum
    }

    [Serializable]
    public sealed class TerrainPatchMetrics
    {
        public float TerrainOriginX { get; set; }
        public float TerrainOriginY { get; set; }
        public float TerrainOriginZ { get; set; }
        public float TerrainSizeX { get; set; } = 11500f;
        public float TerrainSizeY { get; set; } = 1750f;
        public float TerrainSizeZ { get; set; } = 11500f;
        public int HeightmapResolution { get; set; } = 2049;
        public int PatchStartX { get; set; }
        public int PatchStartZ { get; set; }

        public static TerrainPatchMetrics FjordCoastSingleTerrain()
        {
            return new TerrainPatchMetrics
            {
                TerrainOriginX = 0f,
                TerrainOriginY = -64f,
                TerrainOriginZ = 0f
            };
        }
    }

    [Serializable]
    public sealed class TerrainHeightStamp
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public float[] Values { get; set; } = Array.Empty<float>();
        public TerrainStampBlendMode BlendMode { get; set; } = TerrainStampBlendMode.Add;
        public float ZeroValue { get; set; } = 0.5f;
        public float BaseWorldY { get; set; }
        public float AmplitudeMeters { get; set; } = 100f;
    }

    [Serializable]
    public sealed class TerrainEditOperation
    {
        public TerrainEditKind Kind { get; set; }
        public TerrainRegionShape Shape { get; set; } = TerrainRegionShape.Ellipse;
        public float CenterWorldX { get; set; }
        public float CenterWorldZ { get; set; }
        public float SizeX { get; set; } = 100f;
        public float SizeZ { get; set; } = 100f;
        public float RotationDegrees { get; set; }
        public float FalloffMeters { get; set; } = 20f;
        public float Strength { get; set; } = 1f;

        public float DeltaMeters { get; set; }
        public float TargetWorldY { get; set; }
        public float SmoothRadiusMeters { get; set; } = 20f;
        public int Iterations { get; set; } = 1;
        public float ThermalTalusAngleDegrees { get; set; } = 34f;
        public float ThermalRelaxation { get; set; } = 0.18f;
        public float ThermalMaxTransferMeters { get; set; } = 6f;
        public TerrainHeightStamp Stamp { get; set; }
    }

    [Serializable]
    public sealed class TerrainEditRecipe
    {
        public bool PreservePatchBorder { get; set; } = true;
        public float PatchEdgeBlendMeters { get; set; } = 32f;
        public TerrainEditOperation[] Operations { get; set; } = Array.Empty<TerrainEditOperation>();
    }

    /// <summary>
    /// Applies bounded world-space edits to a Unity-style normalized height patch.
    /// Arrays use [z, x] indexing. The input patch and editability mask are never mutated.
    /// </summary>
    public static class FjordTerrainPatchEditor
    {
        const int MaximumOperations = 64;
        const int MaximumIterations = 32;
        const long MaximumCellIterations = 64L * 1024L * 1024L;

        public static float[,] Apply(
            float[,] originalPatch,
            TerrainPatchMetrics metrics,
            float[,] editabilityMask,
            TerrainEditRecipe recipe)
        {
            ValidateInputs(originalPatch, metrics, editabilityMask, recipe);
            var output = (float[,])originalPatch.Clone();
            var context = new PatchContext(output, metrics, editabilityMask, recipe);

            foreach (var operation in recipe.Operations)
            {
                ValidateOperation(operation);
                BrushMask brush = context.CreateBrush(operation);
                if (brush.IsEmpty)
                    continue;

                switch (operation.Kind)
                {
                    case TerrainEditKind.RaiseLower:
                        ApplyRaiseLower(context, brush, operation);
                        break;
                    case TerrainEditKind.Flatten:
                        ApplyFlatten(context, brush, operation);
                        break;
                    case TerrainEditKind.Smooth:
                        ApplySmooth(context, brush, operation);
                        break;
                    case TerrainEditKind.Stamp:
                        ApplyStamp(context, brush, operation);
                        break;
                    case TerrainEditKind.ThermalErosion:
                        ApplyThermalErosion(context, brush, operation);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation.Kind));
                }
            }

            return context.Heights;
        }

        public static float[,] Apply(
            float[,] originalPatch,
            TerrainPatchMetrics metrics,
            TerrainEditRecipe recipe)
        {
            return Apply(originalPatch, metrics, null, recipe);
        }

        static void ApplyRaiseLower(PatchContext context, BrushMask brush, TerrainEditOperation operation)
        {
            float normalizedDelta = operation.DeltaMeters / context.Metrics.TerrainSizeY;
            brush.Visit((z, x, weight) =>
            {
                context.Heights[z, x] = Clamp01(context.Heights[z, x] + normalizedDelta * weight * operation.Strength);
            });
        }

        static void ApplyFlatten(PatchContext context, BrushMask brush, TerrainEditOperation operation)
        {
            float target = Clamp01((operation.TargetWorldY - context.Metrics.TerrainOriginY) /
                context.Metrics.TerrainSizeY);
            brush.Visit((z, x, weight) =>
            {
                float blend = weight * operation.Strength;
                context.Heights[z, x] = Lerp(context.Heights[z, x], target, blend);
            });
        }

        static void ApplySmooth(PatchContext context, BrushMask brush, TerrainEditOperation operation)
        {
            int radiusX = Math.Max(1, (int)Math.Ceiling(operation.SmoothRadiusMeters / context.SampleSpacingX));
            int radiusZ = Math.Max(1, (int)Math.Ceiling(operation.SmoothRadiusMeters / context.SampleSpacingZ));
            RequireBoundedWork(brush, operation.Iterations);

            for (int iteration = 0; iteration < operation.Iterations; iteration++)
            {
                double[,] integral = BuildIntegralImage(context.Heights);
                var next = (float[,])context.Heights.Clone();
                brush.Visit((z, x, weight) =>
                {
                    int minX = Math.Max(0, x - radiusX);
                    int maxX = Math.Min(context.Columns - 1, x + radiusX);
                    int minZ = Math.Max(0, z - radiusZ);
                    int maxZ = Math.Min(context.Rows - 1, z + radiusZ);
                    float average = RectangularAverage(integral, minZ, minX, maxZ, maxX);
                    next[z, x] = Lerp(context.Heights[z, x], average, weight * operation.Strength);
                });
                context.Heights = next;
            }
        }

        static void ApplyStamp(PatchContext context, BrushMask brush, TerrainEditOperation operation)
        {
            TerrainHeightStamp stamp = operation.Stamp;
            double radians = operation.RotationDegrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);

            brush.Visit((z, x, weight) =>
            {
                float worldX = context.WorldX(x);
                float worldZ = context.WorldZ(z);
                float dx = worldX - operation.CenterWorldX;
                float dz = worldZ - operation.CenterWorldZ;
                float localX = cos * dx + sin * dz;
                float localZ = -sin * dx + cos * dz;
                float u = localX / operation.SizeX + 0.5f;
                float v = localZ / operation.SizeZ + 0.5f;
                float sample = SampleStamp(stamp, u, v);
                float current = context.Heights[z, x];
                float candidate;

                if (stamp.BlendMode == TerrainStampBlendMode.Add)
                {
                    candidate = current + (sample - stamp.ZeroValue) * stamp.AmplitudeMeters /
                        context.Metrics.TerrainSizeY;
                }
                else
                {
                    float stampedWorldY = stamp.BaseWorldY + sample * stamp.AmplitudeMeters;
                    float stamped = (stampedWorldY - context.Metrics.TerrainOriginY) /
                        context.Metrics.TerrainSizeY;
                    if (stamp.BlendMode == TerrainStampBlendMode.Maximum)
                        candidate = Math.Max(current, stamped);
                    else if (stamp.BlendMode == TerrainStampBlendMode.Minimum)
                        candidate = Math.Min(current, stamped);
                    else
                        candidate = stamped;
                }

                context.Heights[z, x] = Clamp01(Lerp(current, Clamp01(candidate), weight * operation.Strength));
            });
        }

        static void ApplyThermalErosion(
            PatchContext context,
            BrushMask brush,
            TerrainEditOperation operation)
        {
            RequireBoundedWork(brush, operation.Iterations);
            float talus = (float)Math.Tan(operation.ThermalTalusAngleDegrees * Math.PI / 180.0);
            int localRows = brush.MaxZ - brush.MinZ + 1;
            int localColumns = brush.MaxX - brush.MinX + 1;
            var neighborX = new[] { -1, 0, 1, -1, 1, -1, 0, 1 };
            var neighborZ = new[] { -1, -1, -1, 0, 0, 1, 1, 1 };
            var effectiveExcess = new float[8];

            for (int iteration = 0; iteration < operation.Iterations; iteration++)
            {
                var deltaMeters = new float[localRows, localColumns];
                for (int z = brush.MinZ; z <= brush.MaxZ; z++)
                {
                    for (int x = brush.MinX; x <= brush.MaxX; x++)
                    {
                        float sourceWeight = brush.WeightAt(z, x);
                        if (sourceWeight <= 0f)
                            continue;

                        float totalExcess = 0f;
                        float maximumExcess = 0f;
                        for (int neighbor = 0; neighbor < 8; neighbor++)
                        {
                            int nx = x + neighborX[neighbor];
                            int nz = z + neighborZ[neighbor];
                            float destinationWeight = brush.WeightAt(nz, nx);
                            if (destinationWeight <= 0f)
                            {
                                effectiveExcess[neighbor] = 0f;
                                continue;
                            }

                            float horizontalDistance = (float)Math.Sqrt(
                                neighborX[neighbor] * neighborX[neighbor] * context.SampleSpacingX * context.SampleSpacingX +
                                neighborZ[neighbor] * neighborZ[neighbor] * context.SampleSpacingZ * context.SampleSpacingZ);
                            float heightDifference = (context.Heights[z, x] - context.Heights[nz, nx]) *
                                context.Metrics.TerrainSizeY;
                            float excess = Math.Max(0f, heightDifference - talus * horizontalDistance);
                            excess *= Math.Min(sourceWeight, destinationWeight);
                            effectiveExcess[neighbor] = excess;
                            totalExcess += excess;
                            maximumExcess = Math.Max(maximumExcess, excess);
                        }

                        if (totalExcess <= 0f)
                            continue;

                        float transfer = Math.Min(operation.ThermalMaxTransferMeters,
                            maximumExcess * operation.ThermalRelaxation * operation.Strength);
                        transfer = Math.Min(transfer, context.Heights[z, x] * context.Metrics.TerrainSizeY);
                        deltaMeters[z - brush.MinZ, x - brush.MinX] -= transfer;

                        for (int neighbor = 0; neighbor < 8; neighbor++)
                        {
                            if (effectiveExcess[neighbor] <= 0f)
                                continue;
                            int nx = x + neighborX[neighbor];
                            int nz = z + neighborZ[neighbor];
                            deltaMeters[nz - brush.MinZ, nx - brush.MinX] +=
                                transfer * effectiveExcess[neighbor] / totalExcess;
                        }
                    }
                }

                for (int z = brush.MinZ; z <= brush.MaxZ; z++)
                {
                    for (int x = brush.MinX; x <= brush.MaxX; x++)
                    {
                        float delta = deltaMeters[z - brush.MinZ, x - brush.MinX];
                        context.Heights[z, x] = Clamp01(context.Heights[z, x] +
                            delta / context.Metrics.TerrainSizeY);
                    }
                }
            }
        }

        static double[,] BuildIntegralImage(float[,] heights)
        {
            int rows = heights.GetLength(0);
            int columns = heights.GetLength(1);
            var integral = new double[rows + 1, columns + 1];
            for (int z = 0; z < rows; z++)
            {
                double rowSum = 0.0;
                for (int x = 0; x < columns; x++)
                {
                    rowSum += heights[z, x];
                    integral[z + 1, x + 1] = integral[z, x + 1] + rowSum;
                }
            }
            return integral;
        }

        static float RectangularAverage(double[,] integral, int minZ, int minX, int maxZ, int maxX)
        {
            double sum = integral[maxZ + 1, maxX + 1] - integral[minZ, maxX + 1] -
                integral[maxZ + 1, minX] + integral[minZ, minX];
            int count = (maxZ - minZ + 1) * (maxX - minX + 1);
            return (float)(sum / count);
        }

        static float SampleStamp(TerrainHeightStamp stamp, float u, float v)
        {
            u = Clamp01(u);
            v = Clamp01(v);
            float sourceX = u * (stamp.Width - 1);
            float sourceZ = v * (stamp.Height - 1);
            int x0 = Math.Min(stamp.Width - 1, (int)sourceX);
            int z0 = Math.Min(stamp.Height - 1, (int)sourceZ);
            int x1 = Math.Min(stamp.Width - 1, x0 + 1);
            int z1 = Math.Min(stamp.Height - 1, z0 + 1);
            float tx = sourceX - x0;
            float tz = sourceZ - z0;
            float a = Lerp(stamp.Values[z0 * stamp.Width + x0], stamp.Values[z0 * stamp.Width + x1], tx);
            float b = Lerp(stamp.Values[z1 * stamp.Width + x0], stamp.Values[z1 * stamp.Width + x1], tx);
            return Lerp(a, b, tz);
        }

        static void ValidateInputs(
            float[,] originalPatch,
            TerrainPatchMetrics metrics,
            float[,] editabilityMask,
            TerrainEditRecipe recipe)
        {
            if (originalPatch == null)
                throw new ArgumentNullException(nameof(originalPatch));
            if (metrics == null)
                throw new ArgumentNullException(nameof(metrics));
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            int rows = originalPatch.GetLength(0);
            int columns = originalPatch.GetLength(1);
            Require(rows > 1 && columns > 1, "A terrain patch needs at least two samples per axis.");
            Require(metrics.HeightmapResolution > 1, "Heightmap resolution must be greater than one.");
            Require(metrics.PatchStartX >= 0 && metrics.PatchStartZ >= 0 &&
                metrics.PatchStartX + columns <= metrics.HeightmapResolution &&
                metrics.PatchStartZ + rows <= metrics.HeightmapResolution,
                "Patch bounds exceed the heightmap resolution.");
            RequireFinitePositive(metrics.TerrainSizeX, nameof(metrics.TerrainSizeX));
            RequireFinitePositive(metrics.TerrainSizeY, nameof(metrics.TerrainSizeY));
            RequireFinitePositive(metrics.TerrainSizeZ, nameof(metrics.TerrainSizeZ));
            RequireFinite(metrics.TerrainOriginX, nameof(metrics.TerrainOriginX));
            RequireFinite(metrics.TerrainOriginY, nameof(metrics.TerrainOriginY));
            RequireFinite(metrics.TerrainOriginZ, nameof(metrics.TerrainOriginZ));
            Require(recipe.Operations != null && recipe.Operations.Length <= MaximumOperations,
                "A recipe must contain at most 64 operations.");
            RequireFiniteNonnegative(recipe.PatchEdgeBlendMeters, nameof(recipe.PatchEdgeBlendMeters));

            if (editabilityMask != null)
            {
                Require(editabilityMask.GetLength(0) == rows && editabilityMask.GetLength(1) == columns,
                    "The editability mask must match the patch dimensions.");
            }

            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    RequireFinite(originalPatch[z, x], "originalPatch");
                    Require(originalPatch[z, x] >= 0f && originalPatch[z, x] <= 1f,
                        "Original heights must be normalized to [0, 1].");
                    if (editabilityMask != null)
                    {
                        RequireFinite(editabilityMask[z, x], "editabilityMask");
                        Require(editabilityMask[z, x] >= 0f && editabilityMask[z, x] <= 1f,
                            "Editability mask weights must be in [0, 1].");
                    }
                }
            }
        }

        static void ValidateOperation(TerrainEditOperation operation)
        {
            if (operation == null)
                throw new ArgumentException("Recipe operations cannot contain null entries.");
            RequireFinite(operation.CenterWorldX, nameof(operation.CenterWorldX));
            RequireFinite(operation.CenterWorldZ, nameof(operation.CenterWorldZ));
            RequireFinitePositive(operation.SizeX, nameof(operation.SizeX));
            RequireFinitePositive(operation.SizeZ, nameof(operation.SizeZ));
            RequireFinite(operation.RotationDegrees, nameof(operation.RotationDegrees));
            RequireFiniteNonnegative(operation.FalloffMeters, nameof(operation.FalloffMeters));
            RequireFinite(operation.Strength, nameof(operation.Strength));
            Require(operation.Strength >= 0f && operation.Strength <= 1f,
                "Operation strength must be in [0, 1].");

            if (operation.Kind == TerrainEditKind.RaiseLower)
                RequireFinite(operation.DeltaMeters, nameof(operation.DeltaMeters));
            if (operation.Kind == TerrainEditKind.Flatten)
                RequireFinite(operation.TargetWorldY, nameof(operation.TargetWorldY));
            if (operation.Kind == TerrainEditKind.Smooth)
            {
                RequireFinitePositive(operation.SmoothRadiusMeters, nameof(operation.SmoothRadiusMeters));
                RequireIterations(operation.Iterations);
            }
            if (operation.Kind == TerrainEditKind.Stamp)
                ValidateStamp(operation.Stamp);
            if (operation.Kind == TerrainEditKind.ThermalErosion)
            {
                RequireIterations(operation.Iterations);
                RequireFinite(operation.ThermalTalusAngleDegrees, nameof(operation.ThermalTalusAngleDegrees));
                Require(operation.ThermalTalusAngleDegrees > 0f && operation.ThermalTalusAngleDegrees < 89f,
                    "Thermal talus angle must be between 0 and 89 degrees.");
                RequireFinite(operation.ThermalRelaxation, nameof(operation.ThermalRelaxation));
                Require(operation.ThermalRelaxation > 0f && operation.ThermalRelaxation <= 0.25f,
                    "Thermal relaxation must be in (0, 0.25].");
                RequireFinitePositive(operation.ThermalMaxTransferMeters,
                    nameof(operation.ThermalMaxTransferMeters));
            }
        }

        static void ValidateStamp(TerrainHeightStamp stamp)
        {
            Require(stamp != null, "Stamp operations require stamp data.");
            Require(stamp.Width >= 2 && stamp.Height >= 2, "Stamp dimensions must be at least 2x2.");
            Require(stamp.Values != null && stamp.Values.Length == stamp.Width * stamp.Height,
                "Stamp values must be row-major and match width times height.");
            RequireFinite(stamp.ZeroValue, nameof(stamp.ZeroValue));
            RequireFinite(stamp.BaseWorldY, nameof(stamp.BaseWorldY));
            RequireFinite(stamp.AmplitudeMeters, nameof(stamp.AmplitudeMeters));
            for (int index = 0; index < stamp.Values.Length; index++)
            {
                RequireFinite(stamp.Values[index], "stamp.Values");
                Require(stamp.Values[index] >= 0f && stamp.Values[index] <= 1f,
                    "Stamp values must be normalized to [0, 1].");
            }
        }

        static void RequireBoundedWork(BrushMask brush, int iterations)
        {
            long cells = (long)(brush.MaxX - brush.MinX + 1) * (brush.MaxZ - brush.MinZ + 1);
            Require(cells * iterations <= MaximumCellIterations,
                "Operation exceeds the bounded 64-million cell-iteration budget; split the region or reduce iterations.");
        }

        static void RequireIterations(int iterations)
        {
            Require(iterations >= 1 && iterations <= MaximumIterations,
                "Iteration count must be between 1 and 32.");
        }

        static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        static float SmoothStep01(float value)
        {
            float t = Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new ArgumentException(message);
        }

        static void RequireFinite(float value, string name)
        {
            Require(!float.IsNaN(value) && !float.IsInfinity(value), name + " must be finite.");
        }

        static void RequireFinitePositive(float value, string name)
        {
            RequireFinite(value, name);
            Require(value > 0f, name + " must be positive.");
        }

        static void RequireFiniteNonnegative(float value, string name)
        {
            RequireFinite(value, name);
            Require(value >= 0f, name + " cannot be negative.");
        }

        sealed class PatchContext
        {
            readonly float[,] editabilityMask;
            readonly TerrainEditRecipe recipe;

            public PatchContext(
                float[,] heights,
                TerrainPatchMetrics metrics,
                float[,] editabilityMask,
                TerrainEditRecipe recipe)
            {
                Heights = heights;
                Metrics = metrics;
                this.editabilityMask = editabilityMask;
                this.recipe = recipe;
                SampleSpacingX = metrics.TerrainSizeX / (metrics.HeightmapResolution - 1);
                SampleSpacingZ = metrics.TerrainSizeZ / (metrics.HeightmapResolution - 1);
            }

            public float[,] Heights { get; set; }
            public TerrainPatchMetrics Metrics { get; }
            public int Rows => Heights.GetLength(0);
            public int Columns => Heights.GetLength(1);
            public float SampleSpacingX { get; }
            public float SampleSpacingZ { get; }

            public float WorldX(int localX)
            {
                return Metrics.TerrainOriginX + (Metrics.PatchStartX + localX) * SampleSpacingX;
            }

            public float WorldZ(int localZ)
            {
                return Metrics.TerrainOriginZ + (Metrics.PatchStartZ + localZ) * SampleSpacingZ;
            }

            public BrushMask CreateBrush(TerrainEditOperation operation)
            {
                double radians = operation.RotationDegrees * Math.PI / 180.0;
                float cos = (float)Math.Cos(radians);
                float sin = (float)Math.Sin(radians);
                float halfX = operation.SizeX * 0.5f;
                float halfZ = operation.SizeZ * 0.5f;
                float extentX = Math.Abs(cos) * halfX + Math.Abs(sin) * halfZ;
                float extentZ = Math.Abs(sin) * halfX + Math.Abs(cos) * halfZ;
                int minX = ClampIndex((int)Math.Floor((operation.CenterWorldX - extentX -
                    Metrics.TerrainOriginX) / SampleSpacingX) - Metrics.PatchStartX, Columns);
                int maxX = ClampIndex((int)Math.Ceiling((operation.CenterWorldX + extentX -
                    Metrics.TerrainOriginX) / SampleSpacingX) - Metrics.PatchStartX, Columns);
                int minZ = ClampIndex((int)Math.Floor((operation.CenterWorldZ - extentZ -
                    Metrics.TerrainOriginZ) / SampleSpacingZ) - Metrics.PatchStartZ, Rows);
                int maxZ = ClampIndex((int)Math.Ceiling((operation.CenterWorldZ + extentZ -
                    Metrics.TerrainOriginZ) / SampleSpacingZ) - Metrics.PatchStartZ, Rows);
                if (minX > maxX || minZ > maxZ)
                    return BrushMask.Empty;

                var weights = new float[maxZ - minZ + 1, maxX - minX + 1];
                bool hasWeight = false;
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = WorldX(x) - operation.CenterWorldX;
                        float dz = WorldZ(z) - operation.CenterWorldZ;
                        float localX = cos * dx + sin * dz;
                        float localZ = -sin * dx + cos * dz;
                        float regionWeight = RegionWeight(operation, localX, localZ, halfX, halfZ);
                        float weight = regionWeight * EditabilityAt(z, x) * PatchEdgeWeight(z, x);
                        weights[z - minZ, x - minX] = weight;
                        hasWeight |= weight > 0f;
                    }
                }
                return hasWeight ? new BrushMask(minZ, minX, maxZ, maxX, weights) : BrushMask.Empty;
            }

            float EditabilityAt(int z, int x)
            {
                return editabilityMask == null ? 1f : editabilityMask[z, x];
            }

            float PatchEdgeWeight(int z, int x)
            {
                if (!recipe.PreservePatchBorder)
                    return 1f;
                float blendDistance = Math.Max(recipe.PatchEdgeBlendMeters,
                    Math.Max(SampleSpacingX, SampleSpacingZ));
                float distance = Math.Min(
                    Math.Min(x * SampleSpacingX, (Columns - 1 - x) * SampleSpacingX),
                    Math.Min(z * SampleSpacingZ, (Rows - 1 - z) * SampleSpacingZ));
                return SmoothStep01(distance / blendDistance);
            }

            static float RegionWeight(
                TerrainEditOperation operation,
                float localX,
                float localZ,
                float halfX,
                float halfZ)
            {
                float insideDistance;
                if (operation.Kind == TerrainEditKind.Stamp || operation.Shape == TerrainRegionShape.Box)
                {
                    if (Math.Abs(localX) > halfX || Math.Abs(localZ) > halfZ)
                        return 0f;
                    insideDistance = Math.Min(halfX - Math.Abs(localX), halfZ - Math.Abs(localZ));
                }
                else
                {
                    float normalizedRadius = (float)Math.Sqrt(
                        localX * localX / (halfX * halfX) + localZ * localZ / (halfZ * halfZ));
                    if (normalizedRadius > 1f)
                        return 0f;
                    insideDistance = (1f - normalizedRadius) * Math.Min(halfX, halfZ);
                }

                if (operation.FalloffMeters <= 0f)
                    return 1f;
                return SmoothStep01(insideDistance / operation.FalloffMeters);
            }

            static int ClampIndex(int value, int length)
            {
                return Math.Max(0, Math.Min(length - 1, value));
            }
        }

        sealed class BrushMask
        {
            public static readonly BrushMask Empty = new BrushMask();
            readonly float[,] weights;

            BrushMask()
            {
                IsEmpty = true;
            }

            public BrushMask(int minZ, int minX, int maxZ, int maxX, float[,] weights)
            {
                MinZ = minZ;
                MinX = minX;
                MaxZ = maxZ;
                MaxX = maxX;
                this.weights = weights;
            }

            public bool IsEmpty { get; }
            public int MinZ { get; }
            public int MinX { get; }
            public int MaxZ { get; }
            public int MaxX { get; }

            public float WeightAt(int z, int x)
            {
                if (IsEmpty || z < MinZ || z > MaxZ || x < MinX || x > MaxX)
                    return 0f;
                return weights[z - MinZ, x - MinX];
            }

            public void Visit(Action<int, int, float> visitor)
            {
                for (int z = MinZ; z <= MaxZ; z++)
                {
                    for (int x = MinX; x <= MaxX; x++)
                    {
                        float weight = WeightAt(z, x);
                        if (weight > 0f)
                            visitor(z, x, weight);
                    }
                }
            }
        }
    }
}
