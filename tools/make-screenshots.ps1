# Regenerates the README screenshots from --demo mode (fake devices, no hardware needed).
# Usage:  powershell -File tools\make-screenshots.ps1 [-Exe path\to\PowerlineTool.exe]
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\PowerlineTool\bin\Release\net8.0-windows\PowerlineTool.exe'),
    [string]$Out = (Join-Path $PSScriptRoot '..\docs')
)

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public class ShotWin {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@

function Shot([string]$name, [string[]]$appArgs) {
    $p = Start-Process -FilePath $Exe -ArgumentList (@('--demo', '--lang=en') + $appArgs) -PassThru
    Start-Sleep 5
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(2, 2)   # keep taskbar previews out of the shot
    $p.Refresh()
    [ShotWin]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep 1
    $r = New-Object ShotWin+RECT
    [ShotWin]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $Out "$name.png"))
    $p | Stop-Process
}

Shot 'screenshot'          @('--page=devices', '--theme=light')
Shot 'screenshot-map'      @('--page=map', '--theme=light')
Shot 'screenshot-history'  @('--page=history', '--theme=light')
Shot 'screenshot-dark'     @('--page=devices', '--theme=dark')

