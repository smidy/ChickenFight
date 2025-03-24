namespace GameServer.Shared.Models
{
    public class TilemapData
    {
        public TilemapData(int width, int height, int[] tileData)
        {
            Width = width;
            Height = height;
            TileData = tileData;
        }

        public int Width { get; set; }
        public int Height { get; set; }
        public int[] TileData { get; set; } = null!;
    }
}
