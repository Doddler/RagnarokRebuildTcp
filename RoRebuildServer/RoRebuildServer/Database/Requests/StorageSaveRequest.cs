using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Utility;
using RoRebuildServer.EntityComponents;
using System.Diagnostics;

namespace RoRebuildServer.Database.Requests
{
    public class StorageSaveRequest : IDbRequest
    {
        private readonly int Id;
        private readonly byte[] storageData;
        private readonly int storageSize;
        private readonly int existingId;
        public StorageSaveRequest(Player p)
        {
            //Debug.Assert(p.StorageInventory != null && p.StorageId >= 0);

            Id = p.Connection.AccountId;

            storageData = PlayerDataDbHelper.CompressAndStorePlayerStorage(p, out storageSize);
            existingId = p.StorageId;
        }

        public async Task ExecuteAsync(RoContext dbContext)
        {
            var existing = await dbContext.StorageInventory.Where(s => s.AccountId == Id).Select(s => s.Id).FirstOrDefaultAsync();

            if (existing > 0)
            {
                await dbContext.StorageInventory.Where(u => u.Id == existing)
                    .ExecuteUpdateAsync(s => 
                        s.SetProperty(p => p.StorageData, storageData)
                         .SetProperty(p => p.UncompressedSize, storageSize)
                );
            }
            else
            {
                var storage = new StorageInventory()
                {
                    AccountId = Id,
                    StorageData = storageData,
                    UncompressedSize = storageSize
                };
                dbContext.StorageInventory.Add(storage);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
