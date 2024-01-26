using System;
using System.Net;
using System.Net.Http.Headers;

namespace HttpClient.Extension.Resilience
{
    public sealed class ResponseResult<TResult>
    {
        public bool Succeed { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public HttpResponseHeaders Headers { get; set; }

        public TResult Result { get; set; }

        public string ErrorMessage { get; set; }

        public Exception Exception { get; set; }
    }
}