#ifndef SKY_SCATTERINGS_INCLUDE
#define SKY_SCATTERINGS_INCLUDE

#include "AtmosphereInputs.hlsl"
#include "MathHelpers.hlsl"

#define NUM_SAMPLE 16
#define NUM_SAMPLE_LIT_PATH 16
#define NUM_DIRECTION 64

#define MIN_HEIGHT 2.0
#define MAX_DISTANCE_SCALER 1.05

float2 UvToTransmittanceParameters(float2 uv, float innerRadius, float outerRadius)
{
    float maxTangentLength = sqrt(max(outerRadius * outerRadius - innerRadius * innerRadius, 0.0));
    float tangentLength = uv.y * maxTangentLength;
    float radius = sqrt(max(tangentLength * tangentLength + innerRadius * innerRadius, 0.0));

    float minDistance = outerRadius - radius;
    float maxDistance = (tangentLength + maxTangentLength) * MAX_DISTANCE_SCALER;
    float distance = minDistance + uv.x * (maxDistance - minDistance);
    float unclampedCosTheta = distance < 0.001 ? 1.0 : (outerRadius * outerRadius - radius * radius - distance * distance) / (2.0 * radius * distance);
    float cosTheta = clamp(unclampedCosTheta, -1.0, 1.0);
    
    return float2(radius, cosTheta);
}

float2 TransmittanceParametersToUv(float2 params, float innerRadius, float outerRadius)
{
    float maxTangentLength = sqrt(max(outerRadius * outerRadius - innerRadius * innerRadius, 0.0));
    float tangentLength = sqrt(max(params.x * params.x - innerRadius * innerRadius, 0.0));
    float v = tangentLength / maxTangentLength;

    float minDistance = outerRadius - params.x;
    float maxDistance = (tangentLength + maxTangentLength) * MAX_DISTANCE_SCALER;
    float discriminant = params.x * params.x * (params.y * params.y - 1.0) + outerRadius * outerRadius;
    float distance = max(0.0, (-params.x * params.y + sqrt(discriminant)));
    float u = (distance - minDistance) / (maxDistance - minDistance);
    
    return float2(u, v);
}

float2 UvToTransmittanceParametersUniform(float2 uv, float innerRadius, float outerRadius)
{
    float radius = innerRadius + uv.y * (outerRadius - innerRadius);
    float cosTheta = uv.x * 2.0 - 1.0;
    
    return float2(radius, cosTheta);
}

float2 TransmittanceParametersToUvUniform(float2 params, float innerRadius, float outerRadius)
{
    float v = (params.x - innerRadius) / (outerRadius - innerRadius);
    float u = params.y + 1.0 / 2.0;
    
    return float2(u, v);
}

float3 UvToViewDirection(float2 uv)
{
    float longitude = 2.0 * PI * uv.x;
    float zenith = PI * (1.0 - uv.y);
    
    float z = sin(zenith) * sin(longitude);
    float x = sin(zenith) * cos(longitude);
    float y = cos(zenith);

    return float3(x, y, z);
}

float2 ViewDirectionToUv(float3 direction)
{
    float zenith = acos(direction.y);
    float longitude = atan2(direction.z, direction.x);

    if (abs(direction.x) < 0.01 && abs(direction.z) < 0.01)
    {
        longitude = 0.0;
    }

    if (longitude < 0.0)
    {
        longitude += 2.0 * PI;
    }

    float uvX = longitude / (2.0 * PI);
    float uvY = 1.0 - zenith / PI;

    return float2(uvX, uvY);
}

float3 CompressedUvToViewDirection(float2 uv)
{
    float longitude = 2.0 * PI * uv.x;
    float latitude = DecompressLatitude(uv.y);
    float zenith = 0.5 * PI - latitude;
    
    float z = sin(zenith) * sin(longitude);
    float x = sin(zenith) * cos(longitude);
    float y = cos(zenith);

    return float3(x, y, z);
}

float2 ViewDirectionToCompressedUv(float3 direction)
{
    float zenith = acos(direction.y);
    float latitude = 0.5 * PI - zenith;
    float longitude = atan2(direction.z, direction.x);

    if (abs(direction.x) < 0.01 && abs(direction.z) < 0.01)
    {
        longitude = 0.0;
    }

    if (longitude < 0.0)
    {
        longitude += 2.0 * PI;
    }

    float uvX = longitude / (2.0 * PI);
    float uvY = CompressLatitude(latitude);

    return float2(uvX, uvY);
}

