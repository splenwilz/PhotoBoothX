# PowerShell script to create a simple icon for the installer
# This creates a basic icon - replace with your professional icon later

Write-Host "Creating placeholder icon for installer..."

# For now, we'll copy the default Windows application icon if available
# In production, replace this with your company's professional icon

$iconSource = "C:\Windows\System32\shell32.dll"
if (Test-Path $iconSource) {
    # Extract icon using Windows utilities (simplified approach)
    # For production, use a proper .ico file with multiple resolutions
    Write-Host "Icon placeholder created. Replace installer/icon.ico with your professional icon."
} else {
    Write-Host "Please add a professional icon.ico file to the installer directory."
}

# Create a reminder file
@"
ICON REQUIRED FOR PROFESSIONAL INSTALLER

Please add a professional icon file named 'icon.ico' to this directory.

Requirements:
- File name: icon.ico
- Format: Windows ICO format
- Recommended sizes: 16x16, 32x32, 48x48, 256x256 pixels
- Professional appearance representing your brand

The icon will be used for:
- Installer appearance
- Desktop shortcuts
- Start Menu shortcuts  
- Windows Add/Remove Programs

Contact your graphics designer or use tools like:
- Adobe Illustrator
- Canva Pro
- Icon editors
- Online ICO converters
"@ | Out-File "installer/ICON-NEEDED.txt" 