// for reference
// struct semantics
//float2 TexureCoordinate : TEXCOORD0; //float4 Color : COLOR0; //float3 Normal : NORMAL0; //float3 Tangent : NORMAL1; //float4 boneIds : BLENDINDICES; //float4 boneWeights : BLENDWEIGHT;
// sampler options
//magfilter = LINEAR; //minfilter = LINEAR; //mipfilter = LINEAR; //AddressU = mirror; //AddressV = mirror; 

//_______________________________________________________________
// RiggedModelEffect.fx
//
// Defines
//_______________________________________________________________

#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif



//_______________________________________________________________
// textures and samplers
//
//_______________________________________________________________


Texture2D TextureA; // primary texture.
sampler TextureSamplerA = sampler_state
{
    texture = <TextureA>;
};



//_______________________________________________________________
// members
//
//_______________________________________________________________

matrix World;
matrix View;
matrix Projection;
matrix Bones[128];

float3 WorldLightPosition;
float3 CameraPosition;

float boneIdToSee = -1.0f; // more of a debuging value then anything



//_______________________________________________________________
// structs
// used by:  RiggedModelDraw, SkinedDebugModelDraw
//_______________________________________________________________
struct VsInputSkinnedQuad
{
    float4 Position : POSITION0;
    float2 TexureCoordinateA : TEXCOORD0;
    float3 Normal : NORMAL0;
    float4 BlendIndices : BLENDINDICES0;
    float4 BlendWeights : BLENDWEIGHT0;
};
struct VsOutputSkinnedQuad
{
    float4 Position : SV_Position;
    float4 Color : Color0;
    float2 TexureCoordinateA : TEXCOORD0;
    float3 Position3D : TEXCOORD1;
    float3 Normal3D : TEXCOORD2;
};



//_______________________________________________________________
// techniques 
// RiggedModelDraw
//
//_______________________________________________________________

VsOutputSkinnedQuad VertexShaderSkinnedDraw(VsInputSkinnedQuad input)
{
    VsOutputSkinnedQuad output;
    float4 pos = mul(input.Position, World);
    //float4x4 wvp = mul(World, mul(View, Projection));

    float weightA = input.BlendWeights.x;
    float weightB = input.BlendWeights.y;
    float weightC = input.BlendWeights.z;
    float weightD = input.BlendWeights.w;
    float sum = weightA + weightB + weightC + weightD;

    weightA = weightA / sum;
    weightB = weightB / sum;
    weightC = weightC / sum;
    weightD = weightD / sum;

    pos =
    mul(pos, Bones[input.BlendIndices.x]) * weightA +
    mul(pos, Bones[input.BlendIndices.y]) * weightB +
    mul(pos, Bones[input.BlendIndices.z]) * weightC +
    mul(pos, Bones[input.BlendIndices.w]) * weightD;

    
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    output.Color = float4(1.0f, 1.0f, 1.0f, 1.0f); // place holder dunno if added color.
    output.TexureCoordinateA = input.TexureCoordinateA;
    output.Position3D = pos.xyz;
    output.Normal3D = mul(float4(input.Normal, 1.0f), World).xyz;
    return output;
}

float4 PixelShaderSkinnedDraw(VsOutputSkinnedQuad input) : COLOR0
{
    // some colors.
    // float4(1.0f, 1.0f, 1.0f, 1.0f); //white // float4(0.09f, .99f, 0.99f, 1.0f); teal // float4(1.0f, .75f, 0.0f, 1.0f); rgb(255,215,0) orange // float4(0.85f, .75f, 0.9f, 1.0f); blue tint // float4(0.85f, .85f, 0.99f, 1.0f); // float4(0.09f, .09f, 0.99f, 1.0f); blue
    //
    float3 toLight = normalize(WorldLightPosition - input.Position3D);
    float3 surfaceToCamera = normalize(CameraPosition - input.Position3D); //View._m30_m31_m32
    float3 reflectionTheta = dot(surfaceToCamera, reflect(toLight, input.Normal3D));
    float IsFrontFace = sign(saturate(dot(toLight, input.Normal3D))); // 1 is Frontface 0 is Backface.
    float specularSharpness = 0.6f; // prolly round .5 is normal 0 is like diffuse basically .99 is like a dot.
    float specularAmt = 0.30f;
    float ambientAmt = 0.15f;
    float diffuseAmt = 1.0f - (ambientAmt + specularAmt);
    float4 lightColor = float4(0.89f, .89f, 0.99f, 1.0f);
    float4 texelColor = tex2D(TextureSamplerA, input.TexureCoordinateA) * input.Color;
    float diffuse = saturate(dot(input.Normal3D, toLight)) * diffuseAmt;
    float specular = saturate(reflectionTheta - specularSharpness) * (1.0f / (1.0f - specularSharpness)) * IsFrontFace; // screw that phong shading and power its nice but it also sucks.
    float4 result = texelColor * ambientAmt + texelColor * diffuse + (texelColor * 0.35f + lightColor * 0.65f) * specular * specularAmt;
    return result;
}

technique RiggedModelDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderSkinnedDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderSkinnedDraw();
    }
}




//_______________________________________________________________
// techniques 
// SkinedDebugModelDraw
//
//_______________________________________________________________

VsOutputSkinnedQuad VertexShaderDebugSkinnedDraw(VsInputSkinnedQuad input)
{
    VsOutputSkinnedQuad output;
    float4 pos = mul(input.Position, World);

    float weightA = input.BlendWeights.x;
    float weightB = input.BlendWeights.y;
    float weightC = input.BlendWeights.z;
    float weightD = input.BlendWeights.w;

    float sum = weightA + weightB + weightC + weightD;
    weightA = weightA / sum;
    weightB = weightB / sum;
    weightC = weightC / sum;
    weightD = weightD / sum;

    pos =
    mul(pos, Bones[input.BlendIndices.x]) * weightA +
    mul(pos, Bones[input.BlendIndices.y]) * weightB +
    mul(pos, Bones[input.BlendIndices.z]) * weightC +
    mul(pos, Bones[input.BlendIndices.w]) * weightD;

    float4 col = float4(0.40f, 0.40f, 0.40f, 1.0f);

    // i could get rid of the if with some math but its just a debug shader.
    if (input.BlendIndices.x == boneIdToSee || input.BlendIndices.y == boneIdToSee || input.BlendIndices.z == boneIdToSee || input.BlendIndices.w == boneIdToSee)
        col = float4(0.49f, 0.99f, 0.49f, 1.0f);

    output.Color = col;

    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    output.TexureCoordinateA = input.TexureCoordinateA;
    output.Position3D = pos.xyz;
    output.Normal3D = mul(float4(input.Normal, 1.0f), World).xyz;
    return output;
}

float4 PixelShaderDebugSkinnedDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float4 result = tex2D(TextureSamplerA, input.TexureCoordinateA) * input.Color;
    return result;
}

technique SkinedDebugModelDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderDebugSkinnedDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderDebugSkinnedDraw();
    }
}

