using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace WindroseSaveManager.Pages;

public class IndexModel : PageModel
{
    private readonly WindroseWorldService _worlds;

    public IndexModel(WindroseWorldService worlds)
    {
        _worlds = worlds;
    }

    public SaveManagerState? State { get; private set; }
    public OperationResult? LastOperation { get; private set; }

    [BindProperty]
    public string? WorldId { get; set; }

    [BindProperty]
    public string? Alias { get; set; }

    [BindProperty]
    public IFormFile? WorldZip { get; set; }

    [BindProperty]
    public string? ServerName { get; set; }

    [BindProperty]
    public string? InviteCode { get; set; }

    [BindProperty]
    public bool UpdateInviteCode { get; set; }

    [BindProperty]
    public bool UpdateServerName { get; set; }

    [BindProperty]
    public int? MaxPlayerCount { get; set; }

    [BindProperty]
    public bool UpdateMaxPlayerCount { get; set; }

    [BindProperty]
    public string? P2pProxyAddress { get; set; }

    [BindProperty]
    public bool UpdateP2pProxyAddress { get; set; }

    [BindProperty]
    public string? ServerPassword { get; set; }

    [BindProperty]
    public bool UpdatePassword { get; set; }

    [BindProperty]
    public bool ClearPassword { get; set; }

    [BindProperty]
    public string PresetType { get; set; } = "Medium";

    [BindProperty]
    public bool SharedQuests { get; set; }

    [BindProperty]
    public bool EasyExplore { get; set; }

    [BindProperty]
    public double MobHealthMultiplier { get; set; }

    [BindProperty]
    public double MobDamageMultiplier { get; set; }

    [BindProperty]
    public double ShipsHealthMultiplier { get; set; }

    [BindProperty]
    public double ShipsDamageMultiplier { get; set; }

    [BindProperty]
    public double BoardingDifficultyMultiplier { get; set; }

    [BindProperty]
    public double CoopStatsCorrectionModifier { get; set; }

    [BindProperty]
    public double CoopShipStatsCorrectionModifier { get; set; }

    [BindProperty]
    public string CombatDifficulty { get; set; } = "Normal";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        State = await _worlds.GetStateAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostActivateAsync(CancellationToken cancellationToken)
    {
        LastOperation = await _worlds.ActivateWorldAsync(WorldId ?? string.Empty, cancellationToken);
        State = await _worlds.GetStateAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostBackupAsync(CancellationToken cancellationToken)
    {
        LastOperation = await _worlds.BackupCurrentAsync(cancellationToken);
        State = await _worlds.GetStateAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(CancellationToken cancellationToken)
    {
        LastOperation = await _worlds.ImportWorldZipAsync(WorldZip, cancellationToken);
        State = await _worlds.GetStateAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAliasAsync(CancellationToken cancellationToken)
    {
        LastOperation = await _worlds.SetAliasAsync(WorldId ?? string.Empty, Alias, cancellationToken);
        State = await _worlds.GetStateAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveServerSettingsAsync(CancellationToken cancellationToken)
    {
        LastOperation = await _worlds.UpdateServerSettingsAsync(
            InviteCode,
            UpdateInviteCode,
            ServerName,
            UpdateServerName,
            MaxPlayerCount,
            UpdateMaxPlayerCount,
            P2pProxyAddress,
            UpdateP2pProxyAddress,
            ServerPassword,
            UpdatePassword,
            ClearPassword,
            cancellationToken);
        State = await _worlds.GetStateAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadSaveAsync(string? worldId, CancellationToken cancellationToken)
    {
        var archive = await _worlds.CreateWorldArchiveAsync(worldId ?? string.Empty, cancellationToken);
        if (archive is null)
        {
            return NotFound();
        }

        return File(archive.Content, "application/zip", archive.FileName);
    }

    public async Task<IActionResult> OnPostSaveWorldSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = new WorldSettings(
            PresetType,
            SharedQuests,
            EasyExplore,
            MobHealthMultiplier,
            MobDamageMultiplier,
            ShipsHealthMultiplier,
            ShipsDamageMultiplier,
            BoardingDifficultyMultiplier,
            CoopStatsCorrectionModifier,
            CoopShipStatsCorrectionModifier,
            CombatDifficulty);

        LastOperation = await _worlds.UpdateWorldSettingsAsync(WorldId ?? string.Empty, settings, cancellationToken);
        State = await _worlds.GetStateAsync(cancellationToken);
        return Page();
    }

    public string FormatNumber(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
