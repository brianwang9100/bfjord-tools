#ifndef BFJORD_TERRAIN_SAMPLING_INCLUDED
#define BFJORD_TERRAIN_SAMPLING_INCLUDED

// Translation-only triangular tiling. All material channels share the same
// lattice, offsets and weights. Normals therefore need no arbitrary UV rotation.
uint BFjordTerrainHash(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    return value ^ (value >> 16);
}

float2 BFjordTerrainOffset(float2 vertex)
{
    uint key = BFjordTerrainHash(asuint((int)vertex.x) ^
        BFjordTerrainHash(asuint((int)vertex.y)) ^ asuint((int)_BFjordAppearanceSeed));
    return float2(key & 65535u, BFjordTerrainHash(key) & 65535u) / 65536.0;
}

void BFjordTerrainTriangle(float2 uv, out float2 a, out float2 b, out float2 c, out float3 weights)
{
    // Equilateral lattice in texture space; a scan's metres-per-repeat remains
    // unchanged. Negative coordinates use floor, so both sides of the origin agree.
    float2 latticePosition = uv / _BFjordCellTiles;
    float2 skew = float2(latticePosition.x - latticePosition.y * 0.577350269, latticePosition.y * 1.154700538);
    float2 cell = floor(skew);
    float2 f = frac(skew);
    if (f.x + f.y <= 1.0)
    {
        a = cell; b = cell + float2(1, 0); c = cell + float2(0, 1);
        weights = float3(1.0 - f.x - f.y, f.x, f.y);
    }
    else
    {
        a = cell + 1; b = cell + float2(0, 1); c = cell + float2(1, 0);
        weights = float3(f.x + f.y - 1.0, 1.0 - f.x, 1.0 - f.y);
    }
    // Smooth edge contributions retain more scan detail than a broad linear mix.
    weights = weights * weights * weights;
    weights /= max(dot(weights, 1.0), 1e-6);
    a = uv + BFjordTerrainOffset(a);
    b = uv + BFjordTerrainOffset(b);
    c = uv + BFjordTerrainOffset(c);
}

half4 BFjordTerrainData(TEXTURE2D_PARAM(tex, samp), float2 uv, float meters)
{
    // The 90 m aerial outcrop is already a macro scan: preserve its slab composition.
    if (meters > 32.0) return SAMPLE_TEXTURE2D(tex, samp, uv);
    float2 a, b, c; float3 w;
    BFjordTerrainTriangle(uv, a, b, c, w);
    // Derivatives of the original coordinates avoid false coarse mip levels at
    // lattice boundaries caused by the discrete random translations.
    float2 dx = ddx(uv), dy = ddy(uv);
    return SAMPLE_TEXTURE2D_GRAD(tex, samp, a, dx, dy) * w.x +
        SAMPLE_TEXTURE2D_GRAD(tex, samp, b, dx, dy) * w.y +
        SAMPLE_TEXTURE2D_GRAD(tex, samp, c, dx, dy) * w.z;
}

half3 BFjordTerrainNormal(TEXTURE2D_PARAM(tex, samp), float2 uv, float meters, half strength)
{
    if (meters > 32.0) return UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uv), strength);
    float2 a, b, c; float3 w;
    BFjordTerrainTriangle(uv, a, b, c, w);
    float2 dx = ddx(uv), dy = ddy(uv);
    // Unpack before mixing: packed normal alpha/red conventions differ by platform.
    half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, a, dx, dy), strength) * w.x +
        UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, b, dx, dy), strength) * w.y +
        UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, c, dx, dy), strength) * w.z;
    return normalize(n + half3(0, 0, 1e-5));
}

float BFjordTerrainNoise(float2 position)
{
    float2 cell = floor(position), f = frac(position);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(BFjordTerrainOffset(cell).x, BFjordTerrainOffset(cell + float2(1, 0)).x, f.x),
        lerp(BFjordTerrainOffset(cell + float2(0, 1)).x, BFjordTerrainOffset(cell + 1).x, f.x), f.y);
}

half4 BFjordTerrainColor(TEXTURE2D_PARAM(tex, samp), float2 uv, float meters)
{
    half4 color = BFjordTerrainData(TEXTURE2D_ARGS(tex, samp), uv, meters);
    // World-aligned, metre-scaled variation is shared by every layer. It alters
    // reflectance only; scan AO, height, roughness and alpha keep their meanings.
    float2 position = uv * meters / _BFjordMacroMeters;
    float variation = BFjordTerrainNoise(position) * .65 +
        BFjordTerrainNoise(position * 2.13 + float2(17, 31)) * .35;
    color.rgb *= 1.0 + (variation * 2.0 - 1.0) * _BFjordMacroVariation;
    return color;
}
#endif
