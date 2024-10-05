using System.Buffers;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Database.Requests
{
    public class CreateCharacterRequest : IDbRequest
    {
        private NetworkConnection connection;
        private int accountId;
        private int slot;
        private string name;
        private byte[]? charData;

        public CreateCharacterRequest(NetworkConnection connection, int accountId, int slot, string name, int[] data)
        {
            this.connection = connection;
            this.accountId = accountId;
            this.slot = slot;
            this.name = name;

            charData = ArrayPool<byte>.Shared.Rent(data.Length * sizeof(int));

            Buffer.BlockCopy(data, 0, charData, 0, data.Length * sizeof(int));
        }

        public async Task ExecuteAsync(RoContext dbContext)
        {
            var isSlotOccupied = await dbContext.Character.AnyAsync(c => c.AccountId == accountId && c.CharacterSlot == slot);
            if (isSlotOccupied)
            {
                CommandBuilder.ErrorMessage(connection, "Failed to create character, character slot is already occupied.");
                CleanUp();
                return;
            }

            var doesNameExist = await dbContext.Character.AnyAsync(c => c.Name.ToUpper() == name.ToUpper());
            if (doesNameExist)
            {
                CommandBuilder.ErrorMessage(connection, "Error: A character with that name already exists.");
                CleanUp();
                return;
            }

            var savePoint = new SavePosition();

            var ch = new DbCharacter()
            {
                Id = Guid.Empty,
                Name = name,
                AccountId = accountId,
                CharacterSlot = slot,
                Data = charData,
                SavePoint = new DbSavePoint()
                {
                    MapName = savePoint.MapName,
                    X = savePoint.Position.X,
                    Y = savePoint.Position.Y,
                    Area = savePoint.Area,
                }
            };

            try
            {
                dbContext.Update(ch);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                ServerLogger.LogWarning($"Create character failed due to exception. Exception: " + e.Message);
                CommandBuilder.ErrorMessage(connection, "Error: A character with that name already exists.");
                CleanUp();
                return;
            }
            
            CleanUp();
            
            var req = new LoadCharacterRequest(accountId, ch.Name);
            RoDatabase.EnqueueDbRequest(req);
        }

        private void CleanUp()
        {
            if(charData != null) ArrayPool<byte>.Shared.Return(charData);
            charData = null;
        }
    }
}
