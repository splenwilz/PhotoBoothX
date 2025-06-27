namespace Photobooth.Configuration
{
    /// <summary>
    /// Centralized configuration for Photobooth application settings
    /// </summary>
    public static class PhotoboothConfiguration
    {
        /// <summary>
        /// Template display size constants - centralized to avoid duplication
        /// </summary>
        public static class TemplateDisplaySizes
        {
            // Three consistent template sizes for UI display
            public const double WideWidth = 300.0;     // Even larger cards
            public const double WideHeight = 210.0;    // Even larger cards
            public const double TallWidth = 280.0;     // Even larger cards
            public const double TallHeight = 210.0;    // Even larger cards
            public const double SquareWidth = 290.0;   // Even larger cards
            public const double SquareHeight = 210.0;  // Even larger cards
        }

        /// <summary>
        /// Template aspect ratio thresholds for categorization
        /// </summary>
        public static class AspectRatioThresholds
        {
            public const double WideThreshold = 1.3;   // Ratios > 1.3 are considered wide
            public const double TallThreshold = 0.8;   // Ratios < 0.8 are considered tall
        }

        // Note: ProductValidationRanges removed since database-level TemplateType filtering
        // is the authoritative filter and additional aspect ratio validation was causing
        // valid templates to be incorrectly filtered out
    }
} 