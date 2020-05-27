# HttpClient.Extension

Extension method to httpclient

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
