# Regenerates the README screenshots from --demo mode (fake devices, no hardware needed).
# The app captures its own web view with --shot, so the window does not need to be in front or even visible.
# Usage:  powershell -File tools\make-screenshots.ps1 [-Exe path\to\PowerlineTool.exe]
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\PowerlineTool\bin\Release\net8.0-windows\PowerlineTool.exe'),
    [string]$Out = (Join-Path $PSScriptRoot '..\docs')
)

function Shot([string]$name, [string[]]$appArgs) {
    # Written to the temp folder first: synced folders (OneDrive) can briefly lock a file the app is about to create.
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "plt-shot-$name.png"
    # The previous web view needs a moment to release its profile folder, so retry a couple of times.
    for ($try = 1; $try -le 3; $try++) {
        Start-Sleep -Seconds 2
        Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
        $p = Start-Process -FilePath $Exe -ArgumentList (@('--demo', '--lang=en', "--shot=$tmp") + $appArgs) -PassThru
        if (-not $p.WaitForExit(60000)) { $p.Kill(); continue }
        if (Test-Path -LiteralPath $tmp) {
            Copy-Item -LiteralPath $tmp -Destination (Join-Path $Out "$name.png") -Force
            Remove-Item -LiteralPath $tmp
            Write-Host "wrote $name.png"
            return
        }
    }
    throw "no screenshot written: $name"
}

Shot 'screenshot'          @('--page=devices',  '--theme=dark')
Shot 'screenshot-light'    @('--page=devices',  '--theme=light', '--accent=blue')
Shot 'screenshot-map'      @('--page=map',      '--theme=dark')
Shot 'screenshot-history'  @('--page=history',  '--theme=dark', '--accent=violet')
Shot 'screenshot-settings' @('--page=settings', '--theme=light')
