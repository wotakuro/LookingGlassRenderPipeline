using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{
    public struct LookingGlassInfo
    {
        public int renderTargetW;
        public int renderTargetH;
        public int tileX;
        public int tileY;

        public float fov;
        public float size;
        public float nearClipFactor;
        public float farClipFactor;
    }


    // DrawPass
    public class LookingGlassMultiTextureRenderer : ScriptableRenderPass
    {
        private LookingGlassInfo drawInfo;
        private CommandBuffer commandBuffer;

        private RendererConfiguration rendererConfiguration = RendererConfiguration.None;

        FilterRenderersSettings m_OpaqueFilterSettings;
        FilterRenderersSettings m_TransparentFilterSettings;

        private RenderTexture dstTiledTexture;

        public void Setup(RenderTexture dst,ref LookingGlassInfo dinfo)
        {
            dstTiledTexture = dst;
            this.drawInfo = dinfo;
        }

        public LookingGlassMultiTextureRenderer()
        {

            RegisterShaderPassName("LightweightForward");
            RegisterShaderPassName("SRPDefaultUnlit");


            commandBuffer = new CommandBuffer();

            m_OpaqueFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
            };
            m_TransparentFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.transparent,
            };
        }


        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            int w = drawInfo.renderTargetW / drawInfo.tileX;
            int h = drawInfo.renderTargetH / drawInfo.tileY;

            RenderTextureDescriptor renderTextureDesc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 16);

            RenderTexture tempRenderTexture = RenderTexture.GetTemporary(renderTextureDesc);
            Camera camera = renderingData.cameraData.camera;
            SetupCameraInfo(camera);

            var opaqueSortFlag = renderingData.cameraData.defaultOpaqueSortFlags;
            var opaquedrawSettings = CreateDrawRendererSettings(camera, opaqueSortFlag, rendererConfiguration, renderingData.supportsDynamicBatching);

            var transDrawSettings = CreateDrawRendererSettings(camera, SortFlags.CommonTransparent, rendererConfiguration, renderingData.supportsDynamicBatching);

            // clear Tile Texture
            commandBuffer.ClearRenderTarget(true, true, Color.black);
            context.ExecuteCommandBuffer(commandBuffer);
            context.Submit();
            commandBuffer.Clear();

            var tileSize = new Vector2(w, h);
            int counter = 0;
            int tileNum = drawInfo.tileX * drawInfo.tileY;
            for (int i = 0; i < drawInfo.tileY; ++i)
            {
                for (int j = 0; j < drawInfo.tileX; ++j)
                {
                    SetupVPMatrices(context, commandBuffer, camera, counter, tileNum);

                    commandBuffer.SetRenderTarget(tempRenderTexture);
                    commandBuffer.ClearRenderTarget(true, true, Color.black);
                    context.ExecuteCommandBuffer(commandBuffer);
                    commandBuffer.Clear();

                    // opaque renderer draw
                    context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref opaquedrawSettings, m_OpaqueFilterSettings);

                    // Render objects that did not match any shader pass with error shader
                    renderer.RenderObjectsWithError(context, ref renderingData.cullResults, camera, m_OpaqueFilterSettings, SortFlags.None);

                    // transparent renderer Draw
                    context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref transDrawSettings, m_TransparentFilterSettings);

                    commandBuffer.CopyTexture(tempRenderTexture, 0, 0, 0, 0, tempRenderTexture.width, tempRenderTexture.height,
                        dstTiledTexture, 0, 0, j * w, i * h);

                    context.ExecuteCommandBuffer(commandBuffer);
                    context.Submit();
                    commandBuffer.Clear();
                    ++counter;
                }
            }

            RenderTexture.ReleaseTemporary(tempRenderTexture);
        }
        private void SetupCameraInfo(Camera camera)
        {
            camera.fieldOfView = drawInfo.fov;
            float adjustedDistance = GetAdjustedDistance(drawInfo.fov, drawInfo.size);
            camera.nearClipPlane = adjustedDistance - drawInfo.nearClipFactor * drawInfo.size;
            camera.farClipPlane = adjustedDistance + drawInfo.farClipFactor * drawInfo.size;

            camera.transform.position = new Vector3(0, 0, -adjustedDistance);
            camera.transform.localRotation = Quaternion.identity;
        }

        public static float GetAdjustedDistance(float fov,float size)
        {
            return size / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        }

        private void SetupVPMatrices(ScriptableRenderContext context, CommandBuffer cmd,
            Camera camera,
            int view, int numViews)
        {
            Matrix4x4 projMatrix = camera.projectionMatrix;
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;

            float adjustedDistance = GetAdjustedDistance(drawInfo.fov, drawInfo.size);

            float verticalAngle = 0.0f;
            float horizontalAngle = AngleAtView(view, numViews);

            float offsetX = adjustedDistance * Mathf.Tan(horizontalAngle * Mathf.Deg2Rad);
            float offsetY = adjustedDistance * Mathf.Tan(verticalAngle * Mathf.Deg2Rad);

            // view matrix
            viewMatrix.m03 -= offsetX;
            viewMatrix.m13 -= offsetY;

            // proj matrix
            projMatrix.m02 -= offsetX / (drawInfo.size * camera.aspect);
            projMatrix.m12 -= offsetY / drawInfo.size;


            cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static float AngleAtView(int view, int numViews, float viewCone = 40.0f)
        {
            viewCone = Mathf.Abs(viewCone);

            if (numViews <= 1)
                return 0;

            return -viewCone * 0.5f + (float)view / (numViews - 1f) * viewCone;
        }


        public void Dispose()
        {
            commandBuffer.Release();
        }
    }
}