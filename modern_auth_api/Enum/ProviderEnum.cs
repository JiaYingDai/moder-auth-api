using System.Text.Json.Serialization;

namespace modern_auth_api.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProviderEnum
    {
        Unknown = 0,    //預設值
        Google,
        Local
    }
}
