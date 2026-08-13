using Dynamo.Wpf.Extensions;

namespace DynamoGovernance.ViewExtension;

public sealed class GovernanceViewExtension : IViewExtension
{
    public string UniqueId => "83105D82-E9EF-48B6-9C51-F4027939C59A";

    public string Name => "Dynamo Governance";

    public void Startup(ViewStartupParams viewStartupParams)
    {
    }

    public void Loaded(ViewLoadedParams viewLoadedParams)
    {
        var view = new GovernanceView();
        viewLoadedParams.AddToExtensionsSideBar(this, view);
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
    }
}
