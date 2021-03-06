#ifndef LIGHTWEIGHT_FORWARD_LIT_PASS_INCLUDED
#define LIGHTWEIGHT_FORWARD_LIT_PASS_INCLUDED

#include "Assets/RenderPipeline/render-pipelines.looking/ShaderLibrary/Lighting.hlsl"

// for looking glass
#if LG_SINGLEPASS_INSTANCING
	UNITY_INSTANCING_BUFFER_START(PerDrawLooking)
		UNITY_DEFINE_INSTANCED_PROP(float4, LookingVPOffset)
		UNITY_DEFINE_INSTANCED_PROP(float4, LookingScreenRect)
		// debug
		UNITY_DEFINE_INSTANCED_PROP(float4x4, LookingView)
		UNITY_DEFINE_INSTANCED_PROP(float4x4, LookingProjection)
		UNITY_DEFINE_INSTANCED_PROP(float4x4, LookingVP)
	UNITY_INSTANCING_BUFFER_END(PerDrawLooking)


CBUFFER_START(_LookingGlassBuffer)
	float4 LookingQuiltSize;
CBUFFER_END

#endif
//===================


struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

#ifdef _ADDITIONAL_LIGHTS
    float3 positionWS               : TEXCOORD2;
#endif

#ifdef _NORMALMAP
    half4 normalWS                  : TEXCOORD3;    // xyz: normal, w: viewDir.x
    half4 tangentWS                 : TEXCOORD4;    // xyz: tangent, w: viewDir.y
    half4 bitangentWS                : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
#else
    half3 normalWS                  : TEXCOORD3;
    half3 viewDirWS                 : TEXCOORD4;
#endif

    half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

#ifdef _MAIN_LIGHT_SHADOWS
    float4 shadowCoord              : TEXCOORD7;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#ifdef _ADDITIONAL_LIGHTS
    inputData.positionWS = input.positionWS;
#endif

#ifdef _NORMALMAP
    half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
    inputData.normalWS = TransformTangentToWorld(normalTS,
        half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz));
#else
    half3 viewDirWS = input.viewDirWS;
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);

#if SHADER_HINT_NICE_QUALITY
    viewDirWS = SafeNormalize(viewDirWS);
#endif

    inputData.viewDirectionWS = viewDirWS;
#if defined(_MAIN_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
    inputData.shadowCoord = input.shadowCoord;
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = input.fogFactorAndVertexLight.x;
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);

#if LG_SINGLEPASS_INSTANCING
	float4 lg_vpOffset = UNITY_ACCESS_INSTANCED_PROP(PerDrawLooking,LookingVPOffset);
	unity_MatrixV[0][3] -= lg_vpOffset.x;
	unity_MatrixV[1][3] -= lg_vpOffset.y;
	float4x4 matrixP = UNITY_MATRIX_P;
	matrixP[0][2] -= lg_vpOffset.z;
	matrixP[1][2] -= lg_vpOffset.w;
	unity_MatrixVP = mul( matrixP , unity_MatrixV );
#else
	float4x4 matrixP = UNITY_MATRIX_P;
	unity_MatrixVP = mul( matrixP , unity_MatrixV );

#endif



    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;

#if !SHADER_HINT_NICE_QUALITY
    viewDirWS = SafeNormalize(viewDirWS);
#endif

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);

#ifdef _NORMALMAP
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = viewDirWS;
#endif
    
    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

#ifdef _ADDITIONAL_LIGHTS
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(_MAIN_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

#if LG_SINGLEPASS_INSTANCING

	// Looking Glass Add
	float4 lg_screenRect = UNITY_ACCESS_INSTANCED_PROP(PerDrawLooking,LookingScreenRect);

	float centerX = lg_screenRect.x * output.positionCS.w;
	float centerY = lg_screenRect.y * output.positionCS.w;

	output.positionCS.x = output.positionCS.x * lg_screenRect.z + centerX;
	output.positionCS.y = output.positionCS.y * lg_screenRect.w + centerY;
	
	float tileWParam = lg_screenRect.z * output.positionCS.w;
	float tileHParam = lg_screenRect.w * output.positionCS.w;

	output.positionCS.x  = clamp( output.positionCS.x  , 
		centerX - tileWParam * 1.3,
		centerX + tileWParam * 1.3);
	output.positionCS.y  = clamp( output.positionCS.y  , 
		centerY - tileHParam * 1.3,
		centerY + tileHParam * 1.3);
	// ============================
	
	UNITY_TRANSFER_INSTANCE_ID(input,output);
#endif

    return output;
}

// Used in Standard (Physically Based) shader
half4 LitPassFragment(Varyings input) : SV_Target
{
#if LG_SINGLEPASS_INSTANCING

    UNITY_SETUP_INSTANCE_ID(input);
    float4 lg_screenRect = UNITY_ACCESS_INSTANCED_PROP(PerDrawLooking,LookingScreenRect);
	float4 screenSize = float4(LookingQuiltSize.x , LookingQuiltSize.y,0,0);

	float centerX = screenSize.x * (0.5 * lg_screenRect.x + 0.5);
	float centerY = screenSize.y * (0.5 * -lg_screenRect.y + 0.5);
	float tileWParam = screenSize.x * lg_screenRect.z * 0.5;
	float tileHParam = screenSize.y * lg_screenRect.w * 0.5;
	//
	if( input.positionCS.x > centerX + tileWParam || input.positionCS.x < centerX - tileWParam){
		discard;
	}
	if( lg_screenRect.y < 0.0 ){
		//discard;
	}

	if( input.positionCS.y > centerY + tileHParam || input.positionCS.y < centerY - tileHParam )
	{
		discard;
	}

#endif


    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    half4 color = LightweightFragmentPBR(inputData, surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha);

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

#endif
