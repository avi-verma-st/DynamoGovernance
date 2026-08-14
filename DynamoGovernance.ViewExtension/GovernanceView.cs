using System.Diagnostics;
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
        var panel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Dynamo Governance",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Access trusted Design Automation Hub resources directly from Dynamo.",
            Margin = new Thickness(0, 6, 0, 18),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(CreateResourceButton(
            GovernanceResources.HubHome,
            isPrimary: true));

        panel.Children.Add(new Separator
        {
            Margin = new Thickness(0, 18, 0, 12)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Resources",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (GovernanceResource resource in GovernanceResources.Resources)
        {
            panel.Children.Add(CreateResourceButton(resource, isPrimary: false));
        }

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static Button CreateResourceButton(
        GovernanceResource resource,
        bool isPrimary)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = resource.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize = isPrimary ? 15 : 13
        });
        content.Children.Add(new TextBlock
        {
            Text = resource.Description,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.Normal
        });

        var button = new Button
        {
            Content = content,
            Tag = resource.Url,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = $"Open {resource.Title} in your default browser"
        };

        if (isPrimary)
        {
            button.FontWeight = FontWeights.SemiBold;
        }

        button.Click += OnResourceClicked;

        return button;
    }

    private static void OnResourceClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Uri resourceUrl })
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resourceUrl.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"The resource could not be opened.\n\n{exception.Message}",
                "Dynamo Governance",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
