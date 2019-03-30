using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{
    public class LookingGlassUtil
    {
        public static float GetAdjustedDistance(float fov, float size)
        {
            return size / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        }


        public static Vector4 GetVPMatrixOffsets(float aspect, float fov, float size, int view, int numViews)
        {
            float adjustedDistance = LookingGlassUtil.GetAdjustedDistance(fov, size);

            float verticalAngle = 0.0f;
            float horizontalAngle = AngleAtView(view, numViews);

            float offsetX = adjustedDistance * Mathf.Tan(horizontalAngle * Mathf.Deg2Rad);
            float offsetY = adjustedDistance * Mathf.Tan(verticalAngle * Mathf.Deg2Rad);

            Vector4 result = new Vector4(offsetX, offsetY,
                offsetX / (size * aspect), offsetY / size);
            return result;
        }
        public static void SetupCameraInfo(Camera camera, float fov, float size, float nearClipFactor, float farClipFactor)
        {
            camera.fieldOfView = fov;
            float adjustedDistance = LookingGlassUtil.GetAdjustedDistance(fov, size);
            camera.nearClipPlane = adjustedDistance - nearClipFactor * size;
            camera.farClipPlane = adjustedDistance + farClipFactor * size;

            camera.transform.position = new Vector3(0, 0, -adjustedDistance);
            camera.transform.localRotation = Quaternion.identity;
        }




        public static float AngleAtView(int view, int numViews, float viewCone = 40.0f)
        {
            viewCone = Mathf.Abs(viewCone);

            if (numViews <= 1)
                return 0;

            return -viewCone * 0.5f + (float)view / (numViews - 1f) * viewCone;
        }
    }
}
