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
- Edit advanced world settings, including `Easy`/`Medium`/`Hard`/`Custom` presets, enemy health/damage scaling, ship scaling, boarding difficulty, co-op scaling, shared quests, and immersive exploration.
- Back up the current world or `ServerDescription.json` before destructive changes.
- Stop/start the Windrose Docker Compose service when changes require it.

## Security Warning

This app can stop your Windrose server and edit its save/config files.

Run it on a trusted LAN only unless you put it behind real access control such as:

- Tailscale
- Cloudflare Access
- VPN
- NGINX with authentication and HTTPS

Do not expose it directly to the public internet.

## How Users Should Run It

Recommended setup:

```text
/opt/windrose
  docker-compose.yml
  .env
  server-files/

/opt/windrose-save-manager
  WindroseSaveManager.dll
  appsettings.json
  windrose-save-manager.service
```

Windrose Save Manager does not replace your Windrose Docker setup. It is a companion admin app that reads/writes the mounted `server-files` folder and controls your existing Docker Compose service.

## Supported Windrose Docker Layout

The default configuration expects this layout:

```text
/opt/windrose/docker-compose.yml
/opt/windrose/.env
/opt/windrose/server-files/R5/ServerDescription.json
/opt/windrose/server-files/R5/Saved/SaveProfiles/Default/RocksDB/<game-version>/Worlds
```

Example `docker-compose.yml`:

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

If your paths are different, configure them with environment variables. See [Configuration](#configuration).

## Important Docker Image Setting

If you use `indifferentbroccoli/windrose-server-docker`, the container can generate or patch settings from `.env` on startup.

After your first successful server start, consider setting this in `/opt/windrose/.env`:

```env
GENERATE_SETTINGS=false
```

This prevents the container startup process from overwriting changes made by Windrose Save Manager to:

```text
/opt/windrose/server-files/R5/ServerDescription.json
```

You can still keep other `.env` values for the Docker container itself.

## Step By Step Install

### 1. Prepare The Windrose Server

On your Windrose LXC/server, confirm Docker Compose can see your server:

```bash
cd /opt/windrose
docker compose ps
```

Confirm your server files exist:

```bash
ls -la /opt/windrose/server-files/R5
ls -la /opt/windrose/server-files/R5/Saved/SaveProfiles/Default/RocksDB
```

Create the app install folder:

```bash
mkdir -p /opt/windrose-save-manager
```

### 2. Install .NET Runtime

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

### 3. Publish The App

On your development machine:

```bash
git clone https://github.com/JacquesvZyl/WindroseSaveManager.git
cd WindroseSaveManager
dotnet publish -c Release -o ./publish
```

Copy the app to your Windrose LXC/server:

```bash
scp -r ./publish/* root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp ./windrose-save-manager.service root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp ./windrose-save-manager.env.example root@<LXC-LAN-IP>:/opt/windrose-save-manager/
```

### 4. Configure Paths

If you use the default `/opt/windrose` layout, the app works without extra configuration.

For custom paths, create an environment file:

```bash
cp /opt/windrose-save-manager/windrose-save-manager.env.example /etc/default/windrose-save-manager
nano /etc/default/windrose-save-manager
```

Set the values for your server. Every app path has a default and can be overwritten here:

```env
Windrose__ServerRoot=/opt/windrose/server-files
Windrose__ComposeDirectory=/opt/windrose
Windrose__ComposeFile=/opt/windrose/docker-compose.yml
Windrose__ServiceName=windrose
Windrose__BackupsRoot=/opt/windrose/world-backups
Windrose__LabelsPath=/opt/windrose/world-labels.json
Windrose__DockerCommand=docker
Windrose__ManageContainer=true
ASPNETCORE_URLS=http://0.0.0.0:5085
```

The systemd service file itself defaults to this app install folder:

```text
/opt/windrose-save-manager
```

If you install the app somewhere else, edit these two lines in `/etc/systemd/system/windrose-save-manager.service`:

```ini
WorkingDirectory=/opt/windrose-save-manager
ExecStart=/usr/bin/dotnet /opt/windrose-save-manager/WindroseSaveManager.dll
```

### 5. Test Manual Run

Run the app manually first:

```bash
cd /opt/windrose-save-manager
ASPNETCORE_URLS=http://0.0.0.0:5085 dotnet WindroseSaveManager.dll
```

Open:

```text
http://<LXC-LAN-IP>:5085
```

Stop the manual run with `Ctrl+C` before installing the systemd service.

### 6. Install The systemd Service

```bash
sudo cp /opt/windrose-save-manager/windrose-save-manager.service /etc/systemd/system/windrose-save-manager.service
sudo systemctl daemon-reload
sudo systemctl enable --now windrose-save-manager
```

Open:

```text
http://<LXC-LAN-IP>:5085
```

### 7. Check Status And Logs

```bash
systemctl status windrose-save-manager
journalctl -u windrose-save-manager -f
```

## Configuration

Defaults live in `appsettings.json` and can be overridden with environment variables or `/etc/default/windrose-save-manager`.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Windrose__ServerRoot` | `/opt/windrose/server-files` | Host path where Windrose server files are mounted. This folder should contain `R5/ServerDescription.json`. |
| `Windrose__ComposeDirectory` | `/opt/windrose` | Directory where Docker Compose commands are run. |
| `Windrose__ComposeFile` | `/opt/windrose/docker-compose.yml` | Full path to the Windrose Docker Compose file. |
| `Windrose__ServiceName` | `windrose` | Docker Compose service name to stop/start. |
| `Windrose__BackupsRoot` | `/opt/windrose/world-backups` | Folder where world and config backups are written. |
| `Windrose__LabelsPath` | `/opt/windrose/world-labels.json` | JSON file where optional local world labels are stored. |
| `Windrose__DockerCommand` | `docker` | Docker executable name or full path. |
| `Windrose__ManageContainer` | `true` | If `true`, stop/start the Compose service for changes. If `false`, only edit files. |
| `ASPNETCORE_URLS` | `http://0.0.0.0:5085` | Web UI bind address and port. |

Example `/etc/default/windrose-save-manager`:

```env
Windrose__ServerRoot=/srv/windrose/server-files
Windrose__ComposeDirectory=/srv/windrose
Windrose__ComposeFile=/srv/windrose/docker-compose.yml
Windrose__ServiceName=windrose
Windrose__BackupsRoot=/srv/windrose/backups
Windrose__LabelsPath=/srv/windrose/world-labels.json
Windrose__DockerCommand=docker
Windrose__ManageContainer=true
ASPNETCORE_URLS=http://0.0.0.0:5085
```

After changing `/etc/default/windrose-save-manager`, restart:

```bash
sudo systemctl restart windrose-save-manager
```

## Updating

From your development machine:

```bash
cd WindroseSaveManager
git pull
dotnet publish -c Release -o ./publish
scp -r ./publish/* root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp ./windrose-save-manager.service root@<LXC-LAN-IP>:/opt/windrose-save-manager/
scp ./windrose-save-manager.env.example root@<LXC-LAN-IP>:/opt/windrose-save-manager/
```

On the LXC/server:

```bash
sudo cp /opt/windrose-save-manager/windrose-save-manager.service /etc/systemd/system/windrose-save-manager.service
sudo systemctl daemon-reload
sudo systemctl restart windrose-save-manager
journalctl -u windrose-save-manager -f
```

If the browser still shows the old UI, hard-refresh the page.

## Useful Service Commands

```bash
systemctl status windrose-save-manager
journalctl -u windrose-save-manager -f
sudo systemctl restart windrose-save-manager
sudo systemctl stop windrose-save-manager
sudo systemctl start windrose-save-manager
sudo systemctl disable --now windrose-save-manager
```

## How World Switching Works

Windrose chooses the active world from:

```text
<ServerRoot>/R5/ServerDescription.json
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

1. Stops the configured Docker Compose service if `Windrose__ManageContainer=true`.
2. Backs up the current active world into `Windrose__BackupsRoot`.
3. Updates `WorldIslandId`.
4. Starts the configured Docker Compose service again.

The app never renames world folders. Windrose world IDs must stay unchanged.

When you edit advanced settings for an inactive world, the app backs up and edits that world without stopping the running server. If you edit the active world, the app stops the configured Docker Compose service first and starts it again afterwards.

## Advanced World Settings

Each world card has an **Advanced World Settings** section. These settings are stored in that world's:

```text
<ServerRoot>/R5/Saved/SaveProfiles/Default/RocksDB/<game-version>/Worlds/<world-id>/WorldDescription.json
```

The UI reads and writes:

```json
{
  "WorldDescription": {
    "WorldPresetType": "Custom",
    "WorldSettings": {
      "BoolParameters": {},
      "FloatParameters": {},
      "TagParameters": {}
    }
  }
}
```

### Presets

The preset can be:

- `Easy`
- `Medium`
- `Hard`
- `Custom`

Custom parameters only take effect when `WorldPresetType` is `Custom`.

When you choose `Easy`, `Medium`, or `Hard`, the app saves an empty `WorldSettings` object so Windrose uses the selected preset. The custom fields are disabled in the UI and display their default values.

When you choose `Custom`, the custom fields are enabled and saved into `WorldSettings`.

### Restart Behavior

Changing advanced settings for the **active** world stops the configured Docker Compose service, backs up the world, writes the settings, and starts the service again.

Changing advanced settings for an **inactive** world backs up and edits only that inactive world. The running Windrose server is not stopped or restarted.

### Tooltips

Hover over or keyboard-focus the `?` icon beside each advanced setting in the UI to see what it does.

### Available Parameters

Number fields allow up to 2 decimal places. The app clamps values to the supported range before writing them.

| UI label | Windrose parameter key | Default | Range | Description |
| --- | --- | --- | --- | --- |
| Shared co-op quests | `WDS.Parameter.Coop.SharedQuests` | `true` | `true` / `false` | When a player completes a co-op quest, it auto-completes for all players who have it active. |
| Immersive exploration | `WDS.Parameter.EasyExplore` | `false` | `true` / `false` | Hides map markers for points of interest, making exploration harder. Called Immersive Exploration in-game. |
| Enemy health | `WDS.Parameter.MobHealthMultiplier` | `1.0` | `0.2` - `5.0` | Enemy health multiplier. |
| Enemy damage | `WDS.Parameter.MobDamageMultiplier` | `1.0` | `0.2` - `5.0` | Enemy damage multiplier. |
| Enemy ship health | `WDS.Parameter.ShipsHealthMultiplier` | `1.0` | `0.4` - `5.0` | Enemy ship health multiplier. |
| Enemy ship damage | `WDS.Parameter.ShipsDamageMultiplier` | `1.0` | `0.2` - `2.5` | Enemy ship damage multiplier. |
| Boarding difficulty | `WDS.Parameter.BoardingDifficultyMultiplier` | `1.0` | `0.2` - `5.0` | How many enemy sailors must be defeated to win a boarding action. |
| Co-op enemy scaling | `WDS.Parameter.Coop.StatsCorrectionModifier` | `1.0` | `0.0` - `2.0` | Adjusts enemy health and posture loss based on player count. |
| Co-op ship scaling | `WDS.Parameter.Coop.ShipStatsCorrectionModifier` | `0.0` | `0.0` - `2.0` | Adjusts enemy ship health based on player count. |

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

If you want a clearer admin label, save one in the UI. Labels are stored outside the save itself at `Windrose__LabelsPath`.

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

Imported worlds are extracted into the latest `RocksDB/<game-version>/Worlds` directory under `Windrose__ServerRoot`.

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

1. Stops the configured Docker Compose service if `Windrose__ManageContainer=true`.
2. Backs up the current `ServerDescription.json` into `Windrose__BackupsRoot`.
3. Updates only the checked settings.
4. Starts the configured Docker Compose service again.

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
docker compose -f <ComposeFile> stop <ServiceName>
docker compose -f <ComposeFile> up -d <ServiceName>
```

The included systemd service runs as `root` because many LXC-based Docker installs are administered as root.

If you prefer a dedicated user, make sure that user can:

- Read and write `Windrose__ServerRoot`.
- Read and write `Windrose__BackupsRoot`.
- Read and write `Windrose__LabelsPath`.
- Run Docker Compose for `Windrose__ComposeFile`.

## Troubleshooting

Check the service:

```bash
systemctl status windrose-save-manager
journalctl -u windrose-save-manager -f
```

Check configured paths:

```bash
cat /etc/default/windrose-save-manager
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

Change the port in `/etc/default/windrose-save-manager`:

```env
ASPNETCORE_URLS=http://0.0.0.0:5086
```

Then restart:

```bash
sudo systemctl restart windrose-save-manager
```

## Project Status

This is an unofficial companion tool for self-hosted Windrose dedicated servers.

It is not affiliated with Windrose or indifferent broccoli.

## Contributing

Issues and pull requests are welcome. Please keep the app focused on safe save/config management for self-hosted servers.
