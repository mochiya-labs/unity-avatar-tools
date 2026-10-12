Shader "Hidden/Mochiya/CubemapExport"
{
    Properties { _Cube ("Source", Cube) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            samplerCUBE _Cube;
            float4 _Cube_HDR;
            float3 _Axis;
            float _Face;
            float _Mip;
            float4 frag(v2f_img input) : SV_Target
            {
                // OpenGL cube addressing, rows in increasing texture t order.
                float2 p = input.uv * 2.0 - 1.0;
                float3 direction;
                if (_Face < 0.5) direction = float3(1, -p.y, -p.x);
                else if (_Face < 1.5) direction = float3(-1, -p.y, p.x);
                else if (_Face < 2.5) direction = float3(p.x, 1, p.y);
                else if (_Face < 3.5) direction = float3(p.x, -1, -p.y);
                else if (_Face < 4.5) direction = float3(p.x, -p.y, 1);
                else direction = float3(-p.x, -p.y, -1);
                float4 sample = texCUBElod(_Cube, float4(direction * _Axis, _Mip));
                float3 color = DecodeHDR(sample, _Cube_HDR);
                #ifdef UNITY_COLORSPACE_GAMMA
                    // The fast polynomial only approximates [0,1]; HDR needs the exact curve.
                    color = float3(GammaToLinearSpaceExact(color.r), GammaToLinearSpaceExact(color.g), GammaToLinearSpaceExact(color.b));
                #endif
                return float4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
