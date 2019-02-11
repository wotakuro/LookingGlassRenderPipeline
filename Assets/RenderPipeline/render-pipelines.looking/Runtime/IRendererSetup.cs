namespace UnityEngine.Experimental.Rendering.LookingGlassPipeline
{
    public interface IRendererSetup
    {
        void Setup(ScriptableRenderer renderer, ref RenderingData renderingData);
    }
}
