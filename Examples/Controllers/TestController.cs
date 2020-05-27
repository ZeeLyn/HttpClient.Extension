using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HttpClient.Extension;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
            var client = await HttpClientFactory.CreateClient()
                .AddHeader("c", "c-v", "d", "d-v")
                .Authorization("bearer", "token-asfasfsdf")
                .UserAgent("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.95 Safari/537.11")
                .Timeout(TimeSpan.FromSeconds(10))
                .PostMultipartAsync<string>("http://localhost:57395/test/req-test", new { name = "this is my name" }, "d://2.png");

            //await client.DownloadAsync(
            //    "https://lingque-oss-jssdk.oss-cn-beijing.aliyuncs.com/beiqi_drive/video/b698bfeca64f404ba8536d1dd65fc87f.MP4",
            //    "d://a.mp4", (c, e, d) =>
            //    {
            //        Console.WriteLine(d);
            //    }, null, err =>
            //      {
            //          Console.WriteLine(err);
            //      });
            return Ok();
        }

        [Route("req-test"), Consumes("multipart/form-data")]
        public IActionResult Req([FromForm] string name)
        {
            //var c = new System.Net.Http.HttpConnectionResponseContent();
            var files = Request.Form.Files;
            return Ok(new { id = 1 });
        }

    }
}
