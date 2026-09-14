using System.Globalization;

namespace Devos.Protocol;

public sealed record DevosProtocolVersion(int Major, int Minor) : IComparable<DevosProtocolVersion>
{
    public static DevosProtocolVersion Current { get; } = new(1, 0);

    public static DevosProtocolVersion Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Protocol version cannot be empty.", nameof(value));
        }

        var parts = value.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            throw new FormatException($"Invalid DEVOS protocol version '{value}'. Expected major.minor.");
        }

        return new DevosProtocolVersion(major, minor);
    }

    public int CompareTo(DevosProtocolVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => $"{Major}.{Minor}";
}

public sealed record ProtocolCompatibilityResult(bool IsCompatible, string State, string? Reason = null);

public static class ProtocolNegotiator
{
    public static ProtocolCompatibilityResult Negotiate(string runtimeRequiredVersion, string providerSupportedVersion)
    {
        var required = DevosProtocolVersion.Parse(runtimeRequiredVersion);
        var supported = DevosProtocolVersion.Parse(providerSupportedVersion);

        if (required.Major != supported.Major)
        {
            return new ProtocolCompatibilityResult(false, "BRIDGE_UPDATE_REQUIRED", $"Major version mismatch. Runtime requires {required}; provider supports {supported}.");
        }

        if (supported.CompareTo(required) < 0)
        {
            return new ProtocolCompatibilityResult(false, "BRIDGE_UPDATE_REQUIRED", $"Provider protocol {supported} is older than required {required}.");
        }

        return new ProtocolCompatibilityResult(true, "CONNECTED");
    }
}
