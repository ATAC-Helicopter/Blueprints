namespace Blueprints.Storage.Abstractions;

public interface ICanonicalJsonSerializer
{
    string Serialize<T>(T value);

    T Deserialize<T>(string json);
}
