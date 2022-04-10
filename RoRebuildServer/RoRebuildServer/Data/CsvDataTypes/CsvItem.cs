namespace RoRebuildServer.Data.CsvDataTypes
{
    public class CsvItem
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int Weight { get; set; }
        public int Price { get; set; }
        public bool IsUseable { get; set; }
        public string Effect { get; set; }
    }
}
