using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{
    [RequireComponent(typeof(Camera))]
    public class LookingGlassCameraInfo : MonoBehaviour
    {
        public LookingGlassDeviceConfig config;
        public LookingGlassRenderInfoPerCamera renderInfo;

        private void Awake()
        {
            config.SetUpDefault();
        }
    }
    [System.Serializable]
    public struct LookingGlassRenderInfoPerCamera
    {
        public float fov;
        public float size;
        public float nearClipFactor;
        public float farClipFactor;

        public void SetupDefault()
        {
            fov = 13.5f;
            size = 1.0f;
            nearClipFactor = 1.0f;
            farClipFactor = 1.0f;
        }
    }

    [System.Serializable]
    public struct LookingGlassDeviceConfig
    {
        public float pitch;
        public float slope;
        public float center;
        public float viewCone;
        public float invView;
        public float verticalAngle;
        public float DPI;
        public float screenW;
        public float screenH;
        public float flipImageX;
        public float flipImageY;
        public float flipSubp;

        public void SetUpDefault()
        {
            pitch = 47.60786f;
            slope = -5.446739f;
            center = 0.1345109f;
            viewCone = 40;
            invView = 1;
            verticalAngle = 0;
            DPI = 338;
            screenW = 2560;
            screenH = 1600;
            flipImageX = 0;
            flipImageY = 0;
            flipSubp = 0;
        }

        public static bool AsBool(float param)
        {
            return (param != 0.0f);
        }
    }

}
