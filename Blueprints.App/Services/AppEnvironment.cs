namespace Blueprints.App.Services;

public static class AppEnvironment
{
    public static string GetAppRoot()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataRoot, "Blueprints");
    }

    public static string GetIdentityRoot()
    {
        return Path.Combine(GetAppRoot(), "Identities");
    }

    public static string GetWorkspaceRoot() =>
        Path.Combine(GetAppRoot(), "Workspace", "default");

    public static string GetSharedWorkspaceRoot() =>
        Path.Combine(GetAppRoot(), "Shared", "default");
}
