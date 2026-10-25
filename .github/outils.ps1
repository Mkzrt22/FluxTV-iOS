# Outils communs des essais sur le serveur Windows : journal, lancement de l'app avec des arguments,
# capture de l'écran. À charger avec « . .github/outils.ps1 » après avoir défini $dossier et $journal.
function Note([string]$texte) { Add-Content -Path $journal -Value $texte -Encoding UTF8; Write-Host $texte }

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public enum ActivateOptions { None = 0, DesignMode = 1, NoErrorUI = 2, NoSplashScreen = 4 }
[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IApplicationActivationManager {
    void ActivateApplication([In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [In, MarshalAs(UnmanagedType.LPWStr)] string arguments,
                             [In] ActivateOptions options, [Out] out uint processId);
    void ActivateForFile([In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [In] IntPtr itemArray,
                         [In, MarshalAs(UnmanagedType.LPWStr)] string verb, [Out] out uint processId);
    void ActivateForProtocol([In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [In] IntPtr itemArray, [Out] out uint processId);
}
[ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
public class ApplicationActivationManager { }
public static class Lanceur {
    public static uint Lancer(string aumid, string arguments) {
        var m = (IApplicationActivationManager)new ApplicationActivationManager();
        uint pid;
        m.ActivateApplication(aumid, arguments, ActivateOptions.NoSplashScreen, out pid);
        return pid;
    }
}
"@

$jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$qualite = New-Object System.Drawing.Imaging.EncoderParameters 1
$qualite.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 88L

# -Png : copie sans perte en plus (captures d'écran de la fiche du Store, PNG obligatoire)
function Capturer([string]$nom, [switch]$Png) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $image = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($image)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $image.Save((Join-Path (Resolve-Path $dossier) "$nom.jpg"), $jpeg, $qualite)
    if ($Png) {
        $store = Join-Path (Resolve-Path $dossier) 'store'
        New-Item -ItemType Directory -Force $store | Out-Null
        $image.Save((Join-Path $store "$nom.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $g.Dispose()
    $image.Dispose()
}
