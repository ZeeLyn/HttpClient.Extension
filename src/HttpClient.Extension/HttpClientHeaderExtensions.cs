using System;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;

namespace HttpClient.Extension
{
    public static class HttpClientHeaderExtensions
    {
        public static System.Net.Http.HttpClient AddHeader(this System.Net.Http.HttpClient client, string name, string value)
        {
            client.DefaultRequestHeaders.Add(name, value);
            return client;
        }

        public static System.Net.Http.HttpClient AddHeader(this System.Net.Http.HttpClient client, IDictionary<string, string> headers)
        {
            if (headers == null || !headers.Any())
                return client;
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
            return client;
        }

        public static System.Net.Http.HttpClient AddHeader(this System.Net.Http.HttpClient client, params string[] headers)
        {
            if (headers == null || !headers.Any())
                return client;
            if (headers.Length % 2 != 0)
                throw new Exception("The number of headers members should be even.");
            for (var i = 0; i < headers.Length; i++)
            {
                client.DefaultRequestHeaders.Add(headers[i], headers[i + 1]);
                i++;
            }
            return client;
        }

        public static System.Net.Http.HttpClient Authorization(this System.Net.Http.HttpClient client, string scheme, string parameter)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(scheme, parameter);
            return client;
        }

        public static System.Net.Http.HttpClient UserAgent(this System.Net.Http.HttpClient client, string userAgent)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            return client;
        }

    }
}
