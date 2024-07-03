#nullable enable
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace HttpClient.Extension
{
    public class HttpResilienceOption
    {
        public int RetryAttempts { get; set; } = 3;

        public Func<HttpResponseMessage, bool>? HandleResult { get; set; }

        public Func<Exception, bool>? Handle { get; set; }

        public Action<OnRetryArguments<HttpResponseMessage>>? OnRetry { get; set; }
    }
}