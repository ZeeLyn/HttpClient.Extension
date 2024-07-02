using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Fallback;
using Polly.Retry;

namespace HttpClient.Extension.Resilience
{
    public partial class HttpRequestBuilder
    {
        private async Task<HttpResponsePolicyResult> RequestWrapPolicyAsync(string requestUrl,
            Func<CancellationToken, Task<HttpResponseMessage>> request)
        {
            var result = new HttpResponsePolicyResult();
            Exception exception = default;
            var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

            var predicateBuilder = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(resp =>
                {
                    Console.WriteLine("对结果检查");
                    return _resultHandle?.Invoke(ServiceProvider, resp, this) ?? true;
                })
                .Handle<Exception>(ex =>
                {
                    requestUrl = Client?.BaseAddress + requestUrl;
                    exception = ex;
                    Logger.LogError(ex, "An exception occurred when requesting '{0}'", requestUrl);
                    return _exceptionHandle?.Invoke(ServiceProvider, requestUrl, ex, this) ?? true;
                });


            builder.AddFallback(new FallbackStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = predicateBuilder,
                FallbackAction = async args =>
                {
                    Logger.LogInformation("Executing fallback");
                    result.Exception = exception;
                    return Outcome.FromResult(_fallbackHandleAsync is not null
                        ? await _fallbackHandleAsync.Invoke(ServiceProvider, exception, args.Context)
                        : new HttpResponseMessage
                        {
                            StatusCode = 0,
                            Content = new StringContent(exception is null ? "Request failed" : exception.Message,
                                Encoding.UTF8)
                        });
                },
                OnFallback = async args =>
                {
                    Logger.LogInformation("Start execute fallback");
                    if (_onFallbackAsync is not null)
                        await _onFallbackAsync.Invoke(ServiceProvider, args.Context);
                }
            });
            if (_retry > 0)
            {
                var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = predicateBuilder,
                    MaxRetryAttempts = _retry,
                    OnRetry = async args =>
                    {
                        exception = default;
                        _onRetry?.Invoke(ServiceProvider, TimeSpan.Zero, args.AttemptNumber + 1, args.Context);
                        Logger.LogInformation("Retry: {0}", args.AttemptNumber + 1);
                        await Task.CompletedTask;
                    },
                };


                if (_waitAndRetrySleepDurations?.Any() ?? false)
                {
                    retryOptions.DelayGenerator = async args =>
                    {
                        if (args.AttemptNumber - 1 > _waitAndRetrySleepDurations.Count())
                            return TimeSpan.Zero;
                        return await Task.FromResult(_waitAndRetrySleepDurations.ElementAt(args.AttemptNumber));
                    };
                }

                builder.AddRetry(retryOptions);
            }

