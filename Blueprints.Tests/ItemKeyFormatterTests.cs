using Blueprints.Core.Services;

namespace Blueprints.Tests;

public sealed class ItemKeyFormatterTests
{
    [Fact]
    public void FormatVersionScoped_UsesProjectCodeVersionAndSequence()
    {
        var key = ItemKeyFormatter.FormatVersionScoped("VS", 1, 5, 67);

        Assert.Equal("VS-1567", key);
    }

    [Fact]
    public void FormatProjectScoped_UsesPrefixAndSequence()
    {
        var key = ItemKeyFormatter.FormatProjectScoped("BUG", 1042);

        Assert.Equal("BUG-1042", key);
    }
}
