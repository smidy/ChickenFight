namespace GameServer.Shared.Models
{
    public class TilemapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] TileData { get; set; } = null!;
    }
}
