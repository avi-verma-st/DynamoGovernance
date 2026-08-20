using Dynamo.Wpf.Extensions;
using System.Windows;
using System.Windows.Controls;

namespace DynamoGovernance.ViewExtension;

public sealed class GovernanceViewExtension : IViewExtension
{
    private ViewLoadedParams? _viewLoadedParams;
    private MenuItem? _launchMenuItem;

    public string UniqueId => "83105D82-E9EF-48B6-9C51-F4027939C59A";

    public string Name => "Dynamo Governance";

    public void Startup(ViewStartupParams viewStartupParams)
    {
    }

    public void Loaded(ViewLoadedParams viewLoadedParams)
    {
        _viewLoadedParams = viewLoadedParams;

        var governanceMenuItem = new MenuItem
        {
            Header = Name
        };

        _launchMenuItem = new MenuItem
        {
            Header = "Launch"
        };
        _launchMenuItem.Click += OnLaunchClicked;
        governanceMenuItem.Items.Add(_launchMenuItem);
        viewLoadedParams.AddExtensionMenuItem(governanceMenuItem);

        OpenView();
    }

    public void Shutdown()
    {
        ReleaseMenuItem();
        _viewLoadedParams = null;
    }

    public void Dispose()
    {
        ReleaseMenuItem();
        _viewLoadedParams = null;
    }

    private void OnLaunchClicked(object sender, RoutedEventArgs e)
    {
        OpenView();
    }

    private void OpenView()
    {
        _viewLoadedParams?.AddToExtensionsSideBar(this, new GovernanceView());
    }

    private void ReleaseMenuItem()
    {
        if (_launchMenuItem is null)
        {
            return;
        }

        _launchMenuItem.Click -= OnLaunchClicked;
        _launchMenuItem = null;
    }
}
