using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static async Task<ResponseResult<TResult>> GetAsync<TResult>(this System.Net.Http.HttpClient client, string url)
        {
            using var response = await client.GetAsync(url);
            return new ResponseResult<TResult>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result = await response.Content.ReadAsync<TResult>()
            };
        }
        #region POST

        public static async Task<ResponseResult<TResult>> PostAsync<TResult>(this System.Net.Http.HttpClient client, string url, HttpContent data)
        {
            using var response = await client.PostAsync(url, data);
            return new ResponseResult<TResult>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result = await response.Content.ReadAsync<TResult>()
            };
        }

        public static async Task<ResponseResult<TResult>> PostAsync<TResult>(this System.Net.Http.HttpClient client, string url, string data, string contentType = "application/x-www-form-urlencoded")
        {
            using var response = await client.PostAsync(url, new StringContent(data, Encoding.UTF8, contentType));
            return new ResponseResult<TResult>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result = await response.Content.ReadAsync<TResult>()
            };
        }


        #endregion

        #region POST FORM

        public static async Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, string formQueryString, string contentType = "application/x-www-form-urlencoded")
        {
            using var content = new StringContent(formQueryString, Encoding.UTF8, contentType);
            return await client.PostAsync<TResult>(url, content);
        }


        public static async Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, string contentType = "application/x-www-form-urlencoded")
        {
            return await client.PostFormAsync<TResult>(url, ParameterBuilder.BuildQuery(formData), contentType);
        }

        public static async Task<ResponseResult<TResult>> PostFormAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, string contentType = "application/x-www-form-urlencoded")
        {
            return await client.PostFormAsync<TResult>(url, ParameterBuilder.BuildQuery(formData), contentType);
        }

        #endregion



        #region POST BODY

        public static async Task<ResponseResult<TResult>> PostBodyAsync<TResult>(this System.Net.Http.HttpClient client, string url, string json, string contentType = "application/json")
        {
            using var content = new StringContent(json, Encoding.UTF8, contentType);
            return await client.PostAsync<TResult>(url, content);
        }

        public static async Task<ResponseResult<TResult>> PostBodyAsync<TResult>(this System.Net.Http.HttpClient client, string url, object data, string contentType = "application/json")
        {
            return await client.PostBodyAsync<TResult>(url, JsonConvert.SerializeObject(data), contentType);
        }

        #endregion

        #region POST MULTIPART
        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, params string[] filePaths)
        {
            using var content = new MultipartFormDataContent("form-data");
            foreach (var filePath in filePaths)
            {
                var byteContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(filePath), out var mediaType))
                    mediaType = "application/octet-stream";
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, "file", Path.GetFileName(filePath));
            }
            return await client.PostAsync<TResult>(url, content);
        }


        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, params MultipartFormFileBytesData[] files)
        {
            using var content = new MultipartFormDataContent("form-data");
            foreach (var file in files)
            {
                var byteContent = new ByteArrayContent(file.FileBytes);
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, file.Name, file.FileName);
            }
            return await client.PostAsync<TResult>(url, content);
        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, params MultipartFormFileStreamData[] files)
        {
            using var content = new MultipartFormDataContent("form-data");
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.FileStream);
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(streamContent, file.Name, file.FileName);
            }
            return await client.PostAsync<TResult>(url, content);
        }

        #endregion

        #region MULTIPARTFORM
        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, params string[] filePaths)
        {
            using var content = new MultipartFormDataContent();
            foreach (var filePath in filePaths)
            {
                var byteContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(filePath), out var mediaType))
                    mediaType = "application/octet-stream";
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, "file", Path.GetFileName(filePath));
            }

            foreach (var para in ParameterBuilder.GetAllValues(formData))
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            return await client.PostAsync<TResult>(url, content);

        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, params string[] filePaths)
        {
            using var content = new MultipartFormDataContent();
            foreach (var filePath in filePaths)
            {
                var byteContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(filePath), out var mediaType))
                    mediaType = "application/octet-stream";
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, "file", Path.GetFileName(filePath));
            }

            foreach (var para in formData)
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            return await client.PostAsync<TResult>(url, content);

        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, params MultipartFormFileBytesData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var byteContent = new ByteArrayContent(file.FileBytes);
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, file.Name, file.FileName);
            }

            foreach (var para in ParameterBuilder.GetAllValues(formData))
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            return await client.PostAsync<TResult>(url, content);

        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, params MultipartFormFileBytesData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var byteContent = new ByteArrayContent(file.FileBytes);
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(byteContent, file.Name, file.FileName);
            }

            foreach (var para in formData)
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            return await client.PostAsync<TResult>(url, content);

        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, object formData, params MultipartFormFileStreamData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.FileStream);
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(streamContent, file.Name, file.FileName);
            }

            foreach (var para in ParameterBuilder.GetAllValues(formData))
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            return await client.PostAsync<TResult>(url, content);

        }

        public static async Task<ResponseResult<TResult>> PostMultipartAsync<TResult>(this System.Net.Http.HttpClient client, string url, IDictionary<string, string> formData, params MultipartFormFileStreamData[] files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.FileStream);
                if (!MediaType.MediaTypes.TryGetValue(Path.GetExtension(file.FileName), out var mediaType))
                    mediaType = "application/octet-stream";
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                content.Add(streamContent, file.Name, file.FileName);
            }

            foreach (var para in formData)
            {
                content.Add(new StringContent(para.Value, Encoding.UTF8), para.Key);
            }

            return await client.PostAsync<TResult>(url, content);

        }
        #endregion


        #region PUT

        public static async Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, HttpContent data)
        {
            using var response = await client.PutAsync(url, data);
            return new ResponseResult<TResult>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result = await response.Content.ReadAsync<TResult>()
            };
        }

        public static async Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, string data, string contentType = "application/json")
        {
            using var content = new StringContent(data, Encoding.UTF8, contentType);
            return await client.PutAsync<TResult>(url, content);
        }

        public static async Task<ResponseResult<TResult>> PutAsync<TResult>(this System.Net.Http.HttpClient client, string url, object data, string contentType = "application/json")
        {
            using var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, contentType);
            return await client.PutAsync<TResult>(url, content);
        }

        #endregion

        #region DELETE
        public static async Task<ResponseResult<TResult>> DeleteAsync<TResult>(this System.Net.Http.HttpClient client, string url)
        {
            using var response = await client.DeleteAsync(url);
            return new ResponseResult<TResult>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result = await response.Content.ReadAsync<TResult>()
            };
        }

        #endregion

        #region HEAD

        public static async Task<ResponseResult<HttpContentHeaders>> HeadAsync(this System.Net.Http.HttpClient client, string url)
        {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            return new ResponseResult<HttpContentHeaders>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result = response.Content.Headers
            };
        }

        #endregion


        #region DOWNLOAD

        public static async Task DownloadAsync(this System.Net.Http.HttpClient client, string url, string saveFileName, Action<long, long, decimal> onProgressChanged = null, Action<long> onCompleted = null, Action<string> onError = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(saveFileName))
                throw new ArgumentNullException(nameof(saveFileName));
            var totalSize = 0L;
            var readLen = 0L;
            if (onProgressChanged != null)
            {
                var headResponse = await client.HeadAsync(url);
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
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                onError?.Invoke(response.StatusCode.ToString());
                return;
            }
            var dir = Path.GetDirectoryName(saveFileName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            using var fileStream = new FileStream(saveFileName, FileMode.Create);
            if (cancellationToken.IsCancellationRequested)
                return;
            using var stream = await response.Content.ReadAsStreamAsync();
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
                    onProgressChanged?.Invoke(totalSize, readLen, p);
                }
            }

            onCompleted?.Invoke(totalSize);
        }

        public static async Task<ResponseResult<Stream>> DownloadWithStreamAsync(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken = default)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var result = new ResponseResult<Stream>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result =response.IsSuccessStatusCode ? await response.Content.ReadAsStreamAsync() : default
            };
            if (result.Result.CanSeek)
                result.Result.Seek(0, SeekOrigin.Begin);
            return result;
        }


        public static async Task<ResponseResult<byte[]>> DownloadWithBytesAsync(this System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken = default)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var result = new ResponseResult<byte[]>
            {
                Succeed = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Result = response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : default
            };
            return result;
        }
        #endregion
    }
}
