namespace RoRebuildServer.Data.CsvDataTypes
{
    public class CsvItem
    {
        public required int Id { get; set; }
        public required string Code { get; set; }
        public required int Weight { get; set; }
        public required int Price { get; set; }
        public required bool IsUseable { get; set; }
        public required string Effect { get; set; }
    }
}
