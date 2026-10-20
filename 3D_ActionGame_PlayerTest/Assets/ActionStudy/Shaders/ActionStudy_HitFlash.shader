// ダメージフラッシュ用のオーバーレイシェーダー。
// 敵の Renderer に 2 枚目のマテリアルとして重ねて描画し、_FlashIntensity を
// MaterialPropertyBlock で上下させて「被弾した瞬間に白く光る」表現を作る。
// 輪郭(リム)をより強く光らせることで、ベタ塗りの白よりも立体感のある発光にしている。
Shader "ActionStudy/HitFlash"
{
    Properties
    {
        [HDR] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashIntensity ("Flash Intensity", Float) = 0
        _RimPower ("Rim Power", Float) = 2
        _RimBoost ("Rim Boost", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FlashColor;
                float _FlashIntensity;
                float _RimPower;
                float _RimBoost;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionRWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionRWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionRWS);
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                float3 color = _FlashColor.rgb * _FlashIntensity * (1.0 + rim * _RimBoost);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
