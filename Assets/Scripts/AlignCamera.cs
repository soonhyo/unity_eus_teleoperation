using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;

public class CameraSync : MonoBehaviour
{
    [SerializeField] private string cameraInfoTopic = "/camera/camera_info"; // 카메라 정보 토픽
    [SerializeField] private string imageTopic = "/camera/image_raw"; // 이미지 토픽
    [SerializeField] private bool compressed = false; // 압축 이미지 사용 여부
    [SerializeField] private GameObject target; // 동기화할 대상 GameObject (camera_link 기준)
    [SerializeField] private OVRCameraRig ovrCameraRig; // OVRCameraRig 참조
    [SerializeField] private Camera mainCamera; // 메인 카메라 (centerEyeAnchor)
    [SerializeField] private RawImage rawImage; // Inspector에서 할당할 RawImage
    [SerializeField] private string tfStaticTopic = "/tf_static"; // 정적 TF 토픽

    private ROSConnection ros;
    private bool isParametersSet = false; // CameraInfo 설정 여부
    private bool isSynchronized = false; // 동기화 상태
    private bool isTfStaticReceived = false; // tf_static 수신 여부
    private Texture2D tex; // RawImage에 적용할 텍스처
    private byte[] imageData; // 압축 이미지 데이터 저장용
    private TransformStampedMsg tfTransform; // camera_link와 camera_color_optical_frame 간 변환

    // head_target_pose 퍼블리시용 변수
    private PoseStampedMsg headPoseMsg;
    private PointMsg headRosPosition;
    private QuaternionMsg headRosRotation;
    private HeaderMsg headerMsg;

    void Start()
    {
        // OVRCameraRig 확인
        if (ovrCameraRig == null)
        {
            Debug.LogError("OVRCameraRig가 지정되지 않았습니다!");
            return;
        }

        // 메인 카메라 설정
        if (mainCamera == null)
        {
            mainCamera = ovrCameraRig.centerEyeAnchor.GetComponent<Camera>();
            if (mainCamera == null)
            {
                Debug.LogError("CenterEyeAnchor에 Camera 컴포넌트가 없습니다!");
                return;
            }
        }

        // RawImage 확인
        if (rawImage == null)
        {
            Debug.LogError("RawImage가 지정되지 않았습니다!");
            return;
        }

        // ROS 연결 초기화
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<CameraInfoMsg>(cameraInfoTopic, UpdateCameraParameters);
        ros.Subscribe<TFMessageMsg>(tfStaticTopic, UpdateTFStaticTransform); // tf_static 구독
        if (compressed)
        {
            ros.Subscribe<CompressedImageMsg>(imageTopic, ReceiveCompressedMsg);
            Debug.Log("Subscribing to compressed image topic: " + imageTopic);
        }
        else
        {
            ros.Subscribe<ImageMsg>(imageTopic, ReceiveImageMsg);
            Debug.Log("Subscribing to image topic: " + imageTopic);
        }

        // 텍스처 초기화
        tex = new Texture2D(1, 1, TextureFormat.RGB24, false);
        rawImage.texture = tex;

        // head_target_pose 퍼블리시 초기화
        ros.RegisterPublisher<PoseStampedMsg>("head_target_pose");
        headPoseMsg = new PoseStampedMsg
        {
            header = new HeaderMsg(),
            pose = new PoseMsg
            {
                position = new PointMsg(),
                orientation = new QuaternionMsg()
            }
        };
        headRosPosition = new PointMsg();
        headRosRotation = new QuaternionMsg();
        headerMsg = new HeaderMsg { frame_id = "base_link" };

        // RawImage 업데이트 주기 설정 (30 FPS)
        InvokeRepeating("UpdateRawImage", 0f, 1f / 30f);
    }

