using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class RosImageVisualizer : MonoBehaviour
{
    public Renderer targetRenderer; // Quad의 Renderer 컴포넌트
    public GameObject syncTransformTarget; // Quad의 transform을 동기화할 대상 GameObject
    public string topicName = "/camera/image_raw"; // 단일 토픽 이름
    public bool compressed = false; // Compressed 이미지 사용 여부를 토글
    private ROSConnection ros;
    private Texture2D tex;

    void Start()
    {
        // ROS 연결 초기화
        ros = ROSConnection.GetOrCreateInstance();

        // 토글 값에 따라 적절한 메시지 타입 구독
        if (compressed)
        {
            ros.Subscribe<CompressedImageMsg>(topicName, ReceiveCompressedMsg);
            Debug.Log("Subscribing to compressed image topic: " + topicName);
        }
        else
        {
            ros.Subscribe<ImageMsg>(topicName, ReceiveImageMsg);
            Debug.Log("Subscribing to image topic: " + topicName);
        }

        // 텍스처 초기화 (초기 크기는 임의로 작게 설정, 동적으로 조정됨)
        tex = new Texture2D(1, 1, TextureFormat.RGB24, false);
        targetRenderer.material.mainTexture = tex;
    }

    // 일반 이미지 메시지 수신 처리
    void ReceiveImageMsg(ImageMsg image)
    {
        if (image.encoding == "rgb8" || image.encoding == "bgr8")
        {
            // 텍스처 크기 조정
            if (tex.width != (int)image.width || tex.height != (int)image.height)
            {
                tex.Reinitialize((int)image.width, (int)image.height);
            }

            // RGB8 또는 BGR8 데이터 로드
            tex.LoadRawTextureData(image.data);
            tex.Apply();

            // BGR8인 경우 RGB로 변환
            if (image.encoding == "bgr8")
            {
                Color32[] pixels = tex.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte temp = pixels[i].r;
                    pixels[i].r = pixels[i].b;
                    pixels[i].b = temp;
                }
                tex.SetPixels32(pixels);
                tex.Apply();
            }

            // Quad 크기 및 Transform 업데이트
            UpdateQuadSizeAndTransform(tex.width, tex.height);
        }
        else
        {
            Debug.LogWarning("지원되지 않는 이미지 인코딩: " + image.encoding);
        }
    }

    // 압축 이미지 메시지 수신 처리
    void ReceiveCompressedMsg(CompressedImageMsg compressedImage)
    {
        byte[] imageData = compressedImage.data;

        // 텍스처에 압축 데이터 로드 (JPEG 또는 PNG)
        tex.LoadImage(imageData);
        tex.Apply();

        // Quad 크기 및 Transform 업데이트
        UpdateQuadSizeAndTransform(tex.width, tex.height);
    }

    // Quad 크기와 Transform 동기화
    private void UpdateQuadSizeAndTransform(int width, int height)
    {
        // Quad 크기 조정 (종횡비 유지)
        float aspectRatio = (float)width / height;
        targetRenderer.transform.localScale = new Vector3(aspectRatio, 1f, 1f);

        // Transform 동기화
        if (syncTransformTarget != null)
        {
            targetRenderer.transform.position = syncTransformTarget.transform.position;
            targetRenderer.transform.rotation = syncTransformTarget.transform.rotation;
        }
    }

    void OnDestroy()
    {
        // 텍스처 정리
        if (tex != null)
        {
            Destroy(tex);
        }
    }
}