            var pipeline = builder.Build();
            result.ResponseMessage =
                await pipeline.ExecuteAsync(async cancellationToken => await request(cancellationToken));
            Dispose();
            return result;
        }

        //private async Task<HttpResponsePolicyResult> RequestWrapPolicyAsync(string requestUrl,
        //    Func<Task<HttpResponseMessage>> request)
        //{
        //    requestUrl = Client?.BaseAddress + requestUrl;
        //    var result = new HttpResponsePolicyResult();
        //    if (_retry <= 0 && _exceptionHandle is null)
        //    {
        //        result.ResponseMessage = await request.Invoke();
        //        Dispose();
        //        return result;
        //    }

        //    Exception exception = default;

        //    var basePolicy = Policy<HttpResponseMessage>.Handle<Exception>(ex =>
        //        {
        //            exception = ex;
        //            Logger.LogError(ex, "An exception occurred when requesting '{0}'", requestUrl);
        //            return _exceptionHandle?.Invoke(ServiceProvider, requestUrl, ex) ?? true;
        //        }
        //    );

        //    //var resultPolicy = Policy<HttpResponseMessage>.HandleResult(resp => _resultHandle?.Invoke(resp) ?? true);

        //    var policies = new List<IAsyncPolicy<HttpResponseMessage>>();

        //    var fallbackPolicy = basePolicy.FallbackAsync(async (context, _) =>
        //        {
        //            Logger.LogInformation("Executing fallback");
        //            result.Exception = exception;

        //            return _fallbackHandleAsync is not null
        //                ? await _fallbackHandleAsync.Invoke(ServiceProvider, exception, context)
        //                : new HttpResponseMessage
        //                {
        //                    StatusCode = 0,
        //                    Content = new StringContent(exception is null ? "Request failed" : exception.Message,
        //                        Encoding.UTF8)
        //                };
        //        },
        //        async (msg, context) =>
        //        {
        //            Logger.LogInformation("Start execute fallback");
        //            if (_onFallbackAsync is not null)
        //                await _onFallbackAsync.Invoke(ServiceProvider, msg, context);
        //        });

        //    policies.Add(fallbackPolicy);

        //    if (_retry > 0)
        //    {
        //        var retryPolicy = basePolicy.RetryAsync(_retry,
        //            (msg, retryCount, context) =>
        //            {
        //                exception = default;
        //                _onRetry?.Invoke(ServiceProvider, msg, TimeSpan.Zero, retryCount, context);
        //                Logger.LogInformation("Retry: {0}", retryCount);
        //            });
        //        policies.Add(retryPolicy);
        //        var policy = Policy.WrapAsync(policies.ToArray());
        //        result.ResponseMessage = await policy.ExecuteAsync(async () => await request());
        //        Dispose();
        //        return result;
        //    }

        //    if (_waitAndRetrySleepDurations?.Any() ?? false)
        //    {
        //        var retryPolicy = basePolicy.WaitAndRetryAsync(_waitAndRetrySleepDurations,
        //            (msg, ts, retryCount, context) =>
        //            {
        //                exception = default;
        //                _onRetry?.Invoke(ServiceProvider, msg, ts, retryCount, context);
        //                Logger.LogInformation("Sleep:{0}ms,Retry: {1}", ts.TotalMilliseconds, retryCount);
        //            });
        //        policies.Add(retryPolicy);
        //        var policy = Policy.WrapAsync(policies.ToArray());
        //        result.ResponseMessage = await policy.ExecuteAsync(async () => await request());
        //        Dispose();
        //        return result;
        //    }

        //    result.ResponseMessage = await fallbackPolicy.ExecuteAsync(async () => await request());
        //    Dispose();
        //    return result;
        //}

        private async Task<ResponseResult<TResult>> BuildResponseResult<TResult>(HttpResponsePolicyResult response)
        {
            using (response.ResponseMessage)
            {
                return new ResponseResult<TResult>
                {
                    Succeed = response.ResponseMessage.IsSuccessStatusCode,
                    StatusCode = response.ResponseMessage.StatusCode,
                    Headers = response.ResponseMessage.Headers,
                    Exception = response.Exception,
                    ErrorMessage = response.ResponseMessage.IsSuccessStatusCode
                        ? string.Empty
                        : await response.ResponseMessage.Content.ReadAsStringAsync(),
                    Result = response.ResponseMessage.IsSuccessStatusCode
                        ? await response.ResponseMessage.Content.ReadAsync<TResult>()
                        : default
                };
            }
        }

        public async Task<ResponseResult<TResult>> GetAsync<TResult>(string url)
        {
            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.GetAsync(url, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        #region POST

        public async Task<ResponseResult<TResult>> PostAsync<TResult>(string url, HttpContent data)
        {
            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, data, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostAsync<TResult>(
            string url, string data, string contentType = "application/x-www-form-urlencoded")
        {
            return await PostAsync<TResult>(url, new StringContent(data, Encoding.UTF8, contentType));
        }

        #endregion

        #region POST FORM

        public async Task<ResponseResult<TResult>> PostFormAsync<TResult>(
            string url, string formQueryString, string contentType = "application/x-www-form-urlencoded")
        {
            using var content = new StringContent(formQueryString, Encoding.UTF8, contentType);
            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }


        public async Task<ResponseResult<TResult>> PostFormAsync<TResult>(
            string url, object formData, string contentType = "application/x-www-form-urlencoded")
        {
            return await PostFormAsync<TResult>(url, ParameterBuilder.BuildQuery(formData), contentType);
        }

        public async Task<ResponseResult<TResult>> PostFormAsync<TResult>(
            string url, IDictionary<string, string> formData, string contentType = "application/x-www-form-urlencoded")
        {
            return await PostFormAsync<TResult>(url, ParameterBuilder.BuildQuery(formData), contentType);
        }

        #endregion


        #region POST BODY

        public async Task<ResponseResult<TResult>> PostBodyAsync<TResult>(
            string url, string json, string contentType = "application/json")
        {
            using var content = new StringContent(json, Encoding.UTF8, contentType);
            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostBodyAsync<TResult>(
            string url, object data, string contentType = "application/json")
        {
            return await PostBodyAsync<TResult>(url, JsonConvert.SerializeObject(data), contentType);
        }

        #endregion

        #region POST MULTIPART

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, params string[] filePaths)
        {
            using var content = new MultipartFormDataContent("form-data");
            foreach (var filePath in filePaths)
            {
#if NETSTANDARD2_0
                var byteContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(filePath), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var byteContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                var mediaType =
                    MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(filePath), "application/octet-stream");
#endif

                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, "file", Path.GetFileName(filePath));
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }


        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            params MultipartFormFileBytesData[] files)
        {
            using var content = new MultipartFormDataContent("form-data");
            foreach (var file in files)
            {
                var byteContent = new ByteArrayContent(file.FileBytes);
#if NETSTANDARD2_0
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var mediaType = MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(file.FileName) ?? string.Empty,
                    "application/octet-stream");
#endif

                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, file.Name, file.FileName ?? string.Empty);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            params MultipartFormFileStreamData[] files)
        {
            using var content = new MultipartFormDataContent("form-data");
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.FileStream);
#if NETSTANDARD2_0
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var mediaType = MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(file.FileName) ?? string.Empty,
                    "application/octet-stream");
