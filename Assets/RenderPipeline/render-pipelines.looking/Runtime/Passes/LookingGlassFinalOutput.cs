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
        Material lenticularMat;
        private RenderTargetHandle colorAttachmentHandle { get; set; }

        public void SetUp(RenderTargetHandle colorHandle,RenderTexture texture , ref LookingGlassInfo dinfo)
        {
            this.colorAttachmentHandle = colorHandle;
            tiledTexture = texture;
            drawInfo = dinfo;
        }
        public void PassConfigToMaterial()
        {
            lenticularMat.SetFloat("pitch", 372.5203f);

            lenticularMat.SetFloat("tilt", -0.1147476f);

            lenticularMat.SetFloat("center", 0.1345109f);
            lenticularMat.SetFloat("invView", 1);
            lenticularMat.SetFloat("flipX", 0);
            lenticularMat.SetFloat("flipY", 0);

            float subp = 0.0001302083f;// 1f / (config.screenW * 3f);
//            subp *= config.flipImageX.asBool ? -1 : 1;
            lenticularMat.SetFloat("subp", subp);

            lenticularMat.SetInt("ri", 0);
            lenticularMat.SetInt("bi", 2);

            lenticularMat.SetVector("tile", new Vector4(
                drawInfo.tileX,
                drawInfo.tileY,
                0.9997559f,
                0.9997559f
            ));

            lenticularMat.SetVector("aspect", new Vector4(
                1.6f,1.6f,0.0f,0.0f));
        }


        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if( lenticularMat == null)
            {
                lenticularMat = new Material(Shader.Find("HoloPlay/Lenticular"));
            }
            PassConfigToMaterial();
            lenticularMat.mainTexture = tiledTexture;
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
                ScriptableRenderer.RenderFullscreenQuad(cmd, lenticularMat);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

        }
    }

}