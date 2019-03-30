using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{

    
    // DrawPass
    public class LookingGlassInstancingRenderPass : ScriptableRenderPass
    {
        private LookingGlassRenderingInfo drawInfo;
        private LookingGlassRenderInfoPerCamera perCameraInfo;
        private CommandBuffer commandBuffer;
        const string LgInstancingShaderKeyword = "LG_SINGLEPASS_INSTANCING";


        private RendererConfiguration rendererConfiguration = RendererConfiguration.None;

        private RenderTexture dstTiledTexture;

        private Vector4[] m_VpOffsetParam;
        private Vector4[] m_ScreenRectParam;

        private Matrix4x4[] m_RenderMatrix;

        public void Setup(RenderTexture dst,ref LookingGlassRenderingInfo dinfo,
            ref LookingGlassRenderInfoPerCamera perCamInfo)
        {
            dstTiledTexture = dst;
            this.drawInfo = dinfo;
            this.perCameraInfo = perCamInfo;
        }

        public LookingGlassInstancingRenderPass()
        {

            RegisterShaderPassName("LightweightForward");
            RegisterShaderPassName("SRPDefaultUnlit");

            commandBuffer = new CommandBuffer();
            commandBuffer.name = "SingleInstance";
        }


        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            LookingGlassUtil.SetupCameraInfo(camera, perCameraInfo.fov, perCameraInfo.size, perCameraInfo.nearClipFactor, perCameraInfo.farClipFactor);

            // clear Tile Texture
            commandBuffer.SetRenderTarget(dstTiledTexture);
            commandBuffer.ClearRenderTarget(true, true, Color.black);
            context.ExecuteCommandBuffer(commandBuffer);

            // setup 
            CalculateVpMatrixOffsetsAndTileRects(camera);
            Shader.SetGlobalVector("LookingQuiltSize", new Vector4(dstTiledTexture.width,dstTiledTexture.height,0,0));

            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();

            materialPropertyBlock.SetVectorArray("LookingVPOffset", m_VpOffsetParam);
            materialPropertyBlock.SetVectorArray("LookingScreenRect", m_ScreenRectParam);
            DebugSetup(camera, materialPropertyBlock);


            Shader.EnableKeyword(LgInstancingShaderKeyword);
            // todo メッシュ一覧取得周りの仕組みつくる

            var meshFilters = Resources.FindObjectsOfTypeAll<MeshFilter>();
            foreach( var meshFilter in meshFilters)
            {
                var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                var mesh = meshFilter.sharedMesh;
                if (mesh == null) { continue; }
                if (meshRenderer == null || meshRenderer.sharedMaterial == null){ continue; }
                var material = meshRenderer.sharedMaterial;

                if(!material.enableInstancing)
                {
                    continue;
                }

                var matrix = meshFilter.transform.localToWorldMatrix;
                this.CalculateInstancingMatrix(ref matrix);
                

                commandBuffer.DrawMeshInstanced(mesh, 0, material, 0, this.m_RenderMatrix,
                    m_RenderMatrix.Length, materialPropertyBlock);
            }
            context.ExecuteCommandBuffer(commandBuffer);
            context.Submit();
            commandBuffer.Clear();

            Shader.DisableKeyword(LgInstancingShaderKeyword);
        }
        



        private void CalculateInstancingMatrix(ref Matrix4x4 origin)
        {
            int tileNum = drawInfo.tileX * drawInfo.tileY;
            if( m_RenderMatrix == null || m_RenderMatrix.Length != tileNum)
            {
                m_RenderMatrix = new Matrix4x4[tileNum];
            }
            for( int i = 0; i < tileNum; ++i)
            {
                m_RenderMatrix[i] = origin;
            }
        }


        private void CalculateVpMatrixOffsetsAndTileRects(Camera camera)
        {
            int counter = 0;
            int tileNum = drawInfo.tileX * drawInfo.tileY;
            float aspect = camera.aspect;

            if(m_VpOffsetParam == null || m_VpOffsetParam.Length != tileNum)
            {
                m_VpOffsetParam = new Vector4[tileNum];
            }
            if(m_ScreenRectParam == null || m_ScreenRectParam.Length != tileNum)
            {
                m_ScreenRectParam = new Vector4[tileNum];
            }

            float width = 1.0f / (float)drawInfo.tileX;
            float height = 1.0f / (float)drawInfo.tileY;
            for (int i = 0; i < drawInfo.tileY; ++i)
            {
                for (int j = 0; j < drawInfo.tileX; ++j)
                {
                    m_VpOffsetParam[counter] = LookingGlassUtil.GetVPMatrixOffsets(aspect ,perCameraInfo.fov,perCameraInfo.size, counter, tileNum);

                    m_ScreenRectParam[tileNum - counter -1] = new Vector4(
                        (j / (float)drawInfo.tileX) * 2.0f - 1.0f  + width ,
                        (i / (float)drawInfo.tileY) * 2.0f -1.0f + height ,
                        width  ,height  );
                    ++counter;
                }
            }
        }

        private void DebugSetup(Camera camera,MaterialPropertyBlock materialPropertyBlock)
        {

            int tileNum = drawInfo.tileX * drawInfo.tileY;
            Matrix4x4[] lookingView = new Matrix4x4[tileNum];
            Matrix4x4[] lookingProjection = new Matrix4x4[tileNum];
            Matrix4x4[] lookingVp = new Matrix4x4[tileNum];
            int counter = 0;
            for (int i = 0; i < drawInfo.tileY; ++i)
            {
                for (int j = 0; j < drawInfo.tileX; ++j)
                {
                    Matrix4x4 projMatrix = camera.projectionMatrix;
                    Matrix4x4 viewMatrix = camera.worldToCameraMatrix;

                    float adjustedDistance = LookingGlassUtil.GetAdjustedDistance(perCameraInfo.fov, perCameraInfo.size);

                    float verticalAngle = 0.0f;
                    float horizontalAngle = LookingGlassUtil.AngleAtView(counter, tileNum);

                    float offsetX = adjustedDistance * Mathf.Tan(horizontalAngle * Mathf.Deg2Rad);
                    float offsetY = adjustedDistance * Mathf.Tan(verticalAngle * Mathf.Deg2Rad);

                    // view matrix
                    viewMatrix.m03 -= offsetX;
                    viewMatrix.m13 -= offsetY;

                    // proj matrix
                    projMatrix.m02 -= offsetX / (perCameraInfo.size * camera.aspect);
                    projMatrix.m12 -= offsetY / perCameraInfo.size;

                    lookingView[counter] = viewMatrix;
                    lookingProjection[counter] = projMatrix;
                    lookingVp[counter] = viewMatrix * projMatrix;
                    ++ counter;
                }
            }
            materialPropertyBlock.SetMatrixArray("LookingView", lookingView);
            materialPropertyBlock.SetMatrixArray("LookingProjection", lookingProjection);
            materialPropertyBlock.SetMatrixArray("LookingVP", lookingVp);
        }



        public void Dispose()
        {
            commandBuffer.Release();
        }
    }
}