#endif
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(streamContent, file.Name, file.FileName ?? string.Empty);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        #endregion

        #region MULTIPARTFORM

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, object formData,
            params string[] filePaths)
        {
            using var content = new MultipartFormDataContent();
            foreach (var filePath in filePaths)
            {
#if NETSTANDARD2_0
                var byteContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(filePath), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var byteContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                var mediaType =
                    MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(filePath), "application/octet-stream");
#endif

                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, "file", Path.GetFileName(filePath));
            }

            foreach (var para in ParameterBuilder.GetAllValues(formData))
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            IDictionary<string, string> formData,
            params string[] filePaths)
        {
            using var content = new MultipartFormDataContent();
            foreach (var filePath in filePaths)
            {
#if NETSTANDARD2_0
                var byteContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(filePath), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var byteContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                var mediaType =
                    MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(filePath), "application/octet-stream");
#endif

                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, "file", Path.GetFileName(filePath));
            }

            foreach (var para in formData)
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, object formData,
            params MultipartFormFileBytesData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var byteContent = new ByteArrayContent(file.FileBytes);
#if NETSTANDARD2_0
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var mediaType = MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(file.FileName) ?? string.Empty,
                    "application/octet-stream");
#endif
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, file.Name, file.FileName ?? string.Empty);
            }

            foreach (var para in ParameterBuilder.GetAllValues(formData))
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            IDictionary<string, string> formData,
            params MultipartFormFileBytesData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var byteContent = new ByteArrayContent(file.FileBytes);
