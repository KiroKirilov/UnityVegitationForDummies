Shader "DaisyParty/VegetationWind"
{
    Properties
    {
        [Header(Surface Options)]
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        [Header(Wind Settings)]
        _WindSpeed("Wind Speed", Range(0.0, 5.0)) = 1.0
        _WindStrength("Wind Strength", Range(0.0, 0.5)) = 0.05
        _WindDirection("Wind Direction (XZ)", Vector) = (1.0, 0.3, 0, 0)
        _NoiseScale("Noise Scale", Range(0.0, 2.0)) = 0.5
        _WindFrequency("Wind Frequency", Range(0.0, 5.0)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
            "DisableBatching" = "True"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Smoothness;
                float _Metallic;
                float _WindSpeed;
                float _WindStrength;
                float4 _WindDirection;
                float _NoiseScale;
                float _WindFrequency;
            CBUFFER_END

            // Global wind properties (set by WindController)
            float _GlobalWindSpeed;
            float _GlobalWindStrength;
            float4 _GlobalWindDirection;
            float _GlobalNoiseScale;

            // Global player interaction properties (set by WindController)
            float3 _PlayerInteractionPos;
            float3 _PlayerInteractionVelocity;
            float _InteractionRadius;
            float _InteractionStrength;

            // Rotation-based wind bending - grass bends from the base, not slides
            float3 ApplyWindBend(float3 positionOS, float3 worldPos)
            {
                // Use global values if set, otherwise use material values
                float windSpeed = _GlobalWindSpeed > 0 ? _GlobalWindSpeed : _WindSpeed;
                float windStrength = _GlobalWindStrength > 0 ? _GlobalWindStrength : _WindStrength;
                float2 windDir = _GlobalWindDirection.x != 0 || _GlobalWindDirection.y != 0
                    ? _GlobalWindDirection.xy : _WindDirection.xy;
                float noiseScale = _GlobalNoiseScale > 0 ? _GlobalNoiseScale : _NoiseScale;

                // Normalize wind direction
                float windDirLen = length(windDir);
                windDir = windDirLen > 0.001 ? windDir / windDirLen : float2(1, 0);

                // Time-based oscillation with per-object variation from world position
                float phaseOffset = (worldPos.x + worldPos.z) * noiseScale;
                float timeVal = _Time.y * windSpeed * _WindFrequency + phaseOffset;

                // Primary sway (slow, large movement)
                float primaryWave = sin(timeVal);

                // Secondary wiggle (faster, smaller movement)
                float secondaryWave = sin(timeVal * 2.7 + worldPos.x * 0.5) * 0.3;

                // Combined wave creates the bend factor
                float bendFactor = (primaryWave + secondaryWave) * windStrength;

                // Height in object space - vertices at Y=0 (base) don't move
                // Higher vertices bend more (natural grass bending)
                float height = max(0, positionOS.y);

                // Bend amount increases with height (approximates rotation for small angles)
                float bendAmount = bendFactor * height;

                // Apply bend in the wind direction
                float3 bentPos = positionOS;
                bentPos.x += bendAmount * windDir.x;
                bentPos.z += bendAmount * windDir.y;

                // Slight Y compression to maintain approximate blade length
                bentPos.y -= abs(bendAmount) * 0.1 * height;

                return bentPos;
            }

            float3 ApplyPlayerInteraction(float3 bentPos, float3 positionOS, float3 worldPos)
            {
                if (_InteractionStrength <= 0 || _InteractionRadius <= 0)
                    return bentPos;

                float2 toGrass = worldPos.xz - _PlayerInteractionPos.xz;
                float dist = length(toGrass);

                float falloff = 1.0 - smoothstep(0, _InteractionRadius, dist);
                if (falloff <= 0)
                    return bentPos;

                float2 pushDir = dist > 0.001 ? toGrass / dist : float2(1, 0);

                float2 velXZ = _PlayerInteractionVelocity.xz;
                float velMag = length(velXZ);
                if (velMag > 0.5)
                {
                    float2 velDir = velXZ / velMag;
                    pushDir = normalize(pushDir + velDir * 0.5);
                }

                float height = max(0, positionOS.y);
                float bendAmount = falloff * _InteractionStrength * height;

                bentPos.x += bendAmount * pushDir.x;
                bentPos.z += bendAmount * pushDir.y;

                return bentPos;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Get original world position for phase variation (per-object offset)
                float3 worldPosOriginal = TransformObjectToWorld(input.positionOS.xyz);

                // Apply wind bend in OBJECT SPACE - base stays fixed, tips bend
                float3 bentPositionOS = ApplyWindBend(input.positionOS.xyz, worldPosOriginal);

                // Apply player interaction (additive to wind)
                bentPositionOS = ApplyPlayerInteraction(bentPositionOS, input.positionOS.xyz, worldPosOriginal);

                // Transform bent position to world/clip space
                float3 worldPos = TransformObjectToWorld(bentPositionOS);
                output.positionCS = TransformWorldToHClip(worldPos);
                output.positionWS = worldPos;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Sample base texture
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Alpha test
                clip(albedo.a - _Cutoff);

                // Setup surface data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = albedo.a;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1.0;
                surfaceData.emission = 0;

                // Setup input data for lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = 1;

                // Calculate lighting
                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Smoothness;
                float _Metallic;
                float _WindSpeed;
                float _WindStrength;
                float4 _WindDirection;
                float _NoiseScale;
                float _WindFrequency;
            CBUFFER_END

            float _GlobalWindSpeed;
            float _GlobalWindStrength;
            float4 _GlobalWindDirection;
            float _GlobalNoiseScale;

            float3 _PlayerInteractionPos;
            float3 _PlayerInteractionVelocity;
            float _InteractionRadius;
            float _InteractionStrength;

            float3 _LightDirection;

            float3 ApplyWindBendShadow(float3 positionOS, float3 worldPos)
            {
                float windSpeed = _GlobalWindSpeed > 0 ? _GlobalWindSpeed : _WindSpeed;
                float windStrength = _GlobalWindStrength > 0 ? _GlobalWindStrength : _WindStrength;
                float2 windDir = _GlobalWindDirection.x != 0 || _GlobalWindDirection.y != 0
                    ? _GlobalWindDirection.xy : _WindDirection.xy;
                float noiseScale = _GlobalNoiseScale > 0 ? _GlobalNoiseScale : _NoiseScale;

                float windDirLen = length(windDir);
                windDir = windDirLen > 0.001 ? windDir / windDirLen : float2(1, 0);

                float phaseOffset = (worldPos.x + worldPos.z) * noiseScale;
                float timeVal = _Time.y * windSpeed * _WindFrequency + phaseOffset;

                float primaryWave = sin(timeVal);
                float secondaryWave = sin(timeVal * 2.7 + worldPos.x * 0.5) * 0.3;
                float bendFactor = (primaryWave + secondaryWave) * windStrength;

                float height = max(0, positionOS.y);
                float bendAmount = bendFactor * height;

                float3 bentPos = positionOS;
                bentPos.x += bendAmount * windDir.x;
                bentPos.z += bendAmount * windDir.y;
                bentPos.y -= abs(bendAmount) * 0.1 * height;

                return bentPos;
            }

            float3 ApplyPlayerInteractionShadow(float3 bentPos, float3 positionOS, float3 worldPos)
            {
                if (_InteractionStrength <= 0 || _InteractionRadius <= 0)
                    return bentPos;

                float2 toGrass = worldPos.xz - _PlayerInteractionPos.xz;
                float dist = length(toGrass);

                float falloff = 1.0 - smoothstep(0, _InteractionRadius, dist);
                if (falloff <= 0)
                    return bentPos;

                float2 pushDir = dist > 0.001 ? toGrass / dist : float2(1, 0);

                float2 velXZ = _PlayerInteractionVelocity.xz;
                float velMag = length(velXZ);
                if (velMag > 0.5)
                {
                    float2 velDir = velXZ / velMag;
                    pushDir = normalize(pushDir + velDir * 0.5);
                }

                float height = max(0, positionOS.y);
                float bendAmount = falloff * _InteractionStrength * height;

                bentPos.x += bendAmount * pushDir.x;
                bentPos.z += bendAmount * pushDir.y;

                return bentPos;
            }

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPosOriginal = TransformObjectToWorld(input.positionOS.xyz);
                float3 bentPositionOS = ApplyWindBendShadow(input.positionOS.xyz, worldPosOriginal);
                bentPositionOS = ApplyPlayerInteractionShadow(bentPositionOS, input.positionOS.xyz, worldPosOriginal);
                float3 worldPos = TransformObjectToWorld(bentPositionOS);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_TARGET
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedo.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Smoothness;
                float _Metallic;
                float _WindSpeed;
                float _WindStrength;
                float4 _WindDirection;
                float _NoiseScale;
                float _WindFrequency;
            CBUFFER_END

            float _GlobalWindSpeed;
            float _GlobalWindStrength;
            float4 _GlobalWindDirection;
            float _GlobalNoiseScale;

            float3 _PlayerInteractionPos;
            float3 _PlayerInteractionVelocity;
            float _InteractionRadius;
            float _InteractionStrength;

            float3 ApplyWindBendDepth(float3 positionOS, float3 worldPos)
            {
                float windSpeed = _GlobalWindSpeed > 0 ? _GlobalWindSpeed : _WindSpeed;
                float windStrength = _GlobalWindStrength > 0 ? _GlobalWindStrength : _WindStrength;
                float2 windDir = _GlobalWindDirection.x != 0 || _GlobalWindDirection.y != 0
                    ? _GlobalWindDirection.xy : _WindDirection.xy;
                float noiseScale = _GlobalNoiseScale > 0 ? _GlobalNoiseScale : _NoiseScale;

                float windDirLen = length(windDir);
                windDir = windDirLen > 0.001 ? windDir / windDirLen : float2(1, 0);

                float phaseOffset = (worldPos.x + worldPos.z) * noiseScale;
                float timeVal = _Time.y * windSpeed * _WindFrequency + phaseOffset;

                float primaryWave = sin(timeVal);
                float secondaryWave = sin(timeVal * 2.7 + worldPos.x * 0.5) * 0.3;
                float bendFactor = (primaryWave + secondaryWave) * windStrength;

                float height = max(0, positionOS.y);
                float bendAmount = bendFactor * height;

                float3 bentPos = positionOS;
                bentPos.x += bendAmount * windDir.x;
                bentPos.z += bendAmount * windDir.y;
                bentPos.y -= abs(bendAmount) * 0.1 * height;

                return bentPos;
            }

            float3 ApplyPlayerInteractionDepth(float3 bentPos, float3 positionOS, float3 worldPos)
            {
                if (_InteractionStrength <= 0 || _InteractionRadius <= 0)
                    return bentPos;

                float2 toGrass = worldPos.xz - _PlayerInteractionPos.xz;
                float dist = length(toGrass);

                float falloff = 1.0 - smoothstep(0, _InteractionRadius, dist);
                if (falloff <= 0)
                    return bentPos;

                float2 pushDir = dist > 0.001 ? toGrass / dist : float2(1, 0);

                float2 velXZ = _PlayerInteractionVelocity.xz;
                float velMag = length(velXZ);
                if (velMag > 0.5)
                {
                    float2 velDir = velXZ / velMag;
                    pushDir = normalize(pushDir + velDir * 0.5);
                }

                float height = max(0, positionOS.y);
                float bendAmount = falloff * _InteractionStrength * height;

                bentPos.x += bendAmount * pushDir.x;
                bentPos.z += bendAmount * pushDir.y;

                return bentPos;
            }

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPosOriginal = TransformObjectToWorld(input.positionOS.xyz);
                float3 bentPositionOS = ApplyWindBendDepth(input.positionOS.xyz, worldPosOriginal);
                bentPositionOS = ApplyPlayerInteractionDepth(bentPositionOS, input.positionOS.xyz, worldPosOriginal);
                float3 worldPos = TransformObjectToWorld(bentPositionOS);

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_TARGET
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedo.a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
