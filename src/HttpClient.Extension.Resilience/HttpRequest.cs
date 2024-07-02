using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;

namespace HttpClient.Extension.Resilience
{
    public sealed class HttpRequest(IHttpClientFactory factory, IServiceProvider serviceProvider)
        : IHttpRequest
    {
        private IHttpClientFactory Factory { get; } = factory;

        private HttpRequestResilienceOptions Options { get; } =
            serviceProvider.GetService<HttpRequestResilienceOptions>();

        private IServiceProvider ServiceProvider { get; } = serviceProvider;

        public IHttpRequestBuilder Create(string name = null)
        {
            var builder = new HttpRequestBuilder(
                string.IsNullOrWhiteSpace(name) ? Factory.CreateClient() : Factory.CreateClient(name),
                ServiceProvider,
                ServiceProvider.GetRequiredService<ILogger<HttpRequestBuilder>>()
            );

            if (Options is null) return builder;

            builder._exceptionHandle = Options.ExceptionHandle;
            builder._resultHandle = Options.ResultHandle;
            builder._retry = Options.Retry;
            builder._waitAndRetrySleepDurations = Options.WaitAndRetrySleepDurations;
            builder._onRetry = Options.OnRetry;
            builder._fallbackHandleAsync = Options.FallbackHandleAsync;
            builder._onFallbackAsync = Options.OnFallbackAsync;

            return builder;
        }
    }
}