using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Photobooth.Models;

namespace Photobooth.Services
{
    /// <summary>
    /// Centralized service for handling all pricing logic and calculations
    /// </summary>
    public interface IPricingService
    {
        /// <summary>
        /// Get the base price for a template based on product type
        /// </summary>
        decimal GetTemplateBasePrice(ProductType productType);
        
        /// <summary>
        /// Get the base price for a template based on template type string
        /// </summary>
        decimal GetTemplateBasePrice(string templateType);
        
        /// <summary>
        /// Calculate the total price for a product with extra copies
        /// </summary>
        decimal CalculateTotalPrice(Product product, int quantity, int extraCopies = 0);
        
        /// <summary>
        /// Calculate extra copy pricing for a product
        /// </summary>
        decimal CalculateExtraCopyPrice(Product product, int extraCopies);
        
        /// <summary>
        /// Get the default pricing configuration for a product type
        /// </summary>
        ProductPricingConfig GetDefaultPricingConfig(ProductType productType);
    }

    public class PricingService : IPricingService
    {
        private readonly IDatabaseService _databaseService;
        
        // Default pricing configuration
        private static readonly Dictionary<ProductType, decimal> DefaultBasePrices = new()
        {
            { ProductType.PhotoStrips, 5.00m },
            { ProductType.Photo4x6, 3.00m },
            { ProductType.SmartphonePrint, 2.00m }
        };

        public PricingService(IDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        /// <summary>
        /// Get the base price for a template based on product type
        /// </summary>
        public decimal GetTemplateBasePrice(ProductType productType)
        {
            return DefaultBasePrices.TryGetValue(productType, out var price) ? price : 3.00m;
        }

        /// <summary>
        /// Get the base price for a template based on template type string
        /// </summary>
        public decimal GetTemplateBasePrice(string templateType)
        {
            if (string.IsNullOrWhiteSpace(templateType))
                return 3.00m;

            return templateType.ToLowerInvariant() switch
            {
                "strips" or "photostrips" => DefaultBasePrices[ProductType.PhotoStrips],
                "4x6" or "photo4x6" => DefaultBasePrices[ProductType.Photo4x6],
                "phone" or "smartphoneprint" => DefaultBasePrices[ProductType.SmartphonePrint],
                _ => 3.00m
            };
        }

        /// <summary>
        /// Calculate the total price for a product with extra copies
        /// </summary>
        public decimal CalculateTotalPrice(Product product, int quantity, int extraCopies = 0)
        {
            if (product == null)
                return 0m;

            var basePrice = product.Price * quantity;
            var extraCopyPrice = CalculateExtraCopyPrice(product, extraCopies);
            
            return basePrice + extraCopyPrice;
        }

        /// <summary>
        /// Calculate extra copy pricing for a product
        /// </summary>
        public decimal CalculateExtraCopyPrice(Product product, int extraCopies)
        {
            if (product == null || extraCopies <= 0)
                return 0m;

            // If custom pricing is not enabled, use base price
            if (!product.UseCustomExtraCopyPricing)
            {
                return product.Price * extraCopies;
            }

            // Use product-specific pricing based on product type
            return product.ProductType switch
            {
                ProductType.PhotoStrips => CalculateStripsExtraCopyPrice(product, extraCopies),
                ProductType.Photo4x6 => CalculatePhoto4x6ExtraCopyPrice(product, extraCopies),
                ProductType.SmartphonePrint => CalculateSmartphoneExtraCopyPrice(product, extraCopies),
                _ => product.Price * extraCopies
            };
        }

        private decimal CalculateStripsExtraCopyPrice(Product product, int extraCopies)
        {
            if (product.StripsExtraCopyPrice.HasValue)
            {
                var baseExtraPrice = product.StripsExtraCopyPrice.Value * extraCopies;
                
                // Apply multiple copy discount if applicable
                if (extraCopies >= 2 && product.StripsMultipleCopyDiscount.HasValue)
                {
                    var discount = ClampPercentage(product.StripsMultipleCopyDiscount.Value) / 100m;
                    baseExtraPrice *= (1 - discount);
                }
                
                return baseExtraPrice;
            }
            
            return product.Price * extraCopies;
        }

        private decimal CalculatePhoto4x6ExtraCopyPrice(Product product, int extraCopies)
        {
            if (product.Photo4x6ExtraCopyPrice.HasValue)
            {
                var baseExtraPrice = product.Photo4x6ExtraCopyPrice.Value * extraCopies;
                
                // Apply multiple copy discount if applicable
                if (extraCopies >= 2 && product.Photo4x6MultipleCopyDiscount.HasValue)
                {
                    var discount = ClampPercentage(product.Photo4x6MultipleCopyDiscount.Value) / 100m;
                    baseExtraPrice *= (1 - discount);
                }
                
                return baseExtraPrice;
            }
            
            return product.Price * extraCopies;
        }

        private decimal CalculateSmartphoneExtraCopyPrice(Product product, int extraCopies)
        {
            if (product.SmartphoneExtraCopyPrice.HasValue)
            {
                var baseExtraPrice = product.SmartphoneExtraCopyPrice.Value * extraCopies;
                
                // Apply multiple copy discount if applicable
                if (extraCopies >= 2 && product.SmartphoneMultipleCopyDiscount.HasValue)
                {
                    var discount = ClampPercentage(product.SmartphoneMultipleCopyDiscount.Value) / 100m;
                    baseExtraPrice *= (1 - discount);
                }
                
                return baseExtraPrice;
            }
            
            return product.Price * extraCopies;
        }

        /// <summary>
        /// Get the default pricing configuration for a product type
        /// </summary>
        public ProductPricingConfig GetDefaultPricingConfig(ProductType productType)
        {
            return new ProductPricingConfig
            {
                BasePrice = GetTemplateBasePrice(productType),
                ProductType = productType,
                UseCustomExtraCopyPricing = false
            };
        }

        /// <summary>
        /// Helper to clamp percentage values to [0, 100] to avoid negative pricing
        /// </summary>
        private static decimal ClampPercentage(decimal value)
        {
            if (value < 0m) return 0m;
            if (value > 100m) return 100m;
            return value;
        }
    }

    /// <summary>
    /// Configuration object for product pricing
    /// </summary>
    public class ProductPricingConfig
    {
        public decimal BasePrice { get; set; }
        public ProductType ProductType { get; set; }
        public bool UseCustomExtraCopyPricing { get; set; }
        public decimal? ExtraCopy1Price { get; set; }
        public decimal? ExtraCopy2Price { get; set; }
        public decimal? ExtraCopy4BasePrice { get; set; }
        public decimal? ExtraCopyAdditionalPrice { get; set; }
        public decimal? StripsExtraCopyPrice { get; set; }
        public decimal? StripsMultipleCopyDiscount { get; set; }
        public decimal? Photo4x6ExtraCopyPrice { get; set; }
        public decimal? Photo4x6MultipleCopyDiscount { get; set; }
        public decimal? SmartphoneExtraCopyPrice { get; set; }
        public decimal? SmartphoneMultipleCopyDiscount { get; set; }
    }
} 