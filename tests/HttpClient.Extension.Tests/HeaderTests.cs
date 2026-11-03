using Xunit;

namespace HttpClient.Extension.Tests;

public class HeaderTests
{
    [Fact]
    public void AcceptSupportsMediaTypeListsAndQualityValues()
    {
        using var client = new System.Net.Http.HttpClient();

        Assert.Same(client, client.Accept("application/json;q=0.8, text/plain"));
        client.Accept("application/xml");

        var values = client.DefaultRequestHeaders.Accept.ToArray();
        Assert.Equal(3, values.Length);
        Assert.Equal("application/json", values[0].MediaType);
        Assert.Equal(0.8, values[0].Quality);
        Assert.Equal("text/plain", values[1].MediaType);
        Assert.Equal("application/xml", values[2].MediaType);
    }

    [Theory]
    [InlineData("not-a-media-type")]
    [InlineData("application/json\r\nX-Injected: yes")]
    public void AcceptRejectsInvalidHeaders(string value)
    {
        using var client = new System.Net.Http.HttpClient();

        Assert.Throws<FormatException>(() => client.Accept(value));
        Assert.Empty(client.DefaultRequestHeaders.Accept);
    }
}
