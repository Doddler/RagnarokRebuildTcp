using RebuildSharedData.Data;

namespace RoRebuildServer.Simulation.Util
{
    public static class MathHelper
    {
        public static float Lerp(float firstFloat, float secondFloat, float by)
        {
            return firstFloat * (1 - by) + secondFloat * by;
        }
        
    }
}
