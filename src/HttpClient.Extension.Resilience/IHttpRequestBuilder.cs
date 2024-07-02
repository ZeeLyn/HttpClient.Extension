using Polly;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClient.Extension.Resilience
{
    public interface IHttpRequestBuilder : IDisposable
    {
        #region Headers

        IHttpRequestBuilder Timeout(TimeSpan timeout);

        IHttpRequestBuilder AddHeader(string name, string value);

        IHttpRequestBuilder AddHeader(IDictionary<string, string> headers);

        IHttpRequestBuilder AddHeader(params string[] headers);

        IHttpRequestBuilder Authorization(string scheme, string parameter);

        IHttpRequestBuilder UserAgent(string userAgent);

        IHttpRequestBuilder Accept(string accept);

        IHttpRequestBuilder Range(long? from, long? to);

        IHttpRequestBuilder Referrer(string referrer);

        #endregion

        #region Policy

        IHttpRequestBuilder ResultHandle(
            Func<IServiceProvider, HttpResponseMessage, IHttpRequestBuilder, bool> handle);


        IHttpRequestBuilder ExceptionHandle(
            Func<IServiceProvider, string, Exception, IHttpRequestBuilder, bool> handle);

        /// <summary>
        /// 重试次数
        /// </summary>
        /// <param name="retry"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        IHttpRequestBuilder Retry(int retry);

        /// <summary>
        /// 重试等待时长，索引跟第几次重试对应
        /// </summary>
        /// <param name="sleepDurations"></param>
        /// <returns></returns>
        IHttpRequestBuilder WaitAndRetry(params TimeSpan[] sleepDurations);

        IHttpRequestBuilder OnRetry(
            Action<IServiceProvider, TimeSpan, int, ResilienceContext> onRetry);

        IHttpRequestBuilder FallbackHandleAsync(
            Func<IServiceProvider, Exception, ResilienceContext, Task<HttpResponseMessage>> handle);

        IHttpRequestBuilder OnFallbackAsync(
            Func<IServiceProvider, ResilienceContext, Task>? action);

        #endregion


        #region Methods

        Task<ResponseResult<TResult>> GetAsync<TResult>(string url);

        Task<ResponseResult<TResult>> PostAsync<TResult>(string url, HttpContent data);

        Task<ResponseResult<TResult>> PostAsync<TResult>(
            string url, string data, string contentType = "application/x-www-form-urlencoded");

        Task<ResponseResult<TResult>> PostFormAsync<TResult>(
            string url, string formQueryString, string contentType = "application/x-www-form-urlencoded");

        Task<ResponseResult<TResult>> PostFormAsync<TResult>(
            string url, object formData, string contentType = "application/x-www-form-urlencoded");

        Task<ResponseResult<TResult>> PostFormAsync<TResult>(
            string url, IDictionary<string, string> formData, string contentType = "application/x-www-form-urlencoded");


        Task<ResponseResult<TResult>> PostBodyAsync<TResult>(
            string url, string json, string contentType = "application/json");

        Task<ResponseResult<TResult>> PostBodyAsync<TResult>(
            string url, object data, string contentType = "application/json");


        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, params string[] filePaths);


        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            params MultipartFormFileBytesData[] files);

        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            params MultipartFormFileStreamData[] files);

        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, object formData,
            params string[] filePaths);

        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            IDictionary<string, string> formData, params string[] filePaths);

        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, object formData,
            params MultipartFormFileBytesData[] files);

        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            IDictionary<string, string> formData, params MultipartFormFileBytesData[] files);

        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, object formData,
            params MultipartFormFileStreamData[] files);

        Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            IDictionary<string, string> formData, params MultipartFormFileStreamData[] files);

        Task<ResponseResult<TResult>> PutAsync<TResult>(string url, HttpContent data);

        Task<ResponseResult<TResult>> PutAsync<TResult>(string url, string data,
            string contentType = "application/json");

        Task<ResponseResult<TResult>> PutAsync<TResult>(
            string url, object data, string contentType = "application/json");

        Task<ResponseResult<TResult>> DeleteAsync<TResult>(string url);

        Task<ResponseResult<HttpContentHeaders>> HeadAsync(string url);

        Task DownloadAsync(string url, string saveFileName,
            Action<long, long, decimal> onProgressChanged = null, Action<long> onCompleted = null,
            Action<string> onError = null, CancellationToken cancellationToken = default);

        #endregion
    }
}