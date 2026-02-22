namespace modern_auth_api.Interface
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string folderName, string authId);
    }
}
