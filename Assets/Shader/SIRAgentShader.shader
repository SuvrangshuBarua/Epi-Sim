Shader "Custom/SIRAgentURP"
{
    Properties
    {
        _SusceptibleColor ("Susceptible Color", Color) = (0.2, 0.8, 0.2, 1)
        _InfectedColor    ("Infected Color",    Color) = (0.9, 0.1, 0.1, 1)
        _RemovedColor     ("Removed Color",     Color) = (0.5, 0.5, 0.5, 1)
        _Smoothness       ("Smoothness",        Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Buffers set from C# ────────────────────────────────────────
            StructuredBuffer<float4x4> _Matrices;   // transform per agent
            StructuredBuffer<int>      _States;     // SIR state per agent

            CBUFFER_START(UnityPerMaterial)
                half4 _SusceptibleColor;
                half4 _InfectedColor;
                half4 _RemovedColor;
                half  _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint   instanceID : SV_InstanceID;  // ← index into buffers
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                int    state       : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Apply per-instance matrix manually
                float4x4 mat = _Matrices[IN.instanceID];
                float3 posWS = mul(mat, float4(IN.positionOS.xyz, 1.0)).xyz;
                float3 nrmWS = normalize(mul((float3x3)mat, IN.normalOS));

                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.positionWS  = posWS;
                OUT.normalWS    = nrmWS;
                OUT.state       = _States[IN.instanceID];

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── Pick color from state ──────────────────────────────────
                half4 baseColor;
                if      (IN.state == 0) baseColor = _SusceptibleColor;
                else if (IN.state == 1) baseColor = _InfectedColor;
                else                   baseColor = _RemovedColor;

                // ── URP Lighting ───────────────────────────────────────────
                float3 normalWS  = normalize(IN.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                Light  mainLight = GetMainLight();
                float  NdotL     = saturate(dot(normalWS, mainLight.direction));
                half3  diffuse   = mainLight.color * NdotL;

                float3 halfDir   = normalize(mainLight.direction + viewDirWS);
                float  NdotH     = saturate(dot(normalWS, halfDir));
                float  specPow   = exp2(_Smoothness * 10.0 + 1.0);
                half3  specular  = mainLight.color * pow(NdotH, specPow) * _Smoothness;

                half3 ambient    = SampleSH(normalWS);
                half3 finalColor = baseColor.rgb * (ambient + diffuse) + specular;

                return half4(finalColor, 1.0);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            StructuredBuffer<float4x4> _Matrices;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;
            

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float4x4 mat  = _Matrices[IN.instanceID];
                float3 posWS  = mul(mat, float4(IN.positionOS.xyz, 1.0)).xyz;
                float3 nrmWS  = normalize(mul((float3x3)mat, IN.normalOS));

                // Apply shadow bias properly
                float3 positionWS = ApplyShadowBias(posWS, nrmWS, _LightDirection);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                
                return OUT;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}