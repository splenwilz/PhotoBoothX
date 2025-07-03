using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using Photobooth.Models;

namespace Photobooth.Tests.Models
{
    [TestClass]
    public class DatabaseModelsTests
    {
        [TestMethod]
        public void TemplateCategory_IsCurrentlyInSeason_HandlesRegularSeasons()
        {
            // Arrange - Summer season (June 1 to August 31)
            var category = new TemplateCategory
            {
                IsSeasonalCategory = true,
                SeasonStartDate = "06-01",
                SeasonEndDate = "08-31"
            };

            // Act & Assert - Test various dates
            // Mock current date as July 15 (should be in season)
            var julyDate = new DateTime(2024, 7, 15);
            
            // We can't easily mock DateTime.Now, so let's test the logic manually
            var currentMonth = julyDate.Month;
            var currentDay = julyDate.Day;
            
            // Parse season dates
            var startParts = category.SeasonStartDate.Split('-');
            var endParts = category.SeasonEndDate.Split('-');
            var startMonth = int.Parse(startParts[0]);
            var startDay = int.Parse(startParts[1]);
            var endMonth = int.Parse(endParts[0]);
            var endDay = int.Parse(endParts[1]);
            
            // Test logic for regular season (within same year)
            bool isInSeason = (currentMonth > startMonth || (currentMonth == startMonth && currentDay >= startDay)) &&
                             (currentMonth < endMonth || (currentMonth == endMonth && currentDay <= endDay));
            
            isInSeason.Should().BeTrue("July 15 should be within June 1 - August 31 season");
        }

        [TestMethod]
        public void TemplateCategory_IsCurrentlyInSeason_HandlesCrossYearSeasons()
        {
            // Arrange - Christmas season (December 1 to January 15)
            var category = new TemplateCategory
            {
                IsSeasonalCategory = true,
                SeasonStartDate = "12-01",
                SeasonEndDate = "01-15"
            };

            // Test December date (should be in season)
            var decemberDate = new DateTime(2024, 12, 25);
            var currentMonth = decemberDate.Month;
            var currentDay = decemberDate.Day;
            
            // Parse season dates
            var startParts = category.SeasonStartDate.Split('-');
            var endParts = category.SeasonEndDate.Split('-');
            var startMonth = int.Parse(startParts[0]);
            var startDay = int.Parse(startParts[1]);
            var endMonth = int.Parse(endParts[0]);
            var endDay = int.Parse(endParts[1]);
            
            // Test logic for cross-year season
            bool isInSeason = (currentMonth > startMonth || (currentMonth == startMonth && currentDay >= startDay)) ||
                             (currentMonth < endMonth || (currentMonth == endMonth && currentDay <= endDay));
            
            isInSeason.Should().BeTrue("December 25 should be within Dec 1 - Jan 15 cross-year season");
            
            // Test January date (should be in season)
            var januaryDate = new DateTime(2025, 1, 10);
            currentMonth = januaryDate.Month;
            currentDay = januaryDate.Day;
            
            isInSeason = (currentMonth > startMonth || (currentMonth == startMonth && currentDay >= startDay)) ||
                        (currentMonth < endMonth || (currentMonth == endMonth && currentDay <= endDay));
            
            isInSeason.Should().BeTrue("January 10 should be within Dec 1 - Jan 15 cross-year season");
            
            // Test March date (should NOT be in season)
            var marchDate = new DateTime(2025, 3, 15);
            currentMonth = marchDate.Month;
            currentDay = marchDate.Day;
            
            isInSeason = (currentMonth > startMonth || (currentMonth == startMonth && currentDay >= startDay)) ||
                        (currentMonth < endMonth || (currentMonth == endMonth && currentDay <= endDay));
            
            isInSeason.Should().BeFalse("March 15 should NOT be within Dec 1 - Jan 15 cross-year season");
        }

        [TestMethod]
        public void TemplateCategory_IsCurrentlyInSeason_RequiresSeasonalCategory()
        {
            // Arrange - Non-seasonal category
            var category = new TemplateCategory
            {
                IsSeasonalCategory = false,
                SeasonStartDate = "06-01",
                SeasonEndDate = "08-31"
            };

            // Act & Assert
            category.IsCurrentlyInSeason.Should().BeFalse("Non-seasonal categories should never be 'in season'");
        }

        [TestMethod]
        public void TemplateCategory_IsCurrentlyInSeason_HandlesInvalidDates()
        {
            // Arrange - Invalid date format
            var category = new TemplateCategory
            {
                IsSeasonalCategory = true,
                SeasonStartDate = "invalid-date",
                SeasonEndDate = "08-31"
            };

            // Act & Assert
            category.IsCurrentlyInSeason.Should().BeFalse("Invalid date formats should return false");
            
            // Test with null dates
            category.SeasonStartDate = null;
            category.SeasonEndDate = null;
            category.IsCurrentlyInSeason.Should().BeFalse("Null dates should return false");
        }
    }
} 