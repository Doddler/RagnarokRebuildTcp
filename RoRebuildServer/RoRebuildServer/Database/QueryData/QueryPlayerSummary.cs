namespace RoRebuildServer.Database.QueryData
{
    public class QueryPlayerSummary
    {
        public required string Name { get; set; }
        public string? Map { get; set; }
        public int CharacterSlot { get; set; }
        public byte[]? SummaryData { get; set; }
    }
}
