using Product.API.Commons;

namespace Product.API.Tests;

public sealed class SizeLimitedWriteStreamTests
{
    private static Stream Wrap(Stream inner,long limit) => (Stream)Activator.CreateInstance(
        typeof(ProductImageInspector).Assembly.GetType("Product.API.Commons.SizeLimitedWriteStream")!,inner,limit)!;

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task Every_write_overload_enforces_limit_before_modifying_destination(int overload)
    {
        using var inner=new MemoryStream();
        await using var stream=Wrap(inner,2);
        async Task Write(byte[] bytes) {
            switch(overload) {
                case 0: stream.Write(bytes,0,bytes.Length);break;
                case 1: stream.Write(bytes.AsSpan());break;
                case 2: await stream.WriteAsync(bytes.AsMemory());break;
                default: await stream.WriteAsync(bytes,0,bytes.Length,CancellationToken.None);break;
            }
        }
        await Write([1,2]);
        var error=await Assert.ThrowsAnyAsync<IOException>(()=>Write([3]));
        Assert.Equal("SizeLimitExceededException",error.GetType().Name);
        Assert.Equal(new byte[]{1,2},inner.ToArray());
        Assert.Equal(2,stream.Length);Assert.Equal(2,stream.Position);
        Assert.Equal(2L,stream.GetType().GetProperty("BytesWritten")!.GetValue(stream));
        Assert.False(stream.CanRead);Assert.False(stream.CanSeek);Assert.True(stream.CanWrite);
        stream.Flush();await stream.FlushAsync(CancellationToken.None);
        Assert.Throws<NotSupportedException>(()=>stream.Position=0);
        Assert.Throws<NotSupportedException>(()=>stream.SetLength(0));
        Assert.Throws<NotSupportedException>(()=>stream.Seek(0,SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(()=>stream.Read(new byte[1],0,1));
        await stream.DisposeAsync();
        Assert.True(inner.CanWrite);
    }

    [Fact]
    public void Negative_write_count_is_rejected_without_touching_destination()
    {
        using var inner=new MemoryStream();using var stream=Wrap(inner,10);
        Assert.ThrowsAny<IOException>(()=>stream.Write([],0,-1));Assert.Empty(inner.ToArray());
    }
}
