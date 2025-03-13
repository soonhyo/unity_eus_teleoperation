using UnityEngine;

public class ClosePanel : MonoBehaviour
{
    public GameObject panel; // 닫을 패널을 Inspector에서 연결

    void Update()
    {
        // Left 컨트롤러의 Two 버튼(X 버튼)이 눌렸는지 체크
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
        {
            TogglePanel();
        }
    }

    // 패널을 토글(켜고 끄기)하는 함수
    private void TogglePanel()
    {
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf); // 현재 상태的反转 (켜져 있으면 끄고, 꺼져 있으면 켬)
        }
    }

    // (선택 사항) 기존 버튼 클릭용 함수 유지
    public void OnCloseButtonClicked()
    {
        if (panel != null)
        {
            panel.SetActive(false); // UI 버튼용으로 패널 닫기
        }
    }
}