    void Update()
    {
        // 왼쪽 컨트롤러 One 버튼으로 동기화 토글
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            isSynchronized = !isSynchronized;
            if (isSynchronized && target != null && isTfStaticReceived)
            {
                ApplyTransformToOVRCamera(); // 변환 적용
                Debug.Log("OVRCameraRig synchronized with target (adjusted by tf_static) and publishing head_target_pose.");
            }
            else
            {
                Debug.Log("Synchronization released or tf_static not available.");
            }
        }
    }

    void LateUpdate()
    {
        if (isSynchronized && target != null && isTfStaticReceived)
        {
            // ApplyTransformToOVRCamera(); // 실시간 동기화 시 변환 적용
            PublishHeadPose(ovrCameraRig.centerEyeAnchor, "head_target_pose");
        }
    }

    // tf_static 메시지를 받아 변환 저장 (한 번만 처리)
    void UpdateTFStaticTransform(TFMessageMsg tfMessage)
    {
        Debug.Log("UpdateTFStaticTransform alignCamera" + tfMessage.transforms);
        if (isTfStaticReceived) return; // 이미 받은 경우 처리하지 않음

        foreach (var transform in tfMessage.transforms)
        {
            if (transform.header.frame_id == "camera_link" && 
                transform.child_frame_id == "camera_color_frame")
            {
                tfTransform = transform;
                isTfStaticReceived = true;
                //ros.Unsubscribe(tfStaticTopic); // 정적 변환은 한 번만 필요하므로 구독 해제
                Debug.Log("TF static transform received: camera_link -> camera_color_frame");
                break;
            }
        }
    }// TODO: need to change camera color frame to camera_color_optical_frame but the transform is not specified in the tf_static,
    // so we use camera_color_frame transform for now. It needs to be changed external trasforming api

    // camera_link에서 camera_color_optical_frame으로의 변환을 OVRCameraRig에 적용
    void ApplyTransformToOVRCamera()
    {
        if (tfTransform == null) return;

        // target (camera_link 기준) 위치와 회전
        Vector3 targetPosition = target.transform.position;
        Quaternion targetRotation = target.transform.rotation;

        // TF 변환 데이터 가져오기
        Vector3 tfPosition = tfTransform.transform.translation.From<FLU>();
        Quaternion tfRotation = tfTransform.transform.rotation.From<FLU>();

        // 변환 적용: camera_link -> camera_color_optical_frame
        Vector3 adjustedPosition = targetPosition + targetRotation * tfPosition;
        // Quaternion adjustedRotation = targetRotation * tfRotation;
        Quaternion adjustedRotation = targetRotation;
        // OVRCameraRig에 적용
        ovrCameraRig.transform.position = adjustedPosition;
        ovrCameraRig.transform.rotation = adjustedRotation;
    }

    void UpdateCameraParameters(CameraInfoMsg message)
    {
        if (isParametersSet)
        {
            ros.Unsubscribe(cameraInfoTopic);
            return;
        }

        float fx = (float)message.K[0]; // 초점 거리 x
        float fy = (float)message.K[4]; // 초점 거리 y
        float cx = (float)message.K[2]; // 광학 중심 x
        float cy = (float)message.K[5]; // 광학 중심 y
        uint width = message.width;     // 이미지 너비
        uint height = message.height;   // 이미지 높이

        // 투영 행렬 계산
        Matrix4x4 projectionMatrix = new Matrix4x4();
        projectionMatrix.m00 = 2.0f * fx / width;
        projectionMatrix.m11 = 2.0f * fy / height;
        projectionMatrix.m02 = 1.0f - 2.0f * cx / width;
        projectionMatrix.m12 = 2.0f * cy / height - 1.0f;
        projectionMatrix.m23 = -1.0f;
        projectionMatrix.m32 = -1.0f;

        // OVRCameraRig 카메라에 투영 행렬 적용
        Camera[] ovrCameras = ovrCameraRig.GetComponentsInChildren<Camera>();
        foreach (Camera camera in ovrCameras)
        {
            camera.projectionMatrix = projectionMatrix;
        }

        // 카메라 FOV와 Aspect 조정
        float aspectRatio = (float)width / height;
        mainCamera.aspect = aspectRatio;
        float fov = 2.0f * Mathf.Atan(width / (2.0f * fx)) * Mathf.Rad2Deg;
        mainCamera.fieldOfView = fov;

        // RawImage 크기, 스케일, 위치 조정
        RectTransform rectTransform = rawImage.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(width, height);

        float screenAspect = (float)Screen.width / Screen.height;
        float imageAspect = (float)width / height;
        Vector2 scale = imageAspect > screenAspect
            ? new Vector2(1.0f, screenAspect / imageAspect)
            : new Vector2(imageAspect / screenAspect, 1.0f);
        rectTransform.localScale = new Vector3(scale.x, scale.y, 1f);

        Vector2 offset = new Vector2(
            (cx - width / 2.0f) / width * scale.x,
            (cy - height / 2.0f) / height * scale.y
        );
        rectTransform.anchoredPosition = new Vector2(
            offset.x * Screen.width,
            offset.y * Screen.height
        );

        isParametersSet = true;

        Debug.Log($"Camera synced: Aspect={aspectRatio}, FOV={fov}, RawImage Size={width}x{height}, Scale={scale}, Offset={offset}");
    }

    void ReceiveImageMsg(ImageMsg image)
    {
        if (image.encoding == "rgb8" || image.encoding == "bgr8")
        {
            if (tex.width != (int)image.width || tex.height != (int)image.height)
            {
                tex.Reinitialize((int)image.width, (int)image.height);
            }

            tex.LoadRawTextureData(image.data);
            tex.Apply();

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
        }
        else
        {
            Debug.LogWarning("Unsupported image encoding: " + image.encoding);
        }
    }

    void ReceiveCompressedMsg(CompressedImageMsg compressedImage)
    {
        imageData = compressedImage.data;
    }

    private void UpdateRawImage()
    {
        if (compressed && imageData != null)
        {
            tex.LoadImage(imageData);
            tex.Apply();
        }
        rawImage.texture = tex;
    }

    void PublishHeadPose(Transform headTransform, string topic)
    {
        var fluPosition = headTransform.position.To<FLU>();
        headRosPosition.x = fluPosition.x;
        headRosPosition.y = fluPosition.y;
        headRosPosition.z = fluPosition.z;

        var fluRotation = headTransform.rotation.To<FLU>();
        headRosRotation.x = fluRotation.x;
        headRosRotation.y = fluRotation.y;
        headRosRotation.z = fluRotation.z;
        headRosRotation.w = fluRotation.w;

        headPoseMsg.header = headerMsg;
        headPoseMsg.pose.position = headRosPosition;
        headPoseMsg.pose.orientation = headRosRotation;

        ros.Publish(topic, headPoseMsg);
    }

    void OnDestroy()
    {
        if (tex != null)
        {
            Destroy(tex);
        }
    }
}
// TODO : change the Ovrcamera to fixed camera when the sync is activated
// TODO : align the image with robot model