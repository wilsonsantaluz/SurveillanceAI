param(
 [string]$Executable = "$PSScriptRoot\..\artifacts\publish\SurveillanceAI.Desktop.exe",
 [string]$ScreenshotDirectory = "$PSScriptRoot\..\artifacts\screenshots",
 [string]$ModelDirectory = "$env:LOCALAPPDATA\SurveillanceAI\models\Florence-2-base-ft",
 [int]$CpuThreads = 6,
 [string]$ImagePath = "$PSScriptRoot\..\tests\Fixtures\bus.jpg"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$dataDirectory = Join-Path ([IO.Path]::GetTempPath()) ("SurveillanceAI-language-" + [Guid]::NewGuid().ToString('N'))
$env:SURVEILLANCEAI_DATA = $dataDirectory
$env:SURVEILLANCEAI_FLORENCE_MODEL = (Resolve-Path -LiteralPath $ModelDirectory).Path
New-Item -ItemType Directory -Force $ScreenshotDirectory | Out-Null
$appProcess = $null
$window = $null
function Wait-For([scriptblock]$Action) {
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do { $value = & $Action; if ($value) { return $value }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $deadline)
    throw ('Timeout no teste: ' + $script:stage)
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
function Find-Name([string]$Name) {
 $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$Name)
 $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
}
try {
 $script:stage="login"
 Start-TestApp
 if($window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -ne [System.Windows.Automation.WindowVisualState]::Maximized){throw 'Janela não abriu maximizada.'}
 $script:stage='carregamento automático obrigatório'
 Wait-For {!(Find-Id 'ModelStartupStatus')} | Out-Null
 $testPassword='Smart-test-'+[Guid]::NewGuid().ToString('N')
 Set-Value (Find-Id 'Username') 'smart-admin'
 Set-Value (Find-Id 'LoginPassword') $testPassword
 Invoke-Button (Find-Id 'Login')
 Wait-For {Find-Id 'Logout'} | Out-Null
 (Find-Id 'TabSmartMonitoring').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 Wait-For {Find-Id 'SaveSmart'} | Out-Null
 foreach($name in @('Pessoas correndo','Gritos e pedidos de socorro','Movimento fora do horário','Facas e objetos cortantes')) {
  if((Find-Name $name).Current.IsEnabled){throw "Template indisponível habilitado indevidamente: $name"}
 }
 Screenshot 'smart-templates'
 $script:stage='expandir configurações'
 (Find-Id 'SmartAdvanced').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
 Wait-For {Find-Id 'FlorenceDirectory'} | Out-Null
 foreach($id in @('AnalyzeEveryVideoFrame','ConfirmGrounding')) { if((Find-Id $id).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On){throw ('Opção padrão desabilitada: '+$id)} }
 (Find-Id 'FlorenceDirectory').SetFocus()
 Set-Value (Find-Id 'FlorenceDirectory') (Resolve-Path -LiteralPath $ModelDirectory).Path
 [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
 Set-Value (Find-Id 'FlorenceCpuThreads') ([string]$CpuThreads)
 [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
 $script:stage='carregar Florence'
 Invoke-Button (Find-Id 'LoadFlorence')
 Wait-For {(Find-Id 'StatusMessage').Current.Name -like 'Florence carregado*'} | Out-Null
 Wait-For {(Find-Id 'FlorenceStatus').Current.Name -like ('*Threads de CPU ativas: '+$CpuThreads)} | Out-Null
 $weapon=Find-Name 'Armas de fogo e simulacros'
 if(!$weapon.Current.IsEnabled){throw 'Florence não disponibilizou o template.'}
 $weapon.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 $script:stage='salvar templates'
 Invoke-Button (Find-Id 'SaveSmart')
 Wait-For {(Find-Id 'StatusMessage').Current.Name -eq 'Opções de monitoramento salvas.'} | Out-Null
 Screenshot 'smart-configured'
 (Find-Id 'TabImageTest').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 (Find-Id 'TestImagePath').SetFocus()
 Set-Value (Find-Id 'TestImagePath') (Resolve-Path -LiteralPath $ImagePath).Path
 [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
 $script:stage='inferência por imagem'
 Invoke-Button (Find-Id 'RunImageTest')
 Wait-For {(Find-Id 'ImageTestSummary').Current.Name -like '*florence2 / OD*'} | Out-Null
 Screenshot 'image-yolo-florence'
 (Find-Id 'TestGrounding').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 (Find-Id 'TestPhrase').SetFocus()
 Set-Value (Find-Id 'TestPhrase') 'bus'
 [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
 $script:stage='inferência por imagem'
 Invoke-Button (Find-Id 'RunImageTest')
 Wait-For {(Find-Id 'ImageTestSummary').Current.Name -like '*CAPTION_TO_PHRASE_GROUNDING*'} | Out-Null
 Screenshot 'image-grounding'
 Invoke-Button (Find-Id 'RunImageTest')
 Wait-For {!(Find-Id 'RunImageTest').Current.IsEnabled} | Out-Null
 Close-TestApp
 $script:stage='restaurar quantidade de threads'
 Start-TestApp
 Set-Value (Find-Id 'Username') 'smart-admin'
 Set-Value (Find-Id 'LoginPassword') $testPassword
 Invoke-Button (Find-Id 'Login')
 Wait-For {Find-Id 'Logout'} | Out-Null
 (Find-Id 'TabSmartMonitoring').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 (Find-Id 'SmartAdvanced').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
 Wait-For {(Find-Id 'FlorenceCpuThreads').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -eq [string]$CpuThreads} | Out-Null
 Wait-For {(Find-Id 'FlorenceStatus').Current.Name -like ('*Threads de CPU ativas: '+$CpuThreads)} | Out-Null
 Close-TestApp
 Write-Output 'PASS: templates honestos, base-ft local CPU, configuração persistida, YOLO+Florence reais por imagem, grounding, latência e encerramento durante inferência.'
 Write-Output "Dados isolados: $dataDirectory"
} catch {
 if($window){Screenshot 'smart-failure'; Write-Output ('Mensagem da interface: '+(Find-Id 'StatusMessage').Current.Name)}
 throw
} finally {
 if($appProcess){for($attempt=0;$attempt -lt 3 -and !$appProcess.HasExited;$attempt++){$appProcess.CloseMainWindow() | Out-Null;$appProcess.WaitForExit(3000) | Out-Null}}
}
