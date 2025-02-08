using Microsoft.Extensions.ObjectPool;
using RebuildSharedData.Util;
using RebuildZoneServer.Networking;

namespace RoRebuildServer.Simulation.Util;

public static class ByteArrayPools
{
    public static ObjectPool<byte[]> ArrayPoolPlayerSummary;

    private class ByteArrayPlayerSummaryPolicy : IPooledObjectPolicy<byte[]>
    {
        public byte[] Create()
        {
            return new byte[(int)PlayerSummaryData.SummaryDataMax * 4];
        }

        public bool Return(byte[] obj)
        {
            //Array.Clear(obj);
            return true;
        }
    }

    static ByteArrayPools()
    {
        ArrayPoolPlayerSummary = new DefaultObjectPool<byte[]>(new ByteArrayPlayerSummaryPolicy(), 4);
    }
}