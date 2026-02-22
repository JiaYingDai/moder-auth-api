using System.Text.Json.Serialization;

namespace modern_auth_api.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TokenTypeEnum
    {
        Unknown = 0,    //預設值
        register,
        forgotpwd,
        refresh
    }
}
