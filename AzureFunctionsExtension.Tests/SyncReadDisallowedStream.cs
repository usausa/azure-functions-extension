namespace AzureFunctionsExtension.Tests;

internal sealed class SyncReadDisallowedStream : Stream
{
    private const string SyncReadMessage = "Synchronous operations are disallowed. Call ReadAsync or set AllowSynchronousIO to true instead.";

    private readonly MemoryStream inner;

    public SyncReadDisallowedStream(ReadOnlySpan<byte> data)
    {
        inner = new MemoryStream([.. data]);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException(SyncReadMessage);

    public override int Read(Span<byte> buffer) => throw new InvalidOperationException(SyncReadMessage);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
