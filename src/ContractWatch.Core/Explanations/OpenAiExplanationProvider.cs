using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ContractWatch.Core.Explanations;

public sealed class OpenAiExplanationProvider(HttpMessageHandler handler, string apiKey, string? model = null, string? baseUrl = null) : IExplanationProvider
{
    public const string DefaultModel = "gpt-4o-mini";
    public const string DefaultBaseUrl = "https://api.openai.com/v1";

    private static readonly JsonSerializerOptions ResponseOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonSerializerOptions RequestOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _client = new(handler);
    private readonly string _endpoint = $"{(baseUrl ?? DefaultBaseUrl).TrimEnd('/')}/chat/completions";
    private readonly string _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model!;

    public string Name => ExplanationProviders.OpenAi;

    public async Task<string?> ExplainAsync(ContractChange change, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(new ChatRequest(
                _model,
                [
                    new ChatMessage("system", SystemPrompt),
                    new ChatMessage("user", BuildPrompt(change)),
                ],
                0), RequestOptions), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new ExplanationProviderException($"el endpoint de chat-completions respondió {(int)response.StatusCode}");

        var payload = JsonSerializer.Deserialize<ChatResponse>(await response.Content.ReadAsStringAsync(cancellationToken), ResponseOptions)
                      ?? throw new ExplanationProviderException("la respuesta de chat-completions está vacía");

        return payload.Choices is { Length: > 0 } choices
            ? choices[0].Message.Content
            : throw new ExplanationProviderException("la respuesta de chat-completions no tiene choices");
    }

    private const string SystemPrompt =
        "You explain breaking changes in OpenAPI contracts to API consumers. " +
        "Answer in at most three concise sentences: why the change breaks consumers and the minimal migration step.";

    private static string BuildPrompt(ContractChange change)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Rule: {change.RuleId} ({change.RuleName})");
        builder.AppendLine($"Severity: {change.Severity}");
        builder.AppendLine($"Operation: {Describe(change.Location)}");
        builder.AppendLine($"Change: {change.Message}");

        if (change.OldValue is { } oldValue)
            builder.AppendLine($"Old value: {oldValue}");

        if (change.NewValue is { } newValue)
            builder.AppendLine($"New value: {newValue}");

        if (change.Suggestion is { } suggestion)
            builder.AppendLine($"Deterministic remediation to expand on: {suggestion}");

        return builder.ToString().TrimEnd();
    }

    private static string Describe(ChangeLocation location) =>
        location.Method is null ? location.Path : $"{location.Method} {location.Path}";

    private sealed record ChatRequest(string Model, ChatMessage[] Messages, double Temperature);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatResponse(ChatChoice[]? Choices);

    private sealed record ChatChoice(ChatMessageDto Message);

    private sealed record ChatMessageDto(string? Content);
}
