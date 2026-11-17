[Português](../security.md) | English

# Security, privacy and release limits

## Authentication and authorization

Passwords use Microsoft's Identity-compatible PasswordHasher (salted, versioned PBKDF2 with verification/rehash). Password length is 12-256 characters; five failed attempts lock an account for five minutes. No default production password or recovery backdoor exists. New accounts default to Viewer; first setup explicitly creates Administrator. This library does not introduce an ASP.NET backend. [Microsoft hashing guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-10.0).

Session.Require executes inside operator-facing services. Administrator manages configuration/users/cameras/models/recipients; Operator monitors/reviews; Viewer reads dashboards/events. Background persistence uses a separate contract and continues after logout. Role/activation changes apply at next login; this single-instance installation has one interactive session. The current administrator cannot deactivate/demote itself.

Windows administrators, database editing or malware under the same Windows account can bypass local controls. There is no MFA, password recovery, per-camera ACL, central identity or tamper-proof audit. Local user authentication is not endpoint isolation.

## Secrets and storage

Entire camera source strings, including embedded RTSP credentials, and SMTP passwords use DPAPI CurrentUser. PasswordBox contents are read at command execution and cleared, but managed strings cannot guarantee memory zeroization. SMTP host/user/sender and recipients are personal/configuration data and remain readable in SQLite.

Recovery requires the original Windows profile/DPAPI keys. Moving to another user/machine usually requires reentering camera/SMTP credentials; user passwords remain one-way hashes. Back up appropriately and test recovery before relying on it.

Data inherits the local Windows profile ACL. SQLite, snapshots and logs are not independently encrypted. Use private NTFS permissions and disk encryption; protect backups. Keep the data root off shared/cloud-sync/repository folders. Evidence paths reject rooted/traversal paths and reparse points and remain under the controlled root. This cannot defend against an administrator racing filesystem changes.

Logs use identifiers/error types, exclude EF commands and do not retain network exception text, passwords, RTSP URLs or per-frame content. Native OpenCV/FFmpeg diagnostics are silenced in Desktop. JSON logs retain 14 days; minimum Information/Warning/Error is configurable. Audit actions exclude secret values.

## Evidence and Brazilian privacy review

Snapshots default to disabled and are created only for confirmed potential detections. No continuous video is recorded. Default event/evidence retention is 30 days with six-hour cleanup; this is a product default, not legal retention advice. Notifications default to disabled and send metadata only. Explicit SMTP enablement authorizes outbound metadata; administrative tests deliberately send one message. No footage upload path exists.

Before deployment, identify controller/operator, document purpose/applicable legal basis, minimize coverage/retention, establish access/deletion and incident-response procedures, provide transparency and assess any mail-provider transfers. Obtain qualified review for the actual installation. This application does not certify LGPD compliance. Consult [ANPD security guidance and checklist](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia-orientativo-sobre-seguranca-da-informacao-para-agentes-de-tratamento-de-pequeno-porte).

## Commercialization

The bundled ONNX metadata declares AGPL-3.0. Review [Ultralytics licensing](https://www.ultralytics.com/license), original weights/sample asset, ONNX Runtime, OpenCvSharp/OpenCV/FFmpeg/codec dependencies and all transitive notices. No repository-wide license choice or commercial clearance is claimed.

Release gates include threat modeling, security/authorization review, field/model/rule validation, capacity/retention tests, privacy review, recovery exercises, licensing, signed packaging and persistent-host testing. Never advertise verified threats, crime prediction, aggression recognition, weapon detection or uninterrupted operation on this foundation.
