using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace GameServer.Shared
{
    public static class JsonConfig
    {
        private static Dictionary<string, Type> MessageTypes;
        public static DefaultContractResolver ContractResolver = new DefaultContractResolver
        {
            //NamingStrategy = new CamelCaseNamingStrategy()
        };

        static JsonConfig()
        {
            MessageTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && t.Namespace != null && t.Namespace.Contains("GameServer.Shared.Messages"))
                .ToDictionary(t => t.Name, t => t);

        }
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = ContractResolver
        };

        public static string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj, Settings);

        public static T? Deserialize<T>(string json) => DeserializeInt<T>(json);

        public static object? Deserialize(string json) => DeserializeInt(json);

        public static T? DeserializeInt<T>(string json)
        {
            var result = DeserializeInt(json);
            if (result != null)
            {
                return (T)result;
            }

            throw new Exception("Unknown message type");
        }

        public static object? DeserializeInt(string json)
        {
            var jsonObject = JObject.Parse(json);
            var typeName = jsonObject.Value<string>("MessageType");
            if (MessageTypes.TryGetValue(typeName, out Type type))
            {
                return JsonConvert.DeserializeObject(json, type);
            }

            return null;
        }
    }
}
