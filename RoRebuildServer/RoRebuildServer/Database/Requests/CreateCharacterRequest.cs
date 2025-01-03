using System.Buffers;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Util;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Utility;
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
        private byte[] summaryData;

        public CreateCharacterRequest(NetworkConnection connection, int accountId, int slot, string name, int[] data)
        {
            this.connection = connection;
            this.accountId = accountId;
            this.slot = slot;
            this.name = name;

            var dataSize = data.Length * sizeof(int);
            charData = ArrayPool<byte>.Shared.Rent(dataSize);
            Array.Clear(charData);
            Buffer.BlockCopy(data, 0, charData, 0, data.Length * sizeof(int));

            //build character summary for our new character
            var summaryBuffer = ArrayPool<int>.Shared.Rent((int)PlayerSummaryData.SummaryDataMax);
            Array.Clear(summaryBuffer);
            summaryData = new byte[(int)PlayerSummaryData.SummaryDataMax * 4];
            PlayerDataDbHelper.PackNewCharacterSummaryData(summaryBuffer, data[(int)PlayerStat.Gender], data[(int)PlayerStat.Head], data[(int)PlayerStat.HairId]);
            Buffer.BlockCopy(summaryBuffer, 0, summaryData, 0, summaryData.Length);
            ArrayPool<int>.Shared.Return(summaryBuffer);
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
                CharacterSummary = summaryData,
                SavePoint = new DbSavePoint()
                {
                    MapName = savePoint.MapName,
                    X = savePoint.Position.X,
                    Y = savePoint.Position.Y,
                    Area = savePoint.Area,
                },
                VersionFormat = 1
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
