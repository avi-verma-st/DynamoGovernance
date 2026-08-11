namespace DynamoGovernance.Core.Services;

public static class IdentityService
{
    public static string GetUserId()
    {
        return $"{Environment.UserDomainName}\\{Environment.UserName}";
    }

    public static string GetMachineId()
    {
        return Environment.MachineName;
    }
}
