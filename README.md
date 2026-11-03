# HttpClient.Extension

English | [简体中文](README.zh-CN.md)

Extensions for `System.Net.Http.HttpClient` covering typed responses, JSON, forms, file uploads, and downloads. Use your existing `HttpClient` or `IHttpClientFactory`; no library-specific service registration is required.

This guide describes **2.0.0**. The library does not provide Resilience features such as retries or circuit breakers.

## Contents

- [Installation and target frameworks](#installation-and-target-frameworks)
- [Quick start](#quick-start)
- [Sending requests](#sending-requests)
- [Responses and failures](#responses-and-failures)
- [Response types and parsing](#response-types-and-parsing)
- [Request headers](#request-headers)
- [File uploads](#file-uploads)
- [Downloads and resource ownership](#downloads-and-resource-ownership)
- [Cancellation and timeouts](#cancellation-and-timeouts)
- [Dependency injection and client reuse](#dependency-injection-and-client-reuse)
- [Custom requests and content reading](#custom-requests-and-content-reading)
- [Upgrading from earlier versions](#upgrading-from-earlier-versions)
- [Local examples and verification](#local-examples-and-verification)

## Installation and target frameworks

Once the version is published to NuGet:

```sh
dotnet add package HttpClient.Extension --version 2.0.0
```

To use an unpublished local package, add the reference first, then restore from both the local directory and NuGet. Run these commands in your application project directory and replace the local source with its actual path:

```sh
dotnet add package HttpClient.Extension --version 2.0.0 --no-restore
dotnet restore --source E:/GitHub/HttpClient.Extension/artifacts/package --source https://api.nuget.org/v3/index.json
```

The package includes assemblies for:

| Category | Target frameworks |
| --- | --- |
| .NET Standard | `netstandard2.0`, `netstandard2.1` |
| .NET | `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0` |

JSON serialization and deserialization use `Newtonsoft.Json 13.0.4`. The library has no runtime dependency on ASP.NET Core or a dependency injection framework. The examples use .NET 8 or later hosts and modern C# syntax. Older projects can use the appropriate library assembly, but may need to adapt example syntax such as top-level statements and `using var`, and choose file I/O APIs available on their runtime.

## Quick start

This is a complete console example. Replace the example host and paths with your API:

```csharp
using System;
using System.Threading;
using HttpClient.Extension;
using Client = System.Net.Http.HttpClient;

using var client = new Client
{
    BaseAddress = new Uri("https://api.example.com/v1/"),
    Timeout = TimeSpan.FromSeconds(30)
};
client.Accept("application/json");

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var response = await client.GetAsync<UserDto>("users/1", cancellation.Token);

if (response.Succeed)
    Console.WriteLine(response.Result?.Name);
else
    Console.WriteLine($"HTTP {(int)response.StatusCode}: {response.Raw}");

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

The remaining snippets assume these imports, an existing reusable `client`, a `CancellationToken cancellationToken`, and the `UserDto` above:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using HttpClient.Extension;
```

End `BaseAddress` with `/` and omit a leading `/` from relative API paths when you want to preserve the base path. The base above plus `users/1` produces `https://api.example.com/v1/users/1`; `/users/1` starts from the host root instead. Pass an absolute URL if no `BaseAddress` is configured.

In an application, reuse the client or use `IHttpClientFactory` as shown below, so each request does not create its own connection pool.

## Sending requests

### Choosing a method

The table omits generic response types, cancellation tokens, and optional `contentType` parameters. Request methods return `Task<ResponseResult<T>>`, except the callback-based file download. `HeadAsync` returns `Task<ResponseResult<HttpContentHeaders>>`.

| Method | Request body | Default Content-Type |
| --- | --- | --- |
| `GetAsync<T>(url)` | None | None |
| `PostAsync<T>(url, string)` | String sent unchanged | `application/x-www-form-urlencoded` |
| `PostAsync<T>(url, HttpContent)` | Supplied content | Defined by the content |
| `PostBodyAsync<T>(url, object)` | Object serialized to JSON | `application/json` |
| `PostBodyAsync<T>(url, string)` | Already serialized JSON, sent unchanged | `application/json` |
| `PostFormAsync<T>(url, object/IDictionary<string, string>)` | Automatically URL-encoded form fields | `application/x-www-form-urlencoded` |
| `PostFormAsync<T>(url, string)` | Already encoded form body, sent unchanged | `application/x-www-form-urlencoded` |
| `PostMultipartAsync<T>(...)` | Files and optional form fields | `multipart/form-data`, with a generated boundary |
| `PutAsync<T>(url, object)` | Object serialized to JSON | `application/json` |
| `PutAsync<T>(url, string)` | String sent unchanged | `application/json` |
| `PutAsync<T>(url, HttpContent)` | Supplied content | Defined by the content |
| `DeleteAsync<T>(url)` | None | None |
| `HeadAsync(url)` | None | None |

**The generic `T` is the response type, not the request body type.** For example, `PostBodyAsync<UserDto>` can send an anonymous object and receive user information from the server.

`client.GetAsync("url")` is the native .NET method and returns `HttpResponseMessage`. Use `client.GetAsync<T>("url")` to receive this library's `ResponseResult<T>`. Keep the same distinction in mind for other extension methods that share native method names.

### GET, query parameters, and DELETE

```csharp
var user = await client.GetAsync<UserDto>("users/1", cancellationToken);
var text = await client.GetAsync<string>("status", cancellationToken);

var keyword = Uri.EscapeDataString("a&b 中文");
var users = await client.GetAsync<List<UserDto>>(
    $"users?keyword={keyword}&page=1", cancellationToken);

var deleted = await client.DeleteAsync<string>("users/1", cancellationToken);
```

The caller constructs query parameters. The library does not automatically append an object to a GET URL. Encode individual query values rather than encoding the entire URL.

### POST JSON

```csharp
var created = await client.PostBodyAsync<UserDto>(
    "users", new { name = "张三", age = 20 }, cancellationToken);

var json = "{\"name\":\"张三\",\"age\":20}";
var fromJson = await client.PostBodyAsync<UserDto>(
    "users", json, cancellationToken);

var customType = await client.PostBodyAsync<UserDto>(
    "users", new { name = "张三" }, cancellationToken,
    contentType: "application/vnd.example.user+json");
```

The object overload calls `JsonConvert.SerializeObject`. The string overload treats its input as existing JSON and does not serialize it again. To send a JSON string value itself, supply a body containing JSON quotes, such as `"\"hello\""`.

The string overload of ordinary `PostAsync<T>` does not detect JSON and defaults to a form content type. Use `PostBodyAsync<T>` for JSON, or explicitly set `contentType: "application/json"`.

### PUT

```csharp
var updated = await client.PutAsync<UserDto>(
    "users/1", new { name = "李四" }, cancellationToken);

var fromJson = await client.PutAsync<UserDto>(
    "users/1", "{\"name\":\"李四\"}", cancellationToken);

var text = await client.PutAsync<string>(
    "notes/1", "Plain text", cancellationToken, contentType: "text/plain");
```

### Forms

```csharp
var fromObject = await client.PostFormAsync<string>(
    "forms", new { name = "a&b+c 中文", amount = 1.5m }, cancellationToken);

var fields = new Dictionary<string, string>
{
    ["name"] = "a&b+c 中文",
    ["note"] = string.Empty
};
var fromDictionary = await client.PostFormAsync<string>(
    "forms", fields, cancellationToken);

var fromEncodedText = await client.PostFormAsync<string>(
    "forms", "name=a%26b%2Bc&note=", cancellationToken);
```

Object and dictionary overloads:

- Encode field names and values automatically. Do not pre-encode Unicode or characters such as `+`, `&`, and `%`.
- Read public instance properties with public getters. Fields, static properties, indexers, and non-public getters are ignored.
- Execute property getters; exceptions from getters fail the request.
- Convert numbers and dates with `InvariantCulture`, so `1.5m` does not become `1,5` under a different system culture. Null values become empty strings.
- Preserve string values before encoding. Nested objects and collections are not recursively flattened or automatically serialized to JSON; convert them into the strings your API expects.
- Recognize `IDictionary<string, string>` fields even when the dictionary is stored in an `object` variable.

The string overload sends the body unchanged. It also supports repeated field names, such as `tag=one&tag=two`, when you construct the encoded body yourself. Changing `contentType` only changes the header, not these encoding rules.

### Custom HttpContent

```csharp
using var content = new StringContent("hello", System.Text.Encoding.UTF8, "text/plain");
var posted = await client.PostAsync<string>("echo", content, cancellationToken);

using var putContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
putContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
var updated = await client.PutAsync<string>("files/1", putContent, cancellationToken);
```

The caller owns directly supplied `HttpContent`. Disposing `StreamContent` also disposes its underlying stream. The library manages content it creates for string, object, form, and built-in multipart overloads.

### HEAD

```csharp
var head = await client.HeadAsync("files/report.pdf", cancellationToken);
if (head.Succeed)
{
    Console.WriteLine(head.ContentHeaders.ContentLength);
    Console.WriteLine(head.ContentHeaders.ContentType);
    Console.WriteLine(head.Result.LastModified);
}
```

`HeadAsync` has no generic type parameter. Its `Result` and `ContentHeaders` refer to the same content header collection, including for HTTP failures. HEAD does not read a response body; `Raw` has no body data and is currently `null`.

## Responses and failures

### ResponseResult<T>

| Property | Type | Meaning |
| --- | --- | --- |
| `Succeed` | `bool` | Whether the HTTP status is in the 200–299 range |
| `StatusCode` | `HttpStatusCode` | Actual HTTP status |
| `Headers` | `HttpResponseHeaders` | General response headers, such as `ETag` or a request ID |
| `ContentHeaders` | `HttpContentHeaders` | Content metadata: type, length, filename, last modification |
| `Result` | `T` | Parsed successful result; `default(T)` for HTTP failures, with the HEAD exception above |
| `Raw` | `string` | Complete decoded body for HTTP failures; an empty string for success, with the HEAD exception above |

`Succeed` reflects only the HTTP status. An HTTP 200 response containing `{"success":false}` still produces `Succeed = true`; inspect `Result` for business-level success.

Successful responses with no body, such as HTTP 204, still enter the parser. A `string` normally becomes empty, a byte array is empty, built-in scalars use their default values, and DTO/JSON types follow Newtonsoft.Json's empty-content behavior. Choose `string` or a suitable nullable result type when the API does not guarantee a body.

### HTTP failures and Raw

```csharp
var response = await client.GetAsync<UserDto>("users/404", cancellationToken);
if (!response.Succeed)
{
    Console.WriteLine((int)response.StatusCode);
    Console.WriteLine(response.Raw);
    Console.WriteLine(response.ContentHeaders.ContentType);
}
```

A non-2xx status does not itself throw and does not force the error body through the `T` deserializer. If reading the body succeeds, all result types, including text, DTOs, scalars, `byte[]`, and `Stream`, receive the error text through `Raw`. For example, `GetAsync<UserDto>` can receive an HTML error page in `Raw` without that page being valid `UserDto` JSON.

```csharp
var response = await client.DownloadWithBytesAsync("files/missing", cancellationToken);
if (!response.Succeed)
{
    // The successful result type is byte[], but the error body is text in Raw.
    Console.WriteLine(response.Raw);
    Console.WriteLine(response.Result is null);
}
```

Details:

- `Raw` contains only HTTP failure bodies, not the original successful response. Use `GetAsync<string>` and deserialize yourself if you need the successful original text.
- `Raw` is decoded text and does not preserve a binary error body byte for byte. Use native `SendAsync` if you need the original error bytes.
- `default(T)` is `null` for reference and nullable types, but `0`, `false`, or another default for value types. Do not use it as the success indicator.
- Network failures, interrupted body reads, buffer limits, cancellation, and parsing errors can still throw. These cases do not return an error-filled `ResponseResult<T>`.
- The callback-based `DownloadAsync` does not return `ResponseResult<T>`; it reports HTTP failures through `onError`, as described below.

### Response headers

```csharp
var response = await client.GetAsync<string>("status", cancellationToken);
if (response.Headers.TryGetValues("X-Request-Id", out var requestIds))
    Console.WriteLine(string.Join(", ", requestIds));

Console.WriteLine(response.ContentHeaders.ContentType?.MediaType);
Console.WriteLine(response.ContentHeaders.ContentLength);
Console.WriteLine(response.ContentHeaders.ContentDisposition?.FileNameStar
    ?? response.ContentHeaders.ContentDisposition?.FileName);
Console.WriteLine(response.ContentHeaders.LastModified);
```

Optional headers may be `null` if the server did not supply them. Content metadata belongs to `ContentHeaders`, not `Headers`. Non-stream responses are disposed after reading, but their header collections remain readable. Previously obtained headers also remain readable after a returned stream is disposed.

### Exceptions

```csharp
try
{
    var response = await client.GetAsync<UserDto>("users/1", cancellationToken);
    if (!response.Succeed)
        Console.WriteLine($"HTTP {(int)response.StatusCode}: {response.Raw}");
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    Console.WriteLine("The caller cancelled the request");
}
catch (OperationCanceledException)
{
    Console.WriteLine("The request timed out or was cancelled");
}
catch (HttpRequestException exception)
{
    Console.WriteLine($"Network, protocol, or response buffer error: {exception.Message}");
}
catch (Newtonsoft.Json.JsonException exception)
{
    Console.WriteLine($"Invalid response JSON: {exception.Message}");
}
```

Scalar parsing can also throw `FormatException` or `OverflowException`, and file operations can throw I/O exceptions. Handle them as needed by your application. The library does not automatically retry requests.

## Response types and parsing

The generic `T` selects the response parser. `Content-Type` does not automatically choose between JSON and scalar parsing.

| Type | Successful response handling |
| --- | --- |
| `string` | Decode text; retain JSON quotes and the literal text `null` |
| `byte[]` | Return original bytes without charset decoding |
| `Stream` | Return a stream that the caller must dispose |
| Integers | `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, parsed with invariant culture |
| Floating-point and decimal | `float`, `double`, `decimal`; use `.` for decimals, allow exponents, reject grouping separators |
| `bool` | Use `bool.Parse`; accept `true`/`false`, not automatic `0`/`1` conversion |
| `char` | Use the first character; return the default character for an empty body |
| Date and other scalars | `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid` |
| Nullable versions of these scalars | Accept plain text, JSON strings, empty bodies, and JSON `null` |
| Other types: DTOs, collections, enums | Use `JsonConvert.DeserializeObject<T>` |

```csharp
var count = await client.GetAsync<int>("metrics/count", cancellationToken);
var price = await client.GetAsync<decimal>("metrics/price", cancellationToken);
var optionalCount = await client.GetAsync<int?>("metrics/optional-count", cancellationToken);
var users = await client.GetAsync<List<UserDto>>("users", cancellationToken);
```

Typical scalar inputs:

| Response body | `T` | Result |
| --- | --- | --- |
| `42` | `int` | `42` |
| `"42"` | `int?` | `42` |
| Empty, whitespace, `null`, `""` | `int?` | `null` |
| Empty or whitespace | `int` | `0` |
| `1.5` or `1e-3` | `decimal` | `1.5m` or `0.001m` |
| `1,5` or `1,234.56` | `decimal` | Format exception |
| `42.0` or `"42"` | `int` | Format exception |
| `"hello"` | `string` | JSON quotes retained |
| `hello` | `char` | `'h'` |

Nullable built-in scalars unwrap JSON string quotes. Non-nullable built-in scalars parse plain text; do not add JSON quotes around a non-nullable number or date. Properties inside a DTO are parsed by Newtonsoft.Json and do not follow this top-level scalar rule.

Text, JSON, and `Raw` honor the response `Content-Type` charset. If none is specified, .NET `HttpContent.ReadAsStringAsync` applies its BOM/UTF-8 rules. Invalid encodings or encodings unavailable in the runtime can throw. The library does not register legacy code pages.

## Request headers

These methods modify `DefaultRequestHeaders` and return the same client. Configure them before sending requests:

```csharp
client
    .Authorization("Bearer", "example-access-token")
    .UserAgent("MyApp/2.0")
    .Accept("application/json;q=0.8, text/plain")
    .AddHeader("X-App-Id", "demo");

client.AddHeader(new Dictionary<string, string>
{
    ["X-Source"] = "desktop",
    ["X-Language"] = "zh-CN"
});

client.AddHeader("X-Trace", "trace-id", "X-Region", "cn");
client.Referrer("https://example.com/");
client.Range(0, 1023);
```

| Method | Behavior |
| --- | --- |
| `AddHeader(name, value)` | Remove an existing header of the same name before setting it |
| `AddHeader(IDictionary<string, string>)` | Replace matching headers individually; an empty or null dictionary changes nothing |
| `AddHeader(params string[])` | Supply name/value pairs; the number of arguments must be even |
| `Authorization(scheme, parameter)` | Set or replace the authentication header |
| `UserAgent(value)` | Parse and append User-Agent product information |
| `Accept(value)` | Parse and append media types, including comma-separated lists and `q` weights |
| `Range(from, to)` | Set a byte range; `Range(1024, null)` starts at that offset, `Range(null, 1024)` requests the last 1024 bytes |
| `Referrer(uri)` | Set Referer |

.NET validates header values and can reject invalid values. Set `Content-Type` on `HttpContent.Headers` or with the request overload's `contentType` parameter.

Do not mutate shared default headers during concurrent requests or switch a shared default authentication header between users. Use a separate `HttpRequestMessage` for request-specific authentication and headers, as shown below.

`Range` only sets a request header; server behavior depends on API support. The file download method does not append to a local file, so setting a range does not provide automatic download resumption.

## File uploads

`PostMultipartAsync<T>` supports paths, byte arrays, and streams. Each input type can be combined with an object or `IDictionary<string, string>` of form fields. The cancellation token goes before the file list, and optional fields go before the token.

### File paths

```csharp
var response = await client.PostMultipartAsync<string>(
    "uploads", cancellationToken, "avatar.png", "report.pdf");

var withFields = await client.PostMultipartAsync<string>(
    "uploads", new { userId = 1, note = "Avatar" }, cancellationToken, "avatar.png");
```

Path uploads stream the file directly rather than loading it fully into memory first. The default file field name is `file`. Multiple paths become multiple files under that field, and each filename is taken from the last part of its path.

### Byte arrays

```csharp
var bytes = await File.ReadAllBytesAsync("avatar.png", cancellationToken);
var avatar = new MultipartFormFileBytesData("avatar.png", bytes, name: "avatar");

var response = await client.PostMultipartAsync<string>(
    "uploads", cancellationToken, avatar);

var fields = new Dictionary<string, string> { ["userId"] = "1" };
var withFields = await client.PostMultipartAsync<string>(
    "uploads", fields, cancellationToken, avatar);
```

`MultipartFormFileBytesData` takes `fileName`, `bytes`, and optional `name`, which defaults to `"file"`. A parameterless constructor and the `FileName`, `FileBytes`, and `Name` properties are also available. Byte arrays require the whole file in memory and suit small files or data already held in memory.

### Streams

```csharp
using var input = File.OpenRead("avatar.png");
var avatar = new MultipartFormFileStreamData("avatar.png", input, name: "avatar");

var response = await client.PostMultipartAsync<string>(
    "uploads", new { userId = 1 }, cancellationToken, avatar);
```

`MultipartFormFileStreamData` takes `fileName`, `fileStream`, and optional `name`. Its `FileName`, `FileStream`, and `Name` properties can also be assigned directly. Upload starts at the current stream position; the library does not rewind it.

**Streams added to the multipart container are disposed with that container and cannot be reused after the request.** Still use `using` for streams you create, to cover cancellation before sending or an earlier assembly failure before the container takes ownership of that stream.

### Encoding and content types

Regular multipart fields use UTF-8 text. Object property selection, conversion, and null handling follow the form rules, but multipart values are not URL-encoded.

File MIME types are looked up by the `FileName` extension in `MediaType.MediaTypes`, ignoring extension case. Unknown extensions use `application/octet-stream`. Query the mapping with `MediaType.MediaTypes.TryGetValue(".png", out var mimeType)`.

To set an individual file's MIME type or other content headers, construct a native multipart container and pass it to `PostAsync<T>`:

```csharp
using var form = new MultipartFormDataContent();
form.Add(new StringContent("1"), "userId");
var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
file.Headers.ContentType = new MediaTypeHeaderValue("application/x-example");
form.Add(file, "attachment", "data.bin");

var response = await client.PostAsync<string>("uploads", form, cancellationToken);
```

Keep the generated boundary. Replacing the request content type with just `multipart/form-data` loses that boundary. Disposing the container also disposes content already added to it.

## Downloads and resource ownership

### Choosing a method

| Method | Successful body handling | Use case |
| --- | --- | --- |
| `DownloadWithBytesAsync(url, token)` | Buffer the complete response and return `byte[]` | Small files or direct byte access |
| `DownloadWithStreamAsync(url, token)` | Obtain headers first, then return the response stream | Large files or custom processing |
| `DownloadAsync(url, saveFileName, ..., token)` | Stream to a temporary file and replace the destination after completion | Saving files with progress callbacks |
| Ordinary typed methods such as `GetAsync<Stream>(url, token)` | Retain native default response buffering, then return a stream | A stream API where buffering is acceptable |

`ResponseResult<T>` does not implement `IDisposable`. The library disposes non-stream HTTP responses after reading. For a successful stream result, the caller disposes `Result`, which also disposes the owning HTTP response.

### Downloading bytes

```csharp
var response = await client.DownloadWithBytesAsync("files/logo.png", cancellationToken);
if (response.Succeed)
    await File.WriteAllBytesAsync("logo.png", response.Result, cancellationToken);
else
    Console.WriteLine(response.Raw);
```

This method honors `HttpClient.MaxResponseContentBufferSize` and throws if the limit is exceeded. Use a stream or file download for large responses to avoid holding the entire body in memory.

### Downloading a stream

```csharp
using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
deadline.CancelAfter(TimeSpan.FromMinutes(5));

var response = await client.DownloadWithStreamAsync("files/video.mp4", deadline.Token);
if (response.Succeed)
{
    using var input = response.Result;
    using var output = File.Create("video.mp4");
    await input.CopyToAsync(output, 81920, deadline.Token);
}
else
{
    Console.WriteLine(response.Raw);
}
```

Dispose the returned stream even if you do not read it completely. A token passed to `DownloadWithStreamAsync` covers sending and obtaining the result. Once the stream is returned, pass the token to each asynchronous `ReadAsync`, `CopyToAsync`, or other read operation.

The manual copy example creates the destination directly and can leave a partial file on failure. Use `DownloadAsync` below to preserve an existing destination. Even when the successful result is a stream, an HTTP failure body is read completely into `Raw`; error bodies are not returned as streams.

### Saving files and reporting progress

```csharp
using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
deadline.CancelAfter(TimeSpan.FromMinutes(5));

await client.DownloadAsync(
    "files/video.mp4",
    "downloads/video.mp4",
    onProgressChanged: (total, downloaded, percent) =>
        Console.WriteLine($"{downloaded}/{total} bytes, {percent}%"),
    onCompleted: downloaded => Console.WriteLine($"Saved {downloaded} bytes"),
    onError: status => Console.WriteLine($"HTTP download failed: {status}"),
    cancellationToken: deadline.Token);
```

| Parameter or callback | Meaning |
| --- | --- |
| `saveFileName` | Relative path, absolute path, or bare filename; create destination directories as needed |
| `onProgressChanged` | `Action<long, long, decimal>`: total bytes, downloaded bytes, percentage from 0 to 100 |
| `onCompleted` | `Action<long>` with actual saved bytes, including 0 for an empty file |
| `onError` | `Action<string>` for unsuccessful HTTP statuses only; receives a name such as `NotFound` |
| `cancellationToken` | Controls sending, body reads, file writes, and cancellation checks before committing |

Behavior:

- Create a `.httpclient-*.tmp` file in the destination directory. Replace or create the destination only after a complete download and declared-length validation.
- A network, I/O, progress callback, or cancellation failure before committing cleans up the temporary file and preserves an existing destination. An I/O failure during cleanup itself also propagates.
- Without `Content-Length`, progress reports `total = 0` and `percent = 0`; completion still receives the actual byte count. Empty files may not produce progress callbacks.
- `onCompleted` runs after saving and closing streams. An exception from it does not roll back the saved file.
- Callbacks execute synchronously and do not marshal to a UI thread. Cancellation, I/O errors, and callback exceptions propagate rather than being routed to `onError`.
- An HTTP failure calls `onError` and returns without exposing the error body. With no `onError`, it still returns. Choose a byte or stream download when you need `Raw`, the status code, and headers.
- Automatic retries, download resumption, and appending to an existing file are not provided.

## Cancellation and timeouts

### Token placement

Existing overloads without a token remain available. Token overloads place it after required arguments and before an optional content type or `params` file list:

```csharp
await client.GetAsync<string>("status", cancellationToken);
await client.PostFormAsync<string>("forms", new { name = "张三" }, cancellationToken);
await client.PostBodyAsync<string>("users", new { name = "张三" }, cancellationToken);
await client.PostAsync<string>("echo", "hello", cancellationToken, "text/plain");
await client.PostMultipartAsync<string>("uploads", cancellationToken, "avatar.png");
await client.PostMultipartAsync<string>(
    "uploads", new { userId = 1 }, cancellationToken, "avatar.png");
```

All three download methods have an optional `cancellationToken` as the last parameter. An already-cancelled token throws `OperationCanceledException` before sending. After a stream is returned, cancelling the earlier token does not automatically cancel later reads that do not receive a token.

### HttpClient.Timeout scope

```csharp
// Configure before the client's first request; assigning client.Timeout also works.
client.Timeout(TimeSpan.FromSeconds(30));
client.MaxResponseContentBufferSize = 16 * 1024 * 1024;
```

| Request type | HttpClient.Timeout scope | Buffer limit |
| --- | --- | --- |
| Ordinary typed requests and `DownloadWithBytesAsync` | Sending through completion of response buffering | Honor `MaxResponseContentBufferSize` |
| `DownloadWithStreamAsync` and `DownloadAsync` | Sending through receipt of headers; not the subsequent complete body read | Use `ResponseHeadersRead`; successful bodies are not buffered in full first |

Pass a token with a deadline for a complete file-download timeout. For a returned stream, pass it to subsequent reads as well. `HttpClient.Timeout` does not limit later synchronous JSON parsing. Cancellation tokens do not forcibly interrupt synchronous serialization or deserialization already in progress; cancellation is observed at checks.

On netstandard compatibility paths, cancellation during content reading may dispose `HttpContent` to interrupt the operation. Do not continue reading that content after cancellation. The owning stream wrapper forwards Span/Memory operations directly on netstandard2.1 and .NET targets; netstandard2.0 uses inherited compatibility paths.

## Dependency injection and client reuse

DI is optional. ASP.NET Core applications can use the framework's named clients. Other applications need to reference and register `Microsoft.Extensions.Http` themselves.

A complete `Program.cs` example for a modern ASP.NET Core application:

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HttpClient.Extension;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("remote", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.Accept("application/json");
});

var app = builder.Build();
app.MapGet("/proxy/users/{id:int}", async Task<IResult> (
    int id, IHttpClientFactory factory, CancellationToken cancellationToken) =>
{
    using var client = factory.CreateClient("remote");
    var response = await client.GetAsync<string>($"users/{id}", cancellationToken);
    if (!response.Succeed)
        return Results.Problem(detail: response.Raw, statusCode: (int)response.StatusCode);
    return Results.Text(response.Result, "application/json");
});
app.Run();
```

Adjust the successful content type and error mapping for your API. Do not expose sensitive upstream error details directly to end users. The factory manages underlying handlers and connection pools, so a client created for an operation can be disposed afterward. This library does not dispose the caller's `HttpClient`.

## Custom requests and content reading

Use native `HttpRequestMessage` and `SendAsync` for PATCH, request-specific authentication, custom headers, or features without a convenience overload. `HttpResponseContentReader.ReadAsync<T>` can read and parse the content independently:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Patch, "users/1");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "example-user-token");
request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));
request.Content = new StringContent(
    "{\"name\":\"张三\"}", System.Text.Encoding.UTF8, "application/json");

using var response = await client.SendAsync(request, cancellationToken);
var (result, raw) = await response.Content.ReadAsync<UserDto>(
    response.IsSuccessStatusCode, cancellationToken);

if (response.IsSuccessStatusCode)
    Console.WriteLine(result?.Name);
else
    Console.WriteLine(raw);
```

The reader returns a `(TResult, string)` tuple containing the parsed result and failure text. It does not create `ResponseResult<T>` or take ownership of the response or content. The caller supplies `success`, normally from `response.IsSuccessStatusCode`. The older overload without a token is also available.

If you read a `Stream` directly through this reader, coordinate the stream, content, and response lifetimes yourself. Unlike request extensions, it does not add the response-owning wrapper. Do not dispose the response while the returned stream is still needed.

## Upgrading from earlier versions

2.0.0 retains the main request calling patterns and adds cancellation tokens and `ContentHeaders`. Review these changes when upgrading:

| Change | Action |
| --- | --- |
| Resilience project removed from the repository | This version does not provide that project's request builders, DI registration, or retry/circuit-breaker configuration; handle those requirements in your own HttpClient pipeline |
| `ParameterBuilder` is now internal | External code can no longer reference, instantiate, or inherit from it |
| Object/dictionary forms automatically URL-encode | Remove pre-encoding for those overloads; use the string overload for an already encoded body |
| Object forms filter properties and use invariant number/date formatting | Do not rely on static properties, indexers, or localized number/date text; explicitly format special values as strings |
| Top-level response scalars use invariant culture | Check decimal separators, grouping, and JSON quotes; select suitable nullable types for nullable or quoted scalar values |
| Text uses the declared response charset | Ensure server declarations match the actual encoding |
| Successful stream results retain their owning response | Dispose every stream result with `using` or `Dispose` |
| Byte downloads honor client timeout and buffer limits | Use file/stream downloads for large bodies and configure limits as needed |
| File downloads replace the destination after completion | Use the completion callback to observe a committed file; do not read the final destination as a live download buffer |

## Local examples and verification

The repository example is a .NET 10 ASP.NET Core application at `http://localhost:5000` by default:

```sh
dotnet run --project Examples
```

| Route | Demonstration |
| --- | --- |
| `GET /test/req` | Typed JSON request |
| `GET /test/req2` | Form submission with special characters |
| `GET /test/req3` | HTTP failure and `Raw` |
| `GET /test/payload` | Local JSON response |
| `GET /test/error_request` | Local HTTP 400 response |
| `POST /test/req-test` | Receive the `name` field and uploaded files; return the file count |

If you change the listening address, update the named client's `BaseAddress` in `Examples/Startup.cs` too.

Build all targets and run the native .NET 8/.NET 10 tests from the repository root:

```sh
dotnet build HttpClient.Extension.sln -c Release -p:GeneratePackageOnBuild=false
dotnet test tests/HttpClient.Extension.Tests/HttpClient.Extension.Tests.csproj -c Release -p:GeneratePackageOnBuild=false
```

Test each netstandard compatibility assembly on both .NET 8 and .NET 10 hosts:

```sh
dotnet test tests/HttpClient.Extension.Tests/HttpClient.Extension.Tests.csproj -c Release -p:GeneratePackageOnBuild=false -p:LibraryTargetFramework=netstandard2.0
dotnet test tests/HttpClient.Extension.Tests/HttpClient.Extension.Tests.csproj -c Release -p:GeneratePackageOnBuild=false -p:LibraryTargetFramework=netstandard2.1
```

Tests verify the loaded library's target framework to prevent accidentally repeating native assembly tests. CI is configured to build all targets on Windows/Linux and run all three assembly variants.

Build a Release package from current source:

```sh
dotnet pack src/HttpClient.Extension/HttpClient.Extension.csproj -c Release -p:GeneratePackageOnBuild=false -o artifacts/package
```

Output goes to `artifacts/package`. The package includes English `README.md` and Chinese `README.zh-CN.md`; NuGet displays the English readme by default. Verify the version, package contents, and CI results before publishing, and use the Release package from the current build.
