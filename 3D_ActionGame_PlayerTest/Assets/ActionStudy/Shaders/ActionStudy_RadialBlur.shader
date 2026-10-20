// HDRP Custom Pass (FullScreen) 用のラジアルブラー。
// HDRP の Volume にはラジアルブラーが無いため Custom Pass で実装している。
// 着弾点(_Center: スクリーン UV)を中心に放射状に色をサンプリングし、
// 「衝撃で空気が震えた」ような一瞬のブレを表現する。中心付近はブレを弱めて着弾点を見失わないようにする。
Shader "Hidden/ActionStudy/RadialBlur"
{
    Properties
    {
        _Strength ("Strength", Float) = 0
        _Center ("Center (Screen UV)", Vector) = (0.5, 0.5, 0, 0)
    }

    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _Strength;
    float4 _Center;

    #define SAMPLE_COUNT 16

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float2 uv = posInput.positionNDC.xy;

        float2 toPixel = uv - _Center.xy;
        // 中心から離れるほど強くブラーをかける
        float falloff = saturate(length(toPixel) * 2.5);

        float3 accum = 0;
        float weightSum = 0;
        [unroll]
        for (int i = 0; i < SAMPLE_COUNT; i++)
        {
            float t = i / (float)(SAMPLE_COUNT - 1);
            float scale = 1.0 - _Strength * falloff * t;
            float weight = 1.0 - t * 0.6;
            accum += CustomPassSampleCameraColor(_Center.xy + toPixel * scale, 0) * weight;
            weightSum += weight;
        }
        return float4(accum / weightSum, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
}
