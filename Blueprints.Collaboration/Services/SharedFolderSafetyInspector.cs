using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Blueprints.Collaboration.Models;

namespace Blueprints.Collaboration.Services;

public sealed class SharedFolderSafetyInspector
{
    public SharedFolderSafetyReport Inspect(string sharedProjectRoot, string? localWorkspaceRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedProjectRoot);

        var findings = new List<SharedFolderSafetyFinding>();
        var sharedFullPath = Normalize(sharedProjectRoot);

        if (!string.IsNullOrWhiteSpace(localWorkspaceRoot))
        {
            var localFullPath = Normalize(localWorkspaceRoot);
            if (PathsOverlap(sharedFullPath, localFullPath))
            {
                findings.Add(new SharedFolderSafetyFinding(
                    "path-overlap",
                    "Error",
                    "The shared sync folder must not be the local workspace or one of its child folders."));
            }
        }

        if (!Directory.Exists(sharedFullPath))
        {
            findings.Add(new SharedFolderSafetyFinding(
                "missing-folder",
                "Warning",
                "The shared sync folder does not exist yet, so permissions could not be inspected."));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddWindowsAclFindings(sharedFullPath, findings);
        }
        else
        {
            findings.Add(new SharedFolderSafetyFinding(
                "acl-check-unavailable",
                "Warning",
                "Windows ACL safety checks are unavailable on this operating system."));
        }

        return new SharedFolderSafetyReport(
            !findings.Any(static finding => string.Equals(finding.Severity, "Error", StringComparison.Ordinal)),
            findings);
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsAclFindings(string sharedFullPath, List<SharedFolderSafetyFinding> findings)
    {
        try
        {
            var security = new DirectoryInfo(sharedFullPath).GetAccessControl(AccessControlSections.Access);
            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.AccessControlType != AccessControlType.Allow || !HasWriteRights(rule.FileSystemRights))
                {
                    continue;
                }

                if (IsBroadPrincipal((SecurityIdentifier)rule.IdentityReference))
                {
                    findings.Add(new SharedFolderSafetyFinding(
                        "broad-write-acl",
                        "Warning",
                        $"Shared folder grants write access to a broad principal ({rule.IdentityReference.Value})."));
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SystemException)
        {
            findings.Add(new SharedFolderSafetyFinding(
                "acl-check-failed",
                "Warning",
                $"Shared folder permissions could not be inspected: {exception.Message}"));
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool HasWriteRights(FileSystemRights rights) =>
        rights.HasFlag(FileSystemRights.FullControl)
        || rights.HasFlag(FileSystemRights.Modify)
        || rights.HasFlag(FileSystemRights.Write)
        || rights.HasFlag(FileSystemRights.WriteData)
        || rights.HasFlag(FileSystemRights.CreateFiles)
        || rights.HasFlag(FileSystemRights.CreateDirectories);

    [SupportedOSPlatform("windows")]
    private static bool IsBroadPrincipal(SecurityIdentifier sid) =>
        sid.IsWellKnown(WellKnownSidType.WorldSid)
        || sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid)
        || sid.IsWellKnown(WellKnownSidType.AnonymousSid)
        || sid.IsWellKnown(WellKnownSidType.BuiltinUsersSid);

    private static bool PathsOverlap(string firstPath, string secondPath) =>
        IsSameOrChild(firstPath, secondPath) || IsSameOrChild(secondPath, firstPath);

    private static bool IsSameOrChild(string candidate, string root)
    {
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(candidate, root, comparison)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
