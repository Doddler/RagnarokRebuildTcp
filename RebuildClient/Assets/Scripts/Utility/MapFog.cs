using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using UnityEngine;

namespace Assets.Scripts.Utility
{
    public static class MapFog
    {
        private static readonly int MinimapFogColor = Shader.PropertyToID("_MinimapFogColor");
        private static readonly int MinimapFogAmount = Shader.PropertyToID("_MinimapFogAmount");

        //the fog data gives near and far as fractions of a 1500 unit view range; over the 400 unit camera distance they become multiples of it
        private const float FogDataToCameraDistance = 1500f / 400f;

        private static bool hasMap;
        private static bool mapHasFog;
        private static float amountAtView;

        public static float NearRatio { get; private set; } = 0.3f;
        public static float FarRatio { get; private set; } = 4f;

        //once the map's scene is active, so the render settings hold its fog
        public static void SetMap(FogInfo info)
        {
            var ratios = RatiosFor(info);
            NearRatio = ratios.x;
            FarRatio = ratios.y;

            hasMap = true;
            mapHasFog = RenderSettings.fog;
            amountAtView = GetAmountAtView(ratios);
            Apply();
        }

        public static Vector2 RatiosFor(FogInfo fog) =>
            (fog != null ? new Vector2(fog.NearPlane, fog.FarPlane) : new Vector2(0.1f, 0.9f)) * FogDataToCameraDistance;

        //the fog's cover at the view's centre, the same at any camera distance
        private static float GetAmountAtView(Vector2 ratios) => Mathf.Clamp01((1f - ratios.x) / (ratios.y - ratios.x));

        public static void Apply()
        {
            if (!hasMap)
                return;

            RenderSettings.fog = mapHasFog && GameConfig.Data.EnableFog;
            Shader.SetGlobalColor(MinimapFogColor, RenderSettings.fogColor);
            Shader.SetGlobalFloat(MinimapFogAmount, RenderSettings.fog ? amountAtView : 0f);
        }
    }
}
