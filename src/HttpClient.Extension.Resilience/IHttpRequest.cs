namespace HttpClient.Extension.Resilience
{
    public interface IHttpRequest
    {
        HttpRequestBuilder Create(string name = null);
    }
}