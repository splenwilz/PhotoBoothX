using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth.Services
{
    public class TemplateManager
    {
        private readonly IDatabaseService _databaseService;
        private readonly string _templatesDirectory;
        private readonly string[] _imageExtensions = { ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".webp", ".png" };

        public TemplateManager(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
            
            // Get templates directory from project directory instead of AppData
            _templatesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            
            // Ensure templates directory exists
            if (!Directory.Exists(_templatesDirectory))
            {
                Directory.CreateDirectory(_templatesDirectory);
            }
            
            System.Diagnostics.Debug.WriteLine($"TemplateManager initialized - Templates directory: {_templatesDirectory}");
        }

        /// <summary>
        /// Upload templates from selected folders
        /// </summary>
        public async Task<TemplateUploadResult> UploadFromFoldersAsync(string[] folderPaths)
        {
            var result = new TemplateUploadResult();
            
            foreach (var folderPath in folderPaths)
            {
                try
                {
                    if (!Directory.Exists(folderPath))
                    {
                        result.Results.Add(new TemplateValidationResult
                        {
                            IsValid = false,
                            Errors = { $"Folder not found: {folderPath}" }
                        });
                        continue;
                    }
                    
                    // Find all template folders in the selected path
                    var templateFolders = FindTemplateFolders(folderPath);
                    
                    foreach (var templateFolder in templateFolders)
                    {
                        var validationResult = await ValidateTemplateFolder(templateFolder);
                        result.Results.Add(validationResult);
                        
                        if (validationResult.IsValid && validationResult.Template != null)
                        {
                            await ProcessValidTemplate(validationResult.Template, templateFolder);
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailureCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Results.Add(new TemplateValidationResult
                    {
                        IsValid = false,
                        Errors = { $"Error processing folder '{folderPath}': {ex.Message}" }
                    });
                    result.FailureCount++;
                }
            }
            
            result.Success = result.SuccessCount > 0;
            result.Message = $"Upload completed: {result.SuccessCount} successful, {result.FailureCount} failed";
            
            return result;
        }

        /// <summary>
        /// Upload templates from ZIP file
        /// </summary>
        public async Task<TemplateUploadResult> UploadFromZipAsync(string zipFilePath)
        {
            var result = new TemplateUploadResult();
            
            try
            {
                if (!File.Exists(zipFilePath))
                {
                    result.Message = "ZIP file not found";
                    return result;
                }
                
                // Create temporary extraction directory
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                
                try
                {
                    // Extract ZIP file
                    ZipFile.ExtractToDirectory(zipFilePath, tempDir);
                    
                    // Find template folders in extracted content
                    var templateFolders = FindTemplateFolders(tempDir);
                    
                    foreach (var templateFolder in templateFolders)
                    {
                        var validationResult = await ValidateTemplateFolder(templateFolder);
                        result.Results.Add(validationResult);
                        
                        if (validationResult.IsValid && validationResult.Template != null)
                        {
                            await ProcessValidTemplate(validationResult.Template, templateFolder);
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailureCount++;
                        }
                    }
                    
                    result.Success = result.SuccessCount > 0;
                    result.Message = $"ZIP upload completed: {result.SuccessCount} successful, {result.FailureCount} failed";
                }
                finally
                {
                    // Clean up temporary directory
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Error processing ZIP file: {ex.Message}";
                result.FailureCount++;
            }
            
            return result;
        }

        /// <summary>
        /// Refresh templates by scanning the templates directory
        /// </summary>
        public async Task<TemplateUploadResult> RefreshTemplatesAsync()
        {
            var result = new TemplateUploadResult();
            
            try
            {
                if (!Directory.Exists(_templatesDirectory))
                {
                    // Create the templates directory if it doesn't exist
                    Directory.CreateDirectory(_templatesDirectory);
                    result.Message = "Templates directory created - no templates found";
                    result.Success = true;
                    return result;
                }
                
                // Get existing templates from database
                var existingTemplatesResult = await _databaseService.GetAllTemplatesAsync();
                var existingTemplates = existingTemplatesResult.Success && existingTemplatesResult.Data != null 
                    ? existingTemplatesResult.Data.ToList() 
                    : new List<Template>();
                
                // Find all template folders
                var templateFolders = FindTemplateFolders(_templatesDirectory);
                
                System.Diagnostics.Debug.WriteLine($"Found {templateFolders.Count} template folders in {_templatesDirectory}");
                
                var processedCount = 0;
                var skippedCount = 0;
                
                foreach (var templateFolder in templateFolders)
                {
                    try
                    {
                        var folderName = Path.GetFileName(templateFolder);
                        
                        // Check if template already exists
                        var existingTemplate = existingTemplates.FirstOrDefault(t => 
                            Path.GetFileName(t.FolderPath) == folderName);
                        
                        if (existingTemplate != null)
                        {
                            // Skip existing templates
                            skippedCount++;
                            System.Diagnostics.Debug.WriteLine($"Skipping existing template: {folderName}");
                            continue;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Processing new template: {folderName}");
                        
                        // Validate and process new template
                        var validationResult = await ValidateTemplateFolder(templateFolder, isRefresh: true);
                        result.Results.Add(validationResult);
                        
                        if (validationResult.IsValid && validationResult.Template != null)
                        {
                            // Don't copy files since they're already in the templates directory
                            await ProcessValidTemplate(validationResult.Template, templateFolder, copyFiles: false);
                            result.SuccessCount++;
                            processedCount++;
                            System.Diagnostics.Debug.WriteLine($"Successfully imported template: {folderName}");
                        }
                        else
                        {
                            result.FailureCount++;
                            System.Diagnostics.Debug.WriteLine($"Failed to import template: {folderName} - {string.Join(", ", validationResult.Errors)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Results.Add(new TemplateValidationResult
                        {
                            IsValid = false,
                            Errors = { $"Error processing template folder '{templateFolder}': {ex.Message}" }
                        });
                        result.FailureCount++;
                        System.Diagnostics.Debug.WriteLine($"Exception processing template folder '{templateFolder}': {ex.Message}");
                    }
                }
                
                result.Success = result.SuccessCount > 0 || result.FailureCount == 0;
                
                // Create informative message
                if (result.SuccessCount > 0)
                {
                    result.Message = $"Imported {result.SuccessCount} new templates";
                    if (skippedCount > 0)
                    {
                        result.Message += $" (skipped {skippedCount} existing)";
                    }
                    if (result.FailureCount > 0)
                    {
                        result.Message += $", {result.FailureCount} failed";
                    }
                }
                else if (skippedCount > 0)
                {
                    result.Message = $"Found {skippedCount} existing templates - no new templates to import";
                    result.Success = true;
                }
                else if (templateFolders.Count == 0)
                {
                    result.Message = "No template folders found in templates directory";
                    result.Success = true;
                }
                else
                {
                    // Only show this if we actually found folders but none were valid
                    result.Message = $"Found {templateFolders.Count} template folders but none were valid to import";
                    if (result.FailureCount > 0)
                    {
                        result.Message += $" ({result.FailureCount} had errors)";
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Template refresh completed: {result.Message}");
            }
            catch (Exception ex)
            {
                result.Message = $"Error during refresh: {ex.Message}";
                result.FailureCount++;
                System.Diagnostics.Debug.WriteLine($"Template refresh error: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Synchronize database with file system - File system is the source of truth
        /// Removes orphaned database records and adds new templates found on disk
        /// PRODUCTION-READY: Handles path inconsistencies and aggressive cleanup
        /// </summary>
        public async Task<TemplateUploadResult> SynchronizeWithFileSystemAsync()
        {
            var result = new TemplateUploadResult();
            
            LoggingService.Application.Information("Template synchronization started",
                ("TemplatesDirectory", _templatesDirectory));
            
            try
            {


                if (!Directory.Exists(_templatesDirectory))
                {

                    Directory.CreateDirectory(_templatesDirectory);
                    result.Message = "Templates directory created - no templates found";
                    result.Success = true;
                    return result;
                }

                // Step 1: Get all existing templates from database
                var existingTemplatesResult = await _databaseService.GetAllTemplatesAsync();
                var existingTemplates = existingTemplatesResult.Success && existingTemplatesResult.Data != null 
                    ? existingTemplatesResult.Data.ToList() 
                    : new List<Template>();

                // Step 2: Get all template folders from file system (AUTHORITATIVE SOURCE)
                var templateFolders = FindTemplateFolders(_templatesDirectory);
                var folderNames = templateFolders.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                LoggingService.Application.Information("Template folders discovered on disk",
                    ("FolderCount", templateFolders.Count),
                    ("Folders", templateFolders.Select(Path.GetFileName).ToArray()));


                foreach (var folder in templateFolders)
                {

                }

                foreach (var template in existingTemplates)
                {

                }

                // Step 3: AGGRESSIVE ORPHAN DETECTION
                // Any template in database that doesn't have a corresponding folder on disk should be removed
                var orphanedTemplates = new List<Template>();
                
                foreach (var template in existingTemplates)
                {
                    var folderName = Path.GetFileName(template.FolderPath);
                    var shouldKeep = false;
                    
                    // Check if this template's folder exists on disk (case-insensitive)
                    if (!string.IsNullOrEmpty(folderName))
                    {
                        // Look for exact folder match (case-insensitive)
                        shouldKeep = folderNames.Contains(folderName);
                        
                        // Also verify the full path exists
                        if (shouldKeep && !string.IsNullOrEmpty(template.FolderPath))
                        {
                            shouldKeep = Directory.Exists(template.FolderPath);
                        }
                    }
                    
                    if (!shouldKeep)
                    {
                        orphanedTemplates.Add(template);


                    }
                }

                // Step 4: AGGRESSIVELY REMOVE ALL ORPHANED TEMPLATES

                var removedCount = 0;
                
                foreach (var orphanedTemplate in orphanedTemplates)
                {

                    var deleteResult = await _databaseService.DeleteTemplateAsync(orphanedTemplate.Id);
                    
                    if (deleteResult.Success)
                    {

                        removedCount++;
                    }
                    else
                    {

                        result.FailureCount++;
                    }
                }

                // Step 5: ADD NEW TEMPLATES FOUND ON DISK

                var addedCount = 0;
                var updatedCount = 0;
                
                // Get fresh list of templates after cleanup
                var refreshedResult = await _databaseService.GetAllTemplatesAsync();
                var currentTemplates = refreshedResult.Success && refreshedResult.Data != null 
                    ? refreshedResult.Data.ToList() 
                    : new List<Template>();

                foreach (var templateFolder in templateFolders)
                {
                    try
                    {
                        var folderName = Path.GetFileName(templateFolder);

                        // Check if template exists in database by exact folder path first, then by folder name
                        var existingTemplate = currentTemplates.FirstOrDefault(t => 
                            !string.IsNullOrEmpty(t.FolderPath) &&
                            (string.Equals(t.FolderPath, templateFolder, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(Path.GetFileName(t.FolderPath), folderName, StringComparison.OrdinalIgnoreCase)));

                        if (existingTemplate == null)
                        {
                            // New template - validate and add

                            var validationResult = await ValidateTemplateFolder(templateFolder, isRefresh: true);
                            result.Results.Add(validationResult);
                            
                            if (validationResult.IsValid && validationResult.Template != null)
                            {
                                // Ensure correct folder path (use current bin directory path)
                                validationResult.Template.FolderPath = templateFolder;
                                
                                await ProcessValidTemplate(validationResult.Template, templateFolder, copyFiles: false);
                                result.SuccessCount++;
                                addedCount++;

                            }
                            else
                            {
                                result.FailureCount++;

                            }
                        }
                        else
                        {
                            // Existing template - verify paths and update if needed

                            var needsUpdate = false;
                            var expectedFolderPath = templateFolder;
                            
                            // Check if path needs updating (different case or location)
                            if (!string.Equals(existingTemplate.FolderPath, expectedFolderPath, StringComparison.Ordinal))
                            {

                                needsUpdate = true;
                            }
                            
                            // Check if template and config files exist
                            var expectedTemplatePath = Path.Combine(templateFolder, "template.png");
                            var expectedConfigPath = Path.Combine(templateFolder, "config.json");
                            
                            if (!File.Exists(existingTemplate.TemplatePath) || 
                                !File.Exists(existingTemplate.ConfigPath) ||
                                !string.Equals(existingTemplate.TemplatePath, expectedTemplatePath, StringComparison.Ordinal) ||
                                !string.Equals(existingTemplate.ConfigPath, expectedConfigPath, StringComparison.Ordinal))
                            {

                                needsUpdate = true;
                            }
                            
                            if (needsUpdate)
                            {
                                // Re-validate and update
                                var validationResult = await ValidateTemplateFolder(templateFolder, isRefresh: true);
                                if (validationResult.IsValid && validationResult.Template != null)
                                {
                                    // Update database record with correct paths
                                    var updateResult = await _databaseService.UpdateTemplatePathsAsync(
                                        existingTemplate.Id,
                                        templateFolder,
                                        validationResult.Template.TemplatePath,
                                        validationResult.Template.PreviewPath
                                    );
                                    
                                    if (updateResult.Success)
                                    {
                                        updatedCount++;

                                    }
                                    else
                                    {

                                    }
                                }
                            }
                            else
                            {

                            }
                        }
                    }
                    catch
                    {
                        result.FailureCount++;

                    }
                }

                // Step 6: BUILD COMPREHENSIVE RESULT MESSAGE
                var messages = new List<string>();
                
                if (addedCount > 0)
                    messages.Add($"{addedCount} new templates added");
                    
                if (updatedCount > 0)
                    messages.Add($"{updatedCount} templates updated");
                    
                if (removedCount > 0)
                    messages.Add($"{removedCount} orphaned records removed");
                    
                if (result.FailureCount > 0)
                    messages.Add($"{result.FailureCount} errors occurred");

                if (messages.Count == 0)
                {
                    result.Message = "Database is already synchronized with file system";
                }
                else
                {
                    result.Message = "Synchronization complete: " + string.Join(", ", messages);
                }

                result.Success = true;


            }
            catch (Exception ex)
            {
                result.Message = $"Error during synchronization: {ex.Message}";
                result.FailureCount++;


            }
            
            return result;
        }

        /// <summary>
        /// Find all template folders in a directory
        /// </summary>
        private List<string> FindTemplateFolders(string rootPath)
        {
            var templateFolders = new List<string>();
            
            LoggingService.Application.Debug("Searching for template folders",
                ("RootPath", rootPath));
            
            try
            {
                // Check if root path itself is a template folder
                if (IsTemplateFolder(rootPath))
                {
                    Console.WriteLine($"Root path is a template folder: {rootPath}");
                    templateFolders.Add(rootPath);
                }
                
                // Check subdirectories
                var subdirectories = Directory.GetDirectories(rootPath);
                Console.WriteLine($"Found {subdirectories.Length} subdirectories in {rootPath}:");
                foreach (var subdirectory in subdirectories)
                {
                    Console.WriteLine($"  Checking subdirectory: {subdirectory}");
                    if (IsTemplateFolder(subdirectory))
                    {
                        Console.WriteLine($"    ✓ Template folder found: {subdirectory}");
                        templateFolders.Add(subdirectory);
                    }
                    else
                    {
                        Console.WriteLine($"    - Not a template folder, searching recursively");
                        // Recursively search subdirectories
                        templateFolders.AddRange(FindTemplateFolders(subdirectory));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding template folders in '{rootPath}': {ex.Message}");
            }
            
            return templateFolders;
        }

        /// <summary>
        /// Check if a folder contains a template
        /// </summary>
        private bool IsTemplateFolder(string folderPath)
        {
            var templatePath = Path.Combine(folderPath, "template.png");
            var exists = File.Exists(templatePath);
            Console.WriteLine($"    Checking for template.png: {templatePath} - {(exists ? "EXISTS" : "NOT FOUND")}");
            return exists;
        }

        /// <summary>
        /// Validate a template folder and create Template object
        /// </summary>
        private async Task<TemplateValidationResult> ValidateTemplateFolder(string folderPath, bool isRefresh = false)
        {
            var result = new TemplateValidationResult();
            var folderName = Path.GetFileName(folderPath);
            
            Console.WriteLine($"=== VALIDATING TEMPLATE FOLDER: {folderName} ===");
            Console.WriteLine($"Full path: {folderPath}");
            
            try
            {
                var templatePath = Path.Combine(folderPath, "template.png");
                
                // Validate required files
                if (!File.Exists(templatePath))
                {
                    result.IsValid = false;
                    result.Errors.Add("template.png not found");
                    return result;
                }
                
                // Get template dimensions
                var (width, height) = GetImageDimensions(templatePath);
                Console.WriteLine($"Template dimensions: {width}x{height}");
                
                if (width == 0 || height == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Could not determine template dimensions");
                    Console.WriteLine("✗ Could not determine template dimensions");
                    return result;
                }
                
                // Find appropriate layout from database based on dimensions and folder path
                var layoutId = await FindBestLayoutForDimensions(width, height, folderPath);
                Console.WriteLine($"Layout ID found: {layoutId}");
                
                if (string.IsNullOrEmpty(layoutId))
                {
                    result.IsValid = false;
                    result.Errors.Add($"No suitable layout found for dimensions {width}x{height}");
                    Console.WriteLine($"✗ No suitable layout found for dimensions {width}x{height}");
                    return result;
                }
                
                // Find preview image
                var previewPath = FindPreviewImage(folderPath);
                if (string.IsNullOrEmpty(previewPath))
                {
                    previewPath = templatePath; // Use template.png as preview
                    result.Warnings.Add("No preview image found, using template.png");
                }
                
                // Determine category from folder structure or use default
                var categoryName = DetermineCategoryFromPath(folderPath);
                var categoryId = await GetOrCreateCategoryAsync(categoryName);
                
                // Get layout to determine template type
                var layoutResult = await _databaseService.GetTemplateLayoutAsync(layoutId);
                var templateType = TemplateType.Strip; // Default to Strip
                
                if (layoutResult.Success && layoutResult.Data != null)
                {
                    Console.WriteLine($"Layout ProductCategoryId: {layoutResult.Data.ProductCategoryId}");
                    // Set template type based on layout's product category
                    templateType = layoutResult.Data.ProductCategoryId switch
                    {
                        1 => TemplateType.Strip,      // Strips category
                        2 => TemplateType.Photo4x6,   // 4x6 category  
                        3 => TemplateType.Photo4x6,   // Smartphone prints use 4x6 templates
                        _ => TemplateType.Strip       // Default to Strip
                    };
                    Console.WriteLine($"Template type determined: {templateType}");
                }
                
                // Create template object
                var template = new Template
                {
                    Name = folderName,
                    CategoryId = categoryId,
                    CategoryName = categoryName,
                    LayoutId = layoutId,
                    FolderPath = isRefresh ? folderPath : "",
                    TemplatePath = isRefresh ? templatePath : "",
                    PreviewPath = isRefresh ? previewPath : "",
                    Price = 0, // Default price
                    Description = $"Template with dimensions {width}x{height}",
                    FileSize = new FileInfo(templatePath).Length,
                    HasPreview = previewPath != templatePath,
                    ValidationWarnings = result.Warnings,
                    TemplateType = templateType // Set the correct template type
                };
                
                Console.WriteLine($"✓ Template object created successfully:");
                Console.WriteLine($"  - Name: {template.Name}");
                Console.WriteLine($"  - LayoutId: {template.LayoutId}");
                Console.WriteLine($"  - TemplateType: {template.TemplateType}");
                Console.WriteLine($"  - CategoryId: {template.CategoryId}");
                Console.WriteLine($"  - FolderPath: {template.FolderPath}");
                
                result.Template = template;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Error validating template '{folderName}': {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Find the best layout for given dimensions and folder path
        /// </summary>
        private async Task<string> FindBestLayoutForDimensions(int width, int height, string folderPath = "")
        {
            Console.WriteLine($"=== FINDING LAYOUT FOR DIMENSIONS: {width}x{height} ===");
            Console.WriteLine($"Folder path: {folderPath}");
            
            try
            {
                var layoutsResult = await _databaseService.GetTemplateLayoutsAsync();
                if (!layoutsResult.Success || layoutsResult.Data == null)
                {
                    Console.WriteLine("Failed to get layouts from database");
                    return "";
                }
                
                var layouts = layoutsResult.Data.Where(l => l.IsActive).ToList();
                Console.WriteLine($"Found {layouts.Count} active layouts in database:");
                foreach (var layout in layouts)
                {
                    Console.WriteLine($"  - {layout.LayoutKey}: {layout.Width}x{layout.Height} (ID: {layout.Id})");
                }
                
                // If folder path is provided, try to match by folder structure first
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var parentFolderName = Path.GetFileName(Path.GetDirectoryName(folderPath));
                    Console.WriteLine($"Parent folder name: {parentFolderName}");
                    
                    if (!string.IsNullOrEmpty(parentFolderName))
                    {
                        Console.WriteLine($"Looking for layout with key: {parentFolderName}");
                        var layoutByFolder = layouts.FirstOrDefault(l => 
                            string.Equals(l.LayoutKey, parentFolderName, StringComparison.OrdinalIgnoreCase) &&
                            l.Width == width && l.Height == height);
                        
                        if (layoutByFolder != null)
                        {
                            Console.WriteLine($"✓ Found matching layout by folder name: {layoutByFolder.LayoutKey} (ID: {layoutByFolder.Id})");
                            return layoutByFolder.Id;
                        }
                        else
                        {
                            Console.WriteLine($"✗ No layout found matching folder name: {parentFolderName}");
                        }
                    }
                }
                
                // First, try to find exact match (fallback for backward compatibility)
                var exactMatch = layouts.FirstOrDefault(l => l.Width == width && l.Height == height);
                if (exactMatch != null)
                {
                    return exactMatch.Id;
                }
                
                // If no exact match, find closest match (within 20px tolerance)
                var tolerance = 20;
                var closeMatch = layouts.FirstOrDefault(l => 
                    Math.Abs(l.Width - width) <= tolerance && 
                    Math.Abs(l.Height - height) <= tolerance);
                
                if (closeMatch != null)
                {
                    return closeMatch.Id;
                }
                
                // If still no match, try to find by aspect ratio and general category
                var aspectRatio = (double)width / height;
                
                // Photo strip layouts (tall and narrow)
                if (aspectRatio < 0.5 && height > 1500)
                {
                    var stripLayout = layouts.FirstOrDefault(l => l.Name.Contains("strip", StringComparison.OrdinalIgnoreCase));
                    if (stripLayout != null) return stripLayout.Id;
                }
                
                // Photo layouts (square or landscape)
                if (aspectRatio >= 0.5)
                {
                    var photoLayout = layouts.FirstOrDefault(l => l.Name.Contains("photo", StringComparison.OrdinalIgnoreCase));
                    if (photoLayout != null) return photoLayout.Id;
                }
                
                // Fallback to first available layout
                return layouts.FirstOrDefault()?.Id ?? "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding layout for dimensions {width}x{height}: {ex.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Determine category from folder path
        /// </summary>
        private string DetermineCategoryFromPath(string folderPath)
        {
            var folderName = Path.GetFileName(folderPath).ToLowerInvariant();
            var parentFolder = Path.GetFileName(Path.GetDirectoryName(folderPath))?.ToLowerInvariant();
            
            // Look for category hints in folder names
            if (folderName.Contains("fun") || folderName.Contains("party") || folderName.Contains("mixtape"))
                return "Fun";
            if (folderName.Contains("classic") || folderName.Contains("elegant") || folderName.Contains("minimalist"))
                return "Classic";
            if (folderName.Contains("holiday") || folderName.Contains("christmas") || folderName.Contains("halloween"))
                return "Holiday";
            if (folderName.Contains("seasonal") || folderName.Contains("summer") || folderName.Contains("winter"))
                return "Seasonal";
            if (folderName.Contains("film") || folderName.Contains("vintage") || folderName.Contains("retro"))
                return "Vintage";
            
            // Default category
            return "Classic";
        }

        /// <summary>
        /// Find the best preview image in the folder
        /// </summary>
        private string FindPreviewImage(string folderPath)
        {
            try
            {
                var files = Directory.GetFiles(folderPath);
                
                // First, look for images that are NOT .png
                var nonPngImages = files.Where(f => 
                    _imageExtensions.Contains(Path.GetExtension(f).ToLower()) &&
                    !Path.GetExtension(f).Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(f).Equals("template.png", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (nonPngImages.Any())
                {
                    return nonPngImages.First();
                }
                
                // If no non-PNG images, look for PNG files (except template.png)
                var pngImages = files.Where(f => 
                    Path.GetExtension(f).Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(f).Equals("template.png", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                return pngImages.FirstOrDefault() ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get image dimensions from file
        /// </summary>
        private (int width, int height) GetImageDimensions(string imagePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                
                return (bitmap.PixelWidth, bitmap.PixelHeight);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// Get existing category or create new one
        /// </summary>
        private async Task<int> GetOrCreateCategoryAsync(string categoryName)
        {
            try
            {
                var categoriesResult = await _databaseService.GetTemplateCategoriesAsync();
                if (categoriesResult.Success && categoriesResult.Data != null)
                {
                    var existing = categoriesResult.Data.FirstOrDefault(c => 
                        c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existing != null)
                    {
                        return existing.Id;
                    }
                }
                
                // Create new category
                var createResult = await _databaseService.CreateTemplateCategoryAsync(categoryName, $"Auto-created category for {categoryName} templates");
                if (createResult.Success && createResult.Data != null)
                {
                    return createResult.Data.Id;
                }
                
                return 1; // Default to first category if creation fails
            }
            catch
            {
                return 1; // Default fallback
            }
        }

        /// <summary>
        /// Process a valid template (copy files and save to database)
        /// </summary>
        private async Task ProcessValidTemplate(Template template, string sourceFolderPath, bool copyFiles = true)
        {
            string targetFolderPath = "";
            
            if (copyFiles)
            {
                // Get layout information to determine proper folder structure
                var layoutResult = await _databaseService.GetTemplateLayoutAsync(template.LayoutId);
                if (!layoutResult.Success || layoutResult.Data == null)
                {
                    throw new Exception($"Layout '{template.LayoutId}' not found");
                }
                
                var layout = layoutResult.Data;
                
                // Create folder structure: Templates/{LayoutKey}/{TemplateName}/
                var layoutFolderPath = Path.Combine(_templatesDirectory, layout.LayoutKey);
                var folderName = Path.GetFileName(sourceFolderPath);
                targetFolderPath = Path.Combine(layoutFolderPath, folderName);
                
                // Ensure layout folder exists
                Directory.CreateDirectory(layoutFolderPath);
                
                // Handle name conflicts
                int suffix = 1;
                while (Directory.Exists(targetFolderPath))
                {
                    targetFolderPath = Path.Combine(layoutFolderPath, $"{folderName}_{suffix}");
                    suffix++;
                }
                
                // Copy entire folder to runtime Templates directory
                CopyDirectory(sourceFolderPath, targetFolderPath);
                
                // Update template paths using relative paths from Templates directory
                var relativeFolderPath = Path.Combine("Templates", layout.LayoutKey, Path.GetFileName(targetFolderPath));
                template.FolderPath = relativeFolderPath;
                template.TemplatePath = Path.Combine(relativeFolderPath, "template.png");
                template.PreviewPath = template.HasPreview 
                    ? Path.Combine(relativeFolderPath, Path.GetFileName(template.PreviewPath))
                    : template.TemplatePath;
                template.ConfigPath = Path.Combine(relativeFolderPath, "config.json");
            }
            else
            {
                // Files are already in place, just update paths
                targetFolderPath = sourceFolderPath;
                template.FolderPath = targetFolderPath;
                template.TemplatePath = Path.Combine(targetFolderPath, "template.png");
                template.ConfigPath = Path.Combine(targetFolderPath, "config.json");
                
                // Find preview image again
                var previewPath = FindPreviewImage(targetFolderPath);
                template.PreviewPath = !string.IsNullOrEmpty(previewPath) ? previewPath : template.TemplatePath;
                template.HasPreview = template.PreviewPath != template.TemplatePath;
            }
            
            // Save to database with improved error handling
            Console.WriteLine($"Saving template to database: {template.Name}");
            var createResult = await _databaseService.CreateTemplateAsync(template);
            if (!createResult.Success)
            {
                // Check if this is a UNIQUE constraint violation
                if (createResult.ErrorMessage?.Contains("UNIQUE constraint failed") == true)
                {
                    // Template already exists - this shouldn't happen with our improved detection
                    // but provide a better error message
                    Console.WriteLine($"✗ Template already exists in database: {template.FolderPath}");
                    throw new Exception($"Template with folder path '{template.FolderPath}' already exists in database");
                }
                else
                {
                    LoggingService.Application.Error("Failed to create template in database", null,
                        ("ErrorMessage", createResult.ErrorMessage ?? "Unknown error"),
                        ("TemplateName", template.Name));
                    throw new Exception($"Failed to create template: {createResult.ErrorMessage}");
                }
            }
            else
            {
                LoggingService.Application.Information("Template saved to database successfully",
                    ("TemplateId", createResult.Data?.Id.ToString() ?? "Unknown"),
                    ("TemplateName", template.Name));
            }
        }

        /// <summary>
        /// Copy directory recursively
        /// </summary>
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            
            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }
            
            // Copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, targetSubDir);
            }
        }

        /// <summary>
        /// Recalculate file sizes for all templates
        /// </summary>
        public async Task<TemplateUploadResult> RecalculateAllFileSizesAsync()
        {
            var result = new TemplateUploadResult();
            
            try
            {
                var templatesResult = await _databaseService.GetAllTemplatesAsync();
                if (!templatesResult.Success || templatesResult.Data == null)
                {
                    result.Message = "Failed to retrieve templates from database";
                    return result;
                }
                
                var updatedCount = 0;
                
                foreach (var template in templatesResult.Data)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(template.TemplatePath) && File.Exists(template.TemplatePath))
                        {
                            var fileInfo = new FileInfo(template.TemplatePath);
                            var newSize = fileInfo.Length;
                            
                            if (template.FileSize != newSize)
                            {
                                await _databaseService.UpdateTemplateFileSizeAsync(template.Id, newSize);
                                updatedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Results.Add(new TemplateValidationResult
                        {
                            IsValid = false,
                            Errors = { $"Error updating file size for template '{template.Name}': {ex.Message}" }
                        });
                        result.FailureCount++;
                    }
                }
                
                result.SuccessCount = updatedCount;
                result.Success = true;
                result.Message = $"File sizes recalculated: {updatedCount} templates updated";
            }
            catch (Exception ex)
            {
                result.Message = $"Error recalculating file sizes: {ex.Message}";
                result.FailureCount++;
            }
            
            return result;
        }

        /// <summary>
        /// Delete template completely (from database and file system)
        /// </summary>
        public async Task<TemplateValidationResult> DeleteTemplateCompletelyAsync(int templateId)
        {
            var result = new TemplateValidationResult();
            
            try
            {
                // Get template details first
                var templateResult = await _databaseService.GetByIdAsync<Template>(templateId);
                if (!templateResult.Success || templateResult.Data == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Template not found in database");
                    return result;
                }
                
                var template = templateResult.Data;
                
                // Delete from database first
                var deleteResult = await _databaseService.DeleteTemplateAsync(templateId);
                if (!deleteResult.Success)
                {
                    result.IsValid = false;
                    result.Errors.Add(deleteResult.ErrorMessage ?? "Failed to delete template from database");
                    return result;
                }
                
                // Delete folder from file system
                if (!string.IsNullOrEmpty(template.FolderPath) && Directory.Exists(template.FolderPath))
                {
                    try
                    {
                        Directory.Delete(template.FolderPath, true);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Could not delete template folder '{template.FolderPath}': {ex.Message}");
                        // Don't fail the operation if folder deletion fails
                    }
                }
                
                result.IsValid = true;
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Error deleting template {templateId}: {ex.Message}");
                return result;
            }
        }







        /// <summary>
        /// Upload templates from selected folders with specific layout
        /// </summary>
        public async Task<TemplateUploadResult> UploadFromFoldersWithLayoutAsync(string[] folderPaths, string layoutId)
        {
            var result = new TemplateUploadResult();
            
            foreach (var folderPath in folderPaths)
            {
                try
                {
                    if (!Directory.Exists(folderPath))
                    {
                        result.Results.Add(new TemplateValidationResult
                        {
                            IsValid = false,
                            Errors = { $"Folder not found: {folderPath}" }
                        });
                        continue;
                    }
                    
                    // Find all template folders in the selected path
                    var templateFolders = FindTemplateFolders(folderPath);
                    
                    foreach (var templateFolder in templateFolders)
                    {
                        var validationResult = await ValidateTemplateFolderWithLayout(templateFolder, layoutId);
                        result.Results.Add(validationResult);
                        
                        if (validationResult.IsValid && validationResult.Template != null)
                        {
                            await ProcessValidTemplate(validationResult.Template, templateFolder);
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailureCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Results.Add(new TemplateValidationResult
                    {
                        IsValid = false,
                        Errors = { $"Error processing folder '{folderPath}': {ex.Message}" }
                    });
                    result.FailureCount++;
                }
            }
            
            result.Success = result.SuccessCount > 0;
            result.Message = $"Upload completed: {result.SuccessCount} successful, {result.FailureCount} failed";
            
            return result;
        }

        /// <summary>
        /// Upload templates from ZIP file with specific layout
        /// </summary>
        public async Task<TemplateUploadResult> UploadFromZipWithLayoutAsync(string zipFilePath, string layoutId)
        {
            var result = new TemplateUploadResult();
            
            try
            {
                if (!File.Exists(zipFilePath))
                {
                    result.Message = "ZIP file not found";
                    return result;
                }
                
                // Create temporary extraction directory
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                
                try
                {
                    // Extract ZIP file
                    ZipFile.ExtractToDirectory(zipFilePath, tempDir);
                    
                    // Find template folders in extracted content
                    var templateFolders = FindTemplateFolders(tempDir);
                    
                    foreach (var templateFolder in templateFolders)
                    {
                        var validationResult = await ValidateTemplateFolderWithLayout(templateFolder, layoutId);
                        result.Results.Add(validationResult);
                        
                        if (validationResult.IsValid && validationResult.Template != null)
                        {
                            await ProcessValidTemplate(validationResult.Template, templateFolder);
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailureCount++;
                        }
                    }
                    
                    result.Success = result.SuccessCount > 0;
                    result.Message = $"ZIP upload completed: {result.SuccessCount} successful, {result.FailureCount} failed";
                }
                finally
                {
                    // Clean up temporary directory
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Error processing ZIP file: {ex.Message}";
                result.FailureCount++;
            }
            
            return result;
        }

        /// <summary>
        /// Validate a template folder with a specific layout
        /// </summary>
        private async Task<TemplateValidationResult> ValidateTemplateFolderWithLayout(string folderPath, string layoutId)
        {
            var result = new TemplateValidationResult();
            var folderName = Path.GetFileName(folderPath);
            
            try
            {
                var templatePath = Path.Combine(folderPath, "template.png");
                
                // Validate required files
                if (!File.Exists(templatePath))
                {
                    result.IsValid = false;
                    result.Errors.Add("template.png not found");
                    return result;
                }
                
                // Verify layout exists
                var layoutResult = await _databaseService.GetTemplateLayoutAsync(layoutId);
                if (!layoutResult.Success || layoutResult.Data == null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Layout '{layoutId}' not found");
                    return result;
                }
                
                var layout = layoutResult.Data;
                
                // Get template dimensions
                var (width, height) = GetImageDimensions(templatePath);
                if (width == 0 || height == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Could not determine template dimensions");
                    return result;
                }
                
                // Verify dimensions match layout (with some tolerance)
                var widthDiff = Math.Abs(width - layout.Width);
                var heightDiff = Math.Abs(height - layout.Height);
                
                if (widthDiff > 10 || heightDiff > 10) // Allow 10px tolerance
                {
                    result.Warnings.Add($"Template dimensions ({width}x{height}) don't exactly match layout dimensions ({layout.Width}x{layout.Height})");
                }
                
                // Find preview image
                var previewPath = FindPreviewImage(folderPath);
                if (string.IsNullOrEmpty(previewPath))
                {
                    previewPath = templatePath; // Use template.png as preview
                    result.Warnings.Add("No preview image found, using template.png");
                }
                
                // Determine category from folder structure or use default
                var categoryName = DetermineCategoryFromPath(folderPath);
                var categoryId = await GetOrCreateCategoryAsync(categoryName);
                
                // Set template type based on layout's product category
                var templateType = layout.ProductCategoryId switch
                {
                    1 => TemplateType.Strip,      // Strips category
                    2 => TemplateType.Photo4x6,   // 4x6 category  
                    3 => TemplateType.Photo4x6,   // Smartphone prints use 4x6 templates
                    _ => TemplateType.Strip       // Default to Strip
                };
                
                // Create template object
                var template = new Template
                {
                    Name = folderName,
                    CategoryId = categoryId,
                    CategoryName = categoryName,
                    LayoutId = layoutId,
                    FolderPath = folderPath,
                    TemplatePath = templatePath,
                    PreviewPath = previewPath,
                    Price = 0, // Default price
                    Description = $"Template for {layout.Name} layout",
                    FileSize = new FileInfo(templatePath).Length,
                    HasPreview = previewPath != templatePath,
                    ValidationWarnings = result.Warnings,
                    TemplateType = templateType // Set the correct template type
                };
                
                result.Template = template;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Error validating template '{folderName}': {ex.Message}");
            }
            
            return result;
        }
    }
} 
