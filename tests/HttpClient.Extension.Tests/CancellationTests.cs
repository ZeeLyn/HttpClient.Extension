using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;
using Client = System.Net.Http.HttpClient;

namespace HttpClient.Extension.Tests;

public class CancellationTests
{
    public static IEnumerable<object[]> RequestCases()
    {
        foreach (var method in new[]
        {
            "GET", "POST", "POST_STRING", "FORM_STRING", "FORM_OBJECT", "FORM_DICTIONARY",
            "BODY_STRING", "BODY_OBJECT", "PUT", "PUT_STRING", "PUT_OBJECT", "DELETE", "HEAD",
            "MULTIPART_PATH", "MULTIPART_BYTES", "MULTIPART_STREAM", "OBJECT_PATH", "DICTIONARY_PATH",
            "OBJECT_BYTES", "DICTIONARY_BYTES", "OBJECT_STREAM", "DICTIONARY_STREAM",
            "DOWNLOAD", "DOWNLOAD_STREAM", "DOWNLOAD_BYTES"
        })
            foreach (var alreadyCancelled in new[] { false, true })
                yield return new object[] { method, alreadyCancelled };
    }

    [Theory]
    [MemberData(nameof(RequestCases))]
    public async Task RequestOverloadsHonorCancellation(string method, bool alreadyCancelled)
    {
        using var directory = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new Client(new CancellableHandler(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("http://localhost/") };
        if (alreadyCancelled)
            cancellation.Cancel();

        var operation = SendAsync(client, method, directory.Path, cancellation.Token);
        if (!alreadyCancelled)
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(!alreadyCancelled, started.Task.IsCompleted);
        using var exclusive = new FileStream(Path.Combine(directory.Path, "upload.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData("BYTES")]
    [InlineData("ERROR_BYTES")]
    [InlineData("ERROR_STREAM")]
    [InlineData("GET")]
    [InlineData("STREAM_READ")]
    [InlineData("FILE")]
    public async Task CancellationInterruptsBlockedHttpBody(string method)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGet("/blocked", async context =>
        {
            if (method.StartsWith("ERROR", StringComparison.Ordinal))
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.StartAsync();
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("abc"));
            await context.Response.Body.FlushAsync();
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });
        await app.StartAsync();
        using var client = new Client { BaseAddress = new Uri(app.Urls.Single()) };
        using var cancellation = new CancellationTokenSource();
        using var directory = new TestDirectory();
        var destination = Path.Combine(directory.Path, "file.bin");
        await File.WriteAllTextAsync(destination, "original");

        if (method == "STREAM_READ")
        {
            var response = await client.DownloadWithStreamAsync("/blocked", cancellation.Token);
            using var stream = response.Result;
            await stream.ReadExactlyAsync(new byte[3]);
            var pendingRead = stream.ReadAsync(new byte[1], 0, 1, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else
        {
            Task operation = method switch
            {
                "BYTES" or "ERROR_BYTES" => client.DownloadWithBytesAsync("/blocked", cancellation.Token),
                "ERROR_STREAM" => client.DownloadWithStreamAsync("/blocked", cancellation.Token),
                "GET" => client.GetAsync<string>("/blocked", cancellation.Token),
                _ => client.DownloadAsync("/blocked", destination, cancellationToken: cancellation.Token)
            };
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        Assert.Equal("original", await File.ReadAllTextAsync(destination));
        Assert.Equal(destination, Assert.Single(Directory.GetFiles(directory.Path)));
    }

    private static async Task SendAsync(Client client, string method, string directory, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "upload.bin");
        await File.WriteAllTextAsync(path, "upload");
        using var content = new StringContent("request");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("upload"));
        var bytes = new[] { new MultipartFormFileBytesData("file.bin", Encoding.UTF8.GetBytes("upload")) };
        var streams = new[] { new MultipartFormFileStreamData("file.bin", stream) };
        var paths = new[] { path };
        var form = new Dictionary<string, string> { ["name"] = "value" };
        var obj = new { name = "value" };
        await (method switch
        {
            "GET" => client.GetAsync<string>("/", cancellationToken),
            "POST" => client.PostAsync<string>("/", content, cancellationToken),
            "POST_STRING" => client.PostAsync<string>("/", "request", cancellationToken),
            "FORM_STRING" => client.PostFormAsync<string>("/", "name=value", cancellationToken),
            "FORM_OBJECT" => client.PostFormAsync<string>("/", obj, cancellationToken),
            "FORM_DICTIONARY" => client.PostFormAsync<string>("/", form, cancellationToken),
            "BODY_STRING" => client.PostBodyAsync<string>("/", "{}", cancellationToken),
            "BODY_OBJECT" => client.PostBodyAsync<string>("/", obj, cancellationToken),
            "PUT" => client.PutAsync<string>("/", content, cancellationToken),
            "PUT_STRING" => client.PutAsync<string>("/", "{}", cancellationToken),
            "PUT_OBJECT" => client.PutAsync<string>("/", obj, cancellationToken),
            "DELETE" => client.DeleteAsync<string>("/", cancellationToken),
            "HEAD" => client.HeadAsync("/", cancellationToken),
            "MULTIPART_PATH" => client.PostMultipartAsync<string>("/", cancellationToken, paths),
            "MULTIPART_BYTES" => client.PostMultipartAsync<string>("/", cancellationToken, bytes),
            "MULTIPART_STREAM" => client.PostMultipartAsync<string>("/", cancellationToken, streams),
            "OBJECT_PATH" => client.PostMultipartAsync<string>("/", obj, cancellationToken, paths),
            "DICTIONARY_PATH" => client.PostMultipartAsync<string>("/", form, cancellationToken, paths),
            "OBJECT_BYTES" => client.PostMultipartAsync<string>("/", obj, cancellationToken, bytes),
            "DICTIONARY_BYTES" => client.PostMultipartAsync<string>("/", form, cancellationToken, bytes),
            "OBJECT_STREAM" => client.PostMultipartAsync<string>("/", obj, cancellationToken, streams),
            "DICTIONARY_STREAM" => client.PostMultipartAsync<string>("/", form, cancellationToken, streams),
            "DOWNLOAD" => client.DownloadAsync("/", Path.Combine(directory, "download.bin"), cancellationToken: cancellationToken),
            "DOWNLOAD_STREAM" => client.DownloadWithStreamAsync("/", cancellationToken),
            _ => (Task)client.DownloadWithBytesAsync("/", cancellationToken)
        });
    }

    private sealed class CancellableHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
