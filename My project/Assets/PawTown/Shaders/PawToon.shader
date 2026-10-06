// PawTown cel shader for URP (Unity 6). Flat palette color, soft 2-tone light/shadow, receives + casts shadows.
Shader "PawTown/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.64, 0.82, 1)
        _RampThreshold ("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmooth ("Ramp Smoothness", Range(0.001, 0.5)) = 0.05
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _Fade ("Fade (camera occlusion)", Range(0, 1)) = 1
        // fur patterns (cat only): 0 none, 1 calico patches, 2 tortie mottle, 3 galaxy, 4 colour points (siamese)
        _PatMode ("Pattern Mode", Float) = 0
        _PatColA ("Pattern Colour A", Color) = (1, 0.6, 0.2, 1)
        _PatColB ("Pattern Colour B", Color) = (0.2, 0.2, 0.2, 1)
        _PatScale ("Pattern Scale", Float) = 4
        _PatCenter ("Pattern Centre (rest pose)", Vector) = (0, 0, 0, 0)
        _PatRadius ("Pattern Radius", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            half _RampThreshold;
            half _RampSmooth;
            half4 _EmissionColor;
            half _Fade;
            half _PatMode;
            half4 _PatColA;
            half4 _PatColB;
            float _PatScale;
            float4 _PatCenter;
            float _PatRadius;
        CBUFFER_END

        // value noise in 3D (rest-pose object space, so the pattern sticks to the fur while she moves)
        float PatHash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
        float PatNoise(float3 x)
        {
            float3 i = floor(x), f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(PatHash(i), PatHash(i + float3(1, 0, 0)), f.x), lerp(PatHash(i + float3(0, 1, 0)), PatHash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(PatHash(i + float3(0, 0, 1)), PatHash(i + float3(1, 0, 1)), f.x), lerp(PatHash(i + float3(0, 1, 1)), PatHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }
        float PatFbm(float3 p) { return PatNoise(p) * 0.6 + PatNoise(p * 2.03 + 7.1) * 0.28 + PatNoise(p * 4.1 + 3.3) * 0.12; }

        // base colour with the pattern applied; 'glow' returns unlit sparkle (galaxy stars)
        half3 FurPattern(half3 baseCol, float3 rest, out half3 glow)
        {
            glow = 0;
            if (_PatMode < 0.5) return baseCol;
            float3 p = rest * _PatScale;
            if (_PatMode < 1.5)          // calico: big soft-edged orange and dark patches on a pale coat
            {
                float3 q = p * 0.7;      // bigger patches, like the icon
                half a = smoothstep(0.53, 0.57, PatFbm(q));
                half b = smoothstep(0.58, 0.62, PatFbm(q + 31.7)) * (1 - a);
                return lerp(lerp(baseCol, _PatColA.rgb, a), _PatColB.rgb, b);
            }
            if (_PatMode < 2.5)          // tortoiseshell: dark coat mottled with orange and golden flecks
            {
                half a = smoothstep(0.45, 0.55, PatFbm(p * 1.4));
                half b = smoothstep(0.68, 0.72, PatNoise(p * 4.0 + 11.0));
                return lerp(lerp(baseCol, _PatColA.rgb, a), _PatColB.rgb, b * 0.8);
            }
            if (_PatMode < 3.5)          // galaxy: nebula clouds + little stars that glow in the shade
            {
                half neb = smoothstep(0.38, 0.66, PatFbm(p * 0.9 + 5.0));
                float3 cell = floor(p * 4.5), fr = frac(p * 4.5) - 0.5;
                half star = step(0.8, PatHash(cell)) * smoothstep(0.26, 0.1, length(fr));
                glow = _PatColB.rgb * star * 1.4;
                return lerp(lerp(baseCol, _PatColA.rgb, neb * 0.9), _PatColB.rgb, star);
            }
            // colour points: ears, face, paws and tail (the parts far from the body centre) go dark
            half d = length((rest - _PatCenter.xyz) / _PatRadius);
            half pt = smoothstep(0.62, 0.95, d + (PatNoise(p * 2.0) - 0.5) * 0.12);
            return lerp(baseCol, _PatColA.rgb, pt);
        }

        // 4x4 ordered dither: _Fade < 1 dissolves the surface into a fine screen-space pattern (cheap "see-through")
        void DitherClip(float2 pixel)
        {
            if (_Fade >= 0.999h) return;
            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            uint2 q = (uint2)pixel % 4u;
            clip(_Fade - (bayer[q.y * 4 + q.x] + 0.5) / 16.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 restOS : TEXCOORD3;        // rest-pose position (set on the cat's meshes only)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float3 restOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                o.restOS = v.restOS;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                DitherClip(i.positionCS.xy);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(normalize(i.normalWS), l.direction) * 0.5h + 0.5h;
                half lit = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, ndl);
                lit *= lerp(1.0h, l.shadowAttenuation, 0.85h);
                half3 glow;
                half3 baseCol = FurPattern(_BaseColor.rgb, i.restOS, glow);
                half3 col = baseCol * lerp(_ShadowColor.rgb, half3(1, 1, 1), lit);
                col += _EmissionColor.rgb + glow;
                // per-pixel fog: huge meshes (far ground, mountains) would fog wrongly if interpolated per vertex
                col = MixFog(col, ComputeFogFactor(TransformWorldToHClip(i.positionWS).z));
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - posWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert (Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag (Varyings i) : SV_Target { DitherClip(i.positionCS.xy); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert (Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag (Varyings i) : SV_Target { DitherClip(i.positionCS.xy); return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
