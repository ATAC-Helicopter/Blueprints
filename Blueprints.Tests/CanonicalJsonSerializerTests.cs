using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class CanonicalJsonSerializerTests
{
    [Fact]
    public void Serialize_SortsObjectPropertiesDeterministically()
    {
        var serializer = new CanonicalJsonSerializer();
        var value = new TestPayload(
            Name: "VaultSync",
            Count: 2,
            Nested: new NestedPayload(
                Beta: "b",
                Alpha: "a"));

        var json = serializer.Serialize(value);

        Assert.Equal(
            """{"count":2,"name":"VaultSync","nested":{"alpha":"a","beta":"b"}}""",
            json);
    }

    private sealed record TestPayload(
        string Name,
        int Count,
        NestedPayload Nested);

    private sealed record NestedPayload(
        string Beta,
        string Alpha);
}
