using System;
using Microsoft.Extensions.DependencyInjection;

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