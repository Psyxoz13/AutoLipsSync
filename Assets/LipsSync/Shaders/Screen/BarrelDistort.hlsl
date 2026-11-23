void BarrelDistort_float(float2 uv, float k, out float2 uv_out)
{
    float2 p = uv * 2.0 - 1.0;
    float r2 = dot(p, p);
    p *= (1.0 + k * r2);
    uv_out = saturate((p + 1.0) * 0.5);
}