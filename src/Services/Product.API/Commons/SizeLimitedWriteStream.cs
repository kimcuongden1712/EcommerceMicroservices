namespace Product.API.Commons
{
    internal sealed class SizeLimitedWriteStream (Stream inner, long maximumLength) : Stream
    {
        private long _bytesWritten;
        public long BytesWritten => _bytesWritten;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => _bytesWritten;
        public override long Position
        {
            get => _bytesWritten; set => throw new NotSupportedException();
        }
        public override void Flush () => inner.Flush();
        public override Task FlushAsync (CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read (byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength (long value) => throw new NotSupportedException();

        public override void Write (byte[] buffer, int offset, int count)
        {
            EnsureLimit(count);
            inner.Write(buffer, offset, count);
            _bytesWritten += count;
        }

        public override void Write (ReadOnlySpan<byte> buffer)
        {
            EnsureLimit(buffer.Length);
            inner.Write(buffer);
            _bytesWritten += buffer.Length;
        }

        public override async ValueTask WriteAsync (ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureLimit(buffer.Length);
            await inner.WriteAsync(buffer, cancellationToken);
            _bytesWritten += buffer.Length;
        }

        public override async Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureLimit(count);
            await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
            _bytesWritten += count;
        }

        protected override void Dispose (bool disposing) => base.Dispose(disposing);

        private void EnsureLimit (int count)
        {
            if (count < 0 || _bytesWritten + count > maximumLength)
                throw new SizeLimitExceededException();
        }
    }

    internal sealed class SizeLimitExceededException : IOException
    {
    }
}
