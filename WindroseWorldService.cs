using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace WindroseSaveManager;

public sealed class WindroseWorldService
{
    private readonly WindroseOptions _options;
    private readonly CommandRunner _commands;
    private readonly ILogger<WindroseWorldService> _logger;

    public WindroseWorldService(
        IOptions<WindroseOptions> options,
        CommandRunner commands,
        ILogger<WindroseWorldService> logger)
    {
        _options = options.Value;
        _commands = commands;
        _logger = logger;
    }

    public async Task<SaveManagerState> GetStateAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var serverDescriptionPath = GetServerDescriptionPath();
        var activeWorldId = File.Exists(serverDescriptionPath)
            ? await ReadJsonValueAsync(serverDescriptionPath, "WorldIslandId", cancellationToken)
            : null;
        var settings = File.Exists(serverDescriptionPath)
            ? await ReadServerSettingsAsync(serverDescriptionPath, cancellationToken)
            : new ServerSettings(null, false, false);

        if (!File.Exists(serverDescriptionPath))
        {
            warnings.Add($"ServerDescription.json was not found at {serverDescriptionPath}.");
        }

        var worldsRoot = GetWorldsRoot();
        if (worldsRoot is null)
        {
            warnings.Add($"No Worlds folder was found under {_options.ServerRoot}/R5/Saved/SaveProfiles/Default/RocksDB.");
            return new SaveManagerState(activeWorldId, null, settings, serverDescriptionPath, null, [], warnings);
        }

        var worlds = new List<WorldInfo>();
        var labels = await ReadLabelsAsync(cancellationToken);
        foreach (var worldDirectory in Directory.GetDirectories(worldsRoot).OrderBy(Path.GetFileName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = Path.GetFileName(worldDirectory);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var descriptionPath = Path.Combine(worldDirectory, "WorldDescription.json");
            var name = File.Exists(descriptionPath)
                ? await ReadJsonValueAsync(descriptionPath, "WorldName", cancellationToken)
                : null;
            labels.TryGetValue(id, out var alias);
            var displayName = string.IsNullOrWhiteSpace(alias) ? name : alias;

            worlds.Add(new WorldInfo(
                id,
                string.IsNullOrWhiteSpace(displayName) ? id : displayName,
                string.IsNullOrWhiteSpace(name) ? null : name,
                string.IsNullOrWhiteSpace(alias) ? null : alias,
                Path.GetFileName(Path.GetDirectoryName(worldsRoot)) ?? "unknown",
                worldDirectory,
                string.Equals(id, activeWorldId, StringComparison.OrdinalIgnoreCase),
                Directory.GetLastWriteTimeUtc(worldDirectory)));
        }

        var activeWorldName = worlds.FirstOrDefault(world => world.IsActive)?.Name;
        return new SaveManagerState(activeWorldId, activeWorldName, settings, serverDescriptionPath, worldsRoot, worlds, warnings);
    }

