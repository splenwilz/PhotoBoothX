# PhotoBoothX Build Script
# Local build and installer creation for development and testing

param(
    [string]$Configuration = "Release",
    [switch]$SkipBuild,
    [switch]$SkipInstaller,
    [switch]$OpenOutput,
    [string]$Version = ""
)

Write-Host "PhotoBoothX Build Script" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

# Configuration
$ProjectPath = "PhotoBooth\PhotoBooth.csproj"
$PublishPath = "PhotoBooth\bin\$Configuration\net8.0-windows\win-x64\publish"
$InstallerPath = "installer\PhotoBoothX.iss"
$DistPath = "dist"

# Check if project exists
if (-not (Test-Path $ProjectPath)) {
    Write-Host "ERROR: Project file not found: $ProjectPath" -ForegroundColor Red
    exit 1
}

try {
    # Clean previous builds
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path $PublishPath) {
        Remove-Item $PublishPath -Recurse -Force
    }
    if (Test-Path $DistPath) {
        Remove-Item $DistPath -Recurse -Force
    }

    if (-not $SkipBuild) {
        Write-Host "Building application..." -ForegroundColor Green
        
        # Restore packages
        Write-Host "Restoring NuGet packages..."
        dotnet restore $ProjectPath
        if ($LASTEXITCODE -ne 0) {
            throw "Package restore failed"
        }

        # Build application
        Write-Host "Building project..."
        dotnet build $ProjectPath --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }

        # Publish application
        Write-Host "Publishing application..."
        dotnet publish $ProjectPath `
            --configuration $Configuration `
            --output $PublishPath `
            --self-contained true `
            --runtime win-x64 `
            /p:PublishSingleFile=true `
            /p:PublishReadyToRun=true
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed"
        }

        Write-Host "Build completed successfully!" -ForegroundColor Green
        Write-Host "Published files location: $PublishPath" -ForegroundColor Gray
    } else {
        Write-Host "Skipping build (as requested)" -ForegroundColor Yellow
    }

    if (-not $SkipInstaller) {
        Write-Host ""
        Write-Host "Creating installer..." -ForegroundColor Green

        # Check if Inno Setup is installed
        $InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
        if (-not (Test-Path $InnoSetupPath)) {
            Write-Host "ERROR: Inno Setup not found at: $InnoSetupPath" -ForegroundColor Red
            Write-Host "Please install Inno Setup 6 from: https://www.jrsoftware.org/isinfo.php" -ForegroundColor Yellow
            exit 1
        }

        # Update version in installer if provided
        if ($Version) {
            Write-Host "Updating installer version to: $Version"
            $installerContent = Get-Content $InstallerPath -Raw
            $installerContent = $installerContent -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""
            Set-Content $InstallerPath -Value $installerContent
        }

        # Create dist directory
        New-Item -ItemType Directory -Force -Path $DistPath | Out-Null

        # Build installer
        Write-Host "Running Inno Setup compiler..."
        & $InnoSetupPath $InstallerPath
        if ($LASTEXITCODE -ne 0) {
            throw "Installer creation failed"
        }

        Write-Host "Installer created successfully!" -ForegroundColor Green
        
        # List created files
        $installerFiles = Get-ChildItem -Path $DistPath -Filter "*.exe"
        if ($installerFiles) {
            Write-Host "Installer files created:" -ForegroundColor Gray
            foreach ($file in $installerFiles) {
                $sizeKB = [math]::Round($file.Length / 1KB, 2)
                Write-Host "   - $($file.Name) ($sizeKB KB)" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "Skipping installer creation (as requested)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Build process completed successfully!" -ForegroundColor Green
    
    if ($OpenOutput -and (Test-Path $DistPath)) {
        Write-Host "Opening output directory..."
        Start-Process "explorer.exe" -ArgumentList $DistPath
    }

    # Summary
    Write-Host ""
    Write-Host "Build Summary:" -ForegroundColor Cyan
    Write-Host "   Configuration: $Configuration" -ForegroundColor Gray
    Write-Host "   Published app: $PublishPath" -ForegroundColor Gray
    Write-Host "   Installer: $DistPath" -ForegroundColor Gray
    
    if (-not $SkipInstaller -and (Test-Path "$DistPath\*.exe")) {
        Write-Host ""
        Write-Host "Ready for deployment!" -ForegroundColor Green
        Write-Host "   Your client can install using the .exe file in the dist folder" -ForegroundColor Gray
    }
} catch {
    Write-Host ""
    Write-Host "Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Build script completed!" -ForegroundColor Cyan 