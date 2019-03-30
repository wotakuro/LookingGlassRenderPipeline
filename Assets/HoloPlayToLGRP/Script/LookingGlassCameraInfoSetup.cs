using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.LookingGlassPipeline;

#if !DONT_USE_HOLO_PLAY
using HoloPlay;
#endif

namespace HoloPlayToLGRP
{
    [RequireComponent(typeof(LookingGlassCameraInfo))]
    public class LookingGlassCameraInfoSetup : MonoBehaviour
    {
        public void Start()
        {
#if !DONT_USE_HOLO_PLAY
            LookingGlassCameraInfo cameraInfo = this.GetComponent<LookingGlassCameraInfo>();
            LookingGlassDeviceConfig dstConfig = cameraInfo.config;

            Config.VisualConfig deviceConfig = null;
            Config.LoadVisualFromFile(out deviceConfig, Config.visualFileName);

            if (deviceConfig != null) {
                dstConfig.pitch = deviceConfig.pitch;
                dstConfig.slope = deviceConfig.slope;
                dstConfig.center = deviceConfig.center;
                dstConfig.viewCone = deviceConfig.viewCone;
                dstConfig.invView = deviceConfig.invView;
                dstConfig.verticalAngle = deviceConfig.verticalAngle;
                dstConfig.DPI = deviceConfig.DPI;
                dstConfig.screenW = deviceConfig.screenW;
                dstConfig.screenH = deviceConfig.screenH;
                dstConfig.flipImageX = deviceConfig.flipImageX;
                dstConfig.flipImageY = deviceConfig.flipImageY;
                dstConfig.flipSubp = deviceConfig.flipSubp;
            }
            cameraInfo.config = dstConfig;
#endif
        }
    }
}
