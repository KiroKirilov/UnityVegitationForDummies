Shader "DaisyParty/VegetationWindIndirect"
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
        }

        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct GrassInstance
        {
            float3 position;
            float4 rotation;
            float scale;
        };

        StructuredBuffer<GrassInstance> _AllInstances;
        StructuredBuffer<uint> _VisibleIndices;

        float4x4 BuildTRS(float3 pos, float4 q, float s)
        {
            float x2 = q.x * 2.0; float y2 = q.y * 2.0; float z2 = q.z * 2.0;
            float xx = q.x * x2; float yy = q.y * y2; float zz = q.z * z2;
            float xy = q.x * y2; float xz = q.x * z2; float yz = q.y * z2;
            float wx = q.w * x2; float wy = q.w * y2; float wz = q.w * z2;

            float4x4 m;
            m[0] = float4((1.0 - (yy + zz)) * s, (xy + wz) * s, (xz - wy) * s, pos.x);
            m[1] = float4((xy - wz) * s, (1.0 - (xx + zz)) * s, (yz + wx) * s, pos.y);
            m[2] = float4((xz + wy) * s, (yz - wx) * s, (1.0 - (xx + yy)) * s, pos.z);
            m[3] = float4(0, 0, 0, 1);
            return m;
        }

        float3 InstanceTransformPoint(float4x4 trs, float3 posOS)
        {
            return mul(trs, float4(posOS, 1.0)).xyz;
        }

        float3 InstanceTransformNormal(float4x4 trs, float3 normalOS)
        {
            return normalize(mul((float3x3)trs, normalOS));
        }

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

        float3 ApplyWindBendIndirect(float3 positionOS, float3 worldPos)
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

        float3 ApplyPlayerInteractionIndirect(float3 bentPos, float3 positionOS, float3 worldPos)
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
        ENDHLSL

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;

                uint visIdx = _VisibleIndices[input.instanceID];
                GrassInstance gi = _AllInstances[visIdx];
                float4x4 trs = BuildTRS(gi.position, gi.rotation, gi.scale);

                float3 worldPosOriginal = InstanceTransformPoint(trs, input.positionOS.xyz);

                float3 bentPositionOS = ApplyWindBendIndirect(input.positionOS.xyz, worldPosOriginal);
                bentPositionOS = ApplyPlayerInteractionIndirect(bentPositionOS, input.positionOS.xyz, worldPosOriginal);

                float3 worldPos = InstanceTransformPoint(trs, bentPositionOS);
                output.positionCS = TransformWorldToHClip(worldPos);
                output.positionWS = worldPos;
                output.normalWS = InstanceTransformNormal(trs, input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                half3 normalWS = normalize(input.normalWS);

                // Manual lighting — UniversalFragmentPBR can't be used with indirect rendering
                // because unity_LightData.z (UnityPerDraw CBUFFER) isn't populated, causing
                // GetMainLight().distanceAttenuation = 0 which kills all direct lighting.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half shadowAtten = MainLightRealtimeShadow(shadowCoord);

                half3 lightDir = half3(_MainLightPosition.xyz);
                half3 lightColor = _MainLightColor.rgb;

                half NdotL = saturate(dot(normalWS, lightDir));
                half3 diffuse = albedo.rgb * lightColor * NdotL * shadowAtten;

                half3 ambient = albedo.rgb * SampleSH(normalWS);

                half3 finalColor = MixFog(diffuse + ambient, input.fogFactor);

                return half4(finalColor, albedo.a);
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float3 _LightDirection;

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;

                uint visIdx = _VisibleIndices[input.instanceID];
                GrassInstance gi = _AllInstances[visIdx];
                float4x4 trs = BuildTRS(gi.position, gi.rotation, gi.scale);

                float3 worldPosOriginal = InstanceTransformPoint(trs, input.positionOS.xyz);
                float3 bentPositionOS = ApplyWindBendIndirect(input.positionOS.xyz, worldPosOriginal);
                bentPositionOS = ApplyPlayerInteractionIndirect(bentPositionOS, input.positionOS.xyz, worldPosOriginal);
                float3 worldPos = InstanceTransformPoint(trs, bentPositionOS);

                float3 normalWS = InstanceTransformNormal(trs, input.normalOS);
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

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;

                uint visIdx = _VisibleIndices[input.instanceID];
                GrassInstance gi = _AllInstances[visIdx];
                float4x4 trs = BuildTRS(gi.position, gi.rotation, gi.scale);

                float3 worldPosOriginal = InstanceTransformPoint(trs, input.positionOS.xyz);
                float3 bentPositionOS = ApplyWindBendIndirect(input.positionOS.xyz, worldPosOriginal);
                bentPositionOS = ApplyPlayerInteractionIndirect(bentPositionOS, input.positionOS.xyz, worldPosOriginal);
                float3 worldPos = InstanceTransformPoint(trs, bentPositionOS);

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

    Fallback Off
}
