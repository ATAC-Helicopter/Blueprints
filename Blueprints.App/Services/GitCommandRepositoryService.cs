using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed partial class GitCommandRepositoryService : IGitRepositoryService
{
    private const int MaximumOutputCharacters = 256 * 1024;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);
    private readonly ILocalGitRepositoryInspector _inspector;

    public GitCommandRepositoryService()
        : this(new GitCommandLocalGitRepositoryInspector())
    {
    }

    public GitCommandRepositoryService(ILocalGitRepositoryInspector inspector)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        _inspector = inspector;
    }

    public LocalGitRepositoryStatus Inspect(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        return _inspector.Inspect(repositoryPath);
    }

    public async Task<GitRepositoryOperationResult> CloneAsync(
        string remote,
        string destinationParent,
        string? folderName,
        CancellationToken cancellationToken = default)
    {
        var normalizedRemote = ValidateRemote(remote);
        var parent = Path.GetFullPath(
            string.IsNullOrWhiteSpace(destinationParent)
                ? throw new InvalidOperationException("Choose where the repository should be cloned.")
                : destinationParent.Trim());
        if (!Directory.Exists(parent))
        {
            throw new InvalidOperationException("The clone destination folder does not exist.");
        }

        var resolvedName = ResolveCloneFolderName(normalizedRemote, folderName);
        var destination = Path.GetFullPath(Path.Combine(parent, resolvedName));
        EnsureDirectChild(parent, destination);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new InvalidOperationException(
                "The clone destination already exists. Choose another name or open that folder instead.");
        }

        var clone = await RunGitAsync(
            parent,
            ["clone", "--no-checkout", "--no-recurse-submodules", "--", normalizedRemote, destination],
            cancellationToken);
        EnsureSuccess(clone, "Clone failed");

        EnsureNoExecutableRepositoryConfiguration(destination);
        var checkout = await RunGitAsync(
            destination,
            ["checkout", "--force"],
            cancellationToken);
        EnsureSuccess(checkout, "The repository was downloaded, but its working files could not be checked out");

        var status = _inspector.Inspect(destination);
        return new GitRepositoryOperationResult(
            true,
            $"Cloned {DisplayRemote(normalizedRemote)} into {destination}. Submodules were not initialized and repository hooks were not run.",
            destination,
            status);
    }

    public async Task<GitRepositoryOperationResult> PullAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        var status = RequireRepository(repositoryPath);
        if (status.IsDirty)
        {
            throw new InvalidOperationException(
                "Pull is blocked because this repository has local changes. Commit or discard them in your Git tool first.");
        }

        EnsureSafeRemote(status.RemoteUrl);
        EnsureNoExecutableRepositoryConfiguration(status.RepositoryRoot);
        var result = await RunGitAsync(
            status.RepositoryRoot,
            ["pull", "--ff-only", "--no-recurse-submodules"],
            cancellationToken);
        EnsureSuccess(result, "Pull failed");
        var refreshed = _inspector.Inspect(status.RepositoryRoot);
        return new GitRepositoryOperationResult(
            true,
            "Pulled the upstream branch with fast-forward-only safety. Hooks and submodule recursion were disabled.",
            status.RepositoryRoot,
            refreshed);
    }

    public async Task<GitRepositoryOperationResult> CommitAllAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default)
    {
        var status = RequireRepository(repositoryPath);
        var normalizedMessage = message?.Trim() ?? string.Empty;
        if (normalizedMessage.Length is < 1 or > 200
            || normalizedMessage.Any(static character => char.IsControl(character)))
        {
            throw new InvalidOperationException(
                "Enter a one-line commit message between 1 and 200 characters.");
        }

        if (!status.IsDirty)
        {
            throw new InvalidOperationException("There are no local changes to commit.");
        }

        EnsureNoExecutableRepositoryConfiguration(status.RepositoryRoot);
        var add = await RunGitAsync(
            status.RepositoryRoot,
            ["add", "--all", "--", "."],
            cancellationToken);
        EnsureSuccess(add, "Could not stage the repository changes");

        var commit = await RunGitAsync(
            status.RepositoryRoot,
            ["-c", "commit.gpgSign=false", "commit", "--no-verify", "-m", normalizedMessage],
            cancellationToken);
        EnsureSuccess(commit, "Commit failed; the selected changes remain staged");
        var refreshed = _inspector.Inspect(status.RepositoryRoot);
        return new GitRepositoryOperationResult(
            true,
            $"Committed all tracked and untracked changes on {refreshed.Branch}. Repository hooks and commit signing were disabled for this operation.",
            status.RepositoryRoot,
            refreshed);
    }

    public async Task<GitRepositoryOperationResult> PushAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        var status = RequireRepository(repositoryPath);
        EnsureSafeRemote(status.RemoteUrl);
        EnsureNoExecutableRepositoryConfiguration(status.RepositoryRoot);

        IReadOnlyList<string> arguments = status.HasUpstream
            ? ["push", "--no-verify"]
            : ["push", "--no-verify", "--set-upstream", "origin", ValidateBranch(status.Branch)];
        var result = await RunGitAsync(status.RepositoryRoot, arguments, cancellationToken);
        EnsureSuccess(result, "Push failed");
        var refreshed = _inspector.Inspect(status.RepositoryRoot);
        return new GitRepositoryOperationResult(
            true,
            $"Pushed {refreshed.Branch} to origin. Repository hooks were disabled.",
            status.RepositoryRoot,
            refreshed);
    }

    private LocalGitRepositoryStatus RequireRepository(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        var status = Inspect(repositoryPath);
        if (!status.IsRepository)
        {
            throw new InvalidOperationException(status.Summary);
        }

        return status;
    }

    private static void EnsureNoExecutableRepositoryConfiguration(string repositoryRoot)
    {
        var risky = RunGit(
            repositoryRoot,
            ["config", "--local", "--get-regexp", @"^(filter\..*\.(clean|smudge|process)|merge\..*\.driver|core\.fsmonitor)$"],
            allowFailure: true);
        if (risky.Success && !string.IsNullOrWhiteSpace(risky.Output))
        {
            throw new InvalidOperationException(
                "This repository configures executable filters, merge drivers, or file monitors. " +
                "Blueprints will not run Git write operations because those commands could execute repository-controlled programs.");
        }
    }

    private static string ValidateRemote(string remote)
    {
        var value = remote?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 2048
            || value[0] == '-'
            || value.Any(static character => char.IsControl(character)))
        {
            throw new InvalidOperationException("Enter a valid HTTPS, SSH, or local repository address.");
        }

        if (Path.IsPathFullyQualified(value))
        {
            if (!Directory.Exists(value))
            {
                throw new InvalidOperationException("The local repository address does not exist.");
            }

            return Path.GetFullPath(value);
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "ssh" or "git"
            && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return value;
        }

        if (ScpRemotePattern().IsMatch(value))
        {
            return value;
        }

        throw new InvalidOperationException(
            "Only HTTPS, SSH, Git protocol, SCP-style SSH, and existing absolute local repository addresses are supported.");
    }

    private static void EnsureSafeRemote(string remote)
    {
        if (remote == "(no origin remote)")
        {
            throw new InvalidOperationException("This repository has no origin remote.");
        }

        _ = ValidateRemote(remote);
    }

    private static string ResolveCloneFolderName(string remote, string? requestedName)
    {
        var name = string.IsNullOrWhiteSpace(requestedName)
            ? Path.GetFileNameWithoutExtension(remote.TrimEnd('/', '\\'))
            : requestedName.Trim();
        if (name.Length is < 1 or > 120
            || name is "." or ".."
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains(Path.DirectorySeparatorChar)
            || name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Enter a simple destination folder name.");
        }

        return name;
    }

    private static void EnsureDirectChild(string parent, string destination)
    {
        var relative = Path.GetRelativePath(parent, destination);
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            || relative.Contains(Path.DirectorySeparatorChar)
            || relative.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("The clone destination must be directly inside the chosen folder.");
        }
    }

    private static string ValidateBranch(string branch)
    {
        if (string.IsNullOrWhiteSpace(branch)
            || branch.StartsWith('(')
            || !SafeBranchPattern().IsMatch(branch))
        {
            throw new InvalidOperationException(
                "Create or select a named local branch before pushing this repository.");
        }

        return branch;
    }

    private static async Task<GitCommandResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var hooksPath = Path.Combine(
            Path.GetTempPath(),
            "Blueprints",
            "disabled-git-hooks",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(hooksPath);
        try
        {
            var completeArguments = new List<string>
            {
                "-c",
                $"core.hooksPath={hooksPath}",
                "-c",
                "protocol.ext.allow=never",
            };
            completeArguments.AddRange(arguments);
            return await RunProcessAsync(workingDirectory, completeArguments, cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(hooksPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static GitCommandResult RunGit(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        bool allowFailure)
    {
        var result = RunProcessAsync(
                workingDirectory,
                arguments,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!result.Success && !allowFailure)
        {
            throw new InvalidOperationException(result.Error);
        }

        return result;
    }

    private static async Task<GitCommandResult> RunProcessAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Git could not be started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        var outputTask = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        var errorTask = ReadBoundedAsync(process.StandardError, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new InvalidOperationException(
                cancellationToken.IsCancellationRequested
                    ? "The Git operation was cancelled."
                    : "The Git operation timed out after five minutes.");
        }

        var output = await outputTask;
        var error = await errorTask;
        return new GitCommandResult(process.ExitCode == 0, output.Trim(), error.Trim());
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var output = new StringBuilder();
        while (true)
        {
            var count = await reader.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                break;
            }

            if (output.Length + count > MaximumOutputCharacters)
            {
                throw new InvalidOperationException("Git produced more output than Blueprints will accept.");
            }

            output.Append(buffer, 0, count);
        }

        return output.ToString();
    }

    private static void EnsureSuccess(GitCommandResult result, string context)
    {
        if (result.Success)
        {
            return;
        }

        var detail = string.IsNullOrWhiteSpace(result.Error)
            ? "Git did not provide an error message."
            : result.Error;
        throw new InvalidOperationException($"{context}: {detail}");
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string DisplayRemote(string remote) =>
        remote.Length <= 100 ? remote : $"{remote[..97]}…";

    [GeneratedRegex(@"^(?:[^@\s/:]+@)?[^@\s/:]+:[^:\s]+$")]
    private static partial Regex ScpRemotePattern();

    [GeneratedRegex(@"^[A-Za-z0-9._/-]+$")]
    private static partial Regex SafeBranchPattern();

    private sealed record GitCommandResult(bool Success, string Output, string Error);
}
