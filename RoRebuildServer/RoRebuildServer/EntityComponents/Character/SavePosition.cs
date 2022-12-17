using RebuildSharedData.Data;
using RoRebuildServer.Data;

namespace RoRebuildServer.EntityComponents.Character
{
    public class SavePosition
    {
        public string MapName { get; set; } = null!;
        public Position Position { get; set; }
        public int Area { get; set; }

        public SavePosition()
        {
            Reset();
        }

        public void Reset()
        {
            var config = ServerConfig.EntryConfig;

            MapName = config.Map;
            Position = config.Position;
            Area = config.Area;
        }
    }
}
