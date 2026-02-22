using modern_auth_api.Interface;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace modern_auth_api.Service
{
    public class Argon2HashService : IHashService
    {
        /// <summary>
        /// 產生雜湊密碼 (Argon2)
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        /// 
        public string PasswordHashSalt(string password)
        {
            var salt = CreateSalt();
            var passwordHash = Hash(password, salt);

            // 回傳必須包含salt
            // 格式範例： "Base64(Salt).Base64(Hash)"
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(passwordHash)}";
        }

        // 雜湊 (Argon2)
        private byte[] Hash(string password, byte[] salt)
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)) {
                DegreeOfParallelism = 8,
                MemorySize = 46 * 1024,
                Iterations = 3,
                Salt = salt
            };

            return argon2.GetBytes(32);
        }

        /// <summary>
        /// 驗證密碼與雜湊密碼是否相符
        /// </summary>
        /// <param name="password"></param>
        /// <param name="passwordHash"></param>
        /// <returns>true/false</returns>
        public bool Verify(string password, string passwordHash)
        {
            // 將salt與hash分隔
            var hashParts = passwordHash.Split(".");

            // 驗證分隔長度
            if (hashParts.Length != 2) return false;

            var salt = Convert.FromBase64String(hashParts[0]);
            var orgPasswordHashStr = hashParts[1];

            var newPasswordHash = Hash(password, salt);
            var newPasswordHashStr = Convert.ToBase64String(newPasswordHash);

            return newPasswordHashStr == orgPasswordHashStr;
        }

        /// <summary>
        /// 產生16bytes的隨機salt
        /// </summary>
        /// <returns>byte[]</returns>
        private byte[] CreateSalt()
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }
    }
}