    public async Task<OperationResult> UpdateServerSettingsAsync(
        string? serverName,
        bool updateServerName,
        string? password,
        bool updatePassword,
        bool clearPassword,
        CancellationToken cancellationToken)
    {
        if (!updateServerName && !updatePassword && !clearPassword)
        {
            return new OperationResult(false, "Choose at least one setting to update.");
        }

        if (updatePassword && clearPassword)
        {
            return new OperationResult(false, "Choose either set password or clear password, not both.");
        }

        if (updatePassword && string.IsNullOrWhiteSpace(password))
        {
            return new OperationResult(false, "Enter a password, or choose clear password.");
        }

        var serverDescriptionPath = GetServerDescriptionPath();
        if (!File.Exists(serverDescriptionPath))
        {
            return new OperationResult(false, $"ServerDescription.json was not found at {serverDescriptionPath}.");
        }

        if (_options.ManageContainer)
        {
            var stop = await RunComposeAsync("stop", cancellationToken);
            if (!stop.Succeeded)
            {
                return new OperationResult(false, $"Could not stop the Windrose container. {stop.ErrorOrOutput}");
            }
        }

        try
        {
            var backupPath = await BackupServerDescriptionAsync(cancellationToken);
            var root = await ReadJsonAsync(serverDescriptionPath, cancellationToken);

            if (updateServerName)
            {
                SetServerDescriptionValue(root, "ServerName", serverName?.Trim() ?? string.Empty);
            }

            if (clearPassword)
            {
                SetServerDescriptionValue(root, "Password", string.Empty);
                SetServerDescriptionValue(root, "IsPasswordProtected", false);
            }
            else if (updatePassword)
            {
                SetServerDescriptionValue(root, "Password", password!.Trim());
                SetServerDescriptionValue(root, "IsPasswordProtected", true);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(serverDescriptionPath, root.ToJsonString(options), cancellationToken);

            if (_options.ManageContainer)
            {
                var start = await RunComposeAsync("up -d", cancellationToken);
                if (!start.Succeeded)
                {
                    return new OperationResult(false, $"Settings were updated, but the container did not start. {start.ErrorOrOutput}");
                }
            }

            return new OperationResult(true, $"Server settings saved. Backup saved to {backupPath}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update server settings");
            return new OperationResult(false, ex.Message);
        }
    }

    public async Task<OperationResult> ImportWorldZipAsync(IFormFile? zipFile, CancellationToken cancellationToken)
    {
        if (zipFile is null || zipFile.Length == 0)
        {
            return new OperationResult(false, "Choose a Windrose world zip first.");
        }

        if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(false, "Only .zip files are supported.");
        }

        var worldsRoot = GetWorldsRoot();
        if (worldsRoot is null)
        {
            return new OperationResult(false, "No Worlds folder was found on the server.");
        }

        try
        {
            await using var stream = zipFile.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var fileEntries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToArray();

            var topFolders = fileEntries
                .Select(entry => NormalizeZipPath(entry.FullName).Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (topFolders.Length != 1)
            {
                return new OperationResult(false, "The zip must contain exactly one top-level world folder.");
            }

            var worldId = topFolders[0]!;
            var descriptionEntry = fileEntries.FirstOrDefault(entry =>
                NormalizeZipPath(entry.FullName).Equals($"{worldId}/WorldDescription.json", StringComparison.OrdinalIgnoreCase));

            if (descriptionEntry is null)
            {
                return new OperationResult(false, "WorldDescription.json was not found inside the top-level world folder.");
            }

            await using (var descriptionStream = descriptionEntry.Open())
            {
                var descriptionRoot = await JsonNode.ParseAsync(descriptionStream, cancellationToken: cancellationToken);
                var islandId = TryFindJsonValue(descriptionRoot, "islandId") ?? TryFindJsonValue(descriptionRoot, "IslandId");
                if (!string.IsNullOrWhiteSpace(islandId) &&
                    !string.Equals(islandId, worldId, StringComparison.OrdinalIgnoreCase))
                {
                    return new OperationResult(false, $"The zip folder is '{worldId}', but WorldDescription.json says IslandId is '{islandId}'.");
                }
            }

            var destinationRoot = Path.GetFullPath(Path.Combine(worldsRoot, worldId));
            if (Directory.Exists(destinationRoot))
            {
                return new OperationResult(false, $"World '{worldId}' already exists on the server.");
            }

            var worldsRootFull = Path.GetFullPath(worldsRoot);
            foreach (var entry in fileEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.GetFullPath(Path.Combine(worldsRoot, NormalizeZipPath(entry.FullName)));
                if (!destination.StartsWith(worldsRootFull, StringComparison.OrdinalIgnoreCase))
                {
                    return new OperationResult(false, "The zip contains an unsafe file path.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var source = entry.Open();
                await using var target = File.Create(destination);
                await source.CopyToAsync(target, cancellationToken);
            }

            var importedName = await ReadJsonValueAsync(Path.Combine(destinationRoot, "WorldDescription.json"), "WorldName", cancellationToken);
            return new OperationResult(true, $"Imported {importedName ?? worldId}.");
        }
        catch (InvalidDataException ex)
        {
            return new OperationResult(false, $"The uploaded zip could not be read: {ex.Message}");
        }
    }

    public async Task<OperationResult> SetAliasAsync(string worldId, string? alias, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worldId))
        {
            return new OperationResult(false, "Choose a world first.");
        }

        var state = await GetStateAsync(cancellationToken);
        if (!state.Worlds.Any(world => string.Equals(world.Id, worldId, StringComparison.OrdinalIgnoreCase)))
        {
            return new OperationResult(false, $"World '{worldId}' was not found.");
        }

        var labels = await ReadLabelsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(alias))
        {
            labels.Remove(worldId);
        }
        else
        {
            labels[worldId] = alias.Trim();
        }

        await WriteLabelsAsync(labels, cancellationToken);
        return new OperationResult(true, string.IsNullOrWhiteSpace(alias) ? "Label removed." : "Label saved.");
    }

