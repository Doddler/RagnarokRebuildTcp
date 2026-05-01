using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Utility;
using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Database.Requests
{
    public class StorageLoadRequest : IDbRequest
    {
        public bool IsComplete;
        public int AccountId;
        public CharacterBag? StorageBag;
        public int StorageId;

        public StorageLoadRequest(int accountId)
        {
            AccountId = accountId;
            IsComplete = false;
            StorageBag = null;
        }

        public async Task ExecuteAsync(RoContext dbContext)
        {
            var storage = await dbContext.StorageInventory.Where(u => u.AccountId == AccountId).AsNoTracking().FirstOrDefaultAsync();

            if (storage != null)
            {
                StorageBag = PlayerDataDbHelper.DecompressPlayerStorage(storage.StorageData, storage.UncompressedSize);
                StorageId = storage.Id;
            }
            else
                StorageId = -1;

            IsComplete = true;
        }
    }
}