using System.Windows;
using System.Windows.Media;

namespace Paper.ScreenWizzard.UiTests.Support;

/// <summary>Finds a control in WPF's own visual tree by the AutomationId the XAML gave it.</summary>
public static class VisualTreeFinder
{
    public static FrameworkElement? FindByAutomationId(DependencyObject root, string automationId)
    {
        if (root is FrameworkElement element
            && System.Windows.Automation.AutomationProperties.GetAutomationId(element) == automationId)
        {
            return element;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindByAutomationId(VisualTreeHelper.GetChild(root, i), automationId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
