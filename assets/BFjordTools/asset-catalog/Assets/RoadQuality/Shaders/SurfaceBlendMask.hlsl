#ifndef BWORK_SURFACE_BLEND_MASK_INCLUDED
#define BWORK_SURFACE_BLEND_MASK_INCLUDED
// Scalar HLSL also compiled directly by the portable C++ contract check.
// No material-local random seed: adjacent bands share this one metre-XZ field.
float RoadHash(float x, float y)
{
    uint h = (uint)(int)x * 374761393u + (uint)(int)y * 668265263u;
    h = (h ^ (h >> 13u)) * 1274126177u;
    return (float)(h ^ (h >> 16u)) * (1.0 / 4294967296.0);
}
float RoadNoise(float x, float y, float frequency, float footprint)
{
    float qx = x * frequency, qy = y * frequency;
    float ix = floor(qx), iy = floor(qy), u = frac(qx), v = frac(qy);
    u = u * u * (3.0 - 2.0 * u); v = v * v * (3.0 - 2.0 * v);
    float n = lerp(lerp(RoadHash(ix, iy), RoadHash(ix + 1.0, iy), u),
                   lerp(RoadHash(ix, iy + 1.0), RoadHash(ix + 1.0, iy + 1.0), u), v);
    // Resolve subpixel grains toward their average rather than sparkling.
    return 0.5 + (n - 0.5) * (1.0 - smoothstep(0.35, 0.9, footprint * frequency));
}
float RoadEdge(float lateral, float coarse, float grain, float fine)
{
    float d = abs(lateral);
    if (d <= 2.8) return 0.0;
    if (d >= 4.0) return 1.0;
    float center = 3.2 + (coarse - 0.5) * 0.18 + (grain - 0.5) * 0.08;
    float halfWidth = lerp(0.075, 0.2, coarse);
    float edge = smoothstep(center - halfWidth, center + halfWidth, d);
    float dustEnvelope = smoothstep(2.8, 3.05, d) * (1.0 - smoothstep(3.15, 3.45, d));
    float dust = 0.22 * dustEnvelope * smoothstep(0.52, 0.76, fine);
    return saturate(edge + (1.0 - edge) * dust);
}
float RoadTransition(float lateral, float station, float coarse, float grain, float fine)
{
    if (station <= 116.0) return 0.0;
    if (station >= 126.0) return 1.0;
    float center = 120.0 + (coarse - 0.5) * 0.28 + (grain - 0.5) * 0.10;
    float edge = smoothstep(center - 0.16, center + 0.20, station);
    float tracks = 1.0 - smoothstep(0.12, 0.42, abs(abs(lateral) - 1.05));
    float dust = 0.28 * smoothstep(116.0, 119.9, station) * smoothstep(0.52, 0.77, fine) * (1.0 - 0.7 * tracks);
    float chips = 0.24 * (1.0 - smoothstep(120.4, 123.0, station)) * smoothstep(0.55, 0.8, grain);
    return saturate(edge + (1.0 - edge) * dust - edge * chips);
}
float RoadVerge(float lateral, float coarse, float grain)
{
    float d = abs(lateral);
    if (d <= 4.0) return 0.0;
    if (d >= 6.8) return 1.0;
    float t = (d - 4.0) / 2.8;
    float envelope = 4.0 * t * (1.0 - t);
    float irregularity = envelope * ((coarse - 0.5) * 0.85 + (grain - 0.5) * 0.30);
    return smoothstep(4.2, 6.6, d + irregularity);
}
float RoadBlendWeight(float mode, float x, float z, float lateral, float station, float footprint)
{
    float coarse = RoadNoise(x, z, 2.0, footprint);
    float grain = RoadNoise(x, z, 9.0, footprint);
    float fine = RoadNoise(x, z, 23.0, footprint);
    if (mode > 2.5) return RoadTransition(lateral, station, coarse, grain, fine);
    if (mode > 1.5) return RoadVerge(lateral, coarse, grain);
    float edge = RoadEdge(lateral, coarse, grain, fine);
    if (mode < 0.5) return edge;
    float along = RoadTransition(lateral, station, coarse, grain, fine);
    // Preserve bit-identical weights at material/mesh contacts, not 1-(1-w) rounding.
    if (abs(lateral) <= 2.8) return along;
    if (abs(lateral) >= 4.0) return 1.0;
    if (station <= 116.0) return edge;
    if (station >= 126.0) return 1.0;
    return 1.0 - (1.0 - edge) * (1.0 - along);
}
#endif
