using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{
    public class LookingGlassFinalOutput : ScriptableRenderPass
    {
        const string k_FinalBlitTag = "Final Blit Pass";

        private RenderTexture tiledTexture;
        private LookingGlassInfo drawInfo;
        Material material;
        private RenderTargetHandle colorAttachmentHandle { get; set; }

        public void SetUp(RenderTargetHandle colorHandle,RenderTexture texture , ref LookingGlassInfo dinfo)
        {
            this.colorAttachmentHandle = colorHandle;
            tiledTexture = texture;
            drawInfo = dinfo;
        } 

        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if( material == null)
            {
                material = new Material(Shader.Find("HoloPlay/Simple Flip"));
            }
            material.mainTexture = tiledTexture;
            CommandBuffer cmd = CommandBufferPool.Get(k_FinalBlitTag);

                SetRenderTarget(
                    cmd,
                    BuiltinRenderTextureType.CameraTarget,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    ClearFlag.None,
                    Color.black,
                    TextureDimension.Tex2D);

                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
                ScriptableRenderer.RenderFullscreenQuad(cmd, material);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

        }
    }

}