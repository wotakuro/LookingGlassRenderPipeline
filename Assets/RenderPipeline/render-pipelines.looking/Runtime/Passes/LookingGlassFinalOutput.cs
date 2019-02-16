using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{

    public class LookingGlassFinalOutput : ScriptableRenderPass
    {
        const string k_FinalBlitTag = "Final Blit Pass";

        private RenderTexture tiledTexture;
        private LookingGlassRenderingInfo drawInfo;
        private Material lenticularMat;
        private RenderTargetHandle colorAttachmentHandle { get; set; }
        private LookingGlassDeviceConfig deviceConfig;

        public void SetUp(RenderTargetHandle colorHandle,RenderTexture texture , 
            ref LookingGlassRenderingInfo dinfo,ref LookingGlassDeviceConfig dconfig)
        {
            this.colorAttachmentHandle = colorHandle;
            tiledTexture = texture;
            drawInfo = dinfo;
            deviceConfig = dconfig;
        }
        public void PassConfigToMaterial()
        {
            float screenInches = (float)deviceConfig.screenW / deviceConfig.DPI;
            float newPitch = deviceConfig.pitch * screenInches;
            newPitch *= Mathf.Cos(Mathf.Atan(1f / deviceConfig.slope));
            lenticularMat.SetFloat("pitch", newPitch);

            float newTilt = deviceConfig.screenH / (deviceConfig.screenW * deviceConfig.slope);
            newTilt *= LookingGlassDeviceConfig.AsBool(deviceConfig.flipImageX) ? -1 : 1;
            lenticularMat.SetFloat("tilt", newTilt);

            float newCenter = deviceConfig.center;
            newCenter += LookingGlassDeviceConfig.AsBool(deviceConfig.flipImageX) ? 0.5f : 0;
            lenticularMat.SetFloat("center", newCenter);
            lenticularMat.SetFloat("invView", deviceConfig.invView);
            lenticularMat.SetFloat("flipX", deviceConfig.flipImageX);
            lenticularMat.SetFloat("flipY", deviceConfig.flipImageY);

            float subp = 1f / (deviceConfig.screenW * 3f);
            subp *= LookingGlassDeviceConfig.AsBool(deviceConfig.flipImageX) ? -1 : 1;
            lenticularMat.SetFloat("subp", subp);

            lenticularMat.SetInt("ri", !LookingGlassDeviceConfig.AsBool(deviceConfig.flipSubp) ? 0 : 2);
            lenticularMat.SetInt("bi", !LookingGlassDeviceConfig.AsBool(deviceConfig.flipSubp) ? 2 : 0);

            float portionX = 1.0f;
            float portionY = 1.0f;

            drawInfo.CalculatePortion(out portionX, out portionY);

            lenticularMat.SetVector("tile", new Vector4(
                drawInfo.tileX,
                drawInfo.tileY,
                portionX,
                portionY
            ));
            bool overscan = false;
            lenticularMat.SetVector("aspect", new Vector4(
                deviceConfig.screenW / deviceConfig.screenH,
                deviceConfig.screenW / deviceConfig.screenH,
                overscan ? 1 : 0
            ));
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