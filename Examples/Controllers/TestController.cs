using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HttpClient.Extension;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

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

        //[HttpGet("req")]
        //public async Task<IActionResult> RequestAPI()
        //{
        //    //var client = await HttpClientFactory.CreateClient()
        //    //    .AddHeader("c", "c-v", "d", "d-v")
        //    //    .Authorization("bearer", "token-asfasfsdf")
        //    //    .UserAgent(
        //    //        "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.95 Safari/537.11")
        //    //    .Timeout(TimeSpan.FromSeconds(10))
        //    //    .PostMultipartAsync<string>("http://localhost:57395/test/req-test", new { name = "this is my name" },
        //    //        "d://2.png");

        //    //await client.DownloadAsync(
        //    //    "https://lingque-oss-jssdk.oss-cn-beijing.aliyuncs.com/beiqi_drive/video/b698bfeca64f404ba8536d1dd65fc87f.MP4",
        //    //    "d://a.mp4", (c, e, d) =>
        //    //    {
        //    //        Console.WriteLine(d);
        //    //    }, null, err =>
        //    //      {
        //    //          Console.WriteLine(err);
        //    //      });

        //    var result = await HttpRequest.Create("google")
        //        //.ExceptionHandle((sc, url, ex) => true)
        //        .FallbackHandleAsync(async (sc, ex, context) => await Task.FromResult(new HttpResponseMessage
        //        {
        //            StatusCode = 0,
        //            Content = new StringContent("方法里自定义降级消息：" + ex?.Message)
        //        }))
        //        .Retry(3)
        //        .WaitAndRetry(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10))
        //        .Timeout(TimeSpan.FromSeconds(10))
        //        .GetAsync<string>(
        //            "/search?q=翻译&sca_esv=601619779&sxsrf=ACQVn0_SQUKgckqys8GIYJxvaqQckHrQxg%3A1706240783592&source=hp&ei=DyuzZfqRIr-v0-kP1uow&iflsig=ANes7DEAAAAAZbM5H_EwIREkLHg-Pd86kRoLVHcfFFlZ&ved=0ahUKEwi61biekvqDAxW_1zQHHVY1DAAQ4dUDCA0&uact=5&oq=翻译&gs_lp=Egdnd3Mtd2l6Igbnv7vor5EyChAjGIAEGIoFGCcyCxAAGIAEGLEDGIMBMgsQABiABBixAxiDATIIEAAYgAQYsQMyBRAAGIAEMgsQABiABBixAxiDATIFEAAYgAQyBRAAGIAEMgUQABiABDIFEAAYgARIqBFQzghY9A5wAXgAkAEAmAGjAaAB8waqAQMwLja4AQPIAQD4AQGoAgrCAgcQIxjqAhgnwgIREC4YgAQYsQMYgwEYxwEY0QPCAgUQLhiABMICCxAuGIAEGLEDGIMBwgIIEC4YsQMYgATCAg4QLhiABBiKBRixAxiDAcICDhAAGIAEGIoFGLEDGIMB&sclient=gws-wiz");

        //    return Ok(new
        //    {
        //        result.Succeed,
        //        result.Result,
        //        result.Headers,
        //        result.StatusCode,
        //        result.ErrorMessage,
        //        Exception = result.Exception?.Message
        //    });
        //}


        [HttpGet("req2")]
        public async Task<IActionResult> RequestAPI2()
        {
            var result = await HttpClientFactory.CreateClient("hanyunmmip")
                .Authorization("Basic", "YnVzaW5lc3M6WGNtZzEyMw==")
                .Timeout(TimeSpan.FromSeconds(10))
                .PostFormAsync<string>("/auth/oauth/token", new
                {
                    username = "tiaoshi1",
                    password = "asdf123Q",
                    scope = "server",
                    grant_type = "password"
                });
            return BadRequest(result);
        }

        //[HttpGet("req3")]
        //public async Task<IActionResult> RequestAPI3()
        //{
        //    var result = await HttpRequest.Create("local")
        //        .Timeout(TimeSpan.FromSeconds(10))
        //        .GetAsync<string>("/test/req2");
        //    return Ok(result);
        //}


        [Route("req-test"), Consumes("multipart/form-data")]
        public IActionResult Req([FromForm] string name)
        {
            //var c = new System.Net.Http.HttpConnectionResponseContent();
            var files = Request.Form.Files;
            return Ok(new { id = 1, files = files.Count });
        }
    }
}