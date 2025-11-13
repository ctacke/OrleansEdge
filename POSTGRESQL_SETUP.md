# PostgreSQL Setup for OrleansEdge

This guide explains how to set up PostgreSQL for Orleans clustering and grain persistence.

## Prerequisites

- PostgreSQL server running at 192.168.5.10 (or your preferred location)
- Network access from both Raspberry Pis to the PostgreSQL server
- PostgreSQL 12 or later recommended

## Step 1: Create Database and User

Connect to your PostgreSQL server and run:

```sql
-- Connect to PostgreSQL as superuser
psql -U postgres

-- Create the Orleans database
CREATE DATABASE orleans;

-- Create Orleans user with password (change 'your_password' to your actual password)
CREATE USER orleans_user WITH PASSWORD 'your_password';

-- Grant connection privileges
GRANT CONNECT ON DATABASE orleans TO orleans_user;
```

## Step 2: Download Orleans SQL Scripts

Download the required SQL scripts from the Orleans GitHub repository:

- [PostgreSQL-Main.sql](https://raw.githubusercontent.com/dotnet/orleans/main/src/AdoNet/Shared/PostgreSQL-Main.sql)
- [PostgreSQL-Clustering.sql](https://raw.githubusercontent.com/dotnet/orleans/main/src/AdoNet/Orleans.Clustering.AdoNet/PostgreSQL-Clustering.sql)
- [PostgreSQL-Persistence.sql](https://raw.githubusercontent.com/dotnet/orleans/main/src/AdoNet/Orleans.Persistence.AdoNet/PostgreSQL-Persistence.sql)

**Important:** Scripts must be executed in this exact order!

## Step 3: Execute SQL Scripts

Run the scripts in order:

```bash
# Download scripts (if not already downloaded)
wget https://raw.githubusercontent.com/dotnet/orleans/main/src/AdoNet/Shared/PostgreSQL-Main.sql
wget https://raw.githubusercontent.com/dotnet/orleans/main/src/AdoNet/Orleans.Clustering.AdoNet/PostgreSQL-Clustering.sql
wget https://raw.githubusercontent.com/dotnet/orleans/main/src/AdoNet/Orleans.Persistence.AdoNet/PostgreSQL-Persistence.sql

# Execute scripts in order (CRITICAL: order matters!)
psql -U postgres -d orleans -f PostgreSQL-Main.sql
psql -U postgres -d orleans -f PostgreSQL-Clustering.sql
psql -U postgres -d orleans -f PostgreSQL-Persistence.sql
```

## Step 4: Grant Permissions to Orleans User

After tables are created, grant necessary permissions:

```sql
-- Connect to orleans database
\c orleans

-- Grant table permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO orleans_user;

-- Grant sequence permissions
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO orleans_user;

-- Grant function execution permissions
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO orleans_user;

-- Ensure future tables also get permissions (optional but recommended)
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO orleans_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO orleans_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO orleans_user;
```

## Step 5: Configure Network Access

Edit PostgreSQL configuration to allow connections from Raspberry Pis:

### Edit postgresql.conf

```bash
sudo nano /etc/postgresql/[version]/main/postgresql.conf
```

Ensure this line is present (or add it):
```
listen_addresses = '*'
```

### Edit pg_hba.conf

```bash
sudo nano /etc/postgresql/[version]/main/pg_hba.conf
```

Add these lines at the end:
```
# Allow Orleans cluster from Raspberry Pis
host    orleans    orleans_user    192.168.5.210/32    scram-sha-256
host    orleans    orleans_user    192.168.5.211/32    scram-sha-256
```

### Restart PostgreSQL

```bash
sudo systemctl restart postgresql
```

## Step 6: Test Connectivity from Raspberry Pis

From each Raspberry Pi, test the connection:

```bash
# Install PostgreSQL client (if not already installed)
sudo apt-get install postgresql-client

# Test connection
psql -h 192.168.5.10 -U orleans_user -d orleans

# If successful, you should see the postgres prompt
# Type \dt to see tables
# Type \q to quit
```

## Step 7: Update Configuration Files

### For Raspberry Pi #1 (192.168.5.210)

Edit `OrleansEdge.Node/appsettings.Production.json`:

```json
{
  "Orleans": {
    "AdvertisedIPAddress": "192.168.5.210",
    "PostgresConnectionString": "Host=192.168.5.10;Database=orleans;Username=orleans_user;Password=your_password;Timeout=600;Command Timeout=600;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20;"
  }
}
```

### For Raspberry Pi #2 (192.168.5.211)

Create `OrleansEdge.Node/appsettings.Production.json`:

```json
{
  "Orleans": {
    "AdvertisedIPAddress": "192.168.5.211",
    "PostgresConnectionString": "Host=192.168.5.10;Database=orleans;Username=orleans_user;Password=your_password;Timeout=600;Command Timeout=600;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20;"
  }
}
```

**Important:** Replace `your_password` with the actual password you set in Step 1!

## Verification

After setup is complete, you can monitor the cluster:

### Check Cluster Membership

```sql
-- See which silos are in the cluster
SELECT address, port, hostname, status, starttime, iamalivetime
FROM orleansmembershiptable
ORDER BY iamalivetime DESC;
```

Expected output when both Pis are running:
- Two rows, one for each Pi (192.168.5.210 and 192.168.5.211)
- Status should be 1 (Active)
- IAmAliveTime should be recent (within last 30 seconds)

### Check Grain State

```sql
-- See stored grain state
SELECT graintypestring, grainidextensionstring, modifiedon, version,
       payloadjson::text
FROM orleansstorage
ORDER BY modifiedon DESC;
```

You should see an entry for the LED controller grain when you set a color.

## Troubleshooting

### Connection Refused

**Problem:** Raspberry Pi cannot connect to PostgreSQL

**Solutions:**
1. Check firewall: `sudo ufw status` (allow port 5432 if needed)
2. Verify PostgreSQL is listening: `sudo netstat -plnt | grep 5432`
3. Check pg_hba.conf has correct IP addresses
4. Ensure PostgreSQL restarted after config changes

### Authentication Failed

**Problem:** Password authentication fails

**Solutions:**
1. Verify password in connection string matches database user password
2. Check pg_hba.conf uses correct authentication method (scram-sha-256)
3. Try resetting password: `ALTER USER orleans_user WITH PASSWORD 'new_password';`

### Silos Not Discovering Each Other

**Problem:** Only one silo shows in OrleansMembershipTable

**Solutions:**
1. Verify both Pis can connect to PostgreSQL
2. Check ClusterId and ServiceId match in both configurations
3. Check logs for errors: `dotnet run --project OrleansEdge.Node`
4. Verify both Pis are using same PostgreSQL database

### State Not Persisting

**Problem:** Grain state resets after restart

**Solutions:**
1. Check OrleansStorage table has entries: `SELECT * FROM orleansstorage;`
2. Verify grain storage configuration in Program.cs
3. Check for errors in logs during WriteStateAsync
4. Verify orleans_user has INSERT/UPDATE permissions

## Performance Tuning

### Connection Pooling

The default configuration uses connection pooling:
- Minimum Pool Size: 2
- Maximum Pool Size: 20

Adjust based on your load:
```
Minimum Pool Size=2;Maximum Pool Size=50;
```

### Timeout Settings

For edge scenarios with potentially high latency:
```
Timeout=600;Command Timeout=600;
```

Reduce for lower latency networks:
```
Timeout=30;Command Timeout=30;
```

### PostgreSQL Performance

For better performance, tune PostgreSQL:

```sql
-- Increase shared buffers (in postgresql.conf)
shared_buffers = 256MB

-- Increase effective cache size
effective_cache_size = 1GB

-- Increase checkpoint segments
checkpoint_segments = 32
```

## Security Best Practices

1. **Use Strong Passwords:** Never use default or weak passwords in production
2. **SSL/TLS:** For production, enable SSL connections:
   ```
   Host=192.168.5.10;Database=orleans;Username=orleans_user;Password=your_password;SSL Mode=Require;
   ```
3. **Firewall:** Limit PostgreSQL access to only Orleans cluster IPs
4. **Minimal Privileges:** orleans_user should only have SELECT, INSERT, UPDATE, DELETE (not DROP or CREATE)
5. **Regular Backups:** Set up automated PostgreSQL backups

## Backup and Recovery

### Backup Orleans Database

```bash
# Full backup
pg_dump -U postgres orleans > orleans_backup_$(date +%Y%m%d).sql

# Compressed backup
pg_dump -U postgres orleans | gzip > orleans_backup_$(date +%Y%m%d).sql.gz
```

### Restore from Backup

```bash
# Drop and recreate database
psql -U postgres -c "DROP DATABASE orleans;"
psql -U postgres -c "CREATE DATABASE orleans;"

# Restore
psql -U postgres orleans < orleans_backup_20250312.sql
```

## Migration from Development Clustering

If you were using development clustering (UseDevelopmentClustering), you're all set! The code changes have already been made:

- ✅ Removed SQLite initialization
- ✅ Replaced UseDevelopmentClustering with UseAdoNetClustering
- ✅ Replaced AddMemoryGrainStorage with AddAdoNetGrainStorage
- ✅ Updated configuration files

Just set up PostgreSQL following this guide and you're ready to deploy!

## Next Steps

1. Complete PostgreSQL setup following steps above
2. Deploy OrleansEdge.Node to both Raspberry Pis
3. Start both nodes and verify they join the cluster
4. Run OrleansEdge.Controller and test LED control
5. Test failover by stopping one Pi and verifying the other takes over

For more information, see the [Orleans documentation](https://learn.microsoft.com/en-us/dotnet/orleans/).
