using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Utility;
using RoRebuildServer.EntityComponents;
using System.Diagnostics;

namespace RoRebuildServer.Database.Requests
{
    public class StorageSaveRequest : IDbRequest
    {
        private Guid Id;
        private readonly byte[] storageData;
        private readonly int storageSize;
        private readonly int existingId;
        public StorageSaveRequest(Player p)
        {
            Debug.Assert(p.StorageInventory != null && p.StorageId >= 0);

            Id = p.Id;

            storageData = PlayerDataDbHelper.CompressAndStorePlayerStorage(p, out storageSize);
            existingId = p.StorageId;
        }

        public async Task ExecuteAsync(RoContext dbContext)
        {
            if (existingId >= 0)
            {
                await dbContext.StorageInventory.Where(u => u.CharacterId == Id)
                    .ExecuteUpdateAsync(s => 
                        s.SetProperty(p => p.StorageData, storageData)
                         .SetProperty(p => p.UncompressedSize, storageSize)
                );
            }
            else
            {
                var storage = new StorageInventory()
                {
                    CharacterId = Id,
                    StorageData = storageData,
                    UncompressedSize = storageSize
                };
                dbContext.StorageInventory.Add(storage);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
