#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{

    public enum ShadowQuality
    {
        Disabled,
        HardShadows,
        SoftShadows,
    }

    public enum ShadowResolution
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }

    public enum MsaaQuality
    {
        Disabled = 1,
        _2x = 2,
        _4x = 4,
        _8x = 8
    }

    public enum Downsampling
    {
        None,
        _2xBilinear,
        _4xBox,
        _4xBilinear
    }

    internal enum DefaultMaterialType
    {
        Standard,
        Particle,
        Terrain,
        UnityBuiltinDefault
    }

    public enum LightRenderingMode
    {
        Disabled = 0,
        PerVertex = 2,
        PerPixel = 1,
    }

    public enum ShaderVariantLogLevel
    {
        Disabled,
        OnlyLightweightRPShaders,
        AllShaders,
    }

    public class LookingGlassRenderPipelineAsset : RenderPipelineAsset, ISerializationCallbackReceiver
    {
        Shader m_DefaultShader;

        // Default values set when a new LightweightRenderPipeline asset is created
        [SerializeField] int k_AssetVersion = 4;



        // General settings
        [SerializeField] bool m_RequireDepthTexture = false;
        [SerializeField] bool m_RequireOpaqueTexture = false;
        [SerializeField] Downsampling m_OpaqueDownsampling = Downsampling._2xBilinear;

        // Quality settings
        [SerializeField] bool m_SupportsHDR = false;
        [SerializeField] MsaaQuality m_MSAA = MsaaQuality._4x;
        [SerializeField] float m_RenderScale = 1.0f;
        // TODO: Shader Quality Tiers

        // Main directional light Settings
        [SerializeField] LightRenderingMode m_MainLightRenderingMode = LightRenderingMode.PerPixel;
        [SerializeField] bool m_MainLightShadowsSupported = true;
        [SerializeField] ShadowResolution m_MainLightShadowmapResolution = ShadowResolution._2048;

        // Additional lights settings
        [SerializeField] LightRenderingMode m_AdditionalLightsRenderingMode = LightRenderingMode.PerPixel;
        [SerializeField] int m_AdditionalLightsPerObjectLimit = 4;
        [SerializeField] bool m_AdditionalLightShadowsSupported = false;
        [SerializeField] ShadowResolution m_AdditionalLightsShadowmapResolution = ShadowResolution._512;

        // Shadows Settings
        [SerializeField] float m_ShadowDistance = 50.0f;
        [SerializeField] float m_ShadowDepthBias = 1.0f;
        [SerializeField] float m_ShadowNormalBias = 1.0f;
        [SerializeField] bool m_SoftShadowsSupported = false;

        // Advanced settings
        [SerializeField] bool m_SupportsDynamicBatching = true;
        [SerializeField] bool m_MixedLightingSupported = true;

        // looking Glass settings
        [SerializeField] int m_lg_renderTargetW = 4096;
        [SerializeField] int m_lg_renderTargetH = 4096;
        [SerializeField] int m_lg_tileX = 4;
        [SerializeField] int m_lg_tileY = 9;
        [SerializeField] float m_lg_fov = 13.5f;
        [SerializeField] float m_lg_size = 2;
        [SerializeField] float m_lg_nearClipFactor = 1.0f;
        [SerializeField] float m_lg_farClipFactor = 1.5f;


        // TODO: Render Pipeline Batcher

        // Deprecated settings
        [SerializeField] ShadowQuality m_ShadowType = ShadowQuality.HardShadows;
        [SerializeField] bool m_LocalShadowsSupported = false;
        [SerializeField] ShadowResolution m_LocalShadowsAtlasResolution = ShadowResolution._256;
        [SerializeField] int m_MaxPixelLights = 0;
        [SerializeField] ShadowResolution m_ShadowAtlasResolution = ShadowResolution._256;

        [SerializeField] LookingGlassRenderPipelineResources m_ResourcesAsset = null;
        [SerializeField] ShaderVariantLogLevel m_ShaderVariantLogLevel = ShaderVariantLogLevel.Disabled;


#if UNITY_EDITOR
        [NonSerialized]
        LookingGlassRenderPipelineEditorResources m_EditorResourcesAsset;

        static readonly string s_SearchPathProject = "Assets";
        static readonly string s_SearchPathPackage = "Assets/RenderPipeline/render-pipelines.looking";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreateLightweightPipelineAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var instance = CreateInstance<LookingGlassRenderPipelineAsset>();
                instance.m_EditorResourcesAsset = LoadResourceFile<LookingGlassRenderPipelineEditorResources>();
                instance.m_ResourcesAsset = LoadResourceFile<LookingGlassRenderPipelineResources>();
                AssetDatabase.CreateAsset(instance, pathName);
            }
        }

        [MenuItem("Assets/Create/Rendering/LookingGlass Render Pipeline Asset", priority = CoreUtils.assetCreateMenuPriority1)]
        static void CreateLookingGlassPipeline()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateLightweightPipelineAsset>(),
                "LookingGlassRenderPipelineAsset.asset", null, null);
        }

        [MenuItem("Assets/Create/Rendering/Lightweight Pipeline Resources", priority = CoreUtils.assetCreateMenuPriority1)]
        static void CreateLightweightPipelineResources()
        {
            var instance = CreateInstance<LookingGlassRenderPipelineResources>();
            AssetDatabase.CreateAsset(instance, string.Format("Assets/{0}.asset", typeof(LookingGlassRenderPipelineResources).Name));
        }

        //[MenuItem("Assets/Create/Rendering/Lightweight Pipeline Editor Resources", priority = CoreUtils.assetCreateMenuPriority1)]
        static void CreateLightweightPipelineEditorResources()
        {
            var instance = CreateInstance<LookingGlassRenderPipelineEditorResources>();
            AssetDatabase.CreateAsset(instance, string.Format("Assets/{0}.asset", typeof(LookingGlassRenderPipelineEditorResources).Name));
        }

        static T LoadResourceFile<T>() where T : ScriptableObject
        {
            T resourceAsset = null;
            var guids = AssetDatabase.FindAssets(typeof(T).Name + " t:scriptableobject", new[] {s_SearchPathProject});
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                resourceAsset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (resourceAsset != null)
                    break;
            }

            // There's currently an issue that prevents FindAssets from find resources withing the package folder.
            if (resourceAsset == null)
            {
                string path = s_SearchPathPackage + "/Runtime/Data/" + typeof(T).Name + ".asset";
                resourceAsset = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return resourceAsset;
        }

        LookingGlassRenderPipelineEditorResources editorResources
        {
            get
            {
                if (m_EditorResourcesAsset == null)
                    m_EditorResourcesAsset = LoadResourceFile<LookingGlassRenderPipelineEditorResources>();

                return m_EditorResourcesAsset;
            }
        }
#endif
        LookingGlassRenderPipelineResources resources
        {
            get
            {
#if UNITY_EDITOR
                if (m_ResourcesAsset == null)
                    m_ResourcesAsset = LoadResourceFile<LookingGlassRenderPipelineResources>();
#endif
                return m_ResourcesAsset;
            }
        }

        protected override IRenderPipeline InternalCreatePipeline()
        {
            return new LightweightRenderPipeline(this);
        }

        Material GetMaterial(DefaultMaterialType materialType)
        {
#if UNITY_EDITOR
            if (editorResources == null)
                return null;

            switch (materialType)
            {
                case DefaultMaterialType.Standard:
                    return editorResources.litMaterial;

                case DefaultMaterialType.Particle:
                    return editorResources.particleLitMaterial;

                case DefaultMaterialType.Terrain:
                    return editorResources.terrainLitMaterial;

                // Unity Builtin Default
                default:
                    return null;
            }
#else
            return null;
#endif
        }
        public bool supportsCameraDepthTexture
        {
            get { return m_RequireDepthTexture; }
        }

        public bool supportsCameraOpaqueTexture
        {
            get { return m_RequireOpaqueTexture; }
        }

        public Downsampling opaqueDownsampling
        {
            get { return m_OpaqueDownsampling; }
        }

        public bool supportsHDR
        {
            get { return m_SupportsHDR; }
        }

        public int msaaSampleCount
        {
            get { return (int)m_MSAA; }
            set { m_MSAA = (MsaaQuality)value; }
        }

        public float renderScale
        {
            get { return m_RenderScale; }
            set { m_RenderScale = value; }
        }

        public LightRenderingMode mainLightRenderingMode
        {
            get { return m_MainLightRenderingMode; }
        }

        public bool supportsMainLightShadows
        {
            get { return m_MainLightShadowsSupported; }
        }

        public int mainLightShadowmapResolution
        {
            get { return (int)m_MainLightShadowmapResolution; }
        }

        public LightRenderingMode additionalLightsRenderingMode
        {
            get { return m_AdditionalLightsRenderingMode; }
        }

        public int maxAdditionalLightsCount
        {
            get { return m_AdditionalLightsPerObjectLimit; }
        }

        public bool supportsAdditionalLightShadows
        {
            get { return m_AdditionalLightShadowsSupported; }
        }

        public int additionalLightsShadowmapResolution
        {
            get { return (int)m_AdditionalLightsShadowmapResolution; }
        }

        public float shadowDistance
        {
            get { return m_ShadowDistance; }
            set { m_ShadowDistance = value; }
        }

        public int cascadeCount
        {
            get
            {
                return 1;
            }
        }
        public LookingGlassInfo lookingGlassInfo
        {
            get
            {
                LookingGlassInfo info = new LookingGlassInfo();

                info.renderTargetW = m_lg_renderTargetW;
                info.renderTargetH = m_lg_renderTargetH;
                info.tileX = m_lg_tileX;
                info.tileY = m_lg_tileY;
                info.fov = m_lg_fov;
                info.size = m_lg_size;
                info.nearClipFactor = m_lg_nearClipFactor;
                info.farClipFactor = m_lg_farClipFactor;
                return info;
            }
        }


        public float shadowDepthBias
        {
            get { return m_ShadowDepthBias; }
            set { m_ShadowDepthBias = value; }
        }

        public float shadowNormalBias
        {
            get { return m_ShadowNormalBias; }
            set { m_ShadowNormalBias = value; }
        }

        public bool supportsSoftShadows
        {
            get { return m_SoftShadowsSupported; }
        }

        public bool supportsDynamicBatching
        {
            get { return m_SupportsDynamicBatching; }
        }

        public bool supportsMixedLighting
        {
            get { return m_MixedLightingSupported; }
        }

        public ShaderVariantLogLevel shaderVariantLogLevel
        {
            get { return m_ShaderVariantLogLevel; }
        }

        public override Material GetDefaultMaterial()
        {
            return GetMaterial(DefaultMaterialType.Standard);
        }

        #if UNITY_EDITOR
        public override Shader GetAutodeskInteractiveShader()
        {
            return editorResources.autodeskInteractiveShader;
        }

        public override Shader GetAutodeskInteractiveTransparentShader()
        {
            return editorResources.autodeskInteractiveTransparentShader;
        }

        public override Shader GetAutodeskInteractiveMaskedShader()
        {
            return editorResources.autodeskInteractiveMaskedShader;
        }
        #endif

        public override Material GetDefaultParticleMaterial()
        {
            return GetMaterial(DefaultMaterialType.Particle);
        }

        public override Material GetDefaultLineMaterial()
        {
            return GetMaterial(DefaultMaterialType.UnityBuiltinDefault);
        }

        public override Material GetDefaultTerrainMaterial()
        {
            return GetMaterial(DefaultMaterialType.Terrain);
        }

        public override Material GetDefaultUIMaterial()
        {
            return GetMaterial(DefaultMaterialType.UnityBuiltinDefault);
        }

        public override Material GetDefaultUIOverdrawMaterial()
        {
            return GetMaterial(DefaultMaterialType.UnityBuiltinDefault);
        }

        public override Material GetDefaultUIETC1SupportedMaterial()
        {
            return GetMaterial(DefaultMaterialType.UnityBuiltinDefault);
        }

        public override Material GetDefault2DMaterial()
        {
            return GetMaterial(DefaultMaterialType.UnityBuiltinDefault);
        }

        public override Shader GetDefaultShader()
        {
            if (m_DefaultShader == null)
                m_DefaultShader = Shader.Find(ShaderUtils.GetShaderPath(ShaderPathID.PhysicallyBased));
            return m_DefaultShader;
        }

        public Shader blitShader
        {
            get { return resources != null ? resources.blitShader : null; }
        }

        public Shader copyDepthShader
        {
            get { return resources != null ? resources.copyDepthShader : null; }
        }

        public Shader screenSpaceShadowShader
        {
            get { return resources != null ? resources.screenSpaceShadowShader : null; }
        }

        public Shader samplingShader
        {
            get { return resources != null ? resources.samplingShader : null; }
        }
        
        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (k_AssetVersion < 3)
            {
                k_AssetVersion = 3;
                m_SoftShadowsSupported = (m_ShadowType == ShadowQuality.SoftShadows);
            }

            if (k_AssetVersion < 4)
            {
                k_AssetVersion = 4;
                m_AdditionalLightShadowsSupported = m_LocalShadowsSupported;
                m_AdditionalLightsShadowmapResolution = m_LocalShadowsAtlasResolution;
                m_AdditionalLightsPerObjectLimit = m_MaxPixelLights;
                m_MainLightShadowmapResolution = m_ShadowAtlasResolution;
            }
        }
    }
}
