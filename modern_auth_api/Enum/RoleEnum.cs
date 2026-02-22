using System.Text.Json.Serialization;

namespace modern_auth_api.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoleEnum
    {
        Unknown = 0,    //預設值
        user
    }
}
