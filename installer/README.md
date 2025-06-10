# Installer Icon Setup

## Required: Add icon.ico File

The Inno Setup script expects an `icon.ico` file in this directory for the installer icon.

### How to Create icon.ico:

1. **Option 1: Online Converter**
   - Create or find a PhotoBoothX logo/icon image (PNG/JPG)
   - Go to: https://convertico.com/ or https://favicon.io/favicon-converter/
   - Upload your image
   - Download as `icon.ico`
   - Place in this `installer/` directory

2. **Option 2: Using Windows**
   - Create a 256x256 pixel image in Paint or any image editor
   - Save as PNG
   - Rename from `.png` to `.ico` (Windows will convert automatically)

3. **Option 3: Professional Tool**
   - Use IconForge, IcoFX, or similar icon editor
   - Create multi-resolution icon (16x16, 32x32, 48x48, 256x256)
   - Export as `icon.ico`

### Recommended Icon Sizes:
- 16x16 (small icons)
- 32x32 (medium icons)  
- 48x48 (large icons)
- 256x256 (high-DPI displays)

### Temporary Solution:
If you don't have an icon ready, comment out this line in `PhotoBoothX.iss`:
```
; SetupIconFile=icon.ico
```

The installer will work without an icon, just won't have a custom icon during installation. 