cbuffer DynamicBuffer : register(b0)
{
	float3 Position;
}

float4 vertexShader(float4 position : POSITION) : SV_POSITION
{
	return position;
}

float3 pixelShader(float4 position : SV_POSITION) : SV_TARGET
{
	return float3(0.0f, 0.0f, 1.0f);
}