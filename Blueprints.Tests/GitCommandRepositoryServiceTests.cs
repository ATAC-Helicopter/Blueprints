using System.Diagnostics;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class GitCommandRepositoryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "GitRepositoryOperations",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CloneCommitPullAndPush_WorkAsExplicitRepositoryOperations()
    {
        var remote = Path.Combine(_root, "remote.git");
        var seed = Path.Combine(_root, "seed");
        var clones = Path.Combine(_root, "clones");
        Directory.CreateDirectory(clones);
        RunGit(_root, "init", "--bare", remote);
        RunGit(_root, "clone", remote, seed);
        ConfigureIdentity(seed);
        File.WriteAllText(Path.Combine(seed, "README.md"), "first\n");
        RunGit(seed, "add", "README.md");
        RunGit(seed, "commit", "-m", "Initial");
        RunGit(seed, "push", "-u", "origin", "HEAD");

        var service = new GitCommandRepositoryService();
        var clone = await service.CloneAsync(remote, clones, "working");

        Assert.True(clone.Success);
        Assert.True(File.Exists(Path.Combine(clone.RepositoryRoot, "README.md")));

        ConfigureIdentity(clone.RepositoryRoot);
        File.AppendAllText(Path.Combine(clone.RepositoryRoot, "README.md"), "local\n");
        var commit = await service.CommitAllAsync(clone.RepositoryRoot, "Local change");
        var push = await service.PushAsync(clone.RepositoryRoot);

        Assert.True(commit.Success);
        Assert.True(push.Success);

        RunGit(seed, "pull", "--ff-only");
        File.AppendAllText(Path.Combine(seed, "README.md"), "upstream\n");
        RunGit(seed, "add", "README.md");
        RunGit(seed, "commit", "-m", "Upstream change");
        RunGit(seed, "push");

        var pull = await service.PullAsync(clone.RepositoryRoot);

        Assert.True(pull.Success);
        Assert.Contains("upstream", File.ReadAllText(Path.Combine(clone.RepositoryRoot, "README.md")));
    }

    [Fact]
    public async Task CommitAll_DisablesRepositoryHooks()
    {
        var repository = CreateRepository("hooked");
        var hooks = Path.Combine(repository, ".git", "hooks");
        var hook = Path.Combine(hooks, "pre-commit");
        File.WriteAllText(hook, "#!/bin/sh\nexit 41\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                hook,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        File.WriteAllText(Path.Combine(repository, "change.txt"), "safe\n");
        var result = await new GitCommandRepositoryService()
            .CommitAllAsync(repository, "Hooks stay disabled");

        Assert.True(result.Success);
        Assert.Equal(
            "Hooks stay disabled",
            RunGit(repository, "log", "-1", "--pretty=%s").Trim());
    }

    [Fact]
    public async Task Pull_RejectsDirtyWorkingTreeBeforeNetworkMutation()
    {
        var repository = CreateRepository("dirty");
        File.AppendAllText(Path.Combine(repository, "README.md"), "dirty\n");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GitCommandRepositoryService().PullAsync(repository));

        Assert.Contains("local changes", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteOperations_RejectExecutableFilterConfiguration()
    {
        var repository = CreateRepository("filter");
        RunGit(repository, "config", "filter.danger.smudge", "dangerous-command");
        File.AppendAllText(Path.Combine(repository, "README.md"), "changed\n");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GitCommandRepositoryService().CommitAllAsync(repository, "Blocked"));

        Assert.Contains("executable filters", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateRepository(string name)
    {
        var repository = Path.Combine(_root, name);
        Directory.CreateDirectory(repository);
        RunGit(repository, "init");
        ConfigureIdentity(repository);
        File.WriteAllText(Path.Combine(repository, "README.md"), "initial\n");
        RunGit(repository, "add", "README.md");
        RunGit(repository, "commit", "-m", "Initial");
        return repository;
    }

    private static void ConfigureIdentity(string repository)
    {
        RunGit(repository, "config", "user.name", "Blueprints Test");
        RunGit(repository, "config", "user.email", "blueprints@example.invalid");
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        Directory.CreateDirectory(workingDirectory);
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Git for the test.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var file in Directory.EnumerateFiles(
                             _root,
                             "*",
                             SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
            }

            Directory.Delete(_root, recursive: true);
        }
    }
}
