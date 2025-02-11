using Newtonsoft.Json;

namespace GameServer.Shared
{
    public static class JsonConfig
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
        };

        public static string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj, Settings);

        public static T? Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);

        public static object? Deserialize(string json) => JsonConvert.DeserializeObject(json, Settings);
    }
}