#if NETSTANDARD2_0
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var mediaType = MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(file.FileName) ?? string.Empty,
                    "application/octet-stream");
#endif
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, file.Name, file.FileName ?? string.Empty);
            }

            foreach (var para in formData)
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url, object formData,
            params MultipartFormFileStreamData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.FileStream);
#if NETSTANDARD2_0
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var mediaType = MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(file.FileName) ?? string.Empty,
                    "application/octet-stream");
#endif
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(streamContent, file.Name, file.FileName ?? string.Empty);
            }

            foreach (var para in ParameterBuilder.GetAllValues(formData))
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(string url,
            IDictionary<string, string> formData,
            params MultipartFormFileStreamData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.FileStream);
#if NETSTANDARD2_0
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
#else
                var mediaType = MediaType.MediaTypes.GetValueOrDefault(Path.GetExtension(file.FileName) ?? string.Empty,
                    "application/octet-stream");
#endif
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(streamContent, file.Name, file.FileName ?? string.Empty);
            }

            foreach (var para in formData)
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PostAsync(url, content, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        #endregion


        #region PUT

        public async Task<ResponseResult<TResult>> PutAsync<TResult>(
            string url, HttpContent data)
        {
            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.PutAsync(url, data, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        public async Task<ResponseResult<TResult>> PutAsync<TResult>(
            string url, string data, string contentType = "application/json")
        {
            using var content = new StringContent(data, Encoding.UTF8, contentType);
            return await PutAsync<TResult>(url, content);
        }

        public async Task<ResponseResult<TResult>> PutAsync<TResult>(
            string url, object data, string contentType = "application/json")
        {
            using var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, contentType);
            return await PutAsync<TResult>(url, content);
        }

        #endregion

        #region DELETE

        public async Task<ResponseResult<TResult>> DeleteAsync<TResult>(
            string url)
        {
            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) => await Client.DeleteAsync(url, cancellationToken));
            return await BuildResponseResult<TResult>(response);
        }

        #endregion

        #region HEAD

        public async Task<ResponseResult<HttpContentHeaders>> HeadAsync(
            string url)
        {
            using var response =
                await RequestWrapPolicyAsync(url,
                    async (cancellationToken) =>
                        await Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), cancellationToken));
            return await BuildResponseResult<HttpContentHeaders>(response);
        }

        #endregion


        #region DOWNLOAD

        public async Task DownloadAsync(string url, string saveFileName,
            Action<long, long, decimal> onProgressChanged = null, Action<long> onCompleted = null,
            Action<string> onError = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(saveFileName))
                throw new ArgumentNullException(nameof(saveFileName));
            var totalSize = 0L;
            var readLen = 0L;
            if (onProgressChanged != null)
            {
                var headResponse = await HeadAsync(url);
                if (headResponse.Succeed && headResponse.Result.ContentLength.HasValue)
                    totalSize = headResponse.Result.ContentLength.Value;
                else
                {
                    onError?.Invoke(headResponse.StatusCode.ToString());
                    return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                return;
            using var response =
                await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                onError?.Invoke(response.StatusCode.ToString());
                return;
            }

            var dir = Path.GetDirectoryName(saveFileName);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
#if NETSTANDARD2_0
            using var fileStream = new FileStream(saveFileName, FileMode.Create);
#else
            await using var fileStream = new FileStream(saveFileName, FileMode.Create);
#endif
            if (cancellationToken.IsCancellationRequested)
                return;
#if NETSTANDARD2_0
            using var stream = await response.Content.ReadAsStreamAsync();
#else
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#endif
            var buffer = new byte[1024 * 64];
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read == 0)
                    break;
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                if (onProgressChanged != null)
                {
                    readLen += read;
                    var p = Math.Round((decimal)readLen / totalSize * 100, 3);
                    onProgressChanged(totalSize, readLen, p);
                }
            }

            onCompleted?.Invoke(totalSize);
        }

        #endregion
    }
}