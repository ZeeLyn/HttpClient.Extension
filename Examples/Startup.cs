using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HttpClient.Extension.Resilience;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Examples
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
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

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}