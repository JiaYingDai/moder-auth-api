using System.Text.Json.Serialization;

namespace modern_auth_api.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ErrorCodeEnum
    {
        // --- 0: 通用成功狀態 ---
        Success = 0,

        // --- 1000-1999: 系統與通用錯誤 (System / General) ---
        UnknownError = 1000,
        InvalidParameter = 1001,  // 參數錯誤
        DatabaseError = 1002,     // 資料庫錯誤
        SystemError = 1003,         // 系統錯誤
        ResourceNotFound = 1004,    // 找不到 ID
        System_EmailSendFailed = 1005,  // 寄送註冊驗證信失敗，請稍後再試或聯繫客服

        // --- 2000-2999: 認證與Token相關 (Auth & Token) ---
        // 這裡放你原本想要歸類的東西
        Auth_InvalidToken = 2001,    // 找不到Token或格式錯誤
        Auth_TokenExpired = 2002,    // Token過期
        Auth_AlreadyVerified = 2003, // 已經驗證過了 (不需要再驗證)
        Auth_LoginFailed = 2004,     // 帳號密碼錯誤
        Auth_TokenAlreadyUsed = 2005,     // 此連結已失效或已被使用，請重新申請
        Auth_PasswordReuse = 2006,     // 新密碼不能與舊密碼相同
        Auth_TokenRefreshFailed = 2007, // Token 刷新失敗

        // --- 3000-3999: 使用者帳號狀態 (User Account) ---
        User_NotFound = 3001,        // 找不到使用者
        User_EmailNotVerified = 3002,// 帳號存在但未驗證Email
        User_AccountDisabled = 3003, // 帳號被停權 (Active=false)
        User_EmailAlreadyExists = 3004 // 註冊時Email 重複
    }
}
