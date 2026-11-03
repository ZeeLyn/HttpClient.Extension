# HttpClient.Extension

Convenience extensions for `System.Net.Http.HttpClient`: typed responses, JSON and form requests, multipart uploads, and file downloads.

| Package | NuGet |
| --- | --- |
| HttpClient.Extension | [![NuGet package](https://buildstats.info/nuget/HttpClient.Extension)](https://www.nuget.org/packages/HttpClient.Extension) |

## Requests

```csharp
using HttpClient.Extension;

using var client = new System.Net.Http.HttpClient
{
    BaseAddress = new Uri("http://localhost:5000/")
};

var response = await client.PostFormAsync<string>(
    "test/req-test", new { name = "a&b+c 中文" });

if (response.Succeed)
    Console.WriteLine(response.Result);
else
    Console.WriteLine($"{response.StatusCode}: {response.Raw}");
```

Object and dictionary forms are URL-encoded automatically. String form overloads accept an already encoded body and send it unchanged. JSON requests use `PostBodyAsync<T>` and `PutAsync<T>`.

Existing overloads remain available. To cancel a request, pass a token after its required arguments and before an optional content type or file list:

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var response = await client.GetAsync<string>("test/payload", cancellation.Token);
await client.PostMultipartAsync<string>("test/req-test", cancellation.Token, "image.png");
```

Tokens apply to sending the request and reading its response body. Cancellation throws `OperationCanceledException`. After a stream has been returned, pass a token to its `ReadAsync` or `CopyToAsync` calls. Cancelled reads may dispose the response content.

Text and JSON responses honor the declared `Content-Type` charset. Scalar numbers and dates use invariant culture. Numbers use `.` as the decimal separator, support exponents, and reject grouping separators. Nullable scalars accept raw values, JSON strings, empty responses and JSON `null`.

Typed `GetAsync`, `PostAsync`, `PutAsync` and `DeleteAsync` requests return `Succeed = false`, a status code and `Raw` for HTTP errors. Transport and deserialization exceptions propagate to the caller.

Header helpers such as `Authorization` and `AddHeader` configure `DefaultRequestHeaders`. Set fixed defaults before sending requests. For per-user tokens on a shared client, use headers on an individual `HttpRequestMessage`.

## Streams and downloads

```csharp
var response = await client.DownloadWithStreamAsync("files/video.mp4");
if (response.Succeed)
{
    using var stream = response.Result;
    using var output = File.Create("video.mp4");
    await stream.CopyToAsync(output);
}
```

Always dispose a returned stream, including `GetAsync<Stream>` and other typed stream requests. Disposing it also releases the HTTP response. `DownloadWithStreamAsync` reads response headers first; ordinary typed request methods retain `HttpClient`'s default response buffering.

Path-based multipart uploads stream files directly. Stream-based uploads take ownership of the supplied streams and dispose them after the request, including on failure or cancellation.

```csharp
await client.DownloadAsync("files/video.mp4", "video.mp4",
    onProgressChanged: (total, downloaded, percent) =>
        Console.WriteLine($"{downloaded}/{total}: {percent}%"),
    onCompleted: downloaded => Console.WriteLine($"Saved {downloaded} bytes"),
    onError: status => Console.WriteLine(status),
    cancellationToken: cancellationToken);
```

File downloads use a temporary file in the destination directory and replace the destination only after a complete download. Failed or cancelled downloads preserve the existing destination and remove the temporary file. Cancellation throws `OperationCanceledException`; I/O and callback exceptions propagate. `onError` reports unsuccessful HTTP status codes. When the response length is unknown, progress reports `total = 0` and `percent = 0`; `onCompleted` receives the actual number of downloaded bytes.

## Examples and tests

Run the local example API at its default address `http://localhost:5000`:

```sh
dotnet run --project Examples
```

`/test/req`, `/test/req2` and `/test/req3` demonstrate JSON success, encoded forms, and HTTP errors. If the listening address changes, update the named client's base address in `Examples/Startup.cs` as well.

```sh
dotnet test HttpClient.Extension.sln -c Release
```

The regression tests run on .NET 8 and .NET 10.
