using System.Windows;
using System.Windows.Controls;

namespace DynamoGovernance.ViewExtension;

public sealed class GovernanceView : UserControl
{
    public GovernanceView()
    {
        Content = CreateContent();
    }

    private static UIElement CreateContent()
    {
        var title = new TextBlock
        {
            Text = "Dynamo Governance",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var button = new Button
        {
            Content = "Test View Extension",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 8, 16, 8)
        };

        button.Click += OnButtonClicked;

        var panel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        panel.Children.Add(title);
        panel.Children.Add(button);

        return panel;
    }

    private static void OnButtonClicked(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "The Dynamo Governance view extension is working.\nNext step is to link other resources directly within the extension!",
            "Dynamo Governance",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
