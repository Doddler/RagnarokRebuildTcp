///////////////////////////////////////////////////////////////////////////////
// ABOUT:        A unity Shader .cginc to draw numbers in the fragment shader
// AUTHOR:       Freya Holmér
// LICENSE:      Use for whatever, commercial or otherwise!
//               Don't hold me liable for issues though
//               But pls credit me if it works super well <3
// LIMITATIONS:  There's some precision loss beyond 3 decimal places
// CONTRIBUTORS: yes please! if you know a more precise way to get
//               decimal digits then pls lemme know!
//               GetDecimalSymbolAt() could use some more love/precision

// These are the main drawing functions:
// - returns white text on black background (though trailing zeroes are gray)
// - billboarded to always face the camera
// - you can get pxCoord from the frag shader "SV_POSITION" input
//
// Edit: Obtained from https://gist.github.com/FreyaHolmer/71717be9f3030c1b0990d3ed1ae833e3
//
float DrawNumberAtPxPos(float2 pxCoord, float2 pxPos, float number, float fontScale = 2, int decimalCount = 3);
float DrawNumberAtLocalPos(float2 pxCoord, float3 localPos, float number, float scale = 2, int decimalCount = 3);
float DrawNumberAtWorldPos(float2 pxCoord, float3 worldPos, float number, float scale = 2, int decimalCount = 3);

// digit rendering
static uint dBits[5] = {
    3959160828,
    2828738996,
    2881485308,
    2853333412,
    3958634981
};
static uint po10[] = {1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000, 10000000000};

float DrawDigit(int2 px, const int digit)
{
    if (px.x < 0 || px.x > 2 || px.y < 0 || px.y > 4)
        return 0; // pixel out of bounds
    const int xId = (digit == -1) ? 18 : 31 - (3 * digit + px.x);
    return (dBits[4 - px.y] & 1 << xId) != 0;
}

// indexed like: XXX.0123
void GetDecimalSymbolAt(const float v, const int i, const int decimalCount, out int symbol, out float opacity)
{
    // hide if outside the decimal range
    if (i > min(decimalCount - 1, 6))
    {
        symbol = 0;
        opacity = 0;
        return;
    }
    // get the i:th decimal
    const float scale = po10[i + 1];
    const float scaledF = abs(v) * scale;
    symbol = (int)(scaledF) % 10;
    // fade trailing zeroes
    opacity = (frac(scaledF / 10) != 0) ? 1 : 0.5;
}

// indexed like: 210.XXX
void GetIntSymbolAt(const float v, int i, out int symbol, out float opacity)
{
    // don't render more than 9 digits
    if (i <= 9)
    {
        const int scale = po10[i];
        const float vAbs = abs(v);
        // digits
        if (vAbs >= scale)
        {
            const int it = floor(vAbs);
            const int rem = it / scale;
            symbol = rem % 10;
            opacity = 1;
            return;
        }
        // minus symbol
        if ((v < 0) & (vAbs * 10 >= scale))
        {
            symbol = -1;
            opacity = 1;
            return;
        }
    }
    // leading zeroes
    symbol = 0;
    opacity = 0;
}

// Get the digit at the given index of a floating point number
// with -45.78, then with a given dIndex:
// [-3] = - (digit -1)
// [-2] = 4
// [-1] = 5
// [ 0] = . (digit 10)
// [ 1] = 7
// [ 2] = 8
void GetSymbolAtPositionInFloat(float number, int dIndex, int decimalCount, out int symbol, out float opacity)
{
    opacity = 1;
    if (dIndex == 0)
        symbol = 10; // period
    else if (dIndex > 0)
        GetDecimalSymbolAt(number, dIndex - 1, decimalCount, symbol, opacity);
    else
        GetIntSymbolAt(number, -dIndex - 1, symbol, opacity);
}

// Given a pixel coordinate pxCoord, draws a number at pxPos
float DrawNumberAtPxPos(float2 pxCoord, float2 pxPos, float number, float fontScale, int decimalCount)
{
    int2 p = (int2)(floor((pxCoord - pxPos) / fontScale));
    // p.y += 0; // 0 = bottom aligned, 2 = vert. center aligned, 5 = top aligned
    // p.x += 0; // 0 = integers are directly to the left, decimal separator and decimals, to the right
    if (p.y < 0 || p.y > 4)
        return 0; // out of bounds vertically
    // shift placement to make it tighter around the decimal separator
    float shift = 0;
    if (p.x > 1) // decimal digits
        p.x += 1;
    else if (p.x < 0) // integer digits
    {
        p.x += -3;
        shift = -2;
    }
    const int SEP = 4; // separation between characters
    const int dIndex = floor(p.x / SEP); // the digit index to read
    float opacity;
    int digit;
    GetSymbolAtPositionInFloat(number, dIndex, decimalCount, /*out*/ digit, /*out*/ opacity);
    const float2 pos = float2(dIndex * SEP + shift, 0);
    return opacity * DrawDigit(p - pos, digit);
}

// btw this might not work on all platforms, it might be Y-flipped or whatever!
float2 ClipToPixel(float4 clip)
{
    float2 ndc = float2(clip.x, -clip.y) / clip.w;
    ndc = (ndc + 1) / 2;
    return ndc * _ScreenParams.xy;
}

float2 LocalToPixel(float3 locPos) { return ClipToPixel(UnityObjectToClipPos(float4(locPos, 1))); }
float2 WorldToPixel(float3 wPos) { return ClipToPixel(UnityWorldToClipPos(float4(wPos, 1))); }

float DrawNumberAtLocalPos(float2 pxCoord, float3 localPos, float number, float scale, int decimalCount)
{
    const float2 pxPos = LocalToPixel(localPos);
    return DrawNumberAtPxPos(pxCoord, pxPos, number, scale, decimalCount);
}

float DrawNumberAtWorldPos(float2 pxCoord, float3 worldPos, float number, float scale, int decimalCount)
{
    const float2 pxPos = WorldToPixel(worldPos);
    return DrawNumberAtPxPos(pxCoord, pxPos, number, scale, decimalCount);
}
