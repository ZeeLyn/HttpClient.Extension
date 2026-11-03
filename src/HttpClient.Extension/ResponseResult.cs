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

        /// <summary>
        /// 响应内容头，包括 Content-Type、Content-Length 和 Content-Disposition。
        /// </summary>
        public HttpContentHeaders ContentHeaders { get; set; }

        public TResult Result { get; set; }

        /// <summary>
        /// HTTP 非成功状态码时，保存按响应字符集解码的正文。
        /// </summary>
        public string Raw { get; set; }
    }
}
