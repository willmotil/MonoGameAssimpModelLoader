#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

//float2 TexureCoordinate : TEXCOORD0; //float4 Color : COLOR0; //float3 Normal : NORMAL0; //float3 Tangent : NORMAL1; //float4 boneIds : BLENDINDICES; //float4 boneWeights : BLENDWEIGHT;


Texture2D TextureA; // primary texture.
sampler TextureSamplerA = sampler_state
{
    texture = <TextureA>;
	//magfilter = LINEAR; //minfilter = LINEAR; //mipfilter = LINEAR; //AddressU = mirror; //AddressV = mirror; 
};

matrix World;
matrix View;
matrix Projection;

//_______________________________________________________________
// techniques 
// Quad Draw   ,  just draws a quad
//
//_______________________________________________________________
struct VsInputQuad
{
    float4 Position : POSITION0;
    float2 TexureCoordinateA : TEXCOORD0;
};
struct VsOutputQuad
{
    float4 Position : SV_Position;
    float2 TexureCoordinateA : TEXCOORD0;
};


// ____________________________
VsOutputQuad VertexShaderQuadDraw(VsInputQuad input)
{
    VsOutputQuad output;
    float4 pos = mul(input.Position, World);
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    output.TexureCoordinateA = input.TexureCoordinateA;
    return output;
}

float4 PixelShaderQuadDraw(VsOutputQuad input) : COLOR0
{
    float4 result = tex2D(TextureSamplerA, input.TexureCoordinateA); // *input.Color;
    return result;
}
technique TriangleDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderQuadDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderQuadDraw();
    }
}



// VsPs col draw

//_______________________________________________________________
// techniques 
// Quad Draw   ,  just draws a quad
//
//_______________________________________________________________
struct VsInputColorQuad
{
    float4 Position : POSITION0;
    float4 Color : Color0;
    //float2 TexureCoordinateA : TEXCOORD0;
};
struct VsOutputColorQuad
{
    float4 Position : SV_Position;
    float4 Color : Color0;
    //float2 TexureCoordinateA : TEXCOORD0;
};

// ____________________________
VsOutputColorQuad VertexShaderColorQuadDraw(VsInputColorQuad input)
{
    VsOutputColorQuad output;
    float4 pos = mul(input.Position, World);
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    output.Color = input.Color;
    //output.TexureCoordinateA = input.TexureCoordinateA;
    return output;
}

float4 PixelShaderColorQuadDraw(VsOutputColorQuad input) : COLOR0
{
    //float4 result = tex2D(TextureSamplerA, input.TexureCoordinateA); // *input.Color;
    float4 result = input.Color;
    return result;
}
technique ColorTriangleDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderColorQuadDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderColorQuadDraw();
    }
}


//_______________________________________________________________
// techniques 
// TriangleColorTextureDraw   ,  just draws a quad uses color texture as well.
//
//_______________________________________________________________
struct VsInputColorTextureQuad
{
    float4 Position : POSITION0;
    float4 Color : Color0;
    float2 TexureCoordinateA : TEXCOORD0;
};
struct VsOutputColorTextureQuad
{
    float4 Position : SV_Position;
    float4 Color : Color0;
    float2 TexureCoordinateA : TEXCOORD0;
};


// ____________________________
VsOutputColorTextureQuad VertexShaderColorTextureQuadDraw(VsInputColorTextureQuad input)
{
    VsOutputColorTextureQuad output;
    float4 pos = mul(input.Position, World);
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    output.Color = input.Color;
    output.TexureCoordinateA = input.TexureCoordinateA;
    return output;
}

float4 PixelShaderColorTextureQuadDraw(VsOutputColorTextureQuad input) : COLOR0
{
    float4 result = tex2D(TextureSamplerA, input.TexureCoordinateA) *input.Color;
    return result;
}
technique TriangleColorTextureDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderColorTextureQuadDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderColorTextureQuadDraw();
    }
}