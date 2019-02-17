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

        FilterRenderersSettings m_OpaqueFilterSettings;
        FilterRenderersSettings m_TransparentFilterSettings;

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
            Camera camera = renderingData.cameraData.camera;
            SetupCameraInfo(camera);

            // clear Tile Texture
            commandBuffer.SetRenderTarget(dstTiledTexture);
            commandBuffer.ClearRenderTarget(true, true, Color.black);
            context.ExecuteCommandBuffer(commandBuffer);
            context.Submit();
            commandBuffer.Clear();

            // setup 
            CalculateVpMatrixOffsetsAndTileRects(camera);

            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();

            materialPropertyBlock.SetVectorArray("LookingVPOffset", m_VpOffsetParam);
            materialPropertyBlock.SetVectorArray("LookingScreenRect", m_ScreenRectParam);


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

        private void SetupCameraInfo(Camera camera)
        {
            camera.fieldOfView = perCameraInfo.fov;
            float adjustedDistance = GetAdjustedDistance(perCameraInfo.fov, perCameraInfo.size);
            camera.nearClipPlane = adjustedDistance - perCameraInfo.nearClipFactor * perCameraInfo.size;
            camera.farClipPlane = adjustedDistance + perCameraInfo.farClipFactor * perCameraInfo.size;

            camera.transform.position = new Vector3(0, 0, -adjustedDistance);
            camera.transform.localRotation = Quaternion.identity;
        }

        public static float GetAdjustedDistance(float fov,float size)
        {
            return size / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
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

            float width = 2.0f / (float)drawInfo.tileX;
            float height = 2.0f / (float)drawInfo.tileY;
            for (int i = 0; i < drawInfo.tileY; ++i)
            {
                for (int j = 0; j < drawInfo.tileX; ++j)
                {
                    m_VpOffsetParam[counter] = GetVPMatrixOffsets(aspect , counter, tileNum);

                    m_ScreenRectParam[counter] = new Vector4(
                        (j / (float)drawInfo.tileX) * 2.0f - 1.0f  + width *0.5f,
                        (i / (float)drawInfo.tileY) * 2.0f -1.0f + height * 0.5f,
                        width,height );
                    ++counter;
                }
            }
        }


        private Vector4 GetVPMatrixOffsets(float aspect ,int view, int numViews)
        {
            float adjustedDistance = GetAdjustedDistance(perCameraInfo.fov, perCameraInfo.size);

            float verticalAngle = 0.0f;
            float horizontalAngle = AngleAtView(view, numViews);

            float offsetX = adjustedDistance * Mathf.Tan(horizontalAngle * Mathf.Deg2Rad);
            float offsetY = adjustedDistance * Mathf.Tan(verticalAngle * Mathf.Deg2Rad);

            Vector4 result = new Vector4(offsetX, offsetY,
                offsetX / (perCameraInfo.size * aspect), offsetY / perCameraInfo.size);
            return result;
            // view matrix
            /*
            viewMatrix.m03 -= offsetX;
            viewMatrix.m13 -= offsetY;

            // proj matrix
            projMatrix.m02 -= offsetX / (perCameraInfo.size * camera.aspect);
            projMatrix.m12 -= offsetY / perCameraInfo.size;
            */
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