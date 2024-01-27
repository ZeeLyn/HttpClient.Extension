using Polly;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpClient.Extension.Resilience
{
    public partial class HttpRequestBuilder
    {
        /// <summary>
        /// 异常过滤处理器
        /// </summary>
        /// <param name="handle">返回True,表示截获异常，执行重试或降级处理，不再继续向上抛出。反之继续抛出异常，在业务代码里处理异常信息</param>
        /// <returns></returns>
        public IHttpRequestBuilder ExceptionHandle(Func<IServiceProvider, string, Exception, bool> handle)
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
        public IHttpRequestBuilder Retry(int retry)
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
        public IHttpRequestBuilder WaitAndRetry(params TimeSpan[] sleepDurations)
        {
            _retry = 0;
            _waitAndRetrySleepDurations = sleepDurations;
            return this;
        }

        public IHttpRequestBuilder OnRetry(
            Action<IServiceProvider, DelegateResult<HttpResponseMessage>, TimeSpan, int, Context> onRetry)
        {
            _onRetry = onRetry;
            return this;
        }

        /// <summary>
        /// 降级处理器
        /// </summary>
        /// <param name="handle">返回自定义响应消息</param>
        /// <returns></returns>
        public IHttpRequestBuilder FallbackHandleAsync(
            Func<IServiceProvider, Exception, Context, Task<HttpResponseMessage>> handle)
        {
            _fallbackHandleAsync = handle;
            return this;
        }

        public IHttpRequestBuilder OnFallbackAsync(
            Func<IServiceProvider, DelegateResult<HttpResponseMessage>, Context, Task>? action)
        {
            _onFallbackAsync = action;
            return this;
        }
    }
}