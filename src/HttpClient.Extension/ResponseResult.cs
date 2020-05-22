using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace HttpClient.Extension
{
    public sealed class ResponseResult<TResult>
    {
        public bool Succeed { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public HttpResponseHeaders Headers { get; set; }

        public TResult Result { get; set; }
    }
}
