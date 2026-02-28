using Microsoft.SemanticKernel;

namespace AiAgileTeam.Extensions;

public static class SemanticKernelExtensions
{
    public static IKernelBuilder AddVisionSupport(
        this IKernelBuilder kernelBuilder,
        string model,
        string endpoint,
        string apiKey,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(kernelBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        kernelBuilder.AddAzureOpenAIChatCompletion(model, endpoint, apiKey, httpClient: httpClient);
        return kernelBuilder;
    }
}
