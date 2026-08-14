namespace DynamoGovernance.ViewExtension;

internal sealed record GovernanceResource(string Title, string Description, Uri Url);

internal static class GovernanceResources
{
    private static readonly Uri SiteRootUrl = new(
        "https://stantec.sharepoint.com/teams/DesignAutomation/");
    private static readonly Uri HubHomeUrl = new(SiteRootUrl, "SitePages/Home.aspx");

    public static GovernanceResource HubHome { get; } = new(
        "Open Design Automation Hub",
        "Visit the central source for approved design automation guidance, tools, learning, and standards.",
        HubHomeUrl);

    public static IReadOnlyList<GovernanceResource> Resources { get; } =
    [
        new GovernanceResource(
            "Dynamo Training",
            "Access Dynamo learning material, training sessions, and enablement resources.",
            new Uri("https://stantec.sharepoint.com/teams/DesignAutomation/Lists/Design_Automation_Learning_Resources_Cleaned%201/AllItems.aspx?viewid=eabd0110-0ea7-4192-b91b-62fbb6c751f3&env=WebViewList")),
        new GovernanceResource(
            "Dynamo Development Resources",
            "Access shared files and resources for developing supported Dynamo solutions.",
            new Uri("https://stantec.sharepoint.com/teams/DesignAutomation/DA%20Resources%20Files/Forms/AllItems.aspx?id=%2Fteams%2FDesignAutomation%2FDA%20Resources%20Files%2FDynamo%20Develpoment%20Resources&viewid=2862bc99%2D7a98%2D4120%2D8ff0%2D088eebfa6dc4")),
        
    ];
}
