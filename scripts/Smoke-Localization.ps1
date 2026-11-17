param(
    [string]$Executable = "$PSScriptRoot\..\artifacts\publish\SurveillanceAI.Desktop.exe",
    [string]$ScreenshotDirectory = "$PSScriptRoot\..\artifacts\screenshots"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$dataDirectory = Join-Path ([IO.Path]::GetTempPath()) ("SurveillanceAI-language-" + [Guid]::NewGuid().ToString('N'))
$env:SURVEILLANCEAI_DATA = $dataDirectory
New-Item -ItemType Directory -Force $ScreenshotDirectory | Out-Null
$appProcess = $null
$window = $null
function Wait-For([scriptblock]$Action) {
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do { $value = & $Action; if ($value) { return $value }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Timeout na validacao de idioma.'
}
function Find-Id([string]$Id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Set-Value($Element, [string]$Value) { $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value) }
function Invoke-Button($Element) { $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Start-TestApp {
    $script:appProcess = Start-Process -FilePath (Resolve-Path -LiteralPath $Executable) -PassThru -WindowStyle Hidden
    $script:window = Wait-For {
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $appProcess.Id)
        [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
    }
    Wait-For { Find-Id 'Login' } | Out-Null
}
function Close-TestApp {
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$appProcess.WaitForExit(20000)) { throw 'O aplicativo nao encerrou normalmente.' }
    if ($appProcess.ExitCode -ne 0) { throw "Falha do processo: $($appProcess.ExitCode)" }
}
function Select-Language([bool]$English) {
    (Find-Id 'LanguageSelector').SetFocus()
    if ($English) { [System.Windows.Forms.SendKeys]::SendWait('{END}{ENTER}') }
    else { [System.Windows.Forms.SendKeys]::SendWait('{HOME}{ENTER}') }
}
function Screenshot([string]$Name) {
    $bounds = $window.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size); $bitmap.Save((Join-Path (Resolve-Path $ScreenshotDirectory) "$Name.png")) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
try {
    Start-TestApp
    Wait-For { (Find-Id 'Login').Current.Name -eq 'Continuar' } | Out-Null
    Screenshot 'login-pt-BR'
    Set-Value (Find-Id 'Username') 'locale-admin'
    Set-Value (Find-Id 'LoginPassword') 'short'
    Invoke-Button (Find-Id 'Login')
    Wait-For { (Find-Id 'StatusMessage').Current.Name -like 'Use uma senha*' } | Out-Null
    Select-Language $true
    Wait-For { (Find-Id 'Login').Current.Name -eq 'Continue' } | Out-Null
    Screenshot 'login-en-US'
    if ((Find-Id 'Username').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne 'locale-admin') { throw 'A troca de idioma perdeu o formulario.' }
    Close-TestApp

    # Use the same isolated installation to verify startup persistence.
    Start-TestApp
    Wait-For { (Find-Id 'Login').Current.Name -eq 'Continue' } | Out-Null
    Set-Value (Find-Id 'Username') 'locale-admin'
    Set-Value (Find-Id 'LoginPassword') ('Test-' + [Guid]::NewGuid().ToString('N'))
    Invoke-Button (Find-Id 'Login')
    Wait-For { Find-Id 'Logout' } | Out-Null
    Wait-For { (Find-Id 'TabCameras').Current.Name -eq 'Cameras' } | Out-Null
    Select-Language $false
    # Unicode escaped here keeps this Windows PowerShell test independent of source encoding.
    $portugueseCameras = 'C' + [char]0x00E2 + 'meras'
    Wait-For { (Find-Id 'TabCameras').Current.Name -eq $portugueseCameras } | Out-Null
    if ((Find-Id 'Logout').Current.Name -ne 'Sair') { throw 'A sessao nao permaneceu ativa ao trocar idioma.' }
    Screenshot 'dashboard-pt-BR'
    Select-Language $true
    Wait-For { (Find-Id 'TabCameras').Current.Name -eq 'Cameras' } | Out-Null
    Screenshot 'dashboard-en-US'
    Close-TestApp
    Write-Output 'PASS: portugues padrao, validacao traduzida, troca imediata pt/en, formulario/sessao preservados e idioma restaurado apos reiniciar.'
    Write-Output "Dados isolados: $dataDirectory"
} finally {
    if ($appProcess) {
        for ($attempt = 0; $attempt -lt 3 -and !$appProcess.HasExited; $attempt++) {
            $appProcess.CloseMainWindow() | Out-Null
            $appProcess.WaitForExit(3000) | Out-Null
        }
    }
}
