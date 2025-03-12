Shader "Unlit/RGBD"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos: SV_POSITION;
                float4 color: COLOR0;
            };

            struct lidardata
            {
                float3 position; // 12byte
                float3 rgb;      // 12byte
            };

            StructuredBuffer<lidardata> _LidarData;

            uniform float _PointSize;
            uniform float4x4 _ObjectToWorld;

            v2f vert (uint vertexID: SV_VertexID)
            {
                v2f o;
                float3 pos = _LidarData[vertexID].position;
                float4 wpos = mul(_ObjectToWorld, float4(pos, 1.0f));
                o.pos = mul(UNITY_MATRIX_VP, wpos);
                o.color = float4(_LidarData[vertexID].rgb, 1.0f);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}