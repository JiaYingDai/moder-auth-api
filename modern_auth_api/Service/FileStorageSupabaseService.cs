using modern_auth_api.Interface;
using Org.BouncyCastle.Asn1.Ocsp;
using ISupabaseClient = Supabase.Interfaces.ISupabaseClient<
    Supabase.Gotrue.User,
    Supabase.Gotrue.Session,
    Supabase.Realtime.RealtimeSocket,
    Supabase.Realtime.RealtimeChannel,
    Supabase.Storage.Bucket,
    Supabase.Storage.FileObject
>;

namespace modern_auth_api.Service
{
    public class FileStorageSupabaseService : IFileStorageService
    {
        private IConfiguration _configuration;
        private ISupabaseClient _client;


        public FileStorageSupabaseService(IConfiguration configuration, ISupabaseClient client)
        {
            _configuration = configuration;
            _client = client;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folderName, string authId)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            var bucket = _client.Storage.From(folderName);
            var fileName = $"{authId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            await bucket.Upload(imageBytes, fileName);

            // 取得公開網址 (存入DB)
            var publicUrl = bucket.GetPublicUrl(fileName);

            // 4. 回傳檔案路徑 (存DB)
            return publicUrl; // Supabase路徑
        }
    }
}
