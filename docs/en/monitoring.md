[Português](../monitoring.md) | English

# Monitoring and model contract

Each enabled camera owns a capture task, capacity-one latest-frame channel and inference task. Replaced Mats are disposed. Capture, inference, event persistence and JPEG encoding execute off the dispatcher. UI preview polling is twice/second; dashboard/events refresh every ten seconds without replacing a selected review form.

Webcam sources use a nonnegative index and DirectShow. RTSP uses FFmpeg with native 5-second open/3-second read timeouts. Video files are paced at source FPS or a 25 FPS fallback. End-of-file is a disconnect and playback repeats after backoff. Backoff doubles from one to at most 30 seconds; a successful frame resets failures. Disconnect resets temporal state. Managed cancellation cannot interrupt every native camera driver, so physical hardware needs field tests.

Metrics are measured capture/inference averages since the current connection/worker, inference latency, queue depth 0-1, drops, reconnects and working-set memory. Configured inference FPS is a maximum; processing duration adds to pacing delay. No benchmark or supported camera-count claim is made. Native diagnostics are silenced in Desktop to protect source credentials; managed logs identify cameras by GUID/error type.

## Detection bundle v1

The checked-in `onnxmodel/manifest.json` is the working full example. A bundle contains that manifest and its named ONNX file. Required fields: schemaVersion=1; modelFile (plain filename); sha256 (actual 64-digit hash); version; classMapVersion; unique labels; inputName/outputName; width/height; layout; scores; preprocessing; confidence and iou.

Use `Get-FileHash model.onnx -Algorithm SHA256` to calculate a local hash. Never copy an example checksum onto a different model. The app does not fetch models.

Supported preprocessing: BGR to RGB, centered letterbox with pad 114, float32 NCHW divided by 255. Dimensions must be multiples of 32, 32-2048. One input/output is supported; batch is 1 or dynamic, channels=3. Supported outputs are decoded cx/cy/w/h in input pixels followed by C class scores, `[1,4+C,N]` (featuresFirst) or `[1,N,4+C]` (boxesFirst). Label count must match features. Normalized boxes, objectness branches, integrated NMS, segmentation and classifiers are rejected rather than guessed.

Scores explicitly mean probabilities (use directly and validate 0-1) or independent logits (Sigmoid once). No softmax is applied. Runtime rank/feature count and finite scores are checked, boxes map back/clamp to the source, then class-aware NMS runs. The existing graph already contains Sigmoid before output; its old second Sigmoid was corrected only after graph inspection. Real sample detections and direct confidence mapping are tested.

The inspected training Phase 3 contract is ResNet18 classification with ImageNet normalization and `[batch,classes]` logits. It is deliberately incompatible. A future custom detector needs an explicit supported tensor contract and validated performance; availability/weapon detection is not assumed.

## Temporal rules

Rules match exact class/confidence and optional camera. Basic IoU association grants at most one hit per track per frame/timestamp. Hits expire outside the configured window. Multiple hits confirm a potential detection, then per-track cooldown suppresses repeats. Tracks expire/reset on disconnect. These are unvalidated deployment-specific settings and heuristic spatial association, not identity or behavioral recognition. Occlusion/movement can split tracks and cause duplicates.

Experimental legacy proximity code is not a product alert source. Optional distance lines provide diagnostics only; ordinary proximity and multiple people never imply aggression or robbery.

## HTTP camera streams

Register phone or IP camera HTTP/HTTPS streams using camera type `HttpStream` and the exact URL supplied by the camera app (for example `http://192.168.1.246:8080/stream`). Streams are opened through OpenCV with FFmpeg; MJPEG compatibility depends on the source codec and server. The camera origin is now a readable text field instead of a password field. Leave it blank when editing an existing camera to retain the saved protected source. Avoid displaying URLs containing embedded credentials in public screenshots.

## Complementary Florence-2 detector

YOLO is unchanged. Florence has a shared model and independent bounded per-camera queues, cancellation and diagnostics. Image testing runs real OD/phrase grounding. Firearm hypotheses save first evidence with nullable confidence and a persistent episode. See [setup and limits](florence-alerts.md).
