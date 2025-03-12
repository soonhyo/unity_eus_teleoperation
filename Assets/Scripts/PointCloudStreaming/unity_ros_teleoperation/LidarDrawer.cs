using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.Tf2; // TF 메시지 타입 추가
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(LidarDrawer))]
public class LidarDrawerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        LidarDrawer myScript = (LidarDrawer)target;
        if(GUILayout.Button("Toggle Enabled"))
        {
            myScript.ToggleEnabled();
        }
    }
}
#endif

public enum VizType
{
    RGBD = 4 * 6,
}

public class LidarDrawer : MonoBehaviour
{
    public Material rgbd_material;
    GraphicsBuffer _meshTriangles;
    GraphicsBuffer _meshVertices;
    GraphicsBuffer _ptData;

    public float scale = 1.0f;
    public int maxPts = 1_000_0;
    public int displayPts = 1_000_0;
    private RenderParams renderParams;
    public string topic = "/sciurus17/voxel_grid/output";
    public string tfStaticTopic = "/tf_static"; // 정적 TF 토픽 추가
    public VizType vizType = VizType.RGBD;

    private int _LidarDataSize = 4 * 6;
    private ROSConnection _ros;
    private Mesh mesh;
    public bool _enabled = true;
    public GameObject _parent;
    private bool _missingParent = false;
    private int _numPts = 0;
    private double _lastTimestamp = 0; // 최신 타임스탬프 추적
    private TransformStampedMsg tfTransform; // camera_link와 camera_color_optical_frame 간 변환
    private bool isTfStaticReceived = false; // tf_static 수신 여부

    private Vector3 tfPosition;
    private Quaternion tfRotation;

    void Awake()
    {
        _ros = ROSConnection.GetOrCreateInstance();

        _LidarDataSize = (int)vizType;

        mesh = CreateQuadMesh();

        _meshTriangles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, mesh.triangles.Length, 4);
        _meshTriangles.SetData(mesh.triangles);
        _meshVertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, mesh.vertices.Length, 12);
        _meshVertices.SetData(mesh.vertices);
        _ptData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxPts, _LidarDataSize);

        renderParams = new RenderParams(rgbd_material);
        renderParams.worldBounds = new Bounds(Vector3.zero, Vector3.one * 10);
        renderParams.matProps = new MaterialPropertyBlock();

        renderParams.matProps.SetMatrix("_ObjectToWorld", Matrix4x4.Translate(new Vector3(0, 0, 0)));
        renderParams.matProps.SetFloat("_PointSize", scale);
        renderParams.matProps.SetBuffer("_LidarData", _ptData);
        renderParams.matProps.SetInt("_BaseVertexIndex", (int)mesh.GetBaseVertex(0));
        renderParams.matProps.SetBuffer("_Positions", _meshVertices);
    }

    void Start()
    {
        _ros.Subscribe<PointCloud2Msg>(topic, OnPointcloud);
        _ros.Subscribe<TFMessageMsg>(tfStaticTopic, UpdateTFStaticTransform); // tf_static 구독 추가
    }

    private Mesh CreateQuadMesh()
    {
        Mesh quadMesh = new Mesh();
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0), // 좌하단
            new Vector3(0.5f, -0.5f, 0),  // 우하단
            new Vector3(0.5f, 0.5f, 0),   // 우상단
            new Vector3(-0.5f, 0.5f, 0)   // 좌상단
        };
        int[] triangles = new int[]
        {
            0, 1, 2, // 첫 번째 삼각형
            0, 2, 3  // 두 번째 삼각형
        };

        quadMesh.vertices = vertices;
        quadMesh.triangles = triangles;
        return quadMesh;
    }

    // tf_static 메시지를 받아 변환 저장 (한 번만 처리)
    void UpdateTFStaticTransform(TFMessageMsg tfMessage)
    {
        if (isTfStaticReceived) return; // 이미 받은 경우 처리하지 않음

        foreach (var transform in tfMessage.transforms)
        {
            if (transform.header.frame_id == "camera_link" && 
                transform.child_frame_id == "camera_color_frame")
            {
                tfTransform = transform;
                Debug.Log("UpdateTFStaticTransform LidarDrawer " + transform);
                isTfStaticReceived = true;
                // _ros.Unsubscribe(tfStaticTopic); // 정적 변환은 한 번만 필요하므로 구독 해제
                Debug.Log("TF static transform received: camera_link -> camera_color_frame");
                break;
            }
        }
    }// TODO: need to change camera color frame to camera_color_optical_frame but the transform is not specified in the tf_static,
    // so we use camera_color_frame transform for now. It needs to be changed external trasforming api


    // 프레임 변환을 적용하여 포즈 업데이트
    void UpdatePose(string frame)
    {
        // _parent = GameObject.Find(frame);
        // if (_parent == null)
        // {
        //     _parent = GameObject.FindWithTag("root");
        //     _missingParent = true;
        //     Debug.LogWarning("Parent frame not found, using root as fallback: " + frame);
        // }
        if (tfTransform == null) return;

        transform.parent = _parent.transform;
        
        // TF 변환 데이터 가져오기
        tfPosition.x = (float)-tfTransform.transform.translation.y - 0.01f; // manual calibration
        tfPosition.y = (float)tfTransform.transform.translation.z + 0.02f; // manual calibration
        tfPosition.z = (float)tfTransform.transform.translation.x - 0.01f; // manual calibration
        // TODO: need to change using external tf transform api

        // 기본 포즌 (camera_link 기준)
        Vector3 basePosition = Vector3.zero;
        Quaternion baseRotation = Quaternion.Euler(0, 0, 180);

        // 변환 적용: camera_link -> camera_color_optical_frame
        Vector3 adjustedPosition = basePosition + tfPosition;
        Debug.Log("tfPosition: " + tfPosition.ToString("F4"));
        Quaternion adjustedRotation = baseRotation;
        //Quaternion adjustedRotation = baseRotation * tfRotation;
        
        // 변환된 포즌 적용
        transform.localPosition = adjustedPosition;
        transform.localRotation = adjustedRotation;
        transform.localScale = new Vector3(-1, 1, 1);
        Debug.Log("update pose : camera_link -> camera_color_frame");
        // }
        // else
        // {
        //     // TF 변환이 없거나 frame이 camera_link가 아닌 경우 기본 설정
        //     transform.localPosition = Vector3.zero;
        //     transform.localRotation = Quaternion.Euler(0, 0, 180);
        //     transform.localScale = new Vector3(-1, 1, 1);
        //     Debug.Log("update pose : default");
        // }
    }

    private void OnValidate()
    {
        if (renderParams.matProps != null)
        {
            renderParams.matProps.SetFloat("_PointSize", scale);
        }
        if (displayPts > maxPts)
        {
            displayPts = maxPts;
        }
    }

    private void OnDestroy()
    {
        _meshTriangles?.Dispose();
        _meshTriangles = null;
        _meshVertices?.Dispose();
        _meshVertices = null;
        _ptData?.Dispose();
        _ptData = null;
    }

    private void Update()
    {
        if (_enabled)
        {
            renderParams.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
            Graphics.RenderPrimitivesIndexed(renderParams, MeshTopology.Triangles, _meshTriangles, _meshTriangles.count, (int)mesh.GetIndexStart(0), _numPts);
        }
    }

    public void OnPointcloud(PointCloud2Msg pointCloud)
    {
        if (pointCloud.data.Length == 0) return;

        UpdatePose("camera_color_frame"); //Temporary setting

        _ptData.SetData(LidarUtils.ExtractXYZI(pointCloud, displayPts, vizType, out _numPts));
    }
    
    public void OnTopicChange(string topic)
    {
        if (this.topic != null)
        {
            _ros.Unsubscribe(this.topic);
            this.topic = null;
        }
        if (topic == null)
        {
            Debug.Log("Disabling pointcloud display");
            _enabled = false;
            return;
        }
        this.topic = topic;
        _ros.Subscribe<PointCloud2Msg>(topic, OnPointcloud);
        Debug.Log("Subscribed to " + topic);
    }

    public void OnDensityChange(float density)
    {
        displayPts = (int)(density * maxPts);
    }

    public void OnSizeChange(float size)
    {
        scale = size / 10f;
        renderParams.matProps.SetFloat("_PointSize", scale);
    }

    public void ToggleEnabled()
    {
        _enabled = !_enabled;
        if (!_enabled)
        {
            _ros.Unsubscribe(topic);
            _parent = null;
            Debug.Log("Unsubscribed to " + topic);
        }
        else
        {
            _ros.Subscribe<PointCloud2Msg>(topic, OnPointcloud);
            Debug.Log("Subscribed to " + topic);
        }
    }
}