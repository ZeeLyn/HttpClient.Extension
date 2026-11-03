using System;
using System.Net.Http;
using System.Threading.Tasks;
using HttpClient.Extension;
using Microsoft.AspNetCore.Mvc;

namespace Examples.Controllers
{
    [ApiController]
    [Route("test")]
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
            using var client = HttpClientFactory.CreateClient("example")
                .Timeout(TimeSpan.FromSeconds(10));
            var result = await client.GetAsync<TestResponse>("test/payload");
            return Ok(result);
        }

        [HttpGet("req2")]
        public async Task<IActionResult> RequestAPI2()
        {
            using var client = HttpClientFactory.CreateClient("example");
            var result = await client.PostFormAsync<string>("test/req-test", new { name = "a&b+c 中文" });
            return Ok(result);
        }

        [HttpGet("req3")]
        public async Task<IActionResult> RequestAPI3()
        {
            using var client = HttpClientFactory.CreateClient("example");
            var result = await client.GetAsync<TestResponse>("test/error_request");
            return Ok(result);
        }

        [HttpGet("payload")]
        public IActionResult Payload() => Ok(new TestResponse { Id = 1, Name = "example" });

        [HttpGet("error_request")]
        public IActionResult ErrorRequest() => BadRequest("example error");

        [HttpPost("req-test")]
        public IActionResult Req([FromForm] string name) => Ok(new { name, files = Request.Form.Files.Count });
    }

    public class TestResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
