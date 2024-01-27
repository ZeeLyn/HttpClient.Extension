# HttpClient.Extension

Extension method to httpclient

# Packages & Status

| Packages             | NuGet                                                                                                                       |
| -------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| HttpClient.Extension | [![NuGet package](https://buildstats.info/nuget/HttpClient.Extension)](https://www.nuget.org/packages/HttpClient.Extension) |
|HttpClient.Extension.Resilience | [![NuGet package](https://buildstats.info/nuget/HttpClient.Extension.Resilience)](https://www.nuget.org/packages/HttpClient.Extension.Resilience) |
# Use extension

```csharp
public class TestController : ControllerBase
{
    private IHttpClientFactory HttpClientFactory { get; }

    public TestController(IHttpClientFactory clientFactory)
    {
        HttpClientFactory = clientFactory;
    }

    [HttpGet("req")]
    public async Task<IActionResult> RequestAPI()
    {
        var result = await HttpClientFactory.CreateClient()
            .AddHeader("headerKey1", "headerValue1", "headerKey2", "headerValue2")
            .Authorization("bearer", "this is bearer token")
            .UserAgent("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.95 Safari/537.11")
            .Timeout(TimeSpan.FromSeconds(10))
            .PostMultipartAsync<string>("http://localhost:57395/test/req-test", new { name = "this is my name" }, "d://2.png");
        return Ok(result);
    }
}
```

# Use Resilience

```csharp

public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddHttpClient("google", client => { client.BaseAddress = new Uri("https://www.google.com"); });
            services.AddHttpClientResilience(options =>
            {
                options.ExceptionHandle = (sc, url, ex) =>
                {
                    Console.WriteLine(" 请求{0}出现错误：{1}", url, ex.Message);
                    return true;
                };
                options.Retry = 2;
                options.WaitAndRetrySleepDurations = null;
                options.OnRetry = (sc, msg, ts, retryCount, context) => { Console.WriteLine("执行第{0}次重试", retryCount); };
                options.OnFallbackAsync = async (sc, msg, context) =>
                {
                    await Task.CompletedTask;
                    Console.WriteLine("执行降级处理");
                };
                options.FallbackHandleAsync = async (sc, ex, context) =>
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = 0,
                        Content = new StringContent("自定义降级消息：" + ex?.Message)
                    };
                };
            });
        }

public class TestController : ControllerBase
{
    private IHttpRequest HttpRequest { get; }

    public TestController(IHttpRequest httpRequest)
    {
        HttpRequest = httpRequest;
    }

    [HttpGet("req")]
    public async Task<IActionResult> RequestAPI()
    {
        var result = await HttpRequest.Create("google")
                .ExceptionHandle((sc, url, ex) => true)
                .FallbackHandleAsync(async (sc, ex, context) => await Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = 0,
                    Content = new StringContent("方法里自定义降级消息：" + ex?.Message)
                }))
                .Retry(3)
                .Timeout(TimeSpan.FromSeconds(10))
                .GetAsync<string>(
                    "/search?q=翻译&sca_esv=601619779&sxsrf=ACQVn0_SQUKgckqys8GIYJxvaqQckHrQxg%3A1706240783592&source=hp&ei=DyuzZfqRIr-v0-kP1uow&iflsig=ANes7DEAAAAAZbM5H_EwIREkLHg-Pd86kRoLVHcfFFlZ&ved=0ahUKEwi61biekvqDAxW_1zQHHVY1DAAQ4dUDCA0&uact=5&oq=翻译&gs_lp=Egdnd3Mtd2l6Igbnv7vor5EyChAjGIAEGIoFGCcyCxAAGIAEGLEDGIMBMgsQABiABBixAxiDATIIEAAYgAQYsQMyBRAAGIAEMgsQABiABBixAxiDATIFEAAYgAQyBRAAGIAEMgUQABiABDIFEAAYgARIqBFQzghY9A5wAXgAkAEAmAGjAaAB8waqAQMwLja4AQPIAQD4AQGoAgrCAgcQIxjqAhgnwgIREC4YgAQYsQMYgwEYxwEY0QPCAgUQLhiABMICCxAuGIAEGLEDGIMBwgIIEC4YsQMYgATCAg4QLhiABBiKBRixAxiDAcICDhAAGIAEGIoFGLEDGIMB&sclient=gws-wiz");

        return Ok(new
        {
            result.Succeed,
            result.Result,
            result.Headers,
            result.StatusCode,
            result.ErrorMessage,
            Exception = result.Exception?.Message
        });
    }
}
```
