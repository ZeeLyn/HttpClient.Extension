using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;

namespace HttpClient.Extension.Resilience
{
    public partial class HttpRequestBuilder : IDisposable
    {
        public HttpRequestBuilder(System.Net.Http.HttpClient client, IServiceProvider serviceProvider,
            ILogger<HttpRequestBuilder> logger)
        {
            Client = client;
            ServiceProvider = serviceProvider;
            Logger = logger;
        }

        private ILogger Logger { get; }

        private System.Net.Http.HttpClient Client { get; }

        private IServiceProvider ServiceProvider { get; }


        internal Func<IServiceProvider, string, Exception, bool> _exceptionHandle { get; set; } = (_, _, _) => true;

        internal int _retry { get; set; }

        internal IEnumerable<TimeSpan> _waitAndRetrySleepDurations { get; set; }

        internal Action<IServiceProvider, DelegateResult<HttpResponseMessage>, TimeSpan, int, Context>? _onRetry
        {
            get;
            set;
        }

        internal Func<IServiceProvider, Exception, Context, Task<HttpResponseMessage>>? _fallbackHandleAsync
        {
            get;
            set;
        }

        internal Func<IServiceProvider, DelegateResult<HttpResponseMessage>, Context, Task>? _onFallbackAsync
        {
            get;
            set;
        }

        public void Dispose()
        {
            Logger.LogInformation("dispose builder");
            Client?.Dispose();
        }
    }
}