    public async Task<OperationResult> ActivateWorldAsync(string worldId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worldId))
        {
            return new OperationResult(false, "Choose a world first.");
        }

        var state = await GetStateAsync(cancellationToken);
        var target = state.Worlds.FirstOrDefault(world =>
            string.Equals(world.Id, worldId, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return new OperationResult(false, $"World '{worldId}' was not found.");
        }

        if (target.IsActive)
        {
            return new OperationResult(true, $"{target.Name} is already active.");
        }

        if (_options.ManageContainer)
        {
            var stop = await RunComposeAsync("stop", cancellationToken);
            if (!stop.Succeeded)
            {
                return new OperationResult(false, $"Could not stop the Windrose container. {stop.ErrorOrOutput}");
            }
        }

        try
        {
            var backupPath = await BackupCurrentAsync(state, cancellationToken);
            await SetActiveWorldIdAsync(target.Id, cancellationToken);

            if (_options.ManageContainer)
            {
                var start = await RunComposeAsync("up -d", cancellationToken);
                if (!start.Succeeded)
                {
                    return new OperationResult(false, $"World was switched, but the container did not start. {start.ErrorOrOutput}");
                }
            }

            return new OperationResult(true, $"Activated {target.Name}. Backup saved to {backupPath}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate world {WorldId}", target.Id);
            return new OperationResult(false, ex.Message);
        }
    }

    public async Task<OperationResult> BackupCurrentAsync(CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(cancellationToken);
        var backupPath = await BackupCurrentAsync(state, cancellationToken);
        return new OperationResult(true, $"Backup saved to {backupPath}.");
    }

    private async Task<string> BackupCurrentAsync(SaveManagerState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.BackupsRoot);

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var activeName = SanitizeFileName(state.ActiveWorldName ?? state.ActiveWorldId ?? "unknown-world");
        var backupPath = Path.Combine(_options.BackupsRoot, $"{stamp}-{activeName}");
        Directory.CreateDirectory(backupPath);

        if (state.ServerDescriptionPath is not null && File.Exists(state.ServerDescriptionPath))
        {
            File.Copy(state.ServerDescriptionPath, Path.Combine(backupPath, "ServerDescription.json"), overwrite: false);
        }

        var activeWorld = state.Worlds.FirstOrDefault(world => world.IsActive);
        if (activeWorld is not null)
        {
            CopyDirectory(activeWorld.Path, Path.Combine(backupPath, activeWorld.Id), cancellationToken);
        }

        await File.WriteAllTextAsync(
            Path.Combine(backupPath, "backup-info.txt"),
            $"CreatedUtc={DateTimeOffset.UtcNow:O}{Environment.NewLine}ActiveWorldId={state.ActiveWorldId}{Environment.NewLine}",
            cancellationToken);

        return backupPath;
    }

    private async Task<string> BackupServerDescriptionAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.BackupsRoot);

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(_options.BackupsRoot, $"{stamp}-server-settings");
        Directory.CreateDirectory(backupPath);

        var serverDescriptionPath = GetServerDescriptionPath();
        File.Copy(serverDescriptionPath, Path.Combine(backupPath, "ServerDescription.json"), overwrite: false);

        await File.WriteAllTextAsync(
            Path.Combine(backupPath, "backup-info.txt"),
            $"CreatedUtc={DateTimeOffset.UtcNow:O}{Environment.NewLine}Type=ServerSettings{Environment.NewLine}",
            cancellationToken);

        return backupPath;
    }

    private async Task SetActiveWorldIdAsync(string worldId, CancellationToken cancellationToken)
    {
        var serverDescriptionPath = GetServerDescriptionPath();
        var root = await ReadJsonAsync(serverDescriptionPath, cancellationToken);

        if (!TrySetJsonValue(root, "WorldIslandId", worldId))
        {
            if (root is not JsonObject rootObject)
            {
                throw new InvalidOperationException("ServerDescription.json must contain a JSON object.");
            }

            if (rootObject["ServerDescription"] is JsonObject description)
            {
                description["WorldIslandId"] = worldId;
            }
            else
            {
                rootObject["WorldIslandId"] = worldId;
            }
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(serverDescriptionPath, root.ToJsonString(options), cancellationToken);
    }

    private async Task<ServerSettings> ReadServerSettingsAsync(string serverDescriptionPath, CancellationToken cancellationToken)
    {
        var root = await ReadJsonAsync(serverDescriptionPath, cancellationToken);
        var serverName = TryFindJsonValue(root, "ServerName");
        var password = TryFindJsonValue(root, "Password");
        var isPasswordProtected = TryFindJsonBool(root, "IsPasswordProtected") ?? !string.IsNullOrEmpty(password);

        return new ServerSettings(
            string.IsNullOrWhiteSpace(serverName) ? null : serverName,
            isPasswordProtected,
            !string.IsNullOrEmpty(password));
    }

    private string GetServerDescriptionPath()
    {
        return Path.Combine(_options.ServerRoot, "R5", "ServerDescription.json");
    }

    private string? GetWorldsRoot()
    {
        var rocksRoot = Path.Combine(_options.ServerRoot, "R5", "Saved", "SaveProfiles", "Default", "RocksDB");
        if (!Directory.Exists(rocksRoot))
        {
            return null;
        }

        return Directory
            .GetDirectories(rocksRoot)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .Select(versionDirectory => Path.Combine(versionDirectory, "Worlds"))
            .FirstOrDefault(Directory.Exists);
    }

    private async Task<Dictionary<string, string>> ReadLabelsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.LabelsPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(_options.LabelsPath);
        var labels = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken);
        return new Dictionary<string, string>(labels ?? [], StringComparer.OrdinalIgnoreCase);
    }

    private async Task WriteLabelsAsync(Dictionary<string, string> labels, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.LabelsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Create(_options.LabelsPath);
        await JsonSerializer.SerializeAsync(stream, labels.OrderBy(pair => pair.Key).ToDictionary(), options, cancellationToken);
    }

    private async Task<string?> ReadJsonValueAsync(string path, string propertyName, CancellationToken cancellationToken)
    {
        var root = await ReadJsonAsync(path, cancellationToken);
        return TryFindJsonValue(root, propertyName);
    }

    private static async Task<JsonNode> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"{path} is empty or invalid JSON.");
    }

    private static string? TryFindJsonValue(JsonNode? node, string propertyName)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value?.GetValue<string>();
                }

                var child = TryFindJsonValue(property.Value, propertyName);
                if (child is not null)
                {
                    return child;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var child = TryFindJsonValue(item, propertyName);
                if (child is not null)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static bool TrySetJsonValue(JsonNode? node, string propertyName, string value)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    obj[property.Key] = value;
                    return true;
                }

                if (TrySetJsonValue(property.Value, propertyName, value))
                {
                    return true;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (TrySetJsonValue(item, propertyName, value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool? TryFindJsonBool(JsonNode? node, string propertyName)
    {
        return TryFindJsonBoolValue(node, propertyName, out var value) ? value : null;
    }

    private static bool TryFindJsonBoolValue(JsonNode? node, string propertyName, out bool value)
    {
        value = false;

        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value is not null &&
                    property.Value.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                {
                    value = property.Value.GetValue<bool>();
                    return true;
                }

                if (TryFindJsonBoolValue(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (TryFindJsonBoolValue(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void SetServerDescriptionValue(JsonNode root, string propertyName, JsonNode? value)
    {
        if (TrySetJsonValue(root, propertyName, value))
        {
            return;
        }

        if (root is not JsonObject rootObject)
        {
            throw new InvalidOperationException("ServerDescription.json must contain a JSON object.");
        }

        if (rootObject["ServerDescription_Persistent"] is JsonObject persistentDescription)
        {
            persistentDescription[propertyName] = value;
            return;
        }

        if (rootObject["ServerDescription"] is JsonObject description)
        {
            description[propertyName] = value;
            return;
        }

        rootObject[propertyName] = value;
    }

    private static bool TrySetJsonValue(JsonNode? node, string propertyName, JsonNode? value)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    obj[property.Key] = value;
                    return true;
                }

                if (TrySetJsonValue(property.Value, propertyName, value))
                {
                    return true;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (TrySetJsonValue(item, propertyName, value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<CommandResult> RunComposeAsync(string action, CancellationToken cancellationToken)
    {
        return await _commands.RunAsync(
            _options.DockerCommand,
            $"compose -f \"{_options.ComposeFile}\" {action} {_options.ServiceName}",
            _options.ComposeDirectory,
            cancellationToken);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            File.Copy(file, Path.Combine(targetDirectory, relativePath), overwrite: false);
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '-' : ch)).Trim();
    }

    private static string NormalizeZipPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
