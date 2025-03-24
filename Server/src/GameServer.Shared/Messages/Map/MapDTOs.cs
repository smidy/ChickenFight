namespace GameServer.Shared.Messages.Map
{
    /// <summary>
    /// Information about a map
    /// </summary>
    public class MapInfo
    {
        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int PlayerCount { get; }

        public MapInfo(string id, string name, int width, int height, int playerCount)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            PlayerCount = playerCount;
        }
    }
}
