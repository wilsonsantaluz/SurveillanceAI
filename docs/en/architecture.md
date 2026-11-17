[Português](../architecture.md) | English

# Architecture

The WPF App composition root migrates SQLite before starting Generic Host. Window closure awaits host shutdown. A named local mutex prevents concurrent processes using the same data directory.

| Project | Responsibility |
| --- | --- |
| Domain | Entities, roles, review/severity, immutable frame detections and IoU |
| Application | Session authorization, persistence/monitoring/mail contracts, event filters and temporal rules |
| Infrastructure | EF context/migrations, authorized persistence workflows, Identity hashing, DPAPI, evidence, SMTP/outbox and retention |
| Monitoring | Model manifest/ONNX adapter, camera sources, capture/inference workers, hosted manager and metrics |
| Desktop | WPF resources/views, MVVM commands, presentation image decoding, composition and JSON logs |

Infrastructure and Monitoring depend on Application/Domain, never each other. IMonitoringStore bridges background persistence at composition time. Every database operation creates/disposes its own context. There is no cross-thread shared DbContext, generic repository, backend, cloud login or broker.

Small EF-backed workflows live alongside persistence rather than adding an interface for every CRUD method. Authorization and temporal confirmation have no WPF dependency. Code-behind handles view initialization and asynchronous closure only. PasswordBox contents are read at command execution and cleared, not stored as entity passwords.

Camera workers belong to monitoring configuration lifetime, not navigation. Each owns one reusable ONNX session and a one-frame latest-value channel that disposes dropped Mats. Applying camera/model configuration cancels/joins all workers before recreation. Navigation, preview pause and expansion do not recreate workers.

Raw detections stay in frame memory. Confirmed rule matches become reviewable DetectionEvent records. Delivery outbox rows are saved in the event's EF transaction; SMTP runs in its own hosted worker in bounded batches.

WPF was chosen for the requested Windows-first scope and native desktop/DPAPI integration. No current requirement justifies cross-platform UI/security packaging work.

## Future persistent host

Move host composition, Monitoring, Infrastructure and Application into a Windows Service host. Replace preview access with authenticated local IPC; define service-account DPAPI/key ownership and evidence ACLs; add lifecycle, upgrade, recovery and authorization tests. Desktop exit must then detach rather than stop monitoring. This host is not implemented; the current application exits with all workers.
