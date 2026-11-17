[Português](../database.md) | English

# Database and retention

SQLite lives in the installation data root. Checked-in InitialDesktop EF migrations run before hosted services. Foreign keys are enabled and WAL is requested. No production users, cameras, rules or incidents are seeded.

| Table | Data |
| --- | --- |
| Users | Unique normalized name, Identity hash, enum role, active, lockout |
| Cameras | Name/description/location, source kind, DPAPI-protected source, enabled, FPS |
| Rules | Class, optional camera, confidence/IoU, confirmations/window/cooldown, severity |
| Events | UTC, camera/rule IDs, class/confidence/box, model version, review/reviewer/time/notes, optional relative evidence path |
| Recipients | Address/name, enabled, class/camera/severity filters |
| Deliveries | Unique event/recipient pair, message/destination, attempts, retry time, status/error |
| Settings | JSON configuration including protected SMTP password |
| Audit | UTC, user identifier and sanitized action |

Roles are a stable enum, not a redundant lookup table. Credentials are encrypted on their configuration records. One confirmed detection carries its own event metadata; raw frame history/video BLOBs are absent. Model versions are captured on events; deployment catalogs and historical monitoring sessions are planned.

Indexes cover event timestamp/camera and review/severity, delivery status/retry, and unique event/recipient. Camera/rule references on events are restrictive foreign keys. Disable cameras/rules/recipients through editing; destructive deletion is not exposed. Event queries page at 100 rows; keyset pagination may be needed at larger scale.

Retention runs at startup and every six hours in batches of 200. Default 30 days, configurable 1-3650. It removes tracked snapshots, expired events and their delivery records. Files are removed first so deletion failures remain retryable. A crash between file creation and event commit can leave an orphan; reconciliation is future work. Audit history is retained and needs an archival policy before long-term production.

## Migration management

Install `dotnet-ef` 10.0.15 to `.tools`, then use:

```powershell
.tools/dotnet-ef migrations add YourMigration --project src/SurveillanceAI.Infrastructure --startup-project src/SurveillanceAI.Infrastructure --output-dir Migrations
```

If using the local SDK, set DOTNET_ROOT and prepend `.tools/dotnet` to PATH. DesignTimeFactory uses a separate surveillance-design.db. Do not substitute EnsureCreated for migrations. Test upgrades on a copy and back up the entire data root with the application stopped. Copying only the database while WAL is active can omit writes. SQLite is not encrypted by EF and is not a shared-network/distributed database.
