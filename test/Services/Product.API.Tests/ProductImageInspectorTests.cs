using Product.API.Commons;
using SkiaSharp;

namespace Product.API.Tests;

public sealed class ProductImageInspectorTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Png,"image/png")]
    [InlineData(SKEncodedImageFormat.Jpeg,"image/jpeg")]
    [InlineData(SKEncodedImageFormat.Webp,"image/webp")]
    public void Decoding_accepts_supported_formats_and_reports_dimensions(SKEncodedImageFormat format,string mime)
    {
        using var bitmap=new SKBitmap(2,3);using var data=bitmap.Encode(format,100);using var stream=new MemoryStream(data.ToArray());
        stream.Position=stream.Length;Assert.Equal(mime,ProductImageInspector.TryDecode(stream,out var width,out var height));Assert.Equal(2,width);Assert.Equal(3,height);
    }

    [Theory]
    [InlineData(10001,1)] [InlineData(1,10001)] [InlineData(5000,5000)]
    public void Decoding_rejects_excessive_dimensions_before_allocating_decoded_bitmap(int width,int height)
    {
        using var bitmap=new SKBitmap(width,height);using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);using var stream=new MemoryStream(data.ToArray());
        Assert.Null(ProductImageInspector.TryDecode(stream,out var actualWidth,out var actualHeight));Assert.Equal(width,actualWidth);Assert.Equal(height,actualHeight);
    }

    private sealed class NonSeekableStream:MemoryStream { public override bool CanSeek=>false; }
    [Fact]
    public void Unreadable_nonseekable_and_invalid_streams_are_rejected()
    {
        using var nonseekable=new NonSeekableStream();Assert.Null(ProductImageInspector.TryDecode(nonseekable,out _,out _));
        var disposed=new MemoryStream();disposed.Dispose();Assert.Null(ProductImageInspector.TryDecode(disposed,out _,out _));
        using var invalid=new MemoryStream([0,1,2]);Assert.Null(ProductImageInspector.TryDecode(invalid,out _,out _));
    }
}
