
half Angle(float3 center, float3 pos1, float3 pos2)
{
	float3 dir1 = normalize(pos1 - center);
	float3 dir2 = normalize(pos2 - center);
	return degrees(acos(dot(dir1, dir2)));
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

	view.z -= abs(UNITY_NEAR_CLIP_VALUE - view.z) * decRate;

	return view;

	//return mul(UNITY_MATRIX_P, view);
	//return mul(UNITY_MATRIX_VP, float4(worldPos + forward * sin(_Time), 1));
}
