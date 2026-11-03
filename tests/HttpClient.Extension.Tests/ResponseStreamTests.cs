using System.Net;
using Xunit;

namespace HttpClient.Extension.Tests;

public class ResponseStreamTests
{
    [Fact]
    public async Task SingleByteOperationsUseUnderlyingOverrides()
    {
        using var source = new TrackedStream();
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new TrackedContent(source) });
        var response = await client.DownloadWithStreamAsync("/");
        using var stream = response.Result;

        Assert.Equal(42, stream.ReadByte());
        stream.WriteByte(43);

        Assert.Equal(1, source.ByteReads);
        Assert.Equal(1, source.ByteWrites);
    }

#if !HTTPCLIENT_EXTENSION_NETSTANDARD2_0
    [Fact]
    public async Task SpanAndMemoryOperationsUseUnderlyingOverridesAndTokens()
    {
        using var source = new TrackedStream();
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new TrackedContent(source) });
        var response = await client.DownloadWithStreamAsync("/");
        using var stream = response.Result;
        var buffer = new byte[1];
        using var cancellation = new CancellationTokenSource();

        Assert.Equal(1, stream.Read(buffer.AsSpan()));
        stream.Write(buffer.AsSpan());
        Assert.Equal(1, await stream.ReadAsync(buffer.AsMemory(), cancellation.Token));
        Assert.Equal(cancellation.Token, source.LastToken);
        await stream.WriteAsync(buffer.AsMemory(), cancellation.Token);

        Assert.Equal(1, source.SpanReads);
        Assert.Equal(1, source.SpanWrites);
        Assert.Equal(1, source.MemoryReads);
        Assert.Equal(1, source.MemoryWrites);
        Assert.Equal(cancellation.Token, source.LastToken);
    }
#endif

    private sealed class TrackedContent(Stream source) : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(source);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => source.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class TrackedStream : MemoryStream
    {
        public int ByteReads, ByteWrites, SpanReads, SpanWrites, MemoryReads, MemoryWrites;
        public CancellationToken LastToken;
        public override int ReadByte() { ByteReads++; return 42; }
        public override void WriteByte(byte value) => ByteWrites++;
        public override int Read(Span<byte> buffer) { SpanReads++; return 1; }
        public override void Write(ReadOnlySpan<byte> buffer) => SpanWrites++;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            MemoryReads++;
            LastToken = cancellationToken;
            return new ValueTask<int>(1);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            MemoryWrites++;
            LastToken = cancellationToken;
            return default;
        }
    }
}
