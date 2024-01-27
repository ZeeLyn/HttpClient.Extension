using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace HttpClient.Extension.Resilience
{
    public partial class HttpRequestBuilder
    {
        public IHttpRequestBuilder Timeout(TimeSpan timeout)
        {
            Client.Timeout = timeout;
            return this;
        }

        public IHttpRequestBuilder AddHeader(string name, string value)
        {
            if (Client.DefaultRequestHeaders.Contains(name))
                Client.DefaultRequestHeaders.Remove(name);
            Client.DefaultRequestHeaders.Add(name, value);
            return this;
        }

        public IHttpRequestBuilder AddHeader(
            IDictionary<string, string> headers)
        {
            if (headers == null || !headers.Any())
                return this;
            foreach (var header in headers)
            {
                if (Client.DefaultRequestHeaders.Contains(header.Key))
                    Client.DefaultRequestHeaders.Remove(header.Key);
                Client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            return this;
        }

        public IHttpRequestBuilder AddHeader(params string[] headers)
        {
            if (headers == null || !headers.Any())
                return this;
            if (headers.Length % 2 != 0)
                throw new Exception("The number of headers members should be even.");
            for (var i = 0; i < headers.Length; i++)
            {
                if (Client.DefaultRequestHeaders.Contains(headers[i]))
                    Client.DefaultRequestHeaders.Remove(headers[i]);
                Client.DefaultRequestHeaders.Add(headers[i], headers[i + 1]);
                i++;
            }

            return this;
        }

        public IHttpRequestBuilder Authorization(string scheme,
            string parameter)
        {
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(scheme, parameter);
            return this;
        }

        public IHttpRequestBuilder UserAgent(string userAgent)
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            return this;
        }

        public IHttpRequestBuilder Accept(string accept)
        {
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            return this;
        }

        public IHttpRequestBuilder Range(long? from, long? to)
        {
            Client.DefaultRequestHeaders.Range = new RangeHeaderValue(from, to);
            return this;
        }

        public IHttpRequestBuilder Referrer(string referrer)
        {
            Client.DefaultRequestHeaders.Referrer = new Uri(referrer);
            return this;
        }
    }
}