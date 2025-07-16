Shader "Hidden/CatDarkGame/FullScreen/GrayScale"
{
    Properties{}
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
		ZTest Always ZWrite Off Cull Off Blend Off

		HLSLINCLUDE
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            TEXTURE2D_X(_BlitTexture);
            FRAMEBUFFER_INPUT_HALF(0);
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings FullscreenVertShared(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);

                output.positionCS = pos;
                output.texcoord   = uv;

                return output;
            }
        ENDHLSL

        Pass    // 0
        {
            Name "GrayScale CopyColor"
           
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag

                Varyings Vert(Attributes input)
                {
                    return FullscreenVertShared(input);
                }

                half4 Frag(Varyings input) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                    half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord.xy, 0.0);
                    return color;
                }
            ENDHLSL
        }

        Pass    // 1
        {
            Name "GrayScale"
           
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag

                Varyings Vert(Attributes input)
                {
                    return FullscreenVertShared(input);
                }

                half4 Frag(Varyings input) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                    half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord.xy, 0.0);
                    half3 grayScaleColor = dot(color.rgb, half3(0.3, 0.59, 0.11));
                    return half4(grayScaleColor.rgb, 1.0);
                }
            ENDHLSL
        }

        Pass    // 2
        {
            Name "[FB] GrayScale CopyColor"
           
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag

                Varyings Vert(Attributes input)
                {
                    return FullscreenVertShared(input);
                }

                half4 Frag(Varyings input) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                    half4 color = LOAD_FRAMEBUFFER_INPUT(0, input.positionCS.xy);
                    return color;
                }
            ENDHLSL
        }

        Pass    // 3
        {
            Name "[FB] GrayScale"
           
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag

                Varyings Vert(Attributes input)
                {
                    return FullscreenVertShared(input);
                }

                half4 Frag(Varyings input) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                    half4 color = LOAD_FRAMEBUFFER_INPUT(0, input.positionCS.xy);
                    half3 grayScaleColor = dot(color.rgb, half3(0.3, 0.59, 0.11));
                    return half4(grayScaleColor.rgb, 1.0);
                }
            ENDHLSL
        }
    }
}