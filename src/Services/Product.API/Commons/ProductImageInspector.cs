using SkiaSharp;

namespace Product.API.Commons;

public static class ProductImageInspector
{
    private const long MaximumDecodedPixels = 20_000_000;
    private const int MaximumDimension = 10_000;

    public static string? TryDecode (Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!stream.CanRead || !stream.CanSeek)
            return null;

        stream.Position = 0;
        using var codec = SKCodec.Create(stream);
        if (codec is null)
            return null;

        width = codec.Info.Width;
        height = codec.Info.Height;
        if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension ||
            (long)width * height > MaximumDecodedPixels)
            return null;

        var contentType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => null
        };
        if (contentType is null)
            return null;

        using var bitmap = new SKBitmap(codec.Info);
        if (bitmap.GetPixels() == IntPtr.Zero)
            return null;

        try {
            return codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success
                ? contentType
                : null;
        }
        catch (ArgumentException) {
            return null;
        }
    }
}
