# Florence-2 and template alerts

[Português](../florence-alerts.md) | English

Install explicitly with `scripts/Install-FlorenceModel.ps1`, optionally `-Destination C:/Models/Florence-2-base-ft`. The default is `%LOCALAPPDATA%/SurveillanceAI/models/Florence-2-base-ft`. It downloads about 275 MB of INT8 weights plus configuration, checks four ONNX hashes and writes a provenance manifest. The app never downloads models. Keep all four graphs, manifest, vocabulary and configuration together; inference is offline C#/ONNX Runtime without training, Python, APIs or a sidecar.

The checkpoint is `microsoft/Florence-2-base-ft`, exported by [onnx-community](https://huggingface.co/onnx-community/Florence-2-base-ft) at `e88a44eaf3791a35eae0c5a47b3dbcd36e67eb6f`. The export metadata identifies base-ft; we do not use the library's base downloader or assume base equals base-ft. Hashes/provenance were checked; no PyTorch parity test was performed.

In **Alerts**, expand advanced settings, select the installed folder and load Florence on CPU. Choose templates/cameras/actions and save. Existing rules remain under Advanced rules. Florence loads automatically and is mandatory; YOLO remains optional. Object choices come from the actual YOLO manifest. The firearm template is experimental/off by default; knives lack positive image validation, and running/audio/after-hours motion remain unavailable with reasons. There is no audio or verified motion engine.

Automatic camera alerts run OD first and immediately persist a weapon hypothesis. Without an OD match, directed phrase grounding runs using handgun/pistol/revolver/firearm/rifle/person holding a handgun, restored at the user's request for replica evaluation. Grounding may return regions without a real weapon, so incidents remain hypotheses requiring operator review. Confidence remains null. People/posture/intent are not required.

Evidence stores capture UTC, camera, detector/checkpoint, box and first image. One open episode per camera/template is enforced in SQLite. Repeats preserve first evidence and avoid duplicate outbox rows, including after restart. Acknowledge means seen, human confirmation is separate, and close/false-positive ends the episode. After closing or marking false positive, a newer capture without a weapon hypothesis in both OD and grounding is required before rearming. This state survives restart, event deletion and retention; stale frames cannot rearm it.

Image test accepts local files up to 16 MB, mandatory Florence with optional YOLO and optional English phrase grounding. It renders boxes/descriptions/model IDs/latency and supports cancellation without creating incidents. Visual/sound warnings run in the authenticated desktop; email requires configured/enabled SMTP and recipients. Firearm evidence is mandatory regardless of the general snapshot toggle.

One latest-frame queue per camera feeds a shared serialized Florence model. The Florence branch is resized to at most 1280 pixels, uses a configurable ONNX CPU thread count (default four) and defaults to 128 generated tokens (32–256). Dropped metrics include sampling omissions. YOLO does not await Florence; hardware contention is still possible. CPU is packaged/tested. CUDA/DirectML are guarded by native runtime availability, need a compatible distribution/technical configuration and were not tested. No MX450 2 GB fit or real-time guarantee is made.

Real CPU OD/grounding ran on the public bus fixture, and the published UI ran YOLO+Florence with boxes and timings. No firearm/knife imagery was available, so weapon/replica accuracy is unproven. Set `SURVEILLANCEAI_FLORENCE_MODEL` to run the real-model integration test; otherwise only that test is explicitly skipped and CI never downloads weights. Run `scripts/Smoke-SmartMonitoring.ps1 -ModelDirectory <folder>` for the isolated interactive test. Mock-result tests validate event semantics, not weapon recognition.

The MIT Florence2-Sharp source is pinned to commit `392b9f12bed69c6c76d30bf7c5f8d8fbc5610fff` with limited disposal/tokenizer/RGB/generation patches; see `third_party/Florence2/PROVENANCE.md`. OnnxDetector.cs is unchanged. Back up before the compatible SQLite migration. Privacy/licensing/representative imagery/recovery/performance need review before a pilot. Monitoring still stops when the desktop exits.

## Required engine and desktop layout

Florence is mandatory and loads automatically at startup. Legacy disabled settings are normalized to enabled. Missing or invalid weights produce a persistent visible warning; capture and YOLO remain available for recovery. Install the local bundle using the script above, or select its folder and reload in Advanced settings. No automatic network download occurs. Firearm alerts remain an explicit template choice. Image testing always includes Florence, with optional YOLO. The window starts maximized, includes an application icon, and uses consistent dark inputs, calendars, and scrollbars.

Acknowledgement, human confirmation, false-positive review and closure never train the model or change subsequent prompts or weights. Operators/admins can delete an event with confirmation, including evidence and notification records; sent email cannot be recalled. Deletion audit and per-camera rearm state remain, without incident content or images. The selected detail refreshes after actions.

## Enlarged evidence

Select an event and choose **Enlarge evidence**. The maximized viewer provides zoom, fit, actual size, **Detected region**, a box toggle, and PNG export with or without the box. Export retains saved pixel dimensions regardless of display zoom. The yellow box is the recorded model hypothesis.

New Florence evidence retains the captured source resolution using JPEG quality 95. Only inference input is reduced to a 1280-pixel longest side; boxes are mapped back to original evidence pixels. The one-frame-per-camera queue now retains original pixels, increasing memory according to source dimensions. Original files remain unannotated. Older evidence can be enlarged and annotated, but previously discarded detail cannot be recovered.

Each camera card displays its Florence worker state, completed analysis count, latency and last analysis UTC. These are separate from manual image testing. Excluded cameras are identified. Model-loading failures retry every five seconds independently of YOLO. A loaded global model alone does not prove camera processing.

Under Alerts → Advanced settings, CPU threads accepts 1–64. Save monitoring options to recreate the model and persist the value. Diagnostics show the applied thread count. Reload/image testing also use the draft value, while saving is needed for restart persistence. Existing profiles default to four. Compare the same scene at 4/6/8 or other values; higher counts can increase contention without improving latency. This changes ONNX intra-op threads, not the number of concurrent model analyses.

## Sequential file analysis and grounding confirmation

Both advanced options default to enabled. Video files await Florence for every decoded frame, with no sampling or frame-queue dropping. YOLO also waits for queue space. EOF finishes instead of looping. The card shows frame index and FPS-derived video position; incident notes include frame index. UTC remains processing time, not recording time. At 20 FPS and four seconds per frame, one minute can take 80 minutes for one task; extra tasks increase this. Reapplying settings/restarting begins from the start; persistent resume is not implemented. Inference failures retry the same frame.

Live webcam/RTSP/HTTP retain latest-frame sampling. Lossless coverage would require recording and deferred analysis, not implemented here. Disabling the sequential option restores previous file behavior.

Grounding-only hypotheses now require OD on the marked crop before creating an incident or displaying its weapon box. Full-frame OD hypotheses remain immediate. This is the same model running another task, not independent verification. It may reject genuine weapons/replicas and cannot guarantee absence of false positives. Disabling confirmation restores direct grounding hypotheses. Mock crop tests verify control flow, not accuracy on the user's monitor image.
