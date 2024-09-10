using RebuildSharedData.Data;

namespace RoRebuildServer.Simulation.Util
{
    public static class MathHelper
    {
        private static readonly float[] ResistTable;
        private static readonly float[] BoostTable;

        static MathHelper()
        {
            ResistTable = new float[1000];
            BoostTable = new float[1000];

            for (var i = 0; i < 1000; i++)
            {
                ResistTable[i] = MathF.Pow(0.99f, i);
                BoostTable[i] = MathF.Pow(0.01f, i);
            }
        }

        public static float ResistCalc(int value)
        {
            if(value < ResistTable.Length)
                return ResistTable[value];
            return MathF.Pow(0.99f, value);
        }

        public static float BoostCalc(int value)
        {
            if (value < BoostTable.Length)
                return BoostTable[value];
            return MathF.Pow(0.01f, value);
        }
        
        public static float Lerp(float firstFloat, float secondFloat, float by)
        {
            return firstFloat * (1 - by) + secondFloat * by;
        }
        
    }
}
