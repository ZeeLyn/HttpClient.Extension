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
        /// 异常处理器，返回True将执行重试策略
        /// </summary>
        public Func<IServiceProvider, string, Exception, bool> ExceptionHandle { get; set; } = (_, _, _) => true;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int Retry { get; set; }

        /// <summary>
        /// 重试等待时长，索引跟第几次重试对应
        /// </summary>
        public IEnumerable<TimeSpan> WaitAndRetrySleepDurations { get; set; }

        public Action<IServiceProvider, TimeSpan, int, ResilienceContext>? OnRetry { get; set; }

        public Func<IServiceProvider, Exception, ResilienceContext, Task<HttpResponseMessage>>? FallbackHandleAsync
        {
            get;
            set;
        }

        public Func<IServiceProvider, ResilienceContext, Task>? OnFallbackAsync { get; set; }
    }
}