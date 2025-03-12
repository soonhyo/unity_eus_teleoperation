using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class PointCloudRenderer : MonoBehaviour
{
    public PointCloudSubscriber subscriber;

    // Mesh stores the positions and colours of every point in the cloud
    Mesh mesh;
    MeshRenderer meshRenderer;
    MeshFilter mf;

    // Point cloud display settings
    public float pointSize = 1f;
    [Header("Depth Range Settings")]
    public float minDepth = 0f;  // 최소 깊이
    public float maxDepth = 10f; // 최대 깊이

    [Header("Point Cloud Display Settings")]
    public int maxPointsToDisplay = 100000; // 표시할 최대 포인트 수
    [Range(1, 10)]
    public int downsampleRate = 1; // 다운샘플링 비율 (1 = 모든 포인트, 2 = 절반, 10 = 1/10 등)

    [Header("MAKE SURE THESE LISTS ARE MINIMISED OR EDITOR WILL CRASH")]
    private Vector3[] positions = new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0) };
    private Color[] colours = new Color[] { new Color(1f, 0f, 0f), new Color(0f, 1f, 0f) };

    public Transform offset;

    void Start()
    {
        // Initialize components
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        mf = gameObject.AddComponent<MeshFilter>();
        meshRenderer.material = new Material(Shader.Find("Custom/PointCloudShader"));
        mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        transform.position = offset.position;
        transform.rotation = offset.rotation;
    }

    void UpdateMesh()
    {
        // Get raw point cloud data
        Vector3[] rawPositions = subscriber.GetPCL();
        Color[] rawColours = subscriber.GetPCLColor();

        if (rawPositions == null || rawColours == null || rawPositions.Length != rawColours.Length)
        {
            return;
        }

        // Filter and downsample the point cloud
        List<Vector3> filteredPositions = new List<Vector3>();
        List<Color> filteredColours = new List<Color>();

        for (int i = 0; i < rawPositions.Length; i += downsampleRate)
        {
            float depth = rawPositions[i].z; // Assuming Z is the depth axis
            if (depth >= minDepth && depth <= maxDepth)
            {
                filteredPositions.Add(rawPositions[i]);
                filteredColours.Add(rawColours[i]);
            }

            // Stop if we reach the max display limit
            if (filteredPositions.Count >= maxPointsToDisplay)
            {
                break;
            }
        }

        // Update positions and colours arrays
        positions = filteredPositions.ToArray();
        colours = filteredColours.ToArray();

        // Update the mesh
        mesh.Clear();
        mesh.vertices = positions;
        mesh.colors = colours;
        int[] indices = new int[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            indices[i] = i;
        }

        mesh.SetIndices(indices, MeshTopology.Points, 0);
        mf.mesh = mesh;
    }

    void Update()
    {
        transform.position = offset.position;
        transform.rotation = offset.rotation;
        meshRenderer.material.SetFloat("_PointSize", pointSize);
        UpdateMesh();
    }
}