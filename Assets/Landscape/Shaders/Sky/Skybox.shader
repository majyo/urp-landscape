Shader "Custom/Skybox"
{
    Properties
    {
        _StarExposure ("Star Exposure", Range(-16, 16)) = 0
        _StarPower ("Star Power", Range(1, 5)) = 1
        [NoScaleOffset] _StarCubeMap ("Star Cube Texture", Cube) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest LEqual

        Tags
        {
            "RenderType" = "background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "background"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Landscape/ShaderLibrary/Sky/Scatterings.hlsl"

            #define SUN_COLOR_MAGNITUDE 0.75
            #define VISIBLE_VIEW_COS_MULTIPLIER 1.001
            #define SKY_EPSILON 1e-5f
            #define POLE_THRESHOLD 0.9995f

            struct Attributes
            {
                float3 position_os : POSITION;
                float2 base_uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position_cs : SV_POSITION;
                float3 position_ws : TEXCOORD0;
                float2 base_uv : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            float4 _BaseMap_ST;

            TEXTURE2D(_TransmittanceLUT);
            TEXTURE2D(_MultiScatteringLUT);
            SAMPLER(linear_clamp_sampler);

            TEXTURE2D(_SkyViewLUT);
            SAMPLER(sky_view_linear_repeat_sampler);

            TEXTURECUBE(_StarCubeMap);
            SAMPLER(sampler_StarCubeMap);

            float _StarExposure;
            float _StarPower;

            float4x4 _InverseSunRotationMatrix;

            Varyings SkyVertex(Attributes attributes)
            {
                Varyings o;
                o.position_cs = TransformObjectToHClip(attributes.position_os);
                o.position_ws = TransformObjectToWorld(attributes.position_os);
                o.base_uv = TRANSFORM_TEX(attributes.base_uv, _BaseMap);

                return o;
            }

            float GetSunVisibility(float sunDiskAngle, float3 viewDir, float3 lightDir)
            {
                const float cosTheta = dot(viewDir, lightDir);
                const float viewAngle = acos(cosTheta) * 180.0 / PI;
                const float sunDiskAngleInner = sunDiskAngle * 0.5;
                return 1.0 - smoothstep(sunDiskAngleInner, sunDiskAngle, viewAngle);
            }

            float3 GetSunDisk(in AtmosphereData atmosphere, float3 viewPos, float3 viewDir, float3 lightDir,
                              float sunVisibility)
            {
                const float3 sunLuminance = atmosphere.sun_light_color * atmosphere.sun_light_intensity;

                Ray sunLightRay;
                sunLightRay.origin = viewPos;
                sunLightRay.direction = viewDir;
                const float distanceToPlanet = RayHitSphereClosest(sunLightRay, float3(0.0, 0.0, 0.0),
                    atmosphere.planet_radius);

                if (distanceToPlanet >= 0)
                {
                    return float3(0.0, 0.0, 0.0);
                }

                const float distanceToAtmosphere = RayHitSphereClosest(sunLightRay, float3(0.0, 0.0, 0.0),
                    atmosphere.atmosphere_height + atmosphere.planet_radius);

                float3 absorption = 1.0;

                if (distanceToAtmosphere >= 0)
                {
                    absorption = TransmitToAtmosphereLut(atmosphere, viewPos, lightDir, _TransmittanceLUT,
                                                                        linear_clamp_sampler);
                }

                float3 sunColor = sunLuminance * absorption * SUN_COLOR_MAGNITUDE;
                sunColor = saturate(sunColor);
                sunColor *= sunVisibility;

                return sunColor;
            }

            float3 GetSkyColor(float3 viewDir)
            {
                // float2 uv = ViewDirectionToUV(view_dir);
                float2 uv = ViewDirectionToCompressedUv(viewDir);
                return _SkyViewLUT.SampleLevel(linear_clamp_sampler, uv, 0).rgb;
            }

            float3 GetNightSkyColor(const float3 viewDir, const float sunViewDot, const float sunZenithDot,
                const float sunVisibility)
            {
                const float3 sampleDir = normalize(mul((float3x3) _InverseSunRotationMatrix, viewDir));

                const float3 color = SAMPLE_TEXTURECUBE(_StarCubeMap, sampler_StarCubeMap, sampleDir).rgb;
                const float3 powColor = pow(abs(color), _StarPower);
                const float strength = (1 - sunViewDot) * saturate(-sunZenithDot);
                return powColor * exp2(_StarExposure) * strength * saturate(1 - sunVisibility);
            }

            float4 SkyFragment(Varyings varyings) : SV_Target
            {
                const AtmosphereData atmosphere = GetAtmosphereData();

                float3 color = (float3)0;
                const float3 viewDir = normalize(varyings.position_ws);

                const Light mainLight = GetMainLight();
                const float3 lightDir = mainLight.direction;

                const float cameraPos = max(_WorldSpaceCameraPos.y, MIN_HEIGHT);
                const float h = cameraPos - atmosphere.sea_level + atmosphere.planet_radius;
                const float3 viewPos = float3(0.0, h, 0.0);

                float sunVisibility = GetSunVisibility(atmosphere.sun_disk_angle, viewDir, lightDir);
                float sunViewDot = dot(viewDir, lightDir);
                float sunZenithDot = dot(lightDir, float3(0.0, 1.0, 0.0));

                color += GetSkyColor(viewDir);
                color += GetSunDisk(atmosphere, viewPos, viewDir, lightDir, sunVisibility);
                color += GetNightSkyColor(viewDir, sunViewDot, sunZenithDot, sunVisibility);

                color = saturate(color);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}