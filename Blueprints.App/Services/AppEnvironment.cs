namespace Blueprints.App.Services;

public static class AppEnvironment
{
    public static string GetIdentityRoot()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataRoot, "Blueprints", "Identities");
    }
}
