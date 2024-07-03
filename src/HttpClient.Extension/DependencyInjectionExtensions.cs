using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Fallback;
using Polly.Retry;

namespace HttpClient.Extension
{
    public static class DependencyInjectionExtensions
    {
        public static IHttpClientBuilder AddResilience(this IHttpClientBuilder builder)
        {
            return builder.AddResilience(_ => { });
        }

        public static IHttpClientBuilder AddResilience(this IHttpClientBuilder builder,
            Action<HttpResilienceOption> configure)
        {
            var option = new HttpResilienceOption();
            configure(option);

            builder.AddResilienceHandler(builder.Name, (pipeLineBuilder, context) =>
            {
                if (option.RetryAttempts > 0)
                {
                    pipeLineBuilder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                    {
                        MaxRetryAttempts = option.RetryAttempts,
                        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                            .HandleResult(resp =>
                            {
                                Console.WriteLine("对结果检查");
                                //return _resultHandle?.Invoke(ServiceProvider, resp, this) ?? true;
                                return true;
                            })
                            .Handle<Exception>(ex =>
                            {
                                //requestUrl = Client?.BaseAddress + requestUrl;
                                //exception = ex;
                                //Logger.LogError(ex, "An exception occurred when requesting '{0}'", requestUrl);
                                //return _exceptionHandle?.Invoke(ServiceProvider, requestUrl, ex, this) ?? true;
                                return true;
                            }),
                        OnRetry = async args =>
                        {
                            Console.WriteLine("重试...");
                            await Task.CompletedTask;
                        }
                    });
                }

                pipeLineBuilder.AddFallback(new FallbackStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = async e => await PredicateResult.True(),
                    FallbackAction = async args =>
                    {
                        return Outcome.FromResult(new HttpResponseMessage
                        {
                            StatusCode = 0,
                            Content = new StringContent("这是降级消息", Encoding.UTF8)
                        });
                    }
                });
            });
            return builder;
        }
    }
}