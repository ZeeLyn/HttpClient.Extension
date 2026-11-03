using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HttpClient.Extension.Tests;

public class DownloadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChunkedHttpResponseSupportsFileAndStreamDownloads(bool returnStream)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGet("/file", async context =>
        {
            await context.Response.StartAsync();
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("abc"));
        });
        await app.StartAsync();
        using var client = new System.Net.Http.HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var directory = new TestDirectory();

        if (returnStream)
        {
            var response = await client.DownloadWithStreamAsync("/file");
            using var stream = response.Result;
            Assert.False(stream.CanSeek);
            using var reader = new StreamReader(stream);
            Assert.Equal("abc", await reader.ReadToEndAsync());
        }
        else
        {
            var destination = Path.Combine(directory.Path, "file.bin");
            long completed = -1;
            (long total, long read, decimal percent) progress = default;
            await client.DownloadAsync("/file", destination,
                (total, read, percent) => progress = (total, read, percent),
                count => completed = count);
            Assert.Equal("abc", await File.ReadAllTextAsync(destination));
            Assert.Equal((0L, 3L, 0m), progress);
            Assert.Equal(3, completed);
        }
    }

    [Fact]
    public async Task EmptyDownloadProducesEmptyFile()
    {
        using var directory = new TestDirectory();
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });
        var destination = Path.Combine(directory.Path, "file.bin");
        long completed = -1;

        await client.DownloadAsync("/", destination, onCompleted: count => completed = count);

        Assert.Equal(0, completed);
        Assert.Equal(0, new FileInfo(destination).Length);
    }

    [Fact]
    public async Task AlreadyCancelledDownloadDoesNotSendRequest()
    {
        using var directory = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sent = false;
        using var client = RequestTests.CreateClient(_ =>
        {
            sent = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync("/",
            Path.Combine(directory.Path, "file.bin"), cancellationToken: cancellation.Token));

        Assert.False(sent);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulDownloadCommitsFileAndReportsActualBytes(bool existingFile)
    {
        using var directory = new TestDirectory();
        var destination = Path.Combine(directory.Path, "file.bin");
        if (existingFile)
            await File.WriteAllTextAsync(destination, "original");
        using var client = RequestTests.CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("abc") };
        });
        long completed = -1;
        (long total, long read, decimal percent) progress = default;

        await client.DownloadAsync("/", destination,
            (total, read, percent) => progress = (total, read, percent),
            count =>
            {
                completed = count;
                Assert.Equal("abc", File.ReadAllText(destination));
            });

        Assert.Equal(3, completed);
        Assert.Equal((3L, 3L, 100m), progress);
        Assert.Equal(destination, Assert.Single(Directory.GetFiles(directory.Path)));
    }

    [Fact]
    public async Task DownloadWithoutProgressReportsActualBytes()
    {
        using var directory = new TestDirectory();
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("abc") });
        long completed = -1;

        await client.DownloadAsync("/", Path.Combine(directory.Path, "file.bin"), onCompleted: count => completed = count);

        Assert.Equal(3, completed);
    }

    [Fact]
    public async Task DownloadAcceptsBareFileName()
    {
        var destination = $"HttpClient.Extension.Tests-{Guid.NewGuid():N}.bin";
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("abc") });
        try
        {
            await client.DownloadAsync("/", destination);
            Assert.Equal("abc", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrokenDownloadPreservesDestinationAndCleansTemporaryFile(bool existingFile)
    {
        using var directory = new TestDirectory();
        var destination = Path.Combine(directory.Path, "file.bin");
        if (existingFile)
            await File.WriteAllTextAsync(destination, "original");
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream()) });

        await Assert.ThrowsAsync<IOException>(() => client.DownloadAsync("/", destination));

        await AssertDestinationUnchanged(directory, destination, existingFile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationPreservesDestinationAndCleansTemporaryFile(bool existingFile)
    {
        using var directory = new TestDirectory();
        var destination = Path.Combine(directory.Path, "file.bin");
        if (existingFile)
            await File.WriteAllTextAsync(destination, "original");
        using var cancellation = new CancellationTokenSource();
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("abc") });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync("/", destination,
            (_, _, _) => cancellation.Cancel(), cancellationToken: cancellation.Token));

        await AssertDestinationUnchanged(directory, destination, existingFile);
    }

    [Fact]
    public async Task IncorrectContentLengthDoesNotReplaceDestination()
    {
        using var directory = new TestDirectory();
        var destination = Path.Combine(directory.Path, "file.bin");
        await File.WriteAllTextAsync(destination, "original");
        var content = new StringContent("abc");
        content.Headers.ContentLength = 10;
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        await Assert.ThrowsAsync<EndOfStreamException>(() => client.DownloadAsync("/", destination));

        await AssertDestinationUnchanged(directory, destination, true);
    }

    [Fact]
    public async Task ProgressCallbackFailureDoesNotReplaceDestination()
    {
        using var directory = new TestDirectory();
        var destination = Path.Combine(directory.Path, "file.bin");
        await File.WriteAllTextAsync(destination, "original");
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("abc") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadAsync("/", destination,
            (_, _, _) => throw new InvalidOperationException("callback failed")));

        await AssertDestinationUnchanged(directory, destination, true);
    }

    [Fact]
    public async Task HttpFailureDoesNotReplaceDestination()
    {
        using var directory = new TestDirectory();
        var destination = Path.Combine(directory.Path, "file.bin");
        await File.WriteAllTextAsync(destination, "original");
        using var client = RequestTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        string? error = null;

        await client.DownloadAsync("/", destination, onError: value => error = value);

        Assert.Equal("NotFound", error);
        await AssertDestinationUnchanged(directory, destination, true);
    }

    private static async Task AssertDestinationUnchanged(TestDirectory directory, string destination, bool existingFile)
    {
        if (existingFile)
        {
            Assert.Equal("original", await File.ReadAllTextAsync(destination));
            Assert.Equal(destination, Assert.Single(Directory.GetFiles(directory.Path)));
        }
        else
            Assert.Empty(Directory.GetFiles(directory.Path));
    }

    private sealed class BrokenStream : MemoryStream
    {
        private bool read;
        public BrokenStream() : base(Encoding.UTF8.GetBytes("abc")) { }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (read)
                throw new IOException("simulated broken connection");
            read = true;
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }
}
