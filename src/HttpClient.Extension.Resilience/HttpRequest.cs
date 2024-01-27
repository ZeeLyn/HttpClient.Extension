using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;

namespace HttpClient.Extension.Resilience
{
    public sealed class HttpRequest : IHttpRequest
    {
        private IHttpClientFactory Factory { get; }

        private HttpRequestResilienceOptions Options { get; }

        private IServiceProvider ServiceProvider { get; }

        public HttpRequest(IHttpClientFactory factory, IServiceProvider serviceProvider)
        {
            Factory = factory;
            Options = serviceProvider.GetService<HttpRequestResilienceOptions>();
            ServiceProvider = serviceProvider;
        }

        public IHttpRequestBuilder Create(string name = null)
        {
            var builder = new HttpRequestBuilder(
                string.IsNullOrWhiteSpace(name) ? Factory.CreateClient() : Factory.CreateClient(name),
                ServiceProvider,
                ServiceProvider.GetRequiredService<ILogger<HttpRequestBuilder>>()
            );

            if (Options is null) return builder;

            builder._exceptionHandle = Options.ExceptionHandle;
            builder._retry = Options.WaitAndRetrySleepDurations is not null && Options.WaitAndRetrySleepDurations.Any()
                ? 0
                : Options.Retry;
            builder._waitAndRetrySleepDurations = Options.WaitAndRetrySleepDurations;
            builder._onRetry = Options.OnRetry;
            builder._fallbackHandleAsync = Options.FallbackHandleAsync;
            builder._onFallbackAsync = Options.OnFallbackAsync;

            return builder;
        }
    }
}