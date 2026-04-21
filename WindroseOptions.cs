namespace WindroseSaveManager;

public sealed class WindroseOptions
{
    public string ServerRoot { get; set; } = "/opt/windrose/server-files";
    public string ComposeDirectory { get; set; } = "/opt/windrose";
    public string ComposeFile { get; set; } = "/opt/windrose/docker-compose.yml";
    public string ServiceName { get; set; } = "windrose";
    public string BackupsRoot { get; set; } = "/opt/windrose/world-backups";
    public string LabelsPath { get; set; } = "/opt/windrose/world-labels.json";
    public string DockerCommand { get; set; } = "docker";
    public bool ManageContainer { get; set; } = true;
}
