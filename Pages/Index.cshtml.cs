using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
}
