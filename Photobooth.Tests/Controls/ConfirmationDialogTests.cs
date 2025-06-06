using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Controls;
using System.Windows;

namespace Photobooth.Tests.Controls
{
    [TestClass]
    public class ConfirmationDialogTests
    {
        [TestMethod]
        public void ConfirmationDialog_StaticMethods_DoNotThrow()
        {
            // Test that the static method signatures exist and can be called
            // without actually creating UI components
            
            // Act & Assert - These should not throw when the methods exist
            var showConfirmationMethod = typeof(ConfirmationDialog).GetMethod("ShowConfirmation", 
                new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(Window) });
            var showDeleteConfirmationMethod = typeof(ConfirmationDialog).GetMethod("ShowDeleteConfirmation",
                new[] { typeof(string), typeof(string), typeof(Window) });

            // Assert methods exist
            showConfirmationMethod.Should().NotBeNull("ShowConfirmation method should exist");
            showDeleteConfirmationMethod.Should().NotBeNull("ShowDeleteConfirmation method should exist");
        }

        [TestMethod]
        public void ConfirmationDialog_TypeExists_ShouldBeTrue()
        {
            // Test that the ConfirmationDialog type exists and is properly defined
            // Act
            var type = typeof(ConfirmationDialog);

            // Assert
            type.Should().NotBeNull();
            type.IsClass.Should().BeTrue();
            type.IsPublic.Should().BeTrue();
        }

        [TestMethod]
        public void ConfirmationDialog_InheritsFromWindow_ShouldBeTrue()
        {
            // Test that ConfirmationDialog properly inherits from Window
            // Act
            var type = typeof(ConfirmationDialog);

            // Assert
            type.BaseType.Should().Be(typeof(Window));
        }

        [TestMethod]
        public void ConfirmationDialog_HasRequiredMethods_ShouldBeTrue()
        {
            // Test that required static methods exist with correct signatures
            // Act
            var type = typeof(ConfirmationDialog);
            var showConfirmationMethod = type.GetMethod("ShowConfirmation");
            var showDeleteConfirmationMethod = type.GetMethod("ShowDeleteConfirmation");

            // Assert
            showConfirmationMethod.Should().NotBeNull("ShowConfirmation method should exist");
            showDeleteConfirmationMethod.Should().NotBeNull("ShowDeleteConfirmation method should exist");
            
            showConfirmationMethod!.IsStatic.Should().BeTrue("ShowConfirmation should be static");
            showDeleteConfirmationMethod!.IsStatic.Should().BeTrue("ShowDeleteConfirmation should be static");
        }

        [TestMethod]
        public void ConfirmationDialog_ReturnTypes_ShouldBeBoolean()
        {
            // Test that the methods return the expected types
            // Act
            var type = typeof(ConfirmationDialog);
            var showConfirmationMethod = type.GetMethod("ShowConfirmation");
            var showDeleteConfirmationMethod = type.GetMethod("ShowDeleteConfirmation");

            // Assert
            showConfirmationMethod!.ReturnType.Should().Be(typeof(bool));
            showDeleteConfirmationMethod!.ReturnType.Should().Be(typeof(bool));
        }
    }
} 