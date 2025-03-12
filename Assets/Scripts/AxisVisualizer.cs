using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AxisVisualizer : MonoBehaviour
{
    private List<LineRenderer> axisRenderers = new List<LineRenderer>(); // 축 LineRenderer를 저장

    void Start()
    {
        CreateAxis(Vector3.right, Color.red, "X Axis");   // X축 (빨강)
        CreateAxis(Vector3.up, Color.green, "Y Axis");    // Y축 (초록)
        CreateAxis(Vector3.forward, Color.blue, "Z Axis"); // Z축 (파랑)        
    }

    void Update()
    {
        // 오브젝트의 transform이 변경될 때마다 축 위치 갱신
        UpdateAxisPositions();
    }

    private void CreateAxis(Vector3 direction, Color color, string name)
    {
        GameObject axis = new GameObject(name);
        axis.transform.SetParent(transform); // 현재 오브젝트에 부착

        LineRenderer lr = axis.AddComponent<LineRenderer>();
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;

        lr.positionCount = 2;
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, transform.position + transform.TransformDirection(direction * 0.1f));

        // 생성된 LineRenderer를 리스트에 추가
        axisRenderers.Add(lr);
    }

    private void UpdateAxisPositions()
    {
        // 각 축의 방향 정의 (로컬 좌표계 기준)
        Vector3[] directions = { Vector3.right, Vector3.up, Vector3.forward };
        for (int i = 0; i < axisRenderers.Count; i++)
        {
            LineRenderer lr = axisRenderers[i];
            // 시작점은 오브젝트의 현재 위치
            lr.SetPosition(0, transform.position);
            // 끝점은 오브젝트의 transform에 따라 변환된 방향
            lr.SetPosition(1, transform.position + transform.TransformDirection(directions[i] * 0.1f));
        }
    }
}