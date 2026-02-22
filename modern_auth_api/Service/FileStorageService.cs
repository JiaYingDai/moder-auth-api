using modern_auth_api.Interface;

namespace modern_auth_api.Service
{
    public class FileStorageService : IFileStorageService
    {
        private IConfiguration _configuration;

        public FileStorageService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<string> SaveFileAsync(IFormFile file, string folderName, string authId)
        {
            // 0. fileroot路徑
            string fileRoot = _configuration["File:Root"] ?? Directory.GetCurrentDirectory();

            // 1. 產生存檔路徑
            var fileName = $"{authId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var folderPath = Path.Combine(fileRoot, folderName);
            var fullPath = Path.Combine(folderPath, fileName);

            // 2. 確保資料夾存在
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 3. 存檔案
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4. 回傳檔案路徑 (存DB)
            return $"/{folderName}/{fileName}"; // 相對路徑
        }
    }
}
