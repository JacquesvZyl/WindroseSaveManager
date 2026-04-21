# Windrose Save Manager

A small LAN-only Razor Pages admin app for managing a self-hosted Windrose dedicated server.

It is designed to run beside a Docker Compose Windrose server, especially the excellent [`indifferentbroccoli/windrose-server-docker`](https://github.com/indifferentbroccoli/windrose-server-docker) image.

## Features

- List Windrose world saves by friendly `WorldName`, not only folder ID.
- Switch the active world safely.
- Import zipped world folders.
- Add local display labels for saves.
- Edit session name.
- Set, replace, or remove the server password.
- Back up the current world or `ServerDescription.json` before destructive changes.
- Stop/start the Windrose Docker Compose service when changes require it.
- Show a loading screen while long-running actions complete.

## Security Warning

This app can stop your Windrose server and edit its save/config files.

Run it on a trusted LAN only unless you put it behind real access control such as:

- Tailscale
- Cloudflare Access
- VPN
- NGINX with authentication and HTTPS

Do not expose it directly to the public internet.

## Supported Layout

The default configuration expects:

```text
/opt/windrose/docker-compose.yml
/opt/windrose/.env
/opt/windrose/server-files/R5/ServerDescription.json
/opt/windrose/server-files/R5/Saved/SaveProfiles/Default/RocksDB/<game-version>/Worlds
```

Example Windrose Docker Compose service:

```yaml
services:
  windrose:
    image: indifferentbroccoli/windrose-server-docker
    platform: linux/amd64
    restart: unless-stopped
    container_name: windrose
    stop_grace_period: 30s
    network_mode: host
    env_file:
      - .env
    volumes:
      - ./server-files:/home/steam/server-files
```

## Important Docker Image Setting

If you use `indifferentbroccoli/windrose-server-docker`, the container can generate/patch settings from `.env` on startup.

After your first successful server start, consider setting:

```env
GENERATE_SETTINGS=false
```

This prevents container startup from overwriting changes made by Windrose Save Manager to:

```text
/opt/windrose/server-files/R5/ServerDescription.json
```

You can still keep other `.env` values for the Docker container itself.

## Quick Start

On your Windrose LXC/server:

```bash
mkdir -p /opt/windrose-save-manager
```

On your development machine:

```bash
git clone https://github.com/JacquesvZyl/WindroseSaveManager.git
cd WindroseSaveManager
dotnet publish -c Release -o ./publish
scp -r ./publish/* root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp ./windrose-save-manager.service root@<LXC-LAN-IP>:/opt/windrose-save-manager/
```

On your Windrose LXC/server:

```bash
cd /opt/windrose-save-manager
ASPNETCORE_URLS=http://0.0.0.0:5085 dotnet WindroseSaveManager.dll
```

Open:

```text
http://<LXC-LAN-IP>:5085
```

Stop the manual run with `Ctrl+C` before installing the systemd service.

## Install .NET Runtime

Check if .NET is installed:

```bash
dotnet --info
```

On Debian 12:

```bash
apt-get update
apt-get install -y wget apt-transport-https
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
apt-get update
apt-get install -y aspnetcore-runtime-9.0
```

If you are on another distro, install the ASP.NET Core 9 runtime using Microsoft's package instructions for your OS.

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

Override with environment variables:

```bash
export Windrose__ServerRoot=/opt/windrose/server-files
export Windrose__ComposeDirectory=/opt/windrose
export Windrose__ComposeFile=/opt/windrose/docker-compose.yml
export Windrose__ServiceName=windrose
export Windrose__BackupsRoot=/opt/windrose/world-backups
export Windrose__LabelsPath=/opt/windrose/world-labels.json
export Windrose__ManageContainer=true
```

## systemd Service

The included service file runs the app on:

```text
http://0.0.0.0:5085
```

Install it:

```bash
sudo cp /opt/windrose-save-manager/windrose-save-manager.service /etc/systemd/system/windrose-save-manager.service
sudo systemctl daemon-reload
sudo systemctl enable --now windrose-save-manager
```

Open:

```text
http://<LXC-LAN-IP>:5085
```

Useful commands:

```bash
systemctl status windrose-save-manager
journalctl -u windrose-save-manager -f
sudo systemctl restart windrose-save-manager
sudo systemctl stop windrose-save-manager
sudo systemctl start windrose-save-manager
sudo systemctl disable --now windrose-save-manager
```

## Updating

From your development machine:

```bash
git pull
dotnet publish -c Release -o ./publish
scp -r ./publish/* root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp ./windrose-save-manager.service root@<LXC-LAN-IP>:/opt/windrose-save-manager/
```

On the LXC/server:

```bash
sudo cp /opt/windrose-save-manager/windrose-save-manager.service /etc/systemd/system/windrose-save-manager.service
sudo systemctl daemon-reload
sudo systemctl restart windrose-save-manager
journalctl -u windrose-save-manager -f
```

If the browser still shows the old UI, hard-refresh the page.

## How World Switching Works

Windrose chooses the active world from:

```text
/opt/windrose/server-files/R5/ServerDescription.json
```

Specifically:

```json
{
  "ServerDescription_Persistent": {
    "WorldIslandId": "BF5A61023235461F91AF3341458E32B2"
  }
}
```

When you activate a world, the app:

1. Stops the `windrose` Docker Compose service.
2. Backs up the current active world.
3. Updates `WorldIslandId`.
4. Starts the `windrose` service again.

The app never renames world folders. Windrose world IDs must stay unchanged.

## World Names And Labels

Windrose stores the friendly world name in each world's `WorldDescription.json`:

```json
{
  "WorldDescription": {
    "islandId": "BF5A61023235461F91AF3341458E32B2",
    "WorldName": "My World"
  }
}
```

The app displays `WorldName` by default.

If you want a clearer admin label, save one in the UI. Labels are stored outside the save itself:

```text
/opt/windrose/world-labels.json
```

## Importing World Zips

The import form expects a zip with exactly one top-level world folder:

```text
BF5A61023235461F91AF3341458E32B2/
  WorldDescription.json
  *.sst
  ...
```

The app validates:

- `WorldDescription.json` exists.
- Its `islandId` matches the top-level folder name.
- The world does not already exist on the server.
- The zip does not contain unsafe paths.

Imported worlds are extracted into the active `Worlds` directory.

## Session Name And Password

The app can update:

```json
{
  "ServerDescription_Persistent": {
    "ServerName": "My Windrose Server",
    "IsPasswordProtected": true,
    "Password": "example-password"
  }
}
```

Windrose expects `ServerDescription.json` to be changed while the server is stopped. When you save server settings, the app:

1. Stops the `windrose` Docker Compose service.
2. Backs up the current `ServerDescription.json`.
3. Updates only the checked settings.
4. Starts the `windrose` service again.

The form is intentionally explicit:

- Check `Update session name` to overwrite `ServerName`.
- Leave it unchecked to preserve the current session name.
- Check `Set or replace password` and enter a password to overwrite the password.
- Leave it unchecked to preserve the current password.
- Check `Remove password protection` to clear `Password` and set `IsPasswordProtected` to `false`.

Blank password fields do not overwrite an existing password unless you explicitly choose `Remove password protection`.

## Docker Permissions

The app runs:

```bash
docker compose stop windrose
docker compose up -d windrose
```

The included systemd service runs as `root` because many LXC-based Docker installs are administered as root.

If you prefer a dedicated user, make sure that user can:

- Read and write `/opt/windrose/server-files`.
- Read and write `/opt/windrose/world-backups`.
- Read and write `/opt/windrose/world-labels.json`.
- Run Docker Compose for `/opt/windrose/docker-compose.yml`.

## Troubleshooting

Check the service:

```bash
systemctl status windrose-save-manager
journalctl -u windrose-save-manager -f
```

If the app cannot find worlds:

```bash
ls -la /opt/windrose
ls -la /opt/windrose/server-files/R5
ls -la /opt/windrose/server-files/R5/Saved/SaveProfiles/Default/RocksDB
```

If Docker commands fail:

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

## Project Status

This is an unofficial companion tool for self-hosted Windrose dedicated servers.

It is not affiliated with Windrose or indifferent broccoli.

## Contributing

Issues and pull requests are welcome. Please keep the app focused on safe save/config management for self-hosted servers.
