
half Angle(float3 center, float3 pos1, float3 pos2)
{
	float3 dir1 = normalize(pos1 - center);
	float3 dir2 = normalize(pos2 - center);
	return degrees(acos(dot(dir1, dir2)));
}


float rayPlaneIntersection(float3 rayDir, float3 rayPos, float3 planeNormal, float3 planePos)
{
    float denom = dot(planeNormal, rayDir);
    denom = max(denom, 0.000001); // avoid divide by zero
    float3 diff = planePos - rayPos;
    return dot(diff, planeNormal) / denom;
}

			#define COMPUTE_DEPTH_01b -(viewPos.z * _ProjectionParams.w)
			#define COMPUTE_DEPTH_01 -(UnityObjectToViewPos( v.pos ).z * _ProjectionParams.w)

float4 Billboard(float4 vertex, float offset)
{
	
    // our objects are billboarded in script (since the collider needs to be oriented correctly), so we can just rely on the fact it's aiming the right way
    float3 worldPos = mul(unity_ObjectToWorld, float4(vertex.x, vertex.y, -offset, 1)).xyz;
    float4 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1));
    float4 pos = mul(UNITY_MATRIX_P, viewPos);
                     
                // calculate distance to vertical billboard plane seen at this vertex's screen position
    float3 planeNormal = normalize(float3(UNITY_MATRIX_V._m20, 0.0, UNITY_MATRIX_V._m22));
    float3 planePoint = unity_ObjectToWorld._m03_m13_m23;
    float3 rayStart = _WorldSpaceCameraPos.xyz;
    float3 rayDir = -normalize(mul(UNITY_MATRIX_I_V, float4(viewPos.xyz, 1.0)).xyz - rayStart); // convert view to world, minus camera pos
    float dist = rayPlaneIntersection(rayDir, rayStart, planeNormal, planePoint);
 
                // calculate the clip space z for vertical plane
    float4 planeOutPos = mul(UNITY_MATRIX_VP, float4(rayStart + rayDir * dist, 1.0));
    float newPosZ = planeOutPos.z / planeOutPos.w * pos.w;
                //newPosZ += _Offset; //hack?
 
                // use the closest clip space z
#if defined(UNITY_REVERSED_Z)
                    pos.z = max(pos.z, newPosZ);
#else
    pos.z = min(pos.z, newPosZ);
#endif
    return pos;
}

fixed4 Billboard2(float4 pos, float offset)
{
	//float depth = pos.z;

	float3 worldPos = mul(unity_ObjectToWorld, float4(pos.x, pos.y, 0, 1)).xyz;
	float3 originPos = mul(unity_ObjectToWorld, float4(pos.x, 0, 0, 1)).xyz; //world position of origin
	float3 upPos = originPos + float3(0, 1, 0); //up from origin

	half outDist = abs(pos.y); //distance from origin should always be equal to y

	half angleA = Angle(originPos, upPos, worldPos); //angle between vertex position, origin, and up
	half angleB = Angle(worldPos, _WorldSpaceCameraPos.xyz, originPos); //angle between vertex position, camera, and origin

	half camDist = distance(_WorldSpaceCameraPos.xyz, worldPos.xyz);

	if (pos.y > 0)
	{
		angleA = 90 - (angleA - 90);
		angleB = 90 - (angleB - 90);
	}

	half angleC = 180 - angleA - angleB; //the third angle

	half fixDist = 0;
	if (pos.y > 0)
		fixDist = (outDist / sin(radians(angleC))) * sin(radians(angleA)); //supposedly basic trigonometry

	//determine move as a % of the distance from the point to the camera
	half decRate = (fixDist * 0.7 - offset/2) / camDist; //where does the 4 come from? Who knows!
	
	float4 view = mul(UNITY_MATRIX_VP, float4(worldPos, 1));

	#if UNITY_UV_STARTS_AT_TOP
		// Windows - DirectX
		view.z -= abs(UNITY_NEAR_CLIP_VALUE - view.z) * decRate;
	#else
		// WebGL - OpenGL
		view.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate;
	#endif

	return view;

	//return mul(UNITY_MATRIX_P, view);
	//return mul(UNITY_MATRIX_VP, float4(worldPos + forward * sin(_Time), 1));
}
