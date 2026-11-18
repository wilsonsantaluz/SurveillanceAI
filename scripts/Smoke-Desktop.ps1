param(
    [string]$Executable = "$PSScriptRoot\..\artifacts\publish\SurveillanceAI.Desktop.exe",
    [string]$ScreenshotDirectory = "$PSScriptRoot\..\artifacts\screenshots",
    [string]$VideoPath = '',
    [switch]$RequireFlorence
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
$env:SURVEILLANCEAI_DATA = Join-Path ([IO.Path]::GetTempPath()) ("SurveillanceAI-ui-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $ScreenshotDirectory | Out-Null
$appProcess = Start-Process -FilePath (Resolve-Path -LiteralPath $Executable) -PassThru -WindowStyle Hidden
$window = $null
function Wait-For([scriptblock]$Action) {
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do { $result = & $Action; if ($result) { return $result }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Timed out waiting for the desktop UI.'
}
function Find-Id([string]$Id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Find-Control([string]$Name, $Type) {
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $typeCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $Type)
    $condition = New-Object System.Windows.Automation.AndCondition($nameCondition, $typeCondition)
    $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Set-Value($Element, [string]$Value) { $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value) }
function Invoke-Button($Element) { $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Screenshot([string]$Name) {
    $bounds = $window.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size); $bitmap.Save((Join-Path (Resolve-Path $ScreenshotDirectory) "$Name.png")) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
try {
    $window = Wait-For {
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $appProcess.Id)
        [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
    }
    Wait-For { Find-Id 'Username' } | Out-Null
    Screenshot 'login'
    # This account exists only in this run's isolated temporary database.
    $testPassword = 'UI-test-' + [Guid]::NewGuid().ToString('N')
    Set-Value (Find-Id 'Username') 'ui-smoke-admin'
    Set-Value (Find-Id 'LoginPassword') $testPassword
    Invoke-Button (Find-Id 'Login')
    Wait-For { Find-Id 'Logout' } | Out-Null
    Screenshot 'dashboard'
    foreach ($name in @('TabCameras', 'TabEvents', 'TabAlertrules', 'TabNotifications', 'TabSystem', 'TabUsers', 'TabMyaccount')) {
        $tab = Find-Id $name
        if (!$tab) { throw "Missing tab: $name" }
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    (Find-Id 'TabCameras').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Set-Value (Wait-For { Find-Id 'CameraName' }) 'Smoke camera'
    if ($VideoPath) {
        $resolvedVideo = (Resolve-Path -LiteralPath $VideoPath).Path
        $kind = Find-Id 'CameraKind'
        $kind.SetFocus()
        [System.Windows.Forms.SendKeys]::SendWait('{HOME}{DOWN}{DOWN}{ENTER}')
        Set-Value (Find-Id 'CameraSource') $resolvedVideo
    } else {
        Set-Value (Find-Id 'CameraSource') '0'
        (Find-Id 'CameraEnabled').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    }
    Invoke-Button (Find-Id 'SaveCamera')
    Wait-For { $status = Find-Id 'StatusMessage'; if ($status.Current.Name -like 'Camera saved*' -or $status.Current.Name -like 'Câmera salva*') { $true } } | Out-Null
    Screenshot 'cameras'
    if ($VideoPath) {
        (Find-Id 'TabOverviewLive').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Wait-For {
            $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name -like 'yolov8n-coco-*' } | Select-Object -First 1
        } | Out-Null
        if ($RequireFlorence) {
            Wait-For {
                $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name -match 'Florence:.*· [1-9][0-9]* (análises|analyses)' } | Select-Object -First 1
            } | Out-Null
        }
        Screenshot 'live-video'
        Invoke-Button (Find-Id 'TogglePreview')
        Invoke-Button (Find-Id 'TogglePreview')
        Invoke-Button (Find-Id 'ExpandCamera')
        Wait-For { $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Maximized } | Out-Null
        Screenshot 'fullscreen-video'
        Invoke-Button (Find-Id 'CloseFullscreen')
    }
    Invoke-Button (Find-Id 'Logout')
    Wait-For { Find-Id 'LoginPassword' } | Out-Null
    Set-Value (Find-Id 'LoginPassword') $testPassword
    Invoke-Button (Find-Id 'Login')
    Wait-For { Find-Id 'Logout' } | Out-Null
    if (!(Test-Path -LiteralPath (Join-Path $env:SURVEILLANCEAI_DATA 'surveillance.db'))) { throw 'Database was not created.' }
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (!$appProcess.WaitForExit(20000)) { throw 'Desktop did not stop gracefully.' }
    if ($appProcess.ExitCode -ne 0) { throw "Desktop exited with $($appProcess.ExitCode)." }
    Write-Output 'PASS: publish startup, administrator setup, login, navigation, camera save, logout, second login, SQLite creation, graceful shutdown.'
    if ($VideoPath) { Write-Output 'PASS: real video preview with model metrics, preview pause/resume and full-screen camera mode.' }
    Write-Output "Isolated test data: $env:SURVEILLANCEAI_DATA"
} finally {
    # A first close can dismiss a native ComboBox popup during a failed UI step.
    for ($attempt = 0; $attempt -lt 3 -and !$appProcess.HasExited; $attempt++) {
        $appProcess.CloseMainWindow() | Out-Null
        $appProcess.WaitForExit(3000) | Out-Null
    }
    if (!$appProcess.HasExited) { Write-Warning "Test instance $($appProcess.Id) did not close; inspect its isolated data directory." }
}
