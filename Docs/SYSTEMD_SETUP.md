# Systemd Service Setup for Orleans Edge Node

This guide explains how to set up the OrleansEdge.Node application to auto-start on Raspberry Pi boot using systemd.

## Prerequisites

- Raspberry Pi with Raspbian/Raspberry Pi OS
- .NET 8 runtime installed
- User `pi` exists (default on Raspberry Pi OS)

## Installation Steps

### 1. Build and Deploy Application to Raspberry Pi

Build the application.

You can deploy the app even from your Debug output folder.  What you need to copy is:

- all files (not subdirectories) from `OrleansEdge.Node/bin/Debug/net8.0/publish/` (or `Release` if you prefer)
- the single subdirectory `runtimes/linux-x64` and its contents

Copy these files to `/home/pi/orleans-edge` on the Raspberry Pi.

### 2. Install Systemd Service File

Copy the service file to systemd directory:

```bash
# On Raspberry Pi
sudo mv orleans-edge-node.service /etc/systemd/system
sudo chmod 644 /etc/systemd/system/orleans-edge-node.service
# Reload systemd to recognize new service
sudo systemctl daemon-reload

# Enable service to start on boot
sudo systemctl enable orleans-edge-node.service

# Start service now
sudo systemctl start orleans-edge-node.service

```


### View Service Logs

```bash
# View recent logs
sudo journalctl -u orleans-edge-node.service

# Follow logs in real-time
sudo journalctl -u orleans-edge-node.service -f

# View logs from last boot
sudo journalctl -u orleans-edge-node.service -b

# View last 100 lines
sudo journalctl -u orleans-edge-node.service -n 100
```

### Start/Stop/Restart Service

```bash
# Start service
sudo systemctl start orleans-edge-node.service

# Stop service
sudo systemctl stop orleans-edge-node.service

# Restart service
sudo systemctl restart orleans-edge-node.service

# Reload configuration without restarting
sudo systemctl reload-or-restart orleans-edge-node.service
```

### Disable Auto-Start

```bash
# Disable auto-start on boot (but don't stop current instance)
sudo systemctl disable orleans-edge-node.service

# Disable and stop service
sudo systemctl disable --now orleans-edge-node.service
```


## Updating the Application

To update the application:

```bash
# Stop service
sudo systemctl stop orleans-edge-node.service

# Deploy new version
scp -r ./publish/* pi@192.168.5.210:/home/pi/orleans-edge/

# Start service
sudo systemctl start orleans-edge-node.service

# Check status
sudo systemctl status orleans-edge-node.service
```

## Multiple Raspberry Pis

For Pi #2 (192.168.5.211), follow the same steps but ensure:

1. **Different appsettings.Production.json** with `AdvertisedIPAddress: "192.168.5.211"`
2. **Same PostgreSQL connection string**
3. **Same ports** (11111 for silo, 30000 for gateway)

Both Pis will:
- Use same service file
- Connect to same PostgreSQL database
- Form a single Orleans cluster
- Auto-start on boot independently

## Security Considerations

The service file includes basic security hardening:

- `NoNewPrivileges=true` - Prevents privilege escalation
- `PrivateTmp=true` - Isolated /tmp directory
- Runs as non-root user `pi`

For production deployments, consider additional hardening:
- Use dedicated service account (not `pi`)
- Configure firewall rules (ufw)
- Enable SSL for PostgreSQL connections
- Restrict file permissions further

## Monitoring

### Check if Service is Running

```bash
# Quick check
sudo systemctl is-active orleans-edge-node.service

# Detailed status
sudo systemctl status orleans-edge-node.service
```

### Auto-Start on Boot Test

```bash
# Reboot Raspberry Pi
sudo reboot

# After reboot, check service started automatically
sudo systemctl status orleans-edge-node.service
```

### Performance Monitoring

```bash
# View resource usage
systemctl status orleans-edge-node.service

# Detailed process info
ps aux | grep OrleansEdge.Node

# Memory usage
sudo systemctl show orleans-edge-node.service --property=MemoryCurrent
```

## Uninstallation

To completely remove the service:

```bash
# Stop and disable service
sudo systemctl stop orleans-edge-node.service
sudo systemctl disable orleans-edge-node.service

# Remove service file
sudo rm /etc/systemd/system/orleans-edge-node.service

# Reload systemd
sudo systemctl daemon-reload

# Remove application files (optional)
rm -rf /home/pi/orleans-edge
```

## Advanced Configuration

### Custom Environment Variables

Edit service file to add environment variables:

```bash
sudo nano /etc/systemd/system/orleans-edge-node.service
```

Add under `[Service]` section:
```ini
Environment=CUSTOM_VAR=value
Environment=ANOTHER_VAR=value
```

Reload and restart:
```bash
sudo systemctl daemon-reload
sudo systemctl restart orleans-edge-node.service
```

### Change Restart Behavior

Edit service file:

```ini
# Restart only on failure
Restart=on-failure

# Never restart automatically
Restart=no

# Wait 30 seconds before restart
RestartSec=30
```

### Dependency Management

If your application needs other services to start first:

```ini
[Unit]
After=network-online.target postgresql.service my-other-service.service
Requires=postgresql.service
```

## Next Steps

1. Set up service on Pi #1 (192.168.5.210)
2. Set up service on Pi #2 (192.168.5.211)
3. Test failover: Stop one Pi, verify the other continues working
4. Test auto-start: Reboot both Pis, verify they rejoin cluster
5. Configure monitoring/alerting if needed

For more information, see:
- systemd documentation: `man systemd.service`
- Orleans documentation: https://learn.microsoft.com/en-us/dotnet/orleans/
