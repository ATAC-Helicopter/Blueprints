namespace Blueprints.Core.Services;

public static class ItemKeyFormatter
{
    public static string FormatVersionScoped(string projectCode, int major, int minor, int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectCode);

        if (major < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major));
        }

        if (minor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minor));
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        return $"{projectCode}-{major}{minor}{sequence}";
    }

    public static string FormatProjectScoped(string prefix, int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        return $"{prefix}-{sequence}";
    }
}
