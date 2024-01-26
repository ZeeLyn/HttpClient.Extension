using System;
using System.Net.Http;

namespace HttpClient.Extension.Resilience
{
    internal class HttpResponsePolicyResult : IDisposable
    {
        internal Exception Exception { get; set; }

        internal HttpResponseMessage ResponseMessage { get; set; }

        public void Dispose()
        {
            ResponseMessage?.Dispose();
        }
    }
}