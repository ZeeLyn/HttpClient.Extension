namespace HttpClient.Extension.Resilience
{
    public interface IHttpRequest
    {
        IHttpRequestBuilder Create(string name = null);
    }
}