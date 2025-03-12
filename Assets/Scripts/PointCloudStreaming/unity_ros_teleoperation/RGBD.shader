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

            StructuredBuffer<float3> _Positions;
            StructuredBuffer<lidardata> _LidarData;
            uniform uint _BaseVertexIndex;
            uniform float _PointSize;
            uniform float4x4 _ObjectToWorld;

            v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float3 centerPos = _LidarData[instanceID].position;
                float3 vertexOffset = _Positions[_BaseVertexIndex + vertexID];

                // 월드 공간 중심 위치
                float4 worldPos = mul(_ObjectToWorld, float4(centerPos, 1.0f));

                // 카메라 방향 계산 (빌보드)
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
                float3 upDir = float3(0, 1, 0); // 월드 Y축을 기본 업 벡터로 사용
                float3 rightDir = normalize(cross(upDir, viewDir));
                upDir = normalize(cross(viewDir, rightDir)); // 직교화된 업 벡터

                // 사각형 정점을 카메라를 향하도록 변환
                float3 scaledOffset = (rightDir * vertexOffset.x + upDir * vertexOffset.y) * _PointSize;
                worldPos.xyz += scaledOffset;

                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.color = float4(_LidarData[instanceID].rgb, 1.0f);
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