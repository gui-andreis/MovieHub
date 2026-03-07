namespace MovieHub.Services.Implementations;

using MovieHub.Services.Interfaces;

public class ImageService : IImageService // revisar 
{
    private readonly string _imagePath;

    public ImageService(IWebHostEnvironment env)
    {
        // Salva em wwwroot/images dentro do container
        _imagePath = Path.Combine(env.WebRootPath ?? "wwwroot", "images");
        Directory.CreateDirectory(_imagePath);
    }

    public async Task<string?> SaveImageAsync(IFormFile? image)
    {
        if (image == null) return null;

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
        var fullPath = Path.Combine(_imagePath, fileName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        await image.CopyToAsync(stream);

        return $"/images/{fileName}";
    }

    public void DeleteImage(string? imagePath)
    {
        if (imagePath == null) return;

        var fullPath = Path.Combine(
            _imagePath,
            Path.GetFileName(imagePath)
        );

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}