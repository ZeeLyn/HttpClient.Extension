using Polly;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpClient.Extension.Resilience
{
    public partial class HttpRequestBuilder
    {
        public IHttpRequestBuilder ResultHandle(
            Func<IServiceProvider, HttpResponseMessage, IHttpRequestBuilder, bool> handle)
        {
            _resultHandle = handle;
            return this;
        }

        /// <summary>
        /// 异常过滤处理器
        /// </summary>
        /// <param name="handle">返回True,表示截获异常，执行重试或降级处理，不再继续向上抛出。反之继续抛出异常，在业务代码里处理异常信息</param>
        /// <returns></returns>
        public IHttpRequestBuilder ExceptionHandle(
            Func<IServiceProvider, string, Exception, IHttpRequestBuilder, bool> handle)
        {
            _exceptionHandle = handle;
            return this;
        }

        /// <summary>
        /// 重试次数
        /// </summary>
        /// <param name="retry"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public IHttpRequestBuilder Retry(int retry)
        {
            if (retry < 0) throw new ArgumentException("The number of retries must be greater than or equal to 0");
            _retry = retry;
            return this;
        }

        /// <summary>
        /// 重试等待时长，索引跟第几次重试对应
        /// </summary>
        /// <param name="sleepDurations"></param>
        /// <returns></returns>
        public IHttpRequestBuilder WaitAndRetry(params TimeSpan[] sleepDurations)
        {
            _waitAndRetrySleepDurations = sleepDurations;
            return this;
        }

        public IHttpRequestBuilder OnRetry(
            Action<IServiceProvider, TimeSpan, int, ResilienceContext> onRetry)
        {
            _onRetry = onRetry;
            return this;
        }

        public IHttpRequestBuilder FallbackHandleAsync(
            Func<IServiceProvider, Exception, ResilienceContext, Task<HttpResponseMessage>> handle)
        {
            _fallbackHandleAsync = handle;
            return this;
        }

        public IHttpRequestBuilder OnFallbackAsync(
            Func<IServiceProvider, ResilienceContext, Task>? action)
        {
            _onFallbackAsync = action;
            return this;
        }
    }
}