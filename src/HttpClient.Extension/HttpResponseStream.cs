using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClient.Extension
{
    // 调用方释放返回的流时，一并释放其所属的 HTTP 响应。
    internal sealed class HttpResponseStream : Stream
    {
        private readonly HttpResponseMessage _response;
        private readonly Stream _stream;

        internal HttpResponseStream(HttpResponseMessage response, Stream stream)
        {
            _response = response;
            _stream = stream;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override bool CanTimeout => _stream.CanTimeout;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => _stream.Position = value; }
        public override int ReadTimeout { get => _stream.ReadTimeout; set => _stream.ReadTimeout = value; }
        public override int WriteTimeout { get => _stream.WriteTimeout; set => _stream.WriteTimeout = value; }
        public override void Flush() => _stream.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _stream.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.ReadAsync(buffer, offset, count, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.WriteAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void SetLength(long value) => _stream.SetLength(value);
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            _stream.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _response.Dispose();
            base.Dispose(disposing);
        }
    }
}
