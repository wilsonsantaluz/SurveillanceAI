[Português](../validation.md) | English

# Executed validation - 2026-11-17

Validation was performed locally on Windows x64 with the locally installed .NET SDK 10.0.404. Results below describe executed checks, not proposed tests. Remote GitHub Actions has not been run or published.

| Check | Actual result |
| --- | --- |
| Release solution build | Passed, 0 warnings, 0 errors |
| xUnit unit tests | 12 passed, 0 failed, 0 skipped |
| xUnit integration tests | 15 passed, 0 failed, 0 skipped |
| Self-contained Windows x64 folder publish | Passed; output in ignored `artifacts/publish` |
| Published executable UI smoke | Passed setup, login, all tabs, camera save, logout, second login, database creation and graceful shutdown |
| Published executable video smoke | Passed actual detection-box preview, model/latency metrics, pause/resume and borderless full-screen/return |
| NuGet transitive vulnerability audit of Desktop | No vulnerable packages reported by configured sources at execution time |
| Git whitespace/diff check | Passed |

The final test run used `dotnet test SurveillanceAI.sln -c Release --no-build` after a successful release solution build. Publish used `dotnet publish src/SurveillanceAI.Desktop -c Release -r win-x64 --self-contained true -o artifacts/publish`. Here `dotnet` was `.tools/dotnet/dotnet.exe`.

## Automated coverage

- Password hashing/verification, setup idempotency, password change, failed login/lockout, activation, roles and self-deactivation protection.
- Service authorization after login/logout and attempted Viewer/Operator administrative actions.
- Actual EF migration application in isolated temporary SQLite files, camera create/edit/disable, recipient management and encrypted secrets.
- Event storage, class/date/camera/severity/status filtering, pagination, notes/reviewer/review time and event counts.
- Event outbox recipient filters, transactional persistence and unique event/recipient constraint.
- SMTP failure retry/backoff/outcome persistence and sanitized errors using a fake transport. Actual MailKit delivery through a loopback TCP SMTP server; rejection of insecure non-loopback configuration. No external emails were sent.
- Evidence path containment, snapshot persistence and retention cleanup of tracked expired evidence/events.
- Real bundled ONNX execution on a blank frame and the public Ultralytics bus image. Nonempty person/bus detections, direct probability mapping, incompatible tensor rejection, manifest errors and incorrect model hash rejection.
- Generated MJPEG playback through OpenCvSharp, multiple-frame event confirmation, per-camera isolation, expiration, cooldown, spatial association and disconnect reset.
- Mock capture lifecycle, stale-frame dropping, bounded queue depth, reconnect/backoff, cancellation during backoff and source disposal/shutdown.

## UI verification

`scripts/Smoke-Desktop.ps1` was executed against the self-contained executable with an isolated temporary data root. The default smoke registers a disabled webcam definition and does not touch hardware. A second run supplied `-VideoPath artifacts/sample.avi`; that ignored clip was generated locally from the public checked-in test image. It is not private footage or a production incident seed.

Screenshots were inspected, including `artifacts/screenshots/login.png`, `dashboard.png`, `cameras.png`, `live-video.png` and `fullscreen-video.png`. Visual inspection caught and corrected window theme inheritance, clipped forms at 125% scaling and text encoding issues. The live preview showed actual ONNX boxes and measured metrics. These sample runs are functional checks, not throughput benchmarks or accuracy validation.

For another local clip:

```powershell
./scripts/Smoke-Desktop.ps1 -VideoPath C:/path/to/test.avi
```

The script prints its temporary test data root and leaves it for inspection; production tables are never seeded. No camera, notification channel or rule is enabled automatically in a normal fresh installation.

## Remaining validation and release work

Real webcams/RTSP appliances, network outages, native driver blocking, GPU execution, multi-camera soak/capacity, storage exhaustion, power-loss recovery and commercial installer/signing have not been field-tested. The project does not establish model accuracy, crime prediction, behavior recognition, validated weapon detection or 24/7 availability. Review privacy/licensing, deploy-specific thresholds, backups and hardware before a pilot.

Evidence location is selected through the installation data root rather than an independent settings picker. Camera/model configuration currently restarts all workers. A separate persistent host, per-camera reconciliation, long-term audit archival and orphan evidence reconciliation remain follow-up work.

No original model was deleted. The two pre-existing modified net9.0 obj files were preserved; compatibility builds now write under ignored `obj/legacy` so they do not churn historical tracked artifacts. No commit or push was made.

See [changed-file inventory](changes.md) for the actual source/documentation files introduced or modified.

## Internationalization follow-up

35 tests passed (15 unit, 20 integration), none failed/skipped. The Windows publish includes the English satellite. Portuguese video UI smoke passed. Smoke-Localization.ps1 verified default Portuguese, translated validation, live pt/en changes, preserved form/session and persisted language after restart. Screenshots were inspected.
