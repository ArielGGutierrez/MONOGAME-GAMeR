// Adaptive Refinement Shader

/* Matrices */
float4x4  World;
float4x4  View;
float4x4  Projection;
float4x4  WorldInverseTranspose;

float3 CameraPosition;

/* Reflection Shader */
texture environmentMap;
float reflectivity;

int displaceFunc = 0;

/* Sine Function Parameters */
float amplitude = 1;
float period = 3;
float phaseShift = 0;
float verticalShift = 0;

/* Coarse Triangle */
float3 p0, p1, p2, n0, n1, n2;

float dispFunc(float3 v, float3 n)
{
	if (displaceFunc == 0) return 0;
	else if (displaceFunc == 1)
	{
		float dist1 = abs(distance(v, p0));
		float dist2 = abs(distance(v, p1));
		float dist3 = abs(distance(v, p2));
		float min1 = min(dist1, dist2);
		float min2 = min(min1, dist3);

		float3 closestPos, closestNorm;
		if (min2 == dist1) { closestPos = p0; closestNorm = n0; }
		if (min2 == dist2) { closestPos = p1; closestNorm = n1; }
		if (min2 == dist3) { closestPos = p2; closestNorm = n2; }
		if (min2 <= 0.01) return 0;

		float3 p = v - closestPos;
		return -dot(p, closestNorm);
	}
	else
	{
		float dist1 = abs(distance(v, p0));
		float dist2 = abs(distance(v, p1));
		float dist3 = abs(distance(v, p2));

		float min1 = min(dist1, dist2);
		float min2 = min(min1, dist3);
		if (min2 <= 0.01) return 0;

		return amplitude * sin(period * (min2 + phaseShift)) + verticalShift;
	}
}

/* Skybox Sampler */
samplerCUBE SkyBoxSampler = sampler_state
{
	texture = <environmentMap>;
	magfilter = LINEAR;
	minfilter = LINEAR;
	mipfilter = LINEAR;
	AddressU = Mirror;
	AddressV = Mirror;
};

struct VS_IN
{
	float4 Position : POSITION;
};

struct VS_OUT
{
	float4 Position : POSITION;
	float4 Normal : TEXCOORD0;
	float4 WorldPosition : TEXCOORD1;
};

/* Vertex Shader */
VS_OUT TheVertexShader(VS_IN input)
{
	VS_OUT output;
	
	float w = input.Position.x; // w=1-u-v
	float u = input.Position.y;
	float v = input.Position.z; 

	float4 pos    = float4(p0 * w + p1 * u + p2 * v, input.Position.w);
	float4 normal = float4(n0 * w + n1 * u + n2 * v, 1);

	// User-defined Displacement Function
	float d = dispFunc(pos.xyz, normal.xyz);
	pos += d * normal;
	
	float4 worldPos = mul(pos, World);
	float4 viewPos = mul(worldPos, View);
	output.Position = mul(viewPos, Projection);
	output.WorldPosition = worldPos;
	output.Normal = normal;
	
	return output;
}

/* Pixel Shader */
float4 ThePixelShader(VS_OUT input) : COLOR
{
	float3 N = normalize((mul(input.Normal, WorldInverseTranspose)).xyz);
	float3 I = normalize(input.WorldPosition.xyz - CameraPosition);
	float3 R = reflect(I, N);

	// Fetch reflected environment color
	float4 reflectedColor = texCUBE(SkyBoxSampler, R);

	// Fetch base color
	float4 decalColor = input.WorldPosition + 0.5;

	// Put colors together
	float4 color = color = lerp(decalColor, reflectedColor, reflectivity);

	return color;
	
}

technique Technique
{
	pass Pass1
	{
		VertexShader = compile vs_4_0 TheVertexShader();
		PixelShader = compile ps_4_0 ThePixelShader();
	}
}