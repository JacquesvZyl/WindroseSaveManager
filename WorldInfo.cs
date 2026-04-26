namespace WindroseSaveManager;

public sealed record WorldInfo(
    string Id,
    string Name,
    string? GameName,
    string? Alias,
    WorldSettings Settings,
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
    string? InviteCode,
    string? ServerName,
    int? MaxPlayerCount,
    string? P2pProxyAddress,
    bool IsPasswordProtected,
    bool HasPassword);

public sealed record WorldSettings(
    string PresetType,
    bool SharedQuests,
    bool EasyExplore,
    double MobHealthMultiplier,
    double MobDamageMultiplier,
    double ShipsHealthMultiplier,
    double ShipsDamageMultiplier,
    double BoardingDifficultyMultiplier,
    double CoopStatsCorrectionModifier,
    double CoopShipStatsCorrectionModifier,
    string CombatDifficulty);

public sealed record OperationResult(bool Succeeded, string Message);

public sealed record WorldArchive(string FileName, byte[] Content);
