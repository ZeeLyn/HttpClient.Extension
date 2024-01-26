using Polly;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpClient.Extension.Resilience
{
    public partial class HttpRequestBuilder
    {
        public HttpRequestBuilder ExceptionHandle(Func<IServiceProvider, string, Exception, bool> handle)
        {
            _exceptionHandle = handle;
            return this;
        }

        /// <summary>
        /// 设置了此参数，<paramref name="WaitAndRetry"/>将失效，Retry,WaitAndRetry以最后一个设置为有效设置
        /// </summary>
        /// <param name="retry"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public HttpRequestBuilder Retry(int retry)
        {
            if (retry < 0) throw new ArgumentException("The number of retries must be greater than or equal to 0");

            _retry = retry;
            _waitAndRetrySleepDurations = null;
            return this;
        }

        /// <summary>
        /// 设置了此参数，<paramref name="Retry"/>将失效
        /// </summary>
        /// <param name="sleepDurations"></param>
        /// <returns></returns>
        public HttpRequestBuilder WaitAndRetry(params TimeSpan[] sleepDurations)
        {
            _retry = 0;
            _waitAndRetrySleepDurations = sleepDurations;
            return this;
        }

        public HttpRequestBuilder OnRetry(
            Action<IServiceProvider, DelegateResult<HttpResponseMessage>, TimeSpan, int, Context> onRetry)
        {
            _onRetry = onRetry;
            return this;
        }

        public HttpRequestBuilder FallbackHandleAsync(
            Func<IServiceProvider, Exception, Context, Task<HttpResponseMessage>> handle)
        {
            _fallbackHandleAsync = handle;
            return this;
        }

        public HttpRequestBuilder OnFallbackAsync(
            Func<IServiceProvider, DelegateResult<HttpResponseMessage>, Context, Task>? action)
        {
            _onFallbackAsync = action;
            return this;
        }
    }
}