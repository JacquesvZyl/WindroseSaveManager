# Windrose Save Manager

A small LAN-only Razor Pages app for switching Windrose dedicated-server worlds.

It is designed for this layout:

```text
/opt/windrose/docker-compose.yml
/opt/windrose/server-files/R5/ServerDescription.json
/opt/windrose/server-files/R5/Saved/SaveProfiles/Default/RocksDB/0.10.0/Worlds
```

## What It Does

- Lists world folders from the latest `RocksDB/<game version>/Worlds` directory.
- Reads each world's `WorldDescription.json` to show the friendly world name.
- Imports a zipped world folder.
- Stores optional local labels in `/opt/windrose/world-labels.json`.
- Shows the currently active world from `ServerDescription.json`.
- Creates a timestamped backup before switching.
- Stops the `windrose` Docker Compose service.
- Updates `WorldIslandId`.
- Starts the `windrose` Docker Compose service again.

The app does not rename world folders. Windrose world IDs must stay unchanged.

## World Names And Labels

Windrose stores the friendly world name inside each world's `WorldDescription.json`:

```json
{
  "WorldDescription": {
    "islandId": "BF5A61023235461F91AF3341458E32B2",
    "WorldName": "Porra world"
  }
}
```

The app displays `WorldName` by default. If you want a clearer admin label, save one in the UI. Labels are stored outside the save itself:

```text
/opt/windrose/world-labels.json
```

The actual world folder ID is never renamed.

## Importing A World Zip

The import form expects a zip with one top-level world folder:

```text
BF5A61023235461F91AF3341458E32B2/
  WorldDescription.json
  *.sst
  ...
```

The app validates that `WorldDescription.json` exists and that its `islandId` matches the top-level folder name before extracting it into the active `Worlds` directory.

## Run Locally On The LXC

Install the .NET runtime or SDK on the Windrose LXC.

Check whether .NET is installed:

```bash
dotnet --info
```

On Debian 12, install the ASP.NET Core runtime like this:

```bash
apt-get update
apt-get install -y wget apt-transport-https
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
apt-get update
apt-get install -y aspnetcore-runtime-9.0
```

From your development machine, publish the app:

```powershell
cd C:\Code\windrose-save-manager
dotnet publish -c Release -o .\publish
```

Create the target folder on the LXC:

```bash
mkdir -p /opt/windrose-save-manager
```

Copy the published files from Windows to the LXC:

```powershell
scp -r .\publish\* root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp .\windrose-save-manager.service root@<LXC-LAN-IP>:/opt/windrose-save-manager/
```

You can also publish directly on the LXC if the source repo is cloned there:

```bash
dotnet publish -c Release -o /opt/windrose-save-manager
```

Run it on LAN port `5085`:

```bash
cd /opt/windrose-save-manager
ASPNETCORE_URLS=http://0.0.0.0:5085 dotnet WindroseSaveManager.dll
```

Open:

```text
http://<LXC-LAN-IP>:5085
```

Stop the manual run with `Ctrl+C` before installing the systemd service.

## Configuration

Defaults live in `appsettings.json`:

```json
{
  "Windrose": {
    "ServerRoot": "/opt/windrose/server-files",
    "ComposeDirectory": "/opt/windrose",
    "ComposeFile": "/opt/windrose/docker-compose.yml",
    "ServiceName": "windrose",
    "BackupsRoot": "/opt/windrose/world-backups",
    "LabelsPath": "/opt/windrose/world-labels.json",
    "DockerCommand": "docker",
    "ManageContainer": true
  }
}
```

Override values with environment variables if needed:

```bash
export Windrose__ServerRoot=/opt/windrose/server-files
export Windrose__ComposeDirectory=/opt/windrose
export Windrose__ComposeFile=/opt/windrose/docker-compose.yml
export Windrose__ServiceName=windrose
export Windrose__BackupsRoot=/opt/windrose/world-backups
export Windrose__LabelsPath=/opt/windrose/world-labels.json
```

## systemd Service

The included service file runs the app on:

```text
http://0.0.0.0:5085
```

That means it is reachable from your LAN at:

```text
http://<LXC-LAN-IP>:5085
```

Install or update the service file:

```bash
sudo cp windrose-save-manager.service /etc/systemd/system/windrose-save-manager.service
sudo systemctl daemon-reload
sudo systemctl enable --now windrose-save-manager
```

Restart the service after deploying a new build:

```bash
sudo systemctl restart windrose-save-manager
```

Check status:

```bash
systemctl status windrose-save-manager
```

View logs:

```bash
journalctl -u windrose-save-manager -f
```

Stop/start manually:

```bash
sudo systemctl stop windrose-save-manager
sudo systemctl start windrose-save-manager
```

Disable the service:

```bash
sudo systemctl disable --now windrose-save-manager
```

## Updating The App

From Windows:

```powershell
cd C:\Code\windrose-save-manager
dotnet publish -c Release -o .\publish
scp -r .\publish\* root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp .\windrose-save-manager.service root@<LXC-LAN-IP>:/opt/windrose-save-manager/
```

On the LXC:

```bash
sudo cp /opt/windrose-save-manager/windrose-save-manager.service /etc/systemd/system/windrose-save-manager.service
sudo systemctl daemon-reload
sudo systemctl restart windrose-save-manager
journalctl -u windrose-save-manager -f
```

If the browser still shows the old UI, hard-refresh the page.

## Troubleshooting

If the service file is missing:

```bash
ls -la /opt/windrose-save-manager/windrose-save-manager.service
```

If it is not there, copy it from Windows:

```powershell
scp C:\Code\windrose-save-manager\windrose-save-manager.service root@<LXC-LAN-IP>:/opt/windrose-save-manager/
```

If the app cannot find worlds, confirm the Windrose paths:

```bash
ls -la /opt/windrose
ls -la /opt/windrose/server-files/R5
ls -la /opt/windrose/server-files/R5/Saved/SaveProfiles/Default/RocksDB
```

If Docker commands fail, test Compose manually:

```bash
cd /opt/windrose
docker compose ps
docker compose stop windrose
docker compose up -d windrose
```

If port `5085` is already in use:

```bash
ss -ltnp | grep 5085
```

Change the port in `/etc/systemd/system/windrose-save-manager.service`:

```ini
Environment=ASPNETCORE_URLS=http://0.0.0.0:5086
```

Then reload and restart:

```bash
sudo systemctl daemon-reload
sudo systemctl restart windrose-save-manager
```

## Docker Permissions

The app runs `docker compose stop windrose` and `docker compose up -d windrose`.

If the service user cannot run Docker, either:

- Run the service as `root` inside this trusted LXC, as the included service file does.
- Or create a dedicated user with permission to run Docker.

Keep this app LAN-only unless you put it behind authentication such as Cloudflare Access, Tailscale, or NGINX auth.
