using Typee.Core.Llm;

namespace Typee.Core.Tests;

public sealed class ApiKeyResolverTests
{
    [Fact]
    public void Resolve_PrefersCredMan_OverEnv()
    {
        var store = new MemoryApiKeyStore();
        store.Save("default", "credman-secret-key");
        var envName = "TYPEE_TEST_KEY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(envName, "env-secret-key");
        try
        {
            var resolver = new ApiKeyResolver(store);
            var key = resolver.Resolve("default", envName, out var source);
            Assert.Equal("credman", source);
            Assert.Equal("credman-secret-key", key);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public void Resolve_FallsBackToEnv_ThenNone()
    {
        var store = new MemoryApiKeyStore();
        var envName = "TYPEE_TEST_KEY_" + Guid.NewGuid().ToString("N");
        var resolver = new ApiKeyResolver(store);

        var none = resolver.Resolve("default", envName, out var sourceNone);
        Assert.Equal("none", sourceNone);
        Assert.Null(none);

        Environment.SetEnvironmentVariable(envName, "from-env");
        try
        {
            var key = resolver.Resolve("default", envName, out var source);
            Assert.Equal("env", source);
            Assert.Equal("from-env", key);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public void Mask_ShowsLast4Only()
    {
        Assert.Equal("cdef", ApiKeyMask.Last4("abcdefgh-cdef"));
        Assert.Equal("••••cdef", ApiKeyMask.FormatMasked("cdef"));
    }
}
