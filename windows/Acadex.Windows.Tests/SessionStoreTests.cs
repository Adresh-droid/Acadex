using Acadex.Windows.Services;
using Xunit;

namespace Acadex.Windows.Tests;

public sealed class SessionStoreTests
{
    [Fact]
    public async Task SaveLoadAndDeleteRoundTrip()
    {
        var store = new SessionStore();
        store.Delete();
        var token = "test-token-" + Guid.NewGuid().ToString("N");
        await store.SaveAsync(token);
        Assert.Equal(token, await store.LoadAsync());
        store.Delete();
        Assert.Null(await store.LoadAsync());
    }
}
