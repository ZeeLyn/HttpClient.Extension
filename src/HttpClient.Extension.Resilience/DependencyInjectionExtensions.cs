using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Fallback;
using Polly.Retry;

namespace HttpClient.Extension.Resilience
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddHttpClientResilience(this IServiceCollection services,
            Action<HttpRequestResilienceOptions> configure = default)
        {
            if (configure is not null)
            {
                var options = new HttpRequestResilienceOptions();
                configure(options);
                services.AddSingleton(options);
            }

            services.AddHttpClient();
            services.AddSingleton<IHttpRequest, HttpRequest>();
            return services;
        }
    }
}