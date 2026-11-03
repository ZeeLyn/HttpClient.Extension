using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClient.Extension
{
    public static class HttpResponseContentReader
    {
        public static Task<(TResult, string)> ReadAsync<TResult>(this HttpContent content, bool success)
        {
            return content.ReadAsync<TResult>(success, CancellationToken.None);
        }

        public static async Task<(TResult, string)> ReadAsync<TResult>(this HttpContent content, bool success, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!success)
            {
                var raw = await ReadStringAsync(content, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return (default, raw);
            }

            if (typeof(TResult) == typeof(Stream))
            {
                var stream = await ReadStreamAsync(content, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return ((TResult)(object)stream, string.Empty);
            }
            if (typeof(TResult) == typeof(byte[]))
            {
                var bytes = await ReadBytesAsync(content, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return ((TResult)(object)bytes, string.Empty);
            }

            var text = await ReadStringAsync(content, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (typeof(TResult) == typeof(string))
                return ((TResult)(object)text, string.Empty);

            var result = ScalarParser<TResult>.Parse(text);
            cancellationToken.ThrowIfCancellationRequested();
            return (result, string.Empty);
        }

        private static class ScalarParser<TResult>
        {
            // 每个 TResult 只计算一次可空类型信息，避免重复反射及临时类型数组。
            private static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TResult)) != null;

            internal static TResult Parse(string text)
            {
                var json = text;
                if (IsNullable)
                {
                    if (string.IsNullOrWhiteSpace(text) || text.Trim() == "null")
                        return default;
                    if (text.TrimStart().StartsWith("\"", StringComparison.Ordinal))
                        text = JsonConvert.DeserializeObject<string>(text);
                    if (string.IsNullOrEmpty(text))
                        return default;
                }

                if (typeof(TResult) == typeof(char) || typeof(TResult) == typeof(char?))
                    return string.IsNullOrEmpty(text) ? default : (TResult)(object)text[0];

                var empty = string.IsNullOrWhiteSpace(text);
                if (typeof(TResult) == typeof(byte) || typeof(TResult) == typeof(byte?))
                    return empty ? default : (TResult)(object)byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(sbyte) || typeof(TResult) == typeof(sbyte?))
                    return empty ? default : (TResult)(object)sbyte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(short) || typeof(TResult) == typeof(short?))
                    return empty ? default : (TResult)(object)short.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(ushort) || typeof(TResult) == typeof(ushort?))
                    return empty ? default : (TResult)(object)ushort.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(int) || typeof(TResult) == typeof(int?))
                    return empty ? default : (TResult)(object)int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(uint) || typeof(TResult) == typeof(uint?))
                    return empty ? default : (TResult)(object)uint.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(long) || typeof(TResult) == typeof(long?))
                    return empty ? default : (TResult)(object)long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(ulong) || typeof(TResult) == typeof(ulong?))
                    return empty ? default : (TResult)(object)ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(float) || typeof(TResult) == typeof(float?))
                    return empty ? default : (TResult)(object)float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(double) || typeof(TResult) == typeof(double?))
                    return empty ? default : (TResult)(object)double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(decimal) || typeof(TResult) == typeof(decimal?))
                    return empty ? default : (TResult)(object)decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(bool) || typeof(TResult) == typeof(bool?))
                    return empty ? default : (TResult)(object)bool.Parse(text);
                if (typeof(TResult) == typeof(DateTime) || typeof(TResult) == typeof(DateTime?))
                    return empty ? default : (TResult)(object)DateTime.Parse(text, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(DateTimeOffset) || typeof(TResult) == typeof(DateTimeOffset?))
                    return empty ? default : (TResult)(object)DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(TimeSpan) || typeof(TResult) == typeof(TimeSpan?))
                    return empty ? default : (TResult)(object)TimeSpan.Parse(text, CultureInfo.InvariantCulture);
                if (typeof(TResult) == typeof(Guid) || typeof(TResult) == typeof(Guid?))
                    return empty ? default : (TResult)(object)Guid.Parse(text);
                return JsonConvert.DeserializeObject<TResult>(json);
            }
        }

        internal static Task<string> ReadStringAsync(HttpContent content, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1
            // 无取消令牌时直接读取，避免兼容分支的委托和异步包装分配。
            if (!cancellationToken.CanBeCanceled)
                return content.ReadAsStringAsync();
            return ReadWithCancellationAsync(content, content.ReadAsStringAsync, cancellationToken);
#else
            return content.ReadAsStringAsync(cancellationToken);
#endif
        }

        internal static Task<byte[]> ReadBytesAsync(HttpContent content, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1
            if (!cancellationToken.CanBeCanceled)
                return content.ReadAsByteArrayAsync();
            return ReadWithCancellationAsync(content, content.ReadAsByteArrayAsync, cancellationToken);
#else
            return content.ReadAsByteArrayAsync(cancellationToken);
#endif
        }

        internal static Task<Stream> ReadStreamAsync(HttpContent content, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1
            if (!cancellationToken.CanBeCanceled)
                return content.ReadAsStreamAsync();
            return ReadWithCancellationAsync(content, content.ReadAsStreamAsync, cancellationToken);
#else
            return content.ReadAsStreamAsync(cancellationToken);
#endif
        }

#if NETSTANDARD2_0 || NETSTANDARD2_1
        private static async Task<T> ReadWithCancellationAsync<T>(HttpContent content, Func<Task<T>> read, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 旧目标框架没有带取消参数的读取 API，取消时释放内容以中断网络读取。
            using (cancellationToken.Register(content.Dispose))
            {
                try
                {
                    var result = await read();
                    cancellationToken.ThrowIfCancellationRequested();
                    return result;
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }
#endif
    }
}
