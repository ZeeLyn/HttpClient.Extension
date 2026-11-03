# HttpClient.Extension

[English](README.md) | 简体中文

为 `System.Net.Http.HttpClient` 提供类型化响应、JSON、表单、文件上传和下载扩展。直接使用已有的 `HttpClient` 或 `IHttpClientFactory`，无需注册本库专用服务。

本说明对应 **2.0.0**。本库不提供重试、熔断等 Resilience 功能。

## 目录

- [安装与支持框架](#安装与支持框架)
- [快速开始](#快速开始)
- [发送请求](#发送请求)
- [响应与失败处理](#响应与失败处理)
- [响应类型与解析规则](#响应类型与解析规则)
- [请求头](#请求头)
- [文件上传](#文件上传)
- [下载与资源释放](#下载与资源释放)
- [取消与超时](#取消与超时)
- [依赖注入与客户端复用](#依赖注入与客户端复用)
- [自定义请求与内容读取](#自定义请求与内容读取)
- [从旧版本升级](#从旧版本升级)
- [本地示例与验证](#本地示例与验证)

## 安装与支持框架

版本发布到 NuGet 后安装：

```sh
dotnet add package HttpClient.Extension --version 2.0.0
```

如果使用尚未发布的本地包，先添加引用，再从本地目录和 NuGet 官方源还原依赖。以下命令在应用项目目录执行，请将本地源替换为实际路径：

```sh
dotnet add package HttpClient.Extension --version 2.0.0 --no-restore
dotnet restore --source E:/GitHub/HttpClient.Extension/artifacts/package --source https://api.nuget.org/v3/index.json
```

包内包含以下目标框架的程序集：

| 类别 | 目标框架 |
| --- | --- |
| .NET Standard | `netstandard2.0`、`netstandard2.1` |
| .NET | `net5.0`、`net6.0`、`net7.0`、`net8.0`、`net9.0`、`net10.0` |

JSON 序列化和反序列化使用 `Newtonsoft.Json 13.0.4`。本库没有 ASP.NET Core 或依赖注入框架的运行时依赖。本文示例使用 .NET 8 及以上宿主和现代 C# 语法；旧项目可引用适合的库程序集，但需要调整顶级语句、`using var` 等示例语法，并选择其运行时支持的文件 I/O API。

## 快速开始

下面是一个完整的控制台调用示例。示例域名和路径需要替换为实际接口：

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

后续片段默认已引入以下命名空间，已有可复用的 `client` 和 `CancellationToken cancellationToken`，并沿用上面的 `UserDto`：

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

`BaseAddress` 建议以 `/` 结尾，接口相对路径不以 `/` 开头。例如上述地址与 `users/1` 组合为 `https://api.example.com/v1/users/1`；`/users/1` 则从域名根路径开始。没有设置 `BaseAddress` 时应传入绝对 URL。

实际应用应复用客户端，或使用后文的 `IHttpClientFactory`，避免为每个请求手动创建新的连接池。

## 发送请求

### 方法选择

下表省略了泛型结果类型、取消令牌和可选的 `contentType`。除文件下载回调方法外，请求方法返回 `Task<ResponseResult<T>>`；`HeadAsync` 返回 `Task<ResponseResult<HttpContentHeaders>>`。

| 方法 | 请求体 | 默认 Content-Type |
| --- | --- | --- |
| `GetAsync<T>(url)` | 无 | 无 |
| `PostAsync<T>(url, string)` | 原样发送字符串 | `application/x-www-form-urlencoded` |
| `PostAsync<T>(url, HttpContent)` | 使用传入内容 | 由内容决定 |
| `PostBodyAsync<T>(url, object)` | 对象序列化为 JSON | `application/json` |
| `PostBodyAsync<T>(url, string)` | 已序列化的 JSON 字符串，原样发送 | `application/json` |
| `PostFormAsync<T>(url, object/IDictionary<string, string>)` | 表单字段，自动 URL 编码 | `application/x-www-form-urlencoded` |
| `PostFormAsync<T>(url, string)` | 已编码的表单字符串，原样发送 | `application/x-www-form-urlencoded` |
| `PostMultipartAsync<T>(...)` | 文件和可选的表单字段 | `multipart/form-data`，自动生成 boundary |
| `PutAsync<T>(url, object)` | 对象序列化为 JSON | `application/json` |
| `PutAsync<T>(url, string)` | 原样发送字符串 | `application/json` |
| `PutAsync<T>(url, HttpContent)` | 使用传入内容 | 由内容决定 |
| `DeleteAsync<T>(url)` | 无 | 无 |
| `HeadAsync(url)` | 无 | 无 |

**泛型 `T` 表示响应类型，不是请求体类型。** 例如发送一个匿名对象，可以用 `PostBodyAsync<UserDto>` 接收服务端返回的用户信息。

`client.GetAsync("url")` 调用的是 .NET 原生方法，返回 `HttpResponseMessage`；只有 `client.GetAsync<T>("url")` 返回本库的 `ResponseResult<T>`。其他与原生方法同名的扩展也应注意这一点。

### GET、查询参数与 DELETE

```csharp
var user = await client.GetAsync<UserDto>("users/1", cancellationToken);
var text = await client.GetAsync<string>("status", cancellationToken);

var keyword = Uri.EscapeDataString("a&b 中文");
var users = await client.GetAsync<List<UserDto>>(
    $"users?keyword={keyword}&page=1", cancellationToken);

var deleted = await client.DeleteAsync<string>("users/1", cancellationToken);
```

URL 查询参数由调用方构造，本库不会把任意对象自动拼入 GET URL。只对查询参数的值进行编码，避免把整个 URL 编码。

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

对象重载使用 `JsonConvert.SerializeObject`；字符串重载认为输入已经是 JSON，不会再次序列化。若接口需要 JSON 字符串值本身，应传入包含 JSON 引号的正文，例如 `"\"hello\""`。

普通 `PostAsync<T>` 的字符串重载不会自动识别 JSON，默认内容类型是表单。发送 JSON 时优先使用 `PostBodyAsync<T>`，或显式指定 `contentType: "application/json"`。

### PUT

```csharp
var updated = await client.PutAsync<UserDto>(
    "users/1", new { name = "李四" }, cancellationToken);

var fromJson = await client.PutAsync<UserDto>(
    "users/1", "{\"name\":\"李四\"}", cancellationToken);

var text = await client.PutAsync<string>(
    "notes/1", "普通文本", cancellationToken, contentType: "text/plain");
```

### 表单

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

对象和字典重载的行为：

- 自动编码字段名和字段值，特殊字符、中文、`+`、`&` 和 `%` 不需要提前编码。
- 仅提取对象的可公开读取的实例属性；忽略字段、静态属性、索引器和非公开 getter。
- 属性 getter 会被执行；getter 抛出的异常会使请求失败。
- 数字和日期按 `InvariantCulture` 转为文本，`1.5m` 不会随系统区域变为 `1,5`；`null` 值转为空字符串。
- 字符串值保持原样后再编码。嵌套对象、集合不会递归展开，也不会自动序列化为 JSON；请自行转换为接口需要的字符串。
- `IDictionary<string, string>` 即使保存在 `object` 变量中，也按字典字段处理。

字符串重载完全按原文发送，适用于已经编码的正文，例如需要重复字段名的 `tag=one&tag=two`。修改 `contentType` 只改变请求头，不会改变上述编码规则。

### 自定义 HttpContent

```csharp
using var content = new StringContent("hello", System.Text.Encoding.UTF8, "text/plain");
var posted = await client.PostAsync<string>("echo", content, cancellationToken);

using var putContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
putContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
var updated = await client.PutAsync<string>("files/1", putContent, cancellationToken);
```

直接传入 `HttpContent` 时，其释放由调用方负责。使用 `StreamContent` 时，释放内容也会释放内部流。字符串、对象、表单和内置 multipart 重载创建的内容由本库管理。

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

`HeadAsync` 不接收泛型参数。其 `Result` 与 `ContentHeaders` 是同一个内容头集合，HTTP 失败时也保留该集合。HEAD 不读取响应正文，`Raw` 没有正文数据，当前为 `null`。

## 响应与失败处理

### ResponseResult<T>

| 属性 | 类型 | 含义 |
| --- | --- | --- |
| `Succeed` | `bool` | HTTP 状态码是否在 200–299 范围内 |
| `StatusCode` | `HttpStatusCode` | 实际 HTTP 状态码 |
| `Headers` | `HttpResponseHeaders` | 通用响应头，例如 `ETag`、自定义请求 ID |
| `ContentHeaders` | `HttpContentHeaders` | 内容头，例如类型、长度、文件名和最后修改时间 |
| `Result` | `T` | 成功时解析的结果；HTTP 失败时为 `default(T)`，HEAD 例外见上文 |
| `Raw` | `string` | HTTP 失败时按响应字符集解码的完整正文；成功时为空字符串，HEAD 例外见上文 |

`Succeed` 只判断 HTTP 状态码。服务端返回 HTTP 200 和 `{"success":false}` 时，仍是 `Succeed = true`，业务成功与否需要检查 `Result`。

HTTP 204 等没有正文的成功响应也会进入解析流程：`string` 通常得到空字符串、字节数组为空、内置标量得到默认值，DTO/JSON 类型遵循 Newtonsoft.Json 对空内容的解析行为。对于不保证返回正文的接口，可以选择 `string`，或使用可空结果类型。

### HTTP 失败与 Raw

```csharp
var response = await client.GetAsync<UserDto>("users/404", cancellationToken);
if (!response.Succeed)
{
    Console.WriteLine((int)response.StatusCode);
    Console.WriteLine(response.Raw);
    Console.WriteLine(response.ContentHeaders.ContentType);
}
```

非 2xx 响应不会因 HTTP 状态码本身抛异常，也不会强行按 `T` 反序列化错误正文。只要正文成功读取，文本、DTO、标量、`byte[]`、`Stream` 等响应类型都会通过 `Raw` 返回错误内容。错误 HTML 页面也可以用 `GetAsync<UserDto>` 接收并检查 `Raw`，无需先满足 `UserDto` 的 JSON 格式。

```csharp
var response = await client.DownloadWithBytesAsync("files/missing", cancellationToken);
if (!response.Succeed)
{
    // 即使成功结果类型是 byte[]，错误正文仍以文本保存在 Raw。
    Console.WriteLine(response.Raw);
    Console.WriteLine(response.Result is null);
}
```

需要注意：

- `Raw` 只保存 HTTP 失败响应的正文，不保存成功响应的原文；需要成功原文时使用 `GetAsync<string>`，再自行反序列化。
- `Raw` 是解码后的字符串，不保证保留二进制错误响应的原始字节。需要原始错误字节时，使用原生 `SendAsync` 自行读取。
- `default(T)` 对引用类型和可空类型是 `null`，对 `int`、`bool` 等值类型则是 `0`、`false` 等默认值，不能据此判断成功。
- 网络故障、正文读取中断、缓冲限制、取消和解析错误仍可能抛异常，此时不会返回一个包含错误信息的 `ResponseResult<T>`。
- 文件下载回调方法 `DownloadAsync` 不返回 `ResponseResult<T>`，其 HTTP 失败通过 `onError` 报告，见下载章节。

### 响应头

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

服务端没有发送的可选头可能为 `null`。内容头不在 `Headers` 中，应通过 `ContentHeaders` 读取。非流响应读取后会自动释放 HTTP 响应，但上述头集合仍可读取；流结果释放后也可读取已取得的头集合。

### 异常处理

```csharp
try
{
    var response = await client.GetAsync<UserDto>("users/1", cancellationToken);
    if (!response.Succeed)
        Console.WriteLine($"HTTP {(int)response.StatusCode}: {response.Raw}");
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    Console.WriteLine("调用方取消了请求");
}
catch (OperationCanceledException)
{
    Console.WriteLine("请求超时或被取消");
}
catch (HttpRequestException exception)
{
    Console.WriteLine($"网络、协议或响应缓冲错误：{exception.Message}");
}
catch (Newtonsoft.Json.JsonException exception)
{
    Console.WriteLine($"响应 JSON 无法解析：{exception.Message}");
}
```

标量格式错误还可能抛出 `FormatException`、`OverflowException`；文件操作可能抛出 I/O 异常。根据业务需要处理这些异常。本库不会自动重试请求。

## 响应类型与解析规则

响应类型由泛型 `T` 决定，不会根据 `Content-Type` 自动选择 JSON 或标量解析器。

| 类型 | 成功响应的处理方式 |
| --- | --- |
| `string` | 解码为文本，不去掉 JSON 引号，也不把文本 `null` 转为空值 |
| `byte[]` | 返回原始字节，不执行字符集解码 |
| `Stream` | 返回流，调用方必须释放 |
| 整数 | 支持 `byte`、`sbyte`、`short`、`ushort`、`int`、`uint`、`long`、`ulong`，按固定区域格式解析 |
| 小数 | `float`、`double`、`decimal`，使用 `.` 小数点，支持科学计数法，不接受分组分隔符 |
| `bool` | 使用 `bool.Parse`，接受 `true`/`false`，不把 `0`/`1` 自动转为布尔值 |
| `char` | 取文本第一个字符；空正文返回默认字符 |
| 日期与其他标量 | 支持 `DateTime`、`DateTimeOffset`、`TimeSpan`、`Guid` |
| 上述标量的可空类型 | 支持普通文本、JSON 字符串、空正文和 JSON `null` |
| DTO、集合、枚举等其他类型 | 使用 `JsonConvert.DeserializeObject<T>` |

```csharp
var count = await client.GetAsync<int>("metrics/count", cancellationToken);
var price = await client.GetAsync<decimal>("metrics/price", cancellationToken);
var optionalCount = await client.GetAsync<int?>("metrics/optional-count", cancellationToken);
var users = await client.GetAsync<List<UserDto>>("users", cancellationToken);
```

典型标量输入：

| 响应正文 | `T` | 结果 |
| --- | --- | --- |
| `42` | `int` | `42` |
| `"42"` | `int?` | `42` |
| 空正文、空白、`null`、`""` | `int?` | `null` |
| 空正文或空白 | `int` | `0` |
| `1.5` 或 `1e-3` | `decimal` | `1.5m` 或 `0.001m` |
| `1,5` 或 `1,234.56` | `decimal` | 抛出格式异常 |
| `42.0` 或 `"42"` | `int` | 抛出格式异常 |
| `"hello"` | `string` | 保留两侧 JSON 引号 |
| `hello` | `char` | `'h'` |

可空内置标量会解开 JSON 字符串引号；非可空内置标量按普通文本解析，不应给非可空数字或日期额外加 JSON 引号。DTO 内部的属性由 Newtonsoft.Json 解析，不受此顶层标量规则限制。

文本、JSON 和 `Raw` 使用响应 `Content-Type` 声明的 `charset`；未声明时遵循 .NET `HttpContent.ReadAsStringAsync` 的 BOM/UTF-8 规则。无效或运行环境不支持的编码可能抛异常。本库不额外注册传统代码页。

## 请求头

以下方法修改 `DefaultRequestHeaders`，并返回同一个客户端，适合在客户端配置阶段链式设置：

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

| 方法 | 行为 |
| --- | --- |
| `AddHeader(name, value)` | 同名头先移除再设置 |
| `AddHeader(IDictionary<string, string>)` | 逐项覆盖同名头；空字典或 `null` 不修改 |
| `AddHeader(params string[])` | 按名称、值成对传入，数量必须为偶数 |
| `Authorization(scheme, parameter)` | 设置或覆盖认证头 |
| `UserAgent(value)` | 解析并追加 User-Agent 产品信息 |
| `Accept(value)` | 解析并追加媒体类型，支持逗号列表和 `q` 权重 |
| `Range(from, to)` | 设置字节范围；`Range(1024, null)` 从该偏移读取，`Range(null, 1024)` 请求末尾 1024 字节 |
| `Referrer(uri)` | 设置 Referer |

非法头值会由 .NET 校验并抛异常。`Content-Type` 应设置在 `HttpContent.Headers`，或通过请求方法的 `contentType` 参数指定。

共享客户端不要在并发请求期间修改默认头；默认认证头也不适合在不同用户之间反复切换。单次认证、自定义方法等场景使用独立 `HttpRequestMessage`，见后文。

`Range` 仅设置请求头，服务端是否返回范围内容取决于接口支持情况；本库的文件下载不会据此追加本地文件，不能把它当作自动断点续传。

## 文件上传

`PostMultipartAsync<T>` 支持文件路径、字节数组和流三种输入，每种输入均可搭配对象或 `IDictionary<string, string>` 表单字段。取消令牌放在文件列表之前，字段放在取消令牌之前。

### 文件路径

```csharp
var response = await client.PostMultipartAsync<string>(
    "uploads", cancellationToken, "avatar.png", "report.pdf");

var withFields = await client.PostMultipartAsync<string>(
    "uploads", new { userId = 1, note = "头像" }, cancellationToken, "avatar.png");
```

按路径上传直接流式读取文件，不会先把整个文件载入内存。默认文件字段名为 `file`，多个路径发送为多个同名文件字段，文件名取路径的最后一段。

### 字节数组

```csharp
var bytes = await File.ReadAllBytesAsync("avatar.png", cancellationToken);
var avatar = new MultipartFormFileBytesData("avatar.png", bytes, name: "avatar");

var response = await client.PostMultipartAsync<string>(
    "uploads", cancellationToken, avatar);

var fields = new Dictionary<string, string> { ["userId"] = "1" };
var withFields = await client.PostMultipartAsync<string>(
    "uploads", fields, cancellationToken, avatar);
```

`MultipartFormFileBytesData` 的构造参数依次是 `fileName`、`bytes`、可选的 `name`，默认 `name = "file"`。也可使用无参构造并设置 `FileName`、`FileBytes` 和 `Name`。字节数组输入会占用整个文件大小的内存，适合已有内存数据或较小文件。

### 流

```csharp
using var input = File.OpenRead("avatar.png");
var avatar = new MultipartFormFileStreamData("avatar.png", input, name: "avatar");

var response = await client.PostMultipartAsync<string>(
    "uploads", new { userId = 1 }, cancellationToken, avatar);
```

`MultipartFormFileStreamData` 的构造参数依次是 `fileName`、`fileStream`、可选的 `name`，也可设置 `FileName`、`FileStream` 和 `Name` 属性。发送从流的当前位置开始，不会自动回到开头。

**已加入 multipart 容器的流会随容器释放，不能在请求后继续复用。** 调用方仍应使用 `using` 管理自己创建的流，以覆盖发送前已取消、前面的文件组装失败等尚未接管该流的情况。

### 编码与内容类型

普通 multipart 字段以 UTF-8 文本发送，对象属性的筛选、文本转换和空值规则与对象表单相同，但字段值不再做 URL 编码。

文件 MIME 类型根据 `FileName` 的扩展名从 `MediaType.MediaTypes` 获取，扩展名匹配不区分大小写；未识别时使用 `application/octet-stream`。可以通过 `MediaType.MediaTypes.TryGetValue(".png", out var mimeType)` 查询映射。

需要自行指定某个文件的 MIME 类型或其他内容头时，使用原生 multipart 容器，然后交给 `PostAsync<T>`：

```csharp
using var form = new MultipartFormDataContent();
form.Add(new StringContent("1"), "userId");
var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
file.Headers.ContentType = new MediaTypeHeaderValue("application/x-example");
form.Add(file, "attachment", "data.bin");

var response = await client.PostAsync<string>("uploads", form, cancellationToken);
```

保持容器自动生成的 boundary，不要只将整个请求的 `Content-Type` 改成缺少 boundary 的 `multipart/form-data`。容器释放时会释放已加入的内容。

## 下载与资源释放

### 方法选择

| 方法 | 成功正文 | 适用场景 |
| --- | --- | --- |
| `DownloadWithBytesAsync(url, token)` | 整个响应缓冲到内存并返回 `byte[]` | 小文件、需要直接操作字节 |
| `DownloadWithStreamAsync(url, token)` | 先取得响应头，再返回响应流 | 大文件、自定义复制和处理 |
| `DownloadAsync(url, saveFileName, ..., token)` | 流式保存到临时文件，完成后替换目标 | 直接保存文件、进度回调 |
| `GetAsync<Stream>(url, token)` 等普通类型化方法 | 保持原生请求的默认响应缓冲，然后返回流 | 需要流接口但允许缓冲的普通请求 |

`ResponseResult<T>` 本身不是 `IDisposable`。本库会释放已读取的非流 HTTP 响应；对于成功的流结果，由调用方释放 `Result`，同时释放其所属的 HTTP 响应。

### 字节数组下载

```csharp
var response = await client.DownloadWithBytesAsync("files/logo.png", cancellationToken);
if (response.Succeed)
    await File.WriteAllBytesAsync("logo.png", response.Result, cancellationToken);
else
    Console.WriteLine(response.Raw);
```

该方法遵守 `HttpClient.MaxResponseContentBufferSize`，超出上限会抛异常。大文件应选择流或直接保存，避免同时占用完整响应的内存。

### 流下载

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

必须释放返回流，包括未完整读取的情况。调用 `DownloadWithStreamAsync` 时传入令牌，只负责发送请求和取得返回值；流返回后，仍要将令牌传给每次 `ReadAsync`、`CopyToAsync` 等异步读取操作。

上述自行复制示例直接创建目标文件，失败时可能留下部分文件。需要保留已有目标文件时使用下面的 `DownloadAsync`。即使成功结果选择流，HTTP 失败正文仍会完整读成 `Raw`，错误正文不是流式返回的。

### 保存文件与进度

```csharp
using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
deadline.CancelAfter(TimeSpan.FromMinutes(5));

await client.DownloadAsync(
    "files/video.mp4",
    "downloads/video.mp4",
    onProgressChanged: (total, downloaded, percent) =>
        Console.WriteLine($"{downloaded}/{total} bytes, {percent}%"),
    onCompleted: downloaded => Console.WriteLine($"已保存 {downloaded} bytes"),
    onError: status => Console.WriteLine($"HTTP 下载失败：{status}"),
    cancellationToken: deadline.Token);
```

| 参数或回调 | 说明 |
| --- | --- |
| `saveFileName` | 支持相对或绝对路径、单独文件名；需要时创建目标目录 |
| `onProgressChanged` | `Action<long, long, decimal>`，依次为总字节、已下载字节、0–100 的百分比 |
| `onCompleted` | `Action<long>`，接收实际保存的字节数；空文件为 0 |
| `onError` | `Action<string>`，仅用于非成功 HTTP 状态；值为状态名称，例如 `NotFound` |
| `cancellationToken` | 控制请求、正文读取、文件写入及提交前的取消检查 |

下载行为：

- 在目标目录创建 `.httpclient-*.tmp` 临时文件，完整下载并校验声明长度后才替换或创建目标文件。
- 提交前的网络、读写、进度回调失败或取消会清理临时文件，并保留已有目标文件；临时文件清理本身若发生 I/O 异常，也会向调用方传播。
- 没有 `Content-Length` 时，进度中的 `total = 0`、`percent = 0`；完成回调仍返回实际下载长度。空文件可能没有进度回调。
- `onCompleted` 在文件已保存、流已关闭后执行；该回调抛异常不会回滚已保存文件。
- 回调是同步调用，不会自动切换 UI 线程。取消、I/O 和回调异常向外抛出，不会改走 `onError`。
- HTTP 失败时调用 `onError` 后返回，不提供错误正文；不传 `onError` 时同样直接返回。需要 `Raw`、状态码和响应头时选择字节或流下载方法。
- 不提供自动重试、断点续传或已有文件内容的追加。

## 取消与超时

### 取消令牌的位置

原有不带令牌的重载保留。新重载把令牌放在必需参数之后、可选内容类型或 `params` 文件列表之前：

```csharp
await client.GetAsync<string>("status", cancellationToken);
await client.PostFormAsync<string>("forms", new { name = "张三" }, cancellationToken);
await client.PostBodyAsync<string>("users", new { name = "张三" }, cancellationToken);
await client.PostAsync<string>("echo", "hello", cancellationToken, "text/plain");
await client.PostMultipartAsync<string>("uploads", cancellationToken, "avatar.png");
await client.PostMultipartAsync<string>(
    "uploads", new { userId = 1 }, cancellationToken, "avatar.png");
```

三个下载方法的 `cancellationToken` 均为最后一个可选参数。已取消的令牌会在发送前使请求抛出 `OperationCanceledException`。对于已返回的流，取消先前的令牌不等于自动取消后续未传入令牌的读取。

### HttpClient.Timeout 的范围

```csharp
// 在客户端首次发送请求前配置；也可以设置原生 client.Timeout 属性。
client.Timeout(TimeSpan.FromSeconds(30));
client.MaxResponseContentBufferSize = 16 * 1024 * 1024;
```

| 请求方式 | HttpClient.Timeout 覆盖范围 | 缓冲限制 |
| --- | --- | --- |
| 普通类型化请求、`DownloadWithBytesAsync` | 发送请求到完整响应缓冲结束 | 遵守 `MaxResponseContentBufferSize` |
| `DownloadWithStreamAsync`、`DownloadAsync` | 发送请求到取得响应头；不覆盖后续完整正文读取 | 使用 `ResponseHeadersRead`，成功正文不先整体缓冲 |

长时间文件下载必须另传带截止时间的 `CancellationToken`；返回流还要将该令牌传到读取操作中。`HttpClient.Timeout` 不限制之后的同步 JSON 解析，令牌也不会强行中断正在执行的同步序列化或反序列化，只能在检查点观察取消。

在 netstandard 兼容实现中，取消内容读取可能通过释放 `HttpContent` 中断操作；取消后不要继续读取同一个响应内容。内置流包装在 netstandard2.1 和 .NET 目标上直接转发 Span/Memory 读写；netstandard2.0 使用框架继承的兼容路径。

## 依赖注入与客户端复用

本库不要求使用 DI。ASP.NET Core 应用可直接使用框架提供的命名客户端；其他应用需要自行引入并注册 `Microsoft.Extensions.Http`。

以下为现代 ASP.NET Core 项目的完整 `Program.cs` 示例：

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

按实际接口调整成功内容类型和错误映射；不要直接把敏感的上游错误详情暴露给终端用户。工厂管理底层处理程序和连接池，创建的客户端可在本次使用后释放。本库不会替调用方释放 `HttpClient`。

## 自定义请求与内容读取

需要 PATCH、请求级认证、自定义请求头或其他本库未封装的功能时，使用原生 `HttpRequestMessage` 和 `SendAsync`。`HttpResponseContentReader.ReadAsync<T>` 可单独用于读取和解析响应内容：

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

该读取方法返回 `(TResult, string)` 元组，分别是解析结果和失败原文；不创建 `ResponseResult<T>`，也不接管响应或内容的所有权。`success` 参数由调用方提供，通常传入 `response.IsSuccessStatusCode`。不带令牌的旧重载也可使用。

若直接用读取器获取 `Stream`，必须自行协调流、内容和响应的生命周期；它不会像请求扩展那样自动添加响应所有权包装。不要在返回流仍需使用时释放所属的响应。

## 从旧版本升级

2.0.0 保留主要请求扩展的原有调用方式，并增加取消令牌和 `ContentHeaders`。以下变化需要检查现有调用：

| 变化 | 升级时的处理 |
| --- | --- |
| 移除仓库中的 Resilience 项目 | 本版本不提供该项目的请求构建器、DI 注册或重试/熔断配置；按业务需要在自己的 HttpClient 管道中处理 |
| `ParameterBuilder` 改为内部实现 | 外部代码不能继续引用、实例化或继承该类 |
| 对象和字典表单自动 URL 编码 | 删除对这些重载字段的预编码；已编码正文改用字符串重载 |
| 对象表单筛选属性、数字和日期使用固定区域格式 | 不再依赖静态属性、索引器或本地化的数字/日期文本；特殊格式先显式转换为字符串 |
| 顶层响应标量使用固定区域格式 | 检查服务端的小数点、分组符和 JSON 引号；需要可空或带引号标量时选择合适的可空类型 |
| 文本按响应声明的 charset 解码 | 确保服务端声明与实际编码一致 |
| 成功的流结果保持响应存活 | 所有流结果都使用 `using` 或显式 `Dispose` |
| 字节下载遵守客户端超时和缓冲上限 | 大文件改用流或文件下载，按需配置超时和缓冲上限 |
| 文件下载先完成再替换目标 | 用完成回调确认文件已提交；不依赖下载中直接读取最终目标文件 |

## 本地示例与验证

仓库示例是 .NET 10 ASP.NET Core 应用，默认地址为 `http://localhost:5000`：

```sh
dotnet run --project Examples
```

| 路径 | 演示内容 |
| --- | --- |
| `GET /test/req` | 调用类型化 JSON 接口 |
| `GET /test/req2` | 提交包含特殊字符的表单 |
| `GET /test/req3` | HTTP 失败与 `Raw` |
| `GET /test/payload` | 本地 JSON 响应 |
| `GET /test/error_request` | 本地 HTTP 400 响应 |
| `POST /test/req-test` | 接收 `name` 表单字段和文件，返回文件数 |

改变监听地址后，也要同步调整 `Examples/Startup.cs` 中命名客户端的 `BaseAddress`。

在仓库根目录构建所有目标并运行原生 .NET 8/.NET 10 测试：

```sh
dotnet build HttpClient.Extension.sln -c Release -p:GeneratePackageOnBuild=false
dotnet test tests/HttpClient.Extension.Tests/HttpClient.Extension.Tests.csproj -c Release -p:GeneratePackageOnBuild=false
```

在 .NET 8 和 .NET 10 两个宿主上，分别测试 netstandard 兼容程序集：

```sh
dotnet test tests/HttpClient.Extension.Tests/HttpClient.Extension.Tests.csproj -c Release -p:GeneratePackageOnBuild=false -p:LibraryTargetFramework=netstandard2.0
dotnet test tests/HttpClient.Extension.Tests/HttpClient.Extension.Tests.csproj -c Release -p:GeneratePackageOnBuild=false -p:LibraryTargetFramework=netstandard2.1
```

测试会校验实际加载的库程序集目标框架，避免只重复测试原生程序集。CI 配置在 Windows/Linux 上构建全部目标，并运行上述三种程序集组合。

从当前源码构建 Release 包：

```sh
dotnet pack src/HttpClient.Extension/HttpClient.Extension.csproj -c Release -p:GeneratePackageOnBuild=false -o artifacts/package
```

输出目录为 `artifacts/package`。包内包含英文 `README.md` 和中文 `README.zh-CN.md`，NuGet 默认展示英文说明；发布前确认版本、包内容和 CI 结果，并使用本次构建的 Release 包。
