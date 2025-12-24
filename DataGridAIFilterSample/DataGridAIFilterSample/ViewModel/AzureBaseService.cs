namespace DataGridAIFilterSample;

/// <summary>
/// Represents the base class for Azure service configurations.
/// </summary>
public abstract class AzureBaseService
{
    internal const string Endpoint = "Your_Endpoint";
    internal const string DeploymentName = "Deployment_Name";
    internal const string Key = "API_Key";

    protected AzureBaseService() { }
}
