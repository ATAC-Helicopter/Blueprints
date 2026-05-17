using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class GitCommandLocalGitRepositoryInspector : ILocalGitRepositoryInspector
{
    public LocalGitRepositoryStatus Inspect(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var normalizedPath = Path.GetFullPath(repositoryPath.Trim());
        if (!Directory.Exists(normalizedPath))
        {
            return new LocalGitRepositoryStatus(
                false,
                normalizedPath,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                [],
                "Configured path does not exist.");
        }

        var root = RunGit(normalizedPath, "rev-parse --show-toplevel", allowFailure: true);
        if (!root.Success || string.IsNullOrWhiteSpace(root.Output))
        {
            return new LocalGitRepositoryStatus(
                false,
                normalizedPath,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                [],
                string.IsNullOrWhiteSpace(root.Error) ? "Configured path is not a Git repository." : root.Error);
        }

        var repositoryRoot = root.Output.Trim();
        var branch = RunGit(repositoryRoot, "branch --show-current", allowFailure: true).Output.Trim();
        if (string.IsNullOrWhiteSpace(branch))
        {
            branch = RunGit(repositoryRoot, "rev-parse --short HEAD", allowFailure: true).Output.Trim();
        }

        var remoteUrl = RunGit(repositoryRoot, "config --get remote.origin.url", allowFailure: true).Output.Trim();
        var latestTagResult = RunGit(repositoryRoot, "describe --tags --abbrev=0", allowFailure: true);
        var latestTag = latestTagResult.Output.Trim();
        var status = RunGit(repositoryRoot, "status --porcelain", allowFailure: true).Output;
        var isDirty = !string.IsNullOrWhiteSpace(status);
        var recentChanges = ReadRecentChanges(repositoryRoot, latestTag);

        return new LocalGitRepositoryStatus(
            true,
            repositoryRoot,
            string.IsNullOrWhiteSpace(branch) ? "(detached or unknown)" : branch,
            string.IsNullOrWhiteSpace(remoteUrl) ? "(no origin remote)" : remoteUrl,
            isDirty,
            string.IsNullOrWhiteSpace(latestTag) ? "(no tags)" : latestTag,
            recentChanges,
            isDirty ? "Repository has uncommitted changes." : "Repository working tree is clean.");
    }

    private static IReadOnlyList<SourceChangeSummary> ReadRecentChanges(
        string repositoryRoot,
        string latestTag)
    {
        var range = string.IsNullOrWhiteSpace(latestTag) ? string.Empty : $"{latestTag}..HEAD ";
        var log = RunGit(
            repositoryRoot,
            $"log {range}--max-count=20 --date=iso-strict --pretty=format:%H%x1f%an%x1f%aI%x1f%s%x1e",
            allowFailure: true);
        if (!log.Success || string.IsNullOrWhiteSpace(log.Output))
        {
            return [];
        }

        return log.Output
            .Split('\u001e', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseChange)
            .Where(static change => change is not null)
            .Cast<SourceChangeSummary>()
            .ToArray();
    }

    private static SourceChangeSummary? ParseChange(string rawEntry)
    {
        var parts = rawEntry.Split('\u001f');
        if (parts.Length < 4)
        {
            return null;
        }

        var commitHash = parts[0].Trim();
        var authorName = parts[1].Trim();
        var committedUtc = DateTimeOffset.TryParse(
            parts[2].Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsedDate)
            ? parsedDate.ToUniversalTime()
            : DateTimeOffset.MinValue;
        var subject = parts[3].Trim();

        return new SourceChangeSummary(
            commitHash,
            commitHash.Length <= 7 ? commitHash : commitHash[..7],
            subject,
            authorName,
            committedUtc,
            ExtractItemKeys(subject));
    }

    private static IReadOnlyList<string> ExtractItemKeys(string text) =>
        Regex.Matches(text, @"\b[A-Z][A-Z0-9]+-\d+\b")
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static GitCommandResult RunGit(string workingDirectory, string arguments, bool allowFailure)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git", $"-C \"{workingDirectory}\" {arguments}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start git.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 && !allowFailure)
            {
                throw new InvalidOperationException(error.Trim());
            }

            return new GitCommandResult(process.ExitCode == 0, output.Trim(), error.Trim());
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new GitCommandResult(false, string.Empty, $"Git command failed: {exception.Message}");
        }
    }

    private sealed record GitCommandResult(bool Success, string Output, string Error);
}
