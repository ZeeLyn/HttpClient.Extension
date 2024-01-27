using Polly;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpClient.Extension.Resilience
{
    public class HttpRequestResilienceOptions
    {
        /// <summary>
        /// 返回True,表示截获异常，执行重试或降级处理，不再继续向上抛出。反之继续抛出异常，在业务代码里处理异常信息
        /// </summary>
        public Func<IServiceProvider, string, Exception, bool> ExceptionHandle { get; set; } = (_, _, _) => true;

        /// <summary>
        /// 如果设置了 <paramref name="WaitAndRetrySleepDurations"></paramref>，此参数将失效
        /// </summary>
        public int Retry { get; set; }

        /// <summary>
        /// 如果设置了此参数 <paramref name="Retry"></paramref> 将失效
        /// </summary>
        public IEnumerable<TimeSpan> WaitAndRetrySleepDurations { get; set; }

        public Action<IServiceProvider, DelegateResult<HttpResponseMessage>, TimeSpan, int, Context>? OnRetry
        {
            get;
            set;
        }

        /// <summary>
        /// 降级处理，返回自定义响应消息
        /// </summary>
        public Func<IServiceProvider, Exception, Context, Task<HttpResponseMessage>>? FallbackHandleAsync { get; set; }

        public Func<IServiceProvider, DelegateResult<HttpResponseMessage>, Context, Task>? OnFallbackAsync { get; set; }
    }
}