using System;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{
    internal class DefaultRendererSetup : IRendererSetup
    {
        private MainLightShadowCasterPass m_MainLightShadowCasterPass;
        private SetupForwardRenderingPass m_SetupForwardRenderingPass;
        private CreateLightweightRenderTexturesPass m_CreateLightweightRenderTexturesPass;
        private SetupLightweightConstanstPass m_SetupLightweightConstants;
        private RenderOpaqueForwardPass m_RenderOpaqueForwardPass;
        private OpaquePostProcessPass m_OpaquePostProcessPass;
        private DrawSkyboxPass m_DrawSkyboxPass;
        private CopyDepthPass m_CopyDepthPass;
        private RenderTransparentForwardPass m_RenderTransparentForwardPass;
        private FinalBlitPass m_FinalBlitPass;

        private LookingGlassMultiTextureRenderer m_LookingMultiTexturePass;
        private LookingGlassInstancingRenderPass m_LookingInstancingRenderPass;
        private LookingGlassFinalOutput m_LookingFinalPass;

#if UNITY_EDITOR
        private SceneViewDepthCopyPass m_SceneViewDepthCopyPass;
#endif


        private RenderTargetHandle ColorAttachment;
        private RenderTargetHandle DepthAttachment;
        private RenderTargetHandle DepthTexture;
        private RenderTargetHandle OpaqueColor;
        private RenderTargetHandle MainLightShadowmap;
        

        [NonSerialized]
        private bool m_Initialized = false;

        private RenderTexture tileTexture;

        private void Init()
        {
            if (m_Initialized)
                return;
            
            m_MainLightShadowCasterPass = new MainLightShadowCasterPass();
            m_SetupForwardRenderingPass = new SetupForwardRenderingPass();
            m_CreateLightweightRenderTexturesPass = new CreateLightweightRenderTexturesPass();
            m_SetupLightweightConstants = new SetupLightweightConstanstPass();
            m_RenderOpaqueForwardPass = new RenderOpaqueForwardPass();
            m_OpaquePostProcessPass = new OpaquePostProcessPass();
            m_DrawSkyboxPass = new DrawSkyboxPass();
            m_CopyDepthPass = new CopyDepthPass();
            m_RenderTransparentForwardPass = new RenderTransparentForwardPass();
            m_FinalBlitPass = new FinalBlitPass();

            m_LookingMultiTexturePass = new LookingGlassMultiTextureRenderer();
            m_LookingInstancingRenderPass = new LookingGlassInstancingRenderPass();
            m_LookingFinalPass = new LookingGlassFinalOutput();

#if UNITY_EDITOR
            m_SceneViewDepthCopyPass = new SceneViewDepthCopyPass();
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            ColorAttachment.Init("_CameraColorTexture");
            DepthAttachment.Init("_CameraDepthAttachment");
            DepthTexture.Init("_CameraDepthTexture");
            OpaqueColor.Init("_CameraOpaqueTexture");
            MainLightShadowmap.Init("_MainLightShadowmapTexture");

            m_Initialized = true;

        }

        private LookingGlassRenderingInfo oldLookingRenderInfo;


        public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Init();

            Camera camera = renderingData.cameraData.camera;

            renderer.SetupPerObjectLightIndices(ref renderingData.cullResults, ref renderingData.lightData);
            RenderTextureDescriptor baseDescriptor = ScriptableRenderer.CreateRenderTextureDescriptor(ref renderingData.cameraData);
            RenderTextureDescriptor shadowDescriptor = baseDescriptor;
            ClearFlag clearFlag = ScriptableRenderer.GetCameraClearFlag(renderingData.cameraData.camera);
            shadowDescriptor.dimension = TextureDimension.Tex2D;

            bool requiresRenderToTexture = ScriptableRenderer.RequiresIntermediateColorTexture(ref renderingData.cameraData, baseDescriptor);

            RenderTargetHandle colorHandle = RenderTargetHandle.CameraTarget;
            RenderTargetHandle depthHandle = RenderTargetHandle.CameraTarget;

            if (requiresRenderToTexture)
            {
                colorHandle = ColorAttachment;
                depthHandle = DepthAttachment;

                var sampleCount = (SampleCount)renderingData.cameraData.msaaSamples;
                m_CreateLightweightRenderTexturesPass.Setup(baseDescriptor, colorHandle, depthHandle, sampleCount);
                renderer.EnqueuePass(m_CreateLightweightRenderTexturesPass);
            }


            bool mainLightShadows = false;
            if (renderingData.shadowData.supportsMainLightShadows)
            {
                mainLightShadows = m_MainLightShadowCasterPass.Setup(MainLightShadowmap, ref renderingData);
                if (mainLightShadows)
                    renderer.EnqueuePass(m_MainLightShadowCasterPass);
            }



            renderer.EnqueuePass(m_SetupForwardRenderingPass);


            RendererConfiguration rendererConfiguration = ScriptableRenderer.GetRendererConfiguration(renderingData.lightData.additionalLightsCount);

            m_SetupLightweightConstants.Setup(renderer.maxVisibleAdditionalLights, renderer.perObjectLightIndices);
            renderer.EnqueuePass(m_SetupLightweightConstants);
            // GameView at LGRP
            if (!renderingData.cameraData.isSceneViewCamera)
            {
                LookingGlassRenderingInfo info = renderingData.cameraData.lookingGlassInfo;
                LookingGlassDeviceConfig config;
                LookingGlassRenderInfoPerCamera perCameraInfo;

                var cameraDeviceInfo = camera.GetComponent<LookingGlassCameraInfo>();
                if (cameraDeviceInfo != null)
                {
                    config = cameraDeviceInfo.config;
                    perCameraInfo = cameraDeviceInfo.renderInfo;
                }
                else
                {
                    config = new LookingGlassDeviceConfig();
                    config.SetUpDefault();
                    perCameraInfo = new LookingGlassRenderInfoPerCamera();
                    perCameraInfo.SetupDefault();
                }

                int depthValue = (info.renderMethod == LookingGlassRenderingInfo.RenderingMethod.RenderMultiPass) ? 0 : 32;
                if( tileTexture != null && oldLookingRenderInfo.HaveToRemakeRenderTexture( ref info) )
                {
                    tileTexture.Release();
                    tileTexture = null;
                    Debug.Log("Recreate RenderTexture");
                }

                if (tileTexture == null || !tileTexture)
                {
                    tileTexture = new RenderTexture(info.renderTargetW, info.renderTargetH, depthValue);
                    tileTexture.name = "LookingGlassQuiltTexture";
                }

                // tile texture draw( changed by method)
                switch (info.renderMethod)
                {
                    case LookingGlassRenderingInfo.RenderingMethod.RenderMultiPass:
                        m_LookingMultiTexturePass.Setup(tileTexture, ref info, ref perCameraInfo);
                        renderer.EnqueuePass(m_LookingMultiTexturePass);
                        break;
                    case LookingGlassRenderingInfo.RenderingMethod.RenderSinglePassInstancing:
                        m_LookingInstancingRenderPass.Setup(tileTexture, ref info, ref perCameraInfo);
                        renderer.EnqueuePass(m_LookingInstancingRenderPass);
                        break;
                }

                m_LookingFinalPass.SetUp(colorHandle, tileTexture, ref info,ref config);
                renderer.EnqueuePass(m_LookingFinalPass);
                // info setup
                oldLookingRenderInfo = info;
            }
            // SceneView
            else
            {
                m_RenderOpaqueForwardPass.Setup(baseDescriptor, colorHandle, depthHandle, clearFlag, camera.backgroundColor, rendererConfiguration);
                renderer.EnqueuePass(m_RenderOpaqueForwardPass);
                if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
                {
                    m_DrawSkyboxPass.Setup(colorHandle, depthHandle);
                    renderer.EnqueuePass(m_DrawSkyboxPass);
                }
                m_RenderTransparentForwardPass.Setup(baseDescriptor, colorHandle, depthHandle, rendererConfiguration);
                renderer.EnqueuePass(m_RenderTransparentForwardPass);
                if (!renderingData.cameraData.isOffscreenRender && colorHandle != RenderTargetHandle.CameraTarget)
                {
                    m_FinalBlitPass.Setup(baseDescriptor, colorHandle);
                    renderer.EnqueuePass(m_FinalBlitPass);
                }
            }

#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                m_SceneViewDepthCopyPass.Setup(DepthTexture);
                renderer.EnqueuePass(m_SceneViewDepthCopyPass);
            }
#endif
        }

        bool CanCopyDepth(ref CameraData cameraData)
        {
            bool msaaEnabledForCamera = (int)cameraData.msaaSamples > 1;
            bool supportsTextureCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;
            bool supportsDepthTarget = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
            bool supportsDepthCopy = !msaaEnabledForCamera && (supportsDepthTarget || supportsTextureCopy);

            // TODO:  We don't have support to highp Texture2DMS currently and this breaks depth precision.
            // currently disabling it until shader changes kick in.
            //bool msaaDepthResolve = msaaEnabledForCamera && SystemInfo.supportsMultisampledTextures != 0;
            bool msaaDepthResolve = false;
            return supportsDepthCopy || msaaDepthResolve;
        }

        private void SaveRenderTexture(RenderTexture renderTexture,string file)
        {
            if( System.IO.File.Exists(file + ".png"))
            {
                return;
            }

            Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            RenderTexture.active = renderTexture;
            tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tex.Apply();

            // Encode texture into PNG
            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            //Write to a file in the project folder
            System.IO.File.WriteAllBytes( file + ".png", bytes);
            Debug.Log(Application.dataPath);
        }
    }
}
