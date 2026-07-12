using UnityEngine;

public static class RoRenderUtil
{
    private static int charactersLayer = -1;

    public static bool CameraRendersCharacters(Camera cam)
    {
        if (charactersLayer < 0)
            charactersLayer = LayerMask.NameToLayer("Characters");
        return charactersLayer < 0 || (cam.cullingMask & (1 << charactersLayer)) != 0;
    }
}
