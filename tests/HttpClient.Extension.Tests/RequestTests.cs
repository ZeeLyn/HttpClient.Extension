using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;
using Client = System.Net.Http.HttpClient;

namespace HttpClient.Extension.Tests;

public class RequestTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("POST_STRING")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("DOWNLOAD")]
    public async Task StreamResultsRemainReadableUntilDisposed(string method)
    {
        var content = new TrackedContent("response");
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        var result = await SendStreamAsync(client, method);

        Assert.True(result.Succeed);
        Assert.Equal("text/plain", result.ContentHeaders.ContentType!.MediaType);
        Assert.Equal(0, content.DisposeCount);
        using (var stream = result.Result)
        using (var reader = new StreamReader(stream))
            Assert.Equal("response", await reader.ReadToEndAsync());
        Assert.Equal(1, content.DisposeCount);
        result.Result.Dispose();
        Assert.Equal(1, content.DisposeCount);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("POST_STRING")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("DOWNLOAD")]
    public async Task FailedStreamRequestsReturnMetadataAndReleaseResponse(string method)
    {
        var content = new TrackedContent("missing");
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = content });

        var result = await SendStreamAsync(client, method);

        Assert.False(result.Succeed);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Null(result.Result);
        Assert.Equal("missing", result.Raw);
        Assert.Equal("text/plain", result.ContentHeaders.ContentType!.MediaType);
        Assert.Equal(1, content.DisposeCount);
    }

    [Fact]
    public async Task NonStreamResponsesReleaseContentAfterReading()
    {
        var content = new TrackedContent("{\"value\":42}");
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        var result = await client.GetAsync<Dictionary<string, int>>("/");

        Assert.Equal(42, result.Result["value"]);
        Assert.Equal(1, content.DisposeCount);
    }

    [Fact]
    public async Task InvalidJsonReleasesResponseBeforeThrowing()
    {
        var content = new TrackedContent("<html>");
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        await Assert.ThrowsAsync<Newtonsoft.Json.JsonReaderException>(() => client.GetAsync<Dictionary<string, int>>("/"));

        Assert.Equal(1, content.DisposeCount);
    }

    [Fact]
    public async Task HeadReleasesResponseAndKeepsHeaders()
    {
        var content = new TrackedContent("");
        content.Headers.ContentLength = 123;
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        var result = await client.HeadAsync("/");

        Assert.Equal(123, result.Result.ContentLength);
        Assert.Same(result.Result, result.ContentHeaders);
        Assert.Equal(1, content.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentHeadersRemainAvailableAfterResponseDisposal(bool failed)
    {
        var content = new TrackedContent("response");
        content.Headers.ContentLength = 8;
        content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = "file.txt" };
        var modified = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        content.Headers.LastModified = modified;
        using var client = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(failed ? HttpStatusCode.BadRequest : HttpStatusCode.OK) { Content = content };
            response.Headers.Add("X-Request-Id", "request-id");
            return response;
        });

        var result = await client.GetAsync<string>("/");

        Assert.Equal(1, content.DisposeCount);
        Assert.Equal("request-id", Assert.Single(result.Headers.GetValues("X-Request-Id")));
        Assert.Equal("text/plain", result.ContentHeaders.ContentType!.MediaType);
        Assert.Equal(8, result.ContentHeaders.ContentLength);
        Assert.Equal("file.txt", result.ContentHeaders.ContentDisposition!.FileName);
        Assert.Equal(modified, result.ContentHeaders.LastModified);
        Assert.Equal(failed ? "response" : "", result.Raw);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FormValuesAreEncoded(bool useObject)
    {
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues>? received = null;
        string? mediaType = null;
        using var client = CreateClient(async request =>
        {
            received = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync());
            mediaType = request.Content.Headers.ContentType!.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });
        const string value = "a&admin=true+b%20=中文 #";

        if (useObject)
            await client.PostFormAsync<string>("/", new { value, empty = (string?)null });
        else
            await client.PostFormAsync<string>("/", new Dictionary<string, string> { ["value"] = value, ["empty"] = null!, ["a&b"] = "+" });

        Assert.Equal("application/x-www-form-urlencoded", mediaType);
        Assert.Equal(value, received!["value"].ToString());
        Assert.Equal("", received["empty"].ToString());
        Assert.False(received.ContainsKey("admin"));
        if (!useObject)
            Assert.Equal("+", received["a&b"].ToString());
    }

    [Fact]
    public async Task RawFormStringIsNotEncodedAgain()
    {
        const string form = "value=a%26b%2Bc&value=second";
        string? received = null;
        using var client = CreateClient(async request =>
        {
            received = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });

        await client.PostFormAsync<string>("/", form);

        Assert.Equal(form, received);
    }

    [Theory]
    [InlineData("application/custom")]
    [InlineData(null)]
    public async Task EncodedFormPreservesCustomContentType(string? contentType)
    {
        string? mediaType = null;
        using var client = CreateClient(request =>
        {
            mediaType = request.Content!.Headers.ContentType!.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });

        await client.PostFormAsync<string>("/", new { value = "a&b" }, contentType!);

        Assert.Equal(contentType ?? "text/plain", mediaType);
    }

    [Theory]
    [InlineData("PATH")]
    [InlineData("BYTES")]
    [InlineData("STREAM")]
    public async Task MultipartBoundaryDoesNotSplitFileContents(string input)
    {
        using var directory = new TestDirectory();
        const string payload = "abc\r\n--form-data\r\nContent-Disposition: form-data; name=\"injected\"\r\n\r\nYES";
        var parts = new List<string>();
        using var client = CreateClient(async request =>
        {
            var boundary = request.Content!.Headers.ContentType!.Parameters.Single(p => p.Name == "boundary").Value!.Trim('"');
            var reader = new MultipartReader(boundary, await request.Content.ReadAsStreamAsync());
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync()) != null)
                parts.Add(await new StreamReader(section.Body).ReadToEndAsync());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });
        var bytes = Encoding.UTF8.GetBytes(payload);

        switch (input)
        {
            case "PATH":
                var path = Path.Combine(directory.Path, "file.bin");
                await File.WriteAllBytesAsync(path, bytes);
                await client.PostMultipartAsync<string>("/", new[] { path });
                break;
            case "BYTES":
                await client.PostMultipartAsync<string>("/", new MultipartFormFileBytesData("file.bin", bytes));
                break;
            default:
                await client.PostMultipartAsync<string>("/", new MultipartFormFileStreamData("file.bin", new MemoryStream(bytes)));
                break;
        }

        Assert.Equal(payload, Assert.Single(parts));
    }

    [Theory]
    [InlineData("PATH")]
    [InlineData("BYTES")]
    [InlineData("STREAM")]
    public async Task MultipartDictionaryNullFieldsAreSentAsEmptyStrings(string input)
    {
        var fields = new List<string>();
        using var client = CreateClient(async request =>
        {
            var boundary = request.Content!.Headers.ContentType!.Parameters.Single(p => p.Name == "boundary").Value!.Trim('"');
            var reader = new MultipartReader(boundary, await request.Content.ReadAsStreamAsync());
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync()) != null)
                fields.Add(await new StreamReader(section.Body).ReadToEndAsync());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });
        var form = new Dictionary<string, string> { ["empty"] = null! };

        switch (input)
        {
            case "PATH":
                await client.PostMultipartAsync<string>("/", form, Array.Empty<string>());
                break;
            case "BYTES":
                await client.PostMultipartAsync<string>("/", form, Array.Empty<MultipartFormFileBytesData>());
                break;
            default:
                await client.PostMultipartAsync<string>("/", form, Array.Empty<MultipartFormFileStreamData>());
                break;
        }

        Assert.Empty(Assert.Single(fields));
    }

    [Fact]
    public async Task FailedPathUploadReleasesFilesAlreadyOpened()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "file.bin");
        await File.WriteAllTextAsync(path, "file");
        using var client = CreateClient(_ => Task.FromException<HttpResponseMessage>(new InvalidOperationException("request must not be sent")));

        await Assert.ThrowsAsync<FileNotFoundException>(() => client.PostMultipartAsync<string>("/", path, Path.Combine(directory.Path, "missing.bin")));

        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task InvalidMultipartFieldNameReleasesInputStream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("file"));
        using var client = CreateClient(_ => Task.FromException<HttpResponseMessage>(new InvalidOperationException("request must not be sent")));

        await Assert.ThrowsAsync<ArgumentException>(() => client.PostMultipartAsync<string>("/", new MultipartFormFileStreamData("file.bin", stream, "")));

        Assert.False(stream.CanRead);
    }

    private static async Task<ResponseResult<Stream>> SendStreamAsync(Client client, string method)
    {
        using var content = new StringContent("request");
        return await (method switch
        {
            "GET" => client.GetAsync<Stream>("/"),
            "POST" => client.PostAsync<Stream>("/", content),
            "POST_STRING" => client.PostAsync<Stream>("/", "request"),
            "PUT" => client.PutAsync<Stream>("/", content),
            "DELETE" => client.DeleteAsync<Stream>("/"),
            _ => client.DownloadWithStreamAsync("/")
        });
    }

    internal static Client CreateClient(Func<HttpRequestMessage, HttpResponseMessage> send) =>
        CreateClient(request => Task.FromResult(send(request)));

    internal static Client CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    private sealed class TrackedContent(string text) : StringContent(text)
    {
        public int DisposeCount { get; private set; }
        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }
}

internal sealed class TestDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("HttpClient.Extension.Tests-").FullName;

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(Path))
            File.Delete(file);
        Directory.Delete(Path);
    }
}
