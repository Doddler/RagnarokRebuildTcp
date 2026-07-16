#ifndef BILLBOARD_INCLUDED
#define BILLBOARD_INCLUDED

struct Billboard
{
    float4 positionCS;
    float3 positionWS;
    float3 positionVS;
};

half Angle(float3 center, float3 pos1, float3 pos2)
{
    float3 dir1 = normalize(pos1 - center);
    float3 dir2 = normalize(pos2 - center);
    return degrees(acos(dot(dir1, dir2)));
}

float4 Rotate(float4 vert, float3 rotation)
{
    float4 vOut = vert;
    vOut.x = vert.x * cos(radians(rotation)) - vert.y * sin(radians(rotation));
    vOut.y = vert.x * sin(radians(rotation)) + vert.y * cos(radians(rotation));
    return vOut;
}

float rayPlaneIntersection(float3 rayDir, float3 rayPos, float3 planeNormal, float3 planePos)
{
    float denom = dot(planeNormal, rayDir);
    denom = max(denom, 0.000001); // avoid divide by zero
    float3 diff = planePos - rayPos;
    return dot(diff, planeNormal) / denom;
}

Billboard GetBillboard(float4 positionOS, float offset)
{
    float2 pos = positionOS.xy;
    
    float4x4 objectToWorld = UNITY_MATRIX_M;
    float3 worldPos = mul(objectToWorld, float4(pos.x, pos.y, 0, 1)).xyz;
    float3 originPos = mul(objectToWorld, float4(pos.x, 0, 0, 1)).xyz;
    float3 upPos = originPos + float3(0, 1, 0); //up from origin

    float outDist = abs(pos.y); //distance from origin should always be equal to y

    float angleA = Angle(originPos, upPos, worldPos); //angle between vertex position, origin, and up
    float angleB = Angle(worldPos, _WorldSpaceCameraPos.xyz, originPos); //angle between vertex position, camera, and origin

    float camDist = distance(_WorldSpaceCameraPos.xyz, worldPos.xyz);

    if (pos.y > 0)
    {
        angleA = 90 - (angleA - 90);
        angleB = 90 - (angleB - 90);
    }

    float angleC = 180 - angleA - angleB; //the third angle

    float fixDist = 0;
    if (pos.y > 0)
        fixDist = (outDist / sin(radians(angleC))) * sin(radians(angleA)); //supposedly basic trigonometry

    //determine move as a % of the distance from the point to the camera
    float decRate = (fixDist * 0.7 - offset/4) / camDist; //where does the value come from? Who knows!
    float decRateNoOffset = (fixDist * 0.7) / camDist; //where does the value come from? Who knows!
    float decRate2 = (fixDist) / camDist; //where does the value come from? Who knows!

    float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));

    float4 pro = mul(UNITY_MATRIX_P, view);

    #if UNITY_UV_STARTS_AT_TOP
    // Windows - DirectX
    view.z -= abs(UNITY_NEAR_CLIP_VALUE - view.z) * decRate2;
    pro.z -= abs(UNITY_NEAR_CLIP_VALUE - pro.z) * decRate;
    #else
    // WebGL - OpenGL
    view.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate2;
    pro.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate;
    #endif

    Billboard b;
    b.positionCS = pro;
    b.positionWS = worldPos;
    b.positionVS = view;
    return b;
}

// Dynamic Batching
Billboard GetBillboardDB(float3 anchor, float3 cornerOffset, float3 originOffset, float posY, float offset)
{
    float3 worldPos = anchor + cornerOffset;
    float3 originPos = anchor + originOffset;
    float3 upPos = originPos + float3(0, 1, 0);

    float outDist = abs(posY);

    float angleA = Angle(originPos, upPos, worldPos);
    float angleB = Angle(worldPos, _WorldSpaceCameraPos.xyz, originPos);
    float camDist = distance(_WorldSpaceCameraPos.xyz, worldPos.xyz);

    if (posY > 0)
    {
        angleA = 90 - (angleA - 90);
        angleB = 90 - (angleB - 90);
    }

    float angleC = 180 - angleA - angleB;

    float fixDist = 0;
    if (posY > 0)
        fixDist = (outDist / sin(radians(angleC))) * sin(radians(angleA));

    float decRate = (fixDist * 0.7 - offset / 4) / camDist;
    float decRate2 = (fixDist) / camDist;

    float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));
    float4 pro = mul(UNITY_MATRIX_P, view);

    #if UNITY_UV_STARTS_AT_TOP
    view.z -= abs(UNITY_NEAR_CLIP_VALUE - view.z) * decRate2;
    pro.z -= abs(UNITY_NEAR_CLIP_VALUE - pro.z) * decRate;
    #else
    view.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate2;
    pro.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate;
    #endif

    Billboard b;
    b.positionCS = pro;
    b.positionWS = worldPos;
    b.positionVS = view;
    return b;
}

#endif