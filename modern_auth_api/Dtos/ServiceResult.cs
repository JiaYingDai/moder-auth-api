using modern_auth_api.Enum;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace modern_auth_api.Dtos
{
    public class ServiceResult
    {
        public bool IsSuccess { get; set; }
        public ErrorCodeEnum ErrorCode { get; set; }
        public string Message { get; set; } = string.Empty;

        // 成功時的靜態方法
        public static ServiceResult Success(string msg = "成功")
        {
            return new ServiceResult
            {
                IsSuccess = true,
                ErrorCode = ErrorCodeEnum.Success,
                Message = msg
            };
        }

        // 失敗時的靜態方法
        public static ServiceResult Fail(ErrorCodeEnum errorCode, string msg)
        {
            return new ServiceResult
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                Message = msg
            };
        }
    }

    public class ServiceResult<T> : ServiceResult
    {

        // 用泛型T，讓呼叫者清楚知道回傳的資料型態
        public T? Data { get; set; }

        // 成功時的靜態方法
        public static ServiceResult<T> Success(T data, string msg = "成功")
        {
            return new ServiceResult<T>
            {
                IsSuccess = true,
                ErrorCode = ErrorCodeEnum.Success,
                Message = msg,
                Data = data
            };
        }

        // 失敗時的靜態方法 (Data會是null)
        public static new ServiceResult<T> Fail(ErrorCodeEnum errorCode, string msg)
        {
            return new ServiceResult<T>
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                Message = msg,
                Data = default
            };
        }
    }
}
