using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace HttpClient.Extension.Tests;

public class FormTests
{
    [Theory]
    [InlineData("FORM")]
    [InlineData("PATH")]
    [InlineData("BYTES")]
    [InlineData("STREAM")]
    public async Task ObjectFormsOnlySendPublicInstanceReadableProperties(string method)
    {
        var fields = await SendFormAsync(method, new ObjectForm());

        Assert.Equal("value", Assert.Single(fields).Value);
        Assert.Equal("Name", Assert.Single(fields).Key);
    }

    [Theory]
    [InlineData("FORM")]
    [InlineData("PATH")]
    [InlineData("BYTES")]
    [InlineData("STREAM")]
    public async Task DictionariesPassedAsObjectKeepTheirFields(string method)
    {
        object form = new Dictionary<string, string> { ["name"] = "a&b+中文", ["empty"] = null! };

        var fields = await SendFormAsync(method, form);

        Assert.Equal(2, fields.Count);
        Assert.Equal("a&b+中文", fields["name"]);
        Assert.Empty(fields["empty"]);
    }

    [Theory]
    [InlineData("FORM")]
    [InlineData("PATH")]
    [InlineData("BYTES")]
    [InlineData("STREAM")]
    public async Task ObjectFormNumbersUseInvariantCulture(string method)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var fields = await SendFormAsync(method, new { amount = 1.5m, text = "1,5", empty = (int?)null });

            Assert.Equal("1.5", fields["amount"]);
            Assert.Equal("1,5", fields["text"]);
            Assert.Empty(fields["empty"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static async Task<Dictionary<string, string>> SendFormAsync(string method, object form)
    {
        var fields = new Dictionary<string, string>();
        using var client = RequestTests.CreateClient(async request =>
        {
            var content = request.Content!;
            if (method == "FORM")
                foreach (var field in QueryHelpers.ParseQuery(await content.ReadAsStringAsync()))
                    fields.Add(field.Key, field.Value.ToString());
            else
            {
                var boundary = content.Headers.ContentType!.Parameters.Single(p => p.Name == "boundary").Value!.Trim('"');
                var reader = new MultipartReader(boundary, await content.ReadAsStreamAsync());
                MultipartSection? section;
                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    var disposition = System.Net.Http.Headers.ContentDispositionHeaderValue.Parse(section.ContentDisposition!);
                    fields.Add(disposition.Name!.Trim('"'), await new StreamReader(section.Body).ReadToEndAsync());
                }
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });

        await (method switch
        {
            "FORM" => client.PostFormAsync<string>("/", form),
            "PATH" => client.PostMultipartAsync<string>("/", form, Array.Empty<string>()),
            "BYTES" => client.PostMultipartAsync<string>("/", form, Array.Empty<MultipartFormFileBytesData>()),
            _ => client.PostMultipartAsync<string>("/", form, Array.Empty<MultipartFormFileStreamData>())
        });
        return fields;
    }

    private sealed class ObjectForm
    {
        public string Name => "value";
        public static string Static => throw new InvalidOperationException("static getter must not run");
        public string this[int index] => throw new InvalidOperationException("indexer must not run");
        public string PrivateGetter { private get => throw new InvalidOperationException("private getter must not run"); set { } }
        public string WriteOnly { set { } }
    }
}
