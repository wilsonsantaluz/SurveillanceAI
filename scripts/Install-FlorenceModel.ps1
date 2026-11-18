param([string]$Destination = "$env:LOCALAPPDATA\SurveillanceAI\models\Florence-2-base-ft")
$ErrorActionPreference = 'Stop'
# Explicit installation only: the application never downloads weights at startup.
$revision = 'e88a44eaf3791a35eae0c5a47b3dbcd36e67eb6f'
$repository = 'onnx-community/Florence-2-base-ft'
$files = @{
    'decoder_model_merged_quantized.onnx' = 'f22f52f980c33df0efa15932c2f3db6d9d3595ce6387eca938b8cfe23dc4c641'
    'embed_tokens_quantized.onnx' = '6b2258db1c8ee9b160576ccde3cd3814d83a2edaed0dd1c6ca9ff3c38fa62214'
    'encoder_model_quantized.onnx' = 'f4ad7a68f1fb875d3bcf735ea14a7021b7ba7e83baf7cf10289881b4ed6d9b85'
    'vision_encoder_quantized.onnx' = '3b79d54f23f666f731549db23cb070c35a979ce19cbd9720e90e67a78dc9768c'
}
New-Item -ItemType Directory -Force $Destination | Out-Null
foreach ($name in $files.Keys) {
    $path = Join-Path $Destination $name
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path).Hash -eq $files[$name]) { continue }
    Write-Output "Instalando $name (base-ft, CPU INT8)..."
    & curl.exe --fail --location --retry 2 --silent --show-error "https://huggingface.co/$repository/resolve/$revision/onnx/$name" --output "$path.part"
    if ($LASTEXITCODE -ne 0) { throw "Falha no download de $name." }
    if ((Get-FileHash -LiteralPath "$path.part").Hash -ne $files[$name]) { throw "SHA-256 divergente: $name" }
    Move-Item -LiteralPath "$path.part" -Destination $path -Force
}
foreach ($name in @('vocab.json','tokenizer_config.json','config.json','preprocessor_config.json')) {
    $path = Join-Path $Destination $name
    & curl.exe --fail --location --retry 2 --silent --show-error "https://huggingface.co/$repository/resolve/$revision/$name" --output "$path.part"
    if ($LASTEXITCODE -ne 0) { throw "Falha no download de $name." }
    Move-Item -LiteralPath "$path.part" -Destination $path -Force
    $files[$name] = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
}
$manifest = @{ SchemaVersion=1; Checkpoint='microsoft/Florence-2-base-ft'; ExportRepository=$repository; Revision=$revision; Precision='INT8'; Files=$files }
[IO.File]::WriteAllText((Join-Path $Destination 'florence-manifest.json'), ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Output "Modelo instalado e verificado: $Destination"
