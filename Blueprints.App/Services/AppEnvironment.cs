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

    public static string GetLocalPrivateKeyProtectionKeyPath() =>
        Path.Combine(GetAppRoot(), "Security", "local-private-key-protector.key");

    public static string GetWorkspaceRoot() =>
        Path.Combine(GetAppRoot(), "Workspace", "default");

    public static string GetSharedWorkspaceRoot() =>
        Path.Combine(GetAppRoot(), "Shared", "default");

    public static string GetWorkspaceCatalogRoot() =>
        Path.Combine(GetAppRoot(), "Workspaces");

    public static string GetSharedProjectsRoot() =>
        Path.Combine(GetAppRoot(), "SharedProjects");

    public static string GetRecentProjectsPath() =>
        Path.Combine(GetAppRoot(), "recent-projects.json");

    public static string GetIntegrationSettingsPath() =>
        Path.Combine(GetAppRoot(), "integration-settings.json");
}