float3 RayleighCoefficient(in AtmosphereData atoms, float h)
{
    float3 sigma0 = float3(5.802, 13.558, 33.1) * 1e-6;
    float scalarH = atoms.rayleigh_scalar_height;
    float rhoH = exp(-h / scalarH);
    
    return sigma0 * rhoH;
}

float3 RayleighPhase(in AtmosphereData atoms, float cosTheta)
{
    return (3.0 / (16.0 * PI)) * (1.0 + cosTheta * cosTheta);
}

float3 MieCoefficient(in AtmosphereData atoms, float h)
{
    float3 sigma0 = float3(3.996, 3.996, 3.996) * 1e-6;
    float scalarH = atoms.mie_scalar_height;
    float rhoH = exp(-h / scalarH);
    
    return sigma0 * rhoH;
}

float3 MiePhase(in AtmosphereData atoms, float cosTheta)
{
    float g = atoms.mie_anisotropy;
    float g2 = g * g;
    float c1 = 3.0 / (8.0 * PI);
    float c2 = (1.0 - g2) / (2.0 + g2);
    float c3 = (1.0 + cosTheta * cosTheta) / pow(abs(1.0 + g2 - 2.0 * g * cosTheta), 1.5);
    
    return c1 * c2 * c3;
}

float3 MieAbsorption(in AtmosphereData atmosphere, float h)
{
    float3 sigmaA0 = float3(4.4, 4.4, 4.4) * 1e-6;
    float scalarH = atmosphere.mie_scalar_height;
    float rhoH = exp(-h / scalarH);
    
    return sigmaA0 * rhoH;
}

float3 OzoneAbsorption(in AtmosphereData atmosphere, float h)
{
    float3 sigmaO0 = float3(0.65, 1.881, 0.085) * 1e-6;
    float rhoH = max(0.0, 1.0 - abs(h - atmosphere.ozone_center) / atmosphere.ozone_width);
    
    return sigmaO0 * rhoH;
}

float3 Scattering(in AtmosphereData atmosphere, float3 p, float3 inDir, float3 outDir)
{
    float cosTheta = dot(inDir, outDir);
    float h = length(p) - atmosphere.planet_radius;
    float3 rayleigh = RayleighCoefficient(atmosphere, h) * RayleighPhase(atmosphere, cosTheta) * atmosphere.rayleigh_scattering_strength;
    float3 mie = MieCoefficient(atmosphere, h) * MiePhase(atmosphere, cosTheta) * atmosphere.mie_scattering_strength;;
    
    return rayleigh + mie;
}

float3 Transmit(in AtmosphereData atmosphere, float3 p0, float3 p1)
{
    float3 dir = normalize(p1 - p0);
    float distance = length(p1 - p0);
    float ds = distance / float(NUM_SAMPLE);
    float3 sum = 0.0;
    float3 p = p0 + (dir * ds) * 0.5;

    for (int i = 0; i < NUM_SAMPLE; i++)
    {
        float h = length(p) - atmosphere.planet_radius;
        float3 scattering = RayleighCoefficient(atmosphere, h) + MieCoefficient(atmosphere, h);
        float3 absorption = OzoneAbsorption(atmosphere, h) + MieAbsorption(atmosphere, h);
        float3 extinction = scattering + absorption;

        sum += extinction * ds;
        p += dir * ds;
    }
    
    return exp(-sum);
}

float3 TransmitToAtmosphereLut(in AtmosphereData atmosphere, float3 p, float3 dir,
    TEXTURE2D(lut), SAMPLER(sampler_name))
{
    
    // float height = length(p) + atmosphere.sea_level - atmosphere.planet_radius;
    float radius = length(p);
    float3 up = normalize(p);
    float cosTheta = dot(up, dir);
    
    float innerRadius = atmosphere.planet_radius;
    float outerRadius = innerRadius + atmosphere.atmosphere_height;
    float2 uv = TransmittanceParametersToUv(float2(radius, cosTheta), innerRadius, outerRadius);
    
    float3 transmittance = lut.SampleLevel(sampler_name, uv, 0).rgb;
    
    return transmittance;
}

float3 SampleMultiScatteringLut(in AtmosphereData atmosphereData, float3 p, float3 lightDir, TEXTURE2D(lut), SAMPLER(lut_sampler))
{
    float h = length(p) - atmosphereData.planet_radius;
    float cosTheta = dot(normalize(p), lightDir);
    float2 uv = float2(cosTheta * 0.5 + 0.5, h / atmosphereData.atmosphere_height);
    float3 gAll = lut.SampleLevel(lut_sampler, uv, 0).rgb;
    float3 sigmaS = RayleighCoefficient(atmosphereData, h) + MieCoefficient(atmosphereData, h);

    return gAll * sigmaS;
}

#endif
