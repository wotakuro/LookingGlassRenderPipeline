#ifndef LIGHTWEIGHT_UNLIT_META_PASS_INCLUDED
#define LIGHTWEIGHT_UNLIT_META_PASS_INCLUDED

#include "Assets/RenderPipeline/render-pipelines.looking/ShaderLibrary/MetaInput.hlsl"

half4 LightweightFragmentMetaUnlit(Varyings input) : SV_Target
{
    MetaInput metaInput = (MetaInput)0;
    metaInput.Albedo = _Color.rgb * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;

    return MetaFragment(metaInput);
}

#endif
