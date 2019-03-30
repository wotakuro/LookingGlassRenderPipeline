using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{
    public struct LookingGlassRenderingInfo
    {
        public enum RenderingMethod:int
        {
            RenderMultiPass = 0,
            RenderSinglePassInstancing = 1
        }

        public int renderTargetW;
        public int renderTargetH;
        public int tileX;
        public int tileY;
        public int tileSizeX;
        public int tileSizeY;
        public RenderingMethod renderMethod;

        public void CalculatePortion( out float portionX,out float portionY)
        {
            int tileSizeX = (int)renderTargetW / tileX;
            int tileSizeY = (int)renderTargetH / tileY;
            float paddingX = (int)renderTargetW - tileX * tileSizeX;
            float paddingY = (int)renderTargetH - tileY * tileSizeY;
            portionX = (float)tileX * tileSizeX / (float)renderTargetW;
            portionY = (float)tileY * tileSizeY / (float)renderTargetH;
        }

        public bool HaveToRemakeRenderTexture(ref LookingGlassRenderingInfo obj)
        {
            return (this.renderMethod != obj.renderMethod) ||
                (this.renderTargetW != obj.renderTargetW) ||
                (this.renderTargetH != obj.renderTargetH) ||
                (this.tileSizeX != obj.tileSizeX) ||
                (this.tileSizeY != obj.tileSizeY);
        }
    }


    // DrawPass
    public class LookingGlassMultiTextureRenderer : ScriptableRenderPass
    {
        private LookingGlassRenderingInfo drawInfo;
        private LookingGlassRenderInfoPerCamera perCameraInfo;
        private CommandBuffer commandBuffer;

        private RendererConfiguration rendererConfiguration = RendererConfiguration.None;

        FilterRenderersSettings m_OpaqueFilterSettings;
        FilterRenderersSettings m_TransparentFilterSettings;

        private RenderTexture dstTiledTexture;

        public void Setup(RenderTexture dst,ref LookingGlassRenderingInfo dinfo,
            ref LookingGlassRenderInfoPerCamera perCamInfo)
        {
            dstTiledTexture = dst;
            this.drawInfo = dinfo;
            this.perCameraInfo = perCamInfo;
        }

        public LookingGlassMultiTextureRenderer()
        {

            RegisterShaderPassName("LightweightForward");
            RegisterShaderPassName("SRPDefaultUnlit");


            commandBuffer = new CommandBuffer();
            commandBuffer.name = "MultiPassStep";


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
            LookingGlassUtil.SetupCameraInfo(camera,perCameraInfo.fov,perCameraInfo.size,perCameraInfo.nearClipFactor,perCameraInfo.farClipFactor);

            var opaqueSortFlag = renderingData.cameraData.defaultOpaqueSortFlags;
            var opaquedrawSettings = CreateDrawRendererSettings(camera, opaqueSortFlag, rendererConfiguration, renderingData.supportsDynamicBatching);

            var transDrawSettings = CreateDrawRendererSettings(camera, SortFlags.CommonTransparent, rendererConfiguration, renderingData.supportsDynamicBatching);

            // clear Tile Texture
            commandBuffer.SetRenderTarget(dstTiledTexture);
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
                    ++counter;
                }
            }
            commandBuffer.Clear();
            RenderTexture.ReleaseTemporary(tempRenderTexture);
        }

        private void SetupVPMatrices(ScriptableRenderContext context, CommandBuffer cmd,
            Camera camera,
            int view, int numViews)
        {
            Matrix4x4 projMatrix = camera.projectionMatrix;
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;

            var  vpOffset = LookingGlassUtil.GetVPMatrixOffsets(
                camera.aspect,perCameraInfo.fov,perCameraInfo.size,
                view,numViews);

            // view matrix
            viewMatrix.m03 -= vpOffset.x;
            viewMatrix.m13 -= vpOffset.y;

            // proj matrix
            projMatrix.m02 -= vpOffset.z;
            projMatrix.m12 -= vpOffset.w;


            cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }




        public void Dispose()
        {
            commandBuffer.Release();
        }
    }
}