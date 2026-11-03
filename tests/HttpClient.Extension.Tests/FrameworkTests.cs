using System.Reflection;
using System.Runtime.Versioning;
using Xunit;

namespace HttpClient.Extension.Tests;

public class FrameworkTests
{
    [Fact]
    public void LoadsTheRequestedLibraryTargetFramework()
    {
        var tests = typeof(FrameworkTests).Assembly;
        var requested = tests.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "LibraryTargetFramework").Value;
        var expected = string.IsNullOrEmpty(requested)
            ? tests.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName
            : requested.StartsWith("netstandard", StringComparison.Ordinal)
                ? ".NETStandard,Version=v" + requested[11..]
                : ".NETCoreApp,Version=v" + requested[3..];

        Assert.Equal(expected, typeof(ResponseResult<>).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
    }
}
