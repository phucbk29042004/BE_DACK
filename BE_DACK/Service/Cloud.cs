using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace WebAppDoCongNghe.Service
{
    public interface ICloudinaryService
    {
        Task<string?> UploadImageAsync(IFormFile file, string folder);
        Task<bool> DeleteImageAsync(string publicId);
    }

    public class Cloud : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        public Cloud(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        public async Task<bool> DeleteImageAsync(string publicId)
        {
            var deletionParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deletionParams);
            return result.Result == "ok";
        }

        public async Task<string?> UploadImageAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                return null;

            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "SanPham" // ví dụ: "SanPham"
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                    return result.SecureUrl.ToString();

                return null;
            }
        }
    }
}