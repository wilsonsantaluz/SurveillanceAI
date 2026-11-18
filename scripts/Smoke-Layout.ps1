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
 Set-Value (Find-Id 'Username') 'layout-admin'
 Set-Value (Find-Id 'LoginPassword') ('Test-'+[Guid]::NewGuid().ToString('N'))
 Invoke-Button (Find-Id 'Login')
 Wait-For {Find-Id 'Logout'} | Out-Null
 foreach($tab in @('TabEvents','TabCameras','TabSmartMonitoring','TabNotifications','TabSystem','TabAccount')) {
  $element=Find-Id $tab
  if(!$element){continue}
  $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
  Start-Sleep -Milliseconds 250
  Screenshot ('layout-'+$tab)
  if($tab -eq 'TabEvents') {
   $button=Find-Id 'PART_Button'
   if(!$button){throw 'Calendário não encontrado.'}
   Invoke-Button $button
   Start-Sleep -Milliseconds 400
   Screenshot 'layout-calendar'
   [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
  }
 }
 Close-TestApp
 Write-Output 'PASS: navegação e captura das telas revisadas.'
} finally {if($appProcess -and !$appProcess.HasExited){$appProcess.CloseMainWindow() | Out-Null;$appProcess.WaitForExit(5000) | Out-Null}}
