using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class ImageSubscriber : MonoBehaviour
{
    public RawImage rawImage;
    public string topic = "/camera/image_raw";
    public bool compressed = false; 
    private ROSConnection _ros;
    private Texture2D tex;
    private byte[] imageData;
    public bool _enabled = false;

    void Start()
    {

        _ros = ROSConnection.GetOrCreateInstance();

        if (_enabled){
            if (compressed)
            {
                _ros.Subscribe<CompressedImageMsg>(topic, ReceiveCompressedMsg);
                Debug.Log("Subscribing to compressed image topic: " + topic);
            }
            else
            {
                _ros.Subscribe<ImageMsg>(topic, ReceiveImageMsg);
                Debug.Log("Subscribing to image topic: " + topic);
            }
        }

        tex = new Texture2D(1, 1, TextureFormat.RGB24, false);
    }
    void ToggleRepeating()
    {
        if (!_enabled)
        {
            CancelInvoke(nameof(UpdateRawImage));
        }
        else
        {
            InvokeRepeating(nameof(UpdateRawImage), 0f, 1/30f);
        }
    }
    void ReceiveImageMsg(ImageMsg image)
    {
        if (image.encoding == "rgb8" || image.encoding == "bgr8")
        {
            // size of texture
            if (tex.width != (int)image.width || tex.height != (int)image.height)
            {
                tex.Reinitialize((int)image.width, (int)image.height);
            }

            tex.LoadRawTextureData(image.data);
            tex.Apply();

            // BGR8 -> RGB
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

            UpdateRawImage();
        }
        else
        {
            Debug.LogWarning("Not supporting type of image: " + image.encoding);
        }
    }

    float prevTime = 0;
    int count = 0;

    void ReceiveCompressedMsg(CompressedImageMsg compressedImage)
    {
        imageData = compressedImage.data;
        float currentTime = Time.time;
        count++;
        if (currentTime - prevTime > 1.0f)
        {
            Debug.Log(string.Format("Communication Hz: {0:0.00} Hz", count / (currentTime - prevTime)));
            prevTime = currentTime;
            count = 0;
        }
    }

    private void UpdateRawImage()
    {
        tex.LoadImage(imageData);
        tex.Apply();

        rawImage.texture = tex;

    }
    public void ToggleEnabled()
    {
        _enabled = !_enabled;
        if (!_enabled)
        {
            _ros.Unsubscribe(topic);
            Debug.Log("Unsubscribed to " + topic);
        }
        else
        {
            if (compressed)
            {
                _ros.Subscribe<CompressedImageMsg>(topic, ReceiveCompressedMsg);
                Debug.Log("Subscribing to compressed image topic: " + topic);
            }
            else
            {
                _ros.Subscribe<ImageMsg>(topic, ReceiveImageMsg);
                Debug.Log("Subscribing to image topic: " + topic);
            }
        }
        ToggleRepeating();
    }
    
    void OnDestroy()
    {
        if (tex != null)
        {
            Destroy(tex);
        }
    }
}
