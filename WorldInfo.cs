namespace WindroseSaveManager;

public sealed record WorldInfo(
    string Id,
    string Name,
    string? GameName,
    string? Alias,
    string Version,
    string Path,
    bool IsActive,
    DateTimeOffset LastModified);

public sealed record SaveManagerState(
    string? ActiveWorldId,
    string? ActiveWorldName,
    ServerSettings Settings,
    string? ServerDescriptionPath,
    string? WorldsRoot,
    IReadOnlyList<WorldInfo> Worlds,
    IReadOnlyList<string> Warnings);

public sealed record ServerSettings(
    string? ServerName,
    bool IsPasswordProtected,
    bool HasPassword);

public sealed record OperationResult(bool Succeeded, string Message);
