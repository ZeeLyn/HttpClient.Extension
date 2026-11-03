using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClient.Extension
{
    public static class HttpClientRequestExtensions
    {
        public static System.Net.Http.HttpClient Timeout(this System.Net.Http.HttpClient client, TimeSpan timeout)
        {
            client.Timeout = timeout;
            return client;
        }

        public static Task<ResponseResult<TResult>> GetAsync<TResult>(this System.Net.Http.HttpClient client, string url)
        {
            return client.GetAsync<TResult>(url, CancellationToken.None);
        }

        public static async Task<ResponseResult<TResult>> GetAsync<TResult>(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ReadResponseAsync<TResult>(await client.GetAsync(url, cancellationToken), cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostAsync<TResult>(this System.Net.Http.HttpClient client, string url, HttpContent data)
        {
            return client.PostAsync<TResult>(url, data, CancellationToken.None);
        }

        public static async Task<ResponseResult<TResult>> PostAsync<TResult>(this System.Net.Http.HttpClient client, string url, HttpContent data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ReadResponseAsync<TResult>(await client.PostAsync(url, data, cancellationToken), cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostAsync<TResult>(this System.Net.Http.HttpClient client, string url, string data, string contentType = "application/x-www-form-urlencoded")
        {
            return client.PostAsync<TResult>(url, data, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PostAsync<TResult>(this System.Net.Http.HttpClient client, string url, string data, CancellationToken cancellationToken, string contentType = "application/x-www-form-urlencoded")
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new StringContent(data, Encoding.UTF8, contentType);
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, string formQueryString, string contentType = "application/x-www-form-urlencoded")
        {
            return client.PostFormAsync<TResult>(url, formQueryString, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, string formQueryString, CancellationToken cancellationToken, string contentType = "application/x-www-form-urlencoded")
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new StringContent(formQueryString, Encoding.UTF8, contentType);
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, string contentType = "application/x-www-form-urlencoded")
        {
            return client.PostFormAsync<TResult>(url, formData, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, CancellationToken cancellationToken, string contentType = "application/x-www-form-urlencoded")
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await client.PostFormAsync<TResult>(url, ParameterBuilder.GetAllValues(formData), cancellationToken, contentType);
        }

        public static Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, string contentType = "application/x-www-form-urlencoded")
        {
            return client.PostFormAsync<TResult>(url, formData, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, CancellationToken cancellationToken, string contentType = "application/x-www-form-urlencoded")
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "text/plain") { CharSet = "utf-8" };
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostBodyAsync<TResult>(this System.Net.Http.HttpClient client, string url, string json, string contentType = "application/json")
        {
            return client.PostBodyAsync<TResult>(url, json, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PostBodyAsync<TResult>(this System.Net.Http.HttpClient client, string url, string json, CancellationToken cancellationToken, string contentType = "application/json")
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new StringContent(json, Encoding.UTF8, contentType);
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostBodyAsync<TResult>(this System.Net.Http.HttpClient client, string url, object data, string contentType = "application/json")
        {
            return client.PostBodyAsync<TResult>(url, data, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PostBodyAsync<TResult>(this System.Net.Http.HttpClient client, string url, object data, CancellationToken cancellationToken, string contentType = "application/json")
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await client.PostBodyAsync<TResult>(url, JsonConvert.SerializeObject(data), cancellationToken, contentType);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, params string[] filePaths)
        {
            return client.PostMultipartAsync<TResult>(url, CancellationToken.None, filePaths);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken, params string[] filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new MultipartFormDataContent();
            foreach (var filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMultipartFile(content, new StreamContent(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true)), Path.GetFileName(filePath), "file");
            }
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, params string[] filePaths)
        {
            return client.PostMultipartAsync<TResult>(url, formData, CancellationToken.None, filePaths);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, CancellationToken cancellationToken, params string[] filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await client.PostMultipartAsync<TResult>(url, ParameterBuilder.GetAllValues(formData), cancellationToken, filePaths);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, params string[] filePaths)
        {
            return client.PostMultipartAsync<TResult>(url, formData, CancellationToken.None, filePaths);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, CancellationToken cancellationToken, params string[] filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new MultipartFormDataContent();
            foreach (var filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMultipartFile(content, new StreamContent(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true)), Path.GetFileName(filePath), "file");
            }
            foreach (var field in formData)
                content.Add(new StringContent(field.Value ?? string.Empty, Encoding.UTF8), field.Key);
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, params MultipartFormFileBytesData[] files)
        {
            return client.PostMultipartAsync<TResult>(url, CancellationToken.None, files);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken, params MultipartFormFileBytesData[] files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMultipartFile(content, new ByteArrayContent(file.FileBytes), file.FileName, file.Name);
            }
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, params MultipartFormFileBytesData[] files)
        {
            return client.PostMultipartAsync<TResult>(url, formData, CancellationToken.None, files);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, CancellationToken cancellationToken, params MultipartFormFileBytesData[] files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await client.PostMultipartAsync<TResult>(url, ParameterBuilder.GetAllValues(formData), cancellationToken, files);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, params MultipartFormFileBytesData[] files)
        {
            return client.PostMultipartAsync<TResult>(url, formData, CancellationToken.None, files);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, CancellationToken cancellationToken, params MultipartFormFileBytesData[] files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMultipartFile(content, new ByteArrayContent(file.FileBytes), file.FileName, file.Name);
            }
            foreach (var field in formData)
                content.Add(new StringContent(field.Value ?? string.Empty, Encoding.UTF8), field.Key);
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, params MultipartFormFileStreamData[] files)
        {
            return client.PostMultipartAsync<TResult>(url, CancellationToken.None, files);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken, params MultipartFormFileStreamData[] files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMultipartFile(content, new StreamContent(file.FileStream), file.FileName, file.Name);
            }
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, params MultipartFormFileStreamData[] files)
        {
            return client.PostMultipartAsync<TResult>(url, formData, CancellationToken.None, files);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, CancellationToken cancellationToken, params MultipartFormFileStreamData[] files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await client.PostMultipartAsync<TResult>(url, ParameterBuilder.GetAllValues(formData), cancellationToken, files);
        }

        public static Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, params MultipartFormFileStreamData[] files)
        {
            return client.PostMultipartAsync<TResult>(url, formData, CancellationToken.None, files);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, CancellationToken cancellationToken, params MultipartFormFileStreamData[] files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMultipartFile(content, new StreamContent(file.FileStream), file.FileName, file.Name);
            }
            foreach (var field in formData)
                content.Add(new StringContent(field.Value ?? string.Empty, Encoding.UTF8), field.Key);
            return await client.PostAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, HttpContent data)
        {
            return client.PutAsync<TResult>(url, data, CancellationToken.None);
        }

        public static async Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, HttpContent data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ReadResponseAsync<TResult>(await client.PutAsync(url, data, cancellationToken), cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, string data, string contentType = "application/json")
        {
            return client.PutAsync<TResult>(url, data, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, string data, CancellationToken cancellationToken, string contentType = "application/json")
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new StringContent(data, Encoding.UTF8, contentType);
            return await client.PutAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, object data, string contentType = "application/json")
        {
            return client.PutAsync<TResult>(url, data, CancellationToken.None, contentType);
        }

        public static async Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, object data, CancellationToken cancellationToken, string contentType = "application/json")
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, contentType);
            return await client.PutAsync<TResult>(url, content, cancellationToken);
        }

        public static Task<ResponseResult<TResult>> DeleteAsync<TResult>(this System.Net.Http.HttpClient client, string url)
        {
            return client.DeleteAsync<TResult>(url, CancellationToken.None);
        }

        public static async Task<ResponseResult<TResult>> DeleteAsync<TResult>(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ReadResponseAsync<TResult>(await client.DeleteAsync(url, cancellationToken), cancellationToken);
        }

        public static Task<ResponseResult<HttpContentHeaders>> HeadAsync(this System.Net.Http.HttpClient client, string url)
        {
            return client.HeadAsync(url, CancellationToken.None);
        }

        public static async Task<ResponseResult<HttpContentHeaders>> HeadAsync(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request, cancellationToken);
            return new ResponseResult<HttpContentHeaders>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                ContentHeaders = response.Content.Headers,
                Result = response.Content.Headers
            };
        }

        public static async Task DownloadAsync(this System.Net.Http.HttpClient client, string url, string saveFileName, Action<long, long, decimal> onProgressChanged = null, Action<long> onCompleted = null, Action<string> onError = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(saveFileName))
                throw new ArgumentNullException(nameof(saveFileName));
            cancellationToken.ThrowIfCancellationRequested();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                onError?.Invoke(response.StatusCode.ToString());
                return;
            }
            var targetPath = Path.GetFullPath(saveFileName);
            var dir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(dir);
            var tempPath = Path.Combine(dir, ".httpclient-" + Guid.NewGuid().ToString("N") + ".tmp");
            var totalSize = response.Content.Headers.ContentLength;
            var readLen = 0L;
            var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
            try
            {
                using (fileStream)
                using (var stream = await HttpResponseContentReader.ReadStreamAsync(response.Content, cancellationToken))
                {
                    var buffer = new byte[64 * 1024];
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (read == 0)
                            break;
                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                        readLen += read;
                        var percent = totalSize > 0 ? Math.Round((decimal)readLen / totalSize.Value * 100, 3) : 0;
                        onProgressChanged?.Invoke(totalSize ?? 0, readLen, percent);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (totalSize.HasValue && readLen != totalSize.Value)
                    throw new EndOfStreamException("下载内容长度与响应声明不一致。");
                // 临时文件与目标在同一目录，完整下载后再替换，避免失败时破坏原文件。
                if (File.Exists(targetPath))
                    File.Replace(tempPath, targetPath, null);
                else
                    File.Move(tempPath, targetPath);
                onCompleted?.Invoke(readLen);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }


        public static async Task<ResponseResult<Stream>> DownloadWithStreamAsync(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ReadResponseAsync<Stream>(
                await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken), cancellationToken);
        }

        public static Task<ResponseResult<byte[]>> DownloadWithBytesAsync(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken = default)
        {
            return client.GetAsync<byte[]>(url, cancellationToken);
        }

        private static void AddMultipartFile(MultipartFormDataContent form, HttpContent file, string fileName, string name)
        {
            try
            {
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(fileName) ?? string.Empty, out var mediaType))
                    mediaType = "application/octet-stream";
                file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                form.Add(file, name, fileName);
            }
            catch
            {
                // 添加失败时容器尚未接管内容，需在这里释放文件流。
                file.Dispose();
                throw;
            }
        }

        private static async Task<ResponseResult<TResult>> ReadResponseAsync<TResult>(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var ownsResponse = true;
            try
            {
                var data = await response.Content.ReadAsync<TResult>(response.IsSuccessStatusCode, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var result = new ResponseResult<TResult>
                {
                    Succeed = response.IsSuccessStatusCode,
                    StatusCode = response.StatusCode,
                    Headers = response.Headers,
                    ContentHeaders = response.Content.Headers,
                    Result = data.Item1,
                    Raw = data.Item2
                };
                if (typeof(TResult) == typeof(Stream) && result.Result is Stream stream)
                {
                    result.Result = (TResult)(object)new HttpResponseStream(response, stream);
                    ownsResponse = false;
                }
                return result;
            }
            finally
            {
                if (ownsResponse)
                    response.Dispose();
            }
        }
    }
}
