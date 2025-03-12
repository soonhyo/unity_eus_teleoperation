using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

using UnityEngine;
using UnityEngine.UI;
using System.Threading;


public class PointCloudSubscriber : MonoBehaviour
{
    private byte[] byteArray;
    private bool isMessageReceived = false;
    bool readyToProcessMessage = true;
    private int size;
    
    private Vector3[] pcl;
    private Color[] pcl_color;

    int width;
    int height;
    int row_step;
    int point_step;
    private ROSConnection _ros;
    private bool _enabled = false;
    public string topic = "/rgbd/point_cloud"; // ROS 토픽 이름

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        // 초기 토픽 구독
        if (topic != null)
        {
            _ros.Subscribe<PointCloud2Msg>(topic, OnPointcloud);
            _enabled = true;
        }

    }

    public void Update()
    {

        if (isMessageReceived)
        {
            PointCloudRendering();
            isMessageReceived = false;
        }


    }
    // 활성화/비활성화 토글
    public void ToggleEnabled()
    {
        _enabled = !_enabled;
        if (_enabled)
        {
            if (topic != null) _ros.Subscribe<PointCloud2Msg>(topic, OnPointcloud);
        }
        else
        {
            if (topic != null) _ros.Unsubscribe(topic);
        }
    }

    void OnPointcloud(PointCloud2Msg message)
    {


        size = message.data.GetLength(0);

        byteArray = new byte[size];
        byteArray = message.data;


        width = (int)message.width;
        height = (int)message.height;
        row_step = (int)message.row_step;
        point_step = (int)message.point_step;

        size = size / point_step;
        isMessageReceived = true;
    }

    //点群の座標を変換
    void PointCloudRendering()
    {
        pcl = new Vector3[size];
        pcl_color = new Color[size];

        int x_posi;
        int y_posi;
        int z_posi;

        float x;
        float y;
        float z;

        int rgb_posi;
        int rgb_max = 255;

        float r;
        float g;
        float b;

        //この部分でbyte型をfloatに変換         
        for (int n = 0; n < size; n++)
        {
            x_posi = n * point_step + 0;
            y_posi = n * point_step + 4;
            z_posi = n * point_step + 8;

            x = BitConverter.ToSingle(byteArray, x_posi);
            y = BitConverter.ToSingle(byteArray, y_posi);
            z = BitConverter.ToSingle(byteArray, z_posi);


            rgb_posi = n * point_step + 16;

            b = byteArray[rgb_posi + 0];
            g = byteArray[rgb_posi + 1];
            r = byteArray[rgb_posi + 2];

            r = r / rgb_max;
            g = g / rgb_max;
            b = b / rgb_max;
            
            pcl[n] = new Vector3(x, y, z);
            pcl_color[n] = new Color(r, g, b);


        }
    }

    public Vector3[] GetPCL()
    {
        return pcl;
    }

    public Color[] GetPCLColor()
    {
        return pcl_color;
    }
}
