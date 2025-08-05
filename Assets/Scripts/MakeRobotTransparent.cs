using UnityEngine;

public class MakeRobotTransparent : MonoBehaviour
{
    [Range(0f, 1f)]
    public float transparency = 0.5f;

    void Start()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer renderer in renderers)
        {
            // 기본 색상 지정 (혹시 머티리얼에 색상 정보가 없을 때 대비)
            Color baseColor = Color.gray;
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                baseColor = renderer.sharedMaterial.color;

            baseColor.a = transparency;

            // 새 머티리얼 생성 및 Shader 설정
            Material transparentMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            transparentMat.SetFloat("_Surface", 1); // Transparent
            transparentMat.SetFloat("_Blend", 0); // Alpha
            transparentMat.SetFloat("_Mode", 2); // Fade 모드
            transparentMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            transparentMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            transparentMat.SetInt("_ZWrite", 0);
            transparentMat.DisableKeyword("_ALPHATEST_ON");
            transparentMat.EnableKeyword("_ALPHABLEND_ON");
            transparentMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            transparentMat.renderQueue = 3000;
            transparentMat.color = baseColor;

            // 새 머티리얼 적용
            renderer.material = transparentMat;
        }
    }
}
