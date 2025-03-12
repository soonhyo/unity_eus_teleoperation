using UnityEngine;
using UnityEngine.UI; // UI 관련 기능을 사용하기 위해 추가
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class ImageSubscriber : MonoBehaviour
{
    public RawImage rawImage; // Inspector에서 할당할 RawImage 컴포넌트
    public string topicName = "/camera/image_raw"; // 단일 토픽 이름
    public bool compressed = false; // Compressed 이미지 사용 여부를 토글
    private ROSConnection ros;
    private Texture2D tex;
    private byte[] imageData;
    
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

        // RawImage에 텍스처 업데이트
        InvokeRepeating("UpdateRawImage", 0f, 1/30f);
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

            // RawImage에 텍스처 업데이트
            UpdateRawImage();
        }
        else
        {
            Debug.LogWarning("지원되지 않는 이미지 인코딩: " + image.encoding);
        }
    }

    float prevTime = 0;
    int count = 0;

    // 압축 이미지 메시지 수신 처리
    void ReceiveCompressedMsg(CompressedImageMsg compressedImage)
    {
        imageData = compressedImage.data;
           // 통신 주파수 표시
        float currentTime = Time.time;
        count++;
        if (currentTime - prevTime > 1.0f)
        {
            Debug.Log(string.Format("Communication Hz: {0:0.00} Hz", count / (currentTime - prevTime)));
            prevTime = currentTime;
            count = 0;
        }
    }

    // RawImage에 텍스처 업데이트
    private void UpdateRawImage()
    {
        // 텍스처에 압축 데이터 로드 (JPEG 또는 PNG)
        tex.LoadImage(imageData);
        tex.Apply();

        rawImage.texture = tex;

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
