[Português](../audit.md) | English

# Repository audit and incremental migration

Inspected tracked source, project/solution files, export script, README, asset generator, repository inventory and model metadata before modification. Origin is `wilsonsantaluz/SurveillanceAI`. Branch: `feature/desktop-foundation`. No commit or push was authorized or performed.

- Root project was `net9.0`, although the README said .NET 8. Only SDK 9 was installed; SDK 10.0.404 was installed into ignored `.tools/dotnet` for this work.
- Program owned synchronous capture, inference, overlays and console alerts, used webcam 0, spun on failed reads and did not dispose the detector/frame.
- Detector fixed 640x640 input, .65 confidence, .45 IoU, COCO classes and features-first output; it applied Sigmoid blindly.
- Actual ONNX input: float32 `images[batch,3,height,width]`. Output: float32 `output0[batch,84,anchors]`. Final Concat joins decoded boxes with `/model.22/Sigmoid_output_0`: scores already are probabilities. Metadata: Ultralytics 8.4.37, COCO, dynamic export, opset 12, no end-to-end NMS, AGPL-3.0.
- Both ONNX copies hash to `56caff0a3c1c2f20a7b7839cf7ba697d1198b051e037554ef00842a1b2714625`. All original ONNX/PT weights remain.
- ProximityAnalyzer associated positional list entries using arbitrary pixel thresholds and emitted alarming messages. EventAnalyzer classified two people as suspicious. Neither is wired into the desktop. Console no longer invokes proximity analysis or person-count warnings; distance lines are opt-in diagnostics.
- CameraService was a synchronous RTSP opener; Logger was console-only. There was no persistent authentication, UI, event store, notification provider, model manifest or automated test suite.
- Generated obj files are tracked. Two net9.0 generated files were modified before this task and remain preserved. New output is ignored; removing historical tracked artifacts should be a separate reviewed cleanup.

## Safe migration path used

Preserve the console and weights; add modular projects beside them and exclude nested sources from root compilation. Build authentication/persistence first, generate/run EF migrations in temporary databases, then preserve the detector's letterbox/RGB/NMS pipeline while correcting verified score semantics. Connect actual WPF commands to services. Validate camera lifecycle/video, rules, persistence, local SMTP and retention, then publish and launch the executable.

The read-only training inspection covered README, deployment/manifest.py and deployment/export.py on main. The contract is ResNet18 logits `[batch,classes]` with ImageNet normalization, not object-detection boxes. [Training manifest source](https://github.com/wilsonsantaluz/Surveillance-AI-Training/blob/main/src/surveillance_ml/deployment/manifest.py). No training files were modified.
