namespace DataGridAIFilteringSample.ViewModel;

/// <summary>
/// Represents the base class for Azure service configurations.
/// </summary>
public abstract class AzureBaseService
{
    internal const string Endpoint = "https://testingopen.openai.azure.com/";
    internal const string DeploymentName = "gpt-4.1";
    internal const string Key = "hLGD4wgBAVsB4sWMom6ijN7j6JpEcV4Gw1nFWfxLpOlx5NpfpX7JJQQJ99BKACmepeSXJ3w3AAABACOGLIKd";

    protected AzureBaseService() { }
}
