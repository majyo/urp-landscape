#ifndef SKY_ATMOSPHERE_INPUTS_INCLUDE
#define SKY_ATMOSPHERE_INPUTS_INCLUDE

struct AtmosphereData
{
    float rayleigh_scalar_height;
    float rayleigh_scattering_strength;
    float mie_scalar_height;
    float mie_anisotropy;
    float mie_scattering_strength;
    float ozone_center;
    float ozone_width;
    float planet_radius;
    float atmosphere_height;
    float3 sun_light_color;
    float sun_light_intensity;
    float sun_disk_angle;
    float sea_level;
    float3 ground_tint;
};

float _RayleighScalarHeight;
float _RayleighScatteringStrength;
float _MieScalarHeight;
float _MieAnisotropy;
float _MieScatteringStrength;
float _OzoneCenter;
float _OzoneWidth;
float _PlanetRadius;
float _AtmosphereHeight;
float3 _SunLightColor;
float _SunLightIntensity;
float _SunDiskAngle;
float _SeaLevel;

float3 _GroundTint;

AtmosphereData GetAtmosphereData()
{
    AtmosphereData data;

    data.rayleigh_scalar_height = _RayleighScalarHeight;
    data.rayleigh_scattering_strength = _RayleighScatteringStrength;
    data.mie_scalar_height = _MieScalarHeight;
    data.mie_anisotropy = _MieAnisotropy;
    data.mie_scattering_strength = _MieScatteringStrength;
    data.ozone_center = _OzoneCenter;
    data.ozone_width = _OzoneWidth;
    data.planet_radius = _PlanetRadius;
    data.atmosphere_height = _AtmosphereHeight;
    data.sun_light_color = _SunLightColor;
    data.sun_light_intensity = _SunLightIntensity;
    data.sun_disk_angle = _SunDiskAngle;
    data.sea_level = _SeaLevel;
    data.ground_tint = _GroundTint;

    return data;
}

#endif
