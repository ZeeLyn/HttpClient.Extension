using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HttpClient.Extension.Resilience
{
    internal static class HttpResponseContentReader
    {
        internal static async Task<TResult> ReadAsync<TResult>(this HttpContent content)
        {
            var typename = typeof(TResult).FullName?.TrimEnd(']');
            object result = null;
            var isNullable = typename.StartsWith("System.Nullable`1[");
            var basename = isNullable ? typename.Substring(18) : typename;
            switch (basename)
            {
                case "System.IO.Stream":
                    result = await content.ReadAsStreamAsync();
                    break;
                case "System.Byte[":
                    result = await content.ReadAsByteArrayAsync();
                    break;
                case "System.String":
                    result = await content.ReadAsStringAsync();
                    break;
                case "System.Byte":
                    result = await Convert(content, byte.Parse);
                    break;
                case "System.Char":
                    var val = await content.ReadAsStringAsync();
                    if (val.Length > 0) result = val[0];
                    break;
                case "System.Boolean":
                    result = await Convert(content, bool.Parse);
                    break;
                case "System.Int16":
                    result = await Convert(content, short.Parse);
                    break;
                case "System.Int32":
                    result = await Convert(content, int.Parse);
                    break;
                case "System.Int64":
                    result = await Convert(content, long.Parse);
                    break;
                case "System.Single":
                    result = await Convert(content, float.Parse);
                    break;
                case "System.Double":
                    result = await Convert(content, double.Parse);
                    break;
                case "System.Decimal":
                    result = await Convert(content, decimal.Parse);
                    break;
                case "System.SByte":
                    result = await Convert(content, sbyte.Parse);
                    break;
                case "System.UInt16":
                    result = await Convert(content, ushort.Parse);
                    break;
                case "System.UInt32":
                    result = await Convert(content, uint.Parse);
                    break;
                case "System.UInt64":
                    result = await Convert(content, ulong.Parse);
                    break;
                case "System.DateTime":
                    result = await Convert(content, DateTime.Parse);
                    break;
                case "System.DateTimeOffset":
                    result = await Convert(content, DateTimeOffset.Parse);
                    break;
                case "System.TimeSpan":
                    result = await Convert(content, TimeSpan.Parse);
                    break;
                case "System.Guid":
                    result = await Convert(content, Guid.Parse);
                    break;
                default:
                    var json = await content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<TResult>(json);
                    break;
            }

            if (result == null)
                return default;
            return (TResult)result;
        }

        private static async Task<TResult> Convert<TResult>(HttpContent content, Func<string, TResult> converter)
        {
            var str = await content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(str) ? default : converter(str);
        }
    }
}