using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonElement = System.Text.Json.JsonElement;

namespace DataGridAIFilterSample;

/// <summary>
/// Defines the contract for an AI-based filter service that converts natural language prompts into structured filter plans.
/// </summary>
public interface IAIFilterService
{
    /// <summary>
    /// Creates a filter plan based on a natural language prompt.
    /// </summary>
    /// <param name="naturalLanguagePrompt">
    /// The user-provided prompt in plain English describing the filter criteria (e.g., "Show employees with rating ≥ 8 and salary > 5000").
    /// </param>
    Task<FilterPlan?> CreateFilterPlanAsync(string naturalLanguagePrompt);
}

/// <summary>
/// Provides AI-powered natural language filtering capabilities for a .NET MAUI DataGrid.
/// Converts user prompts into structured <see cref="FilterPlan"/> objects using Azure OpenAI only (local parsing removed).
/// </summary>
public class AiFilterService : IAIFilterService
{
    /// <summary>
    /// JSON serializer options for deserializing AI responses into <see cref="FilterPlan"/>.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Schema definition and instructions provided to AI models for generating valid filter plans.
    /// </summary>
    private const string SchemaText = """
        Fields and types:
        - EmployeeId: integer
        - Name: string
        - Title: string
        - Rating: integer
        - BirthDate: date (MM/dd/yyyy)
        - Gender: string (Male|Female)
        - Salary: decimal (USD)

        Allowed operators per field:
        - integers/decimal/date: eq, ne, gt, gte, lt, lte, between, before, after
        - string: eq, ne, contains, startsWith, endsWith, in

        Combine conditions with "and" or "or".

        Return ONLY a compact JSON object that matches this C# schema:
        {
          "logic":"and|or",
          "conditions":[
            { "condition":{ "field":"", "op":"", "value":"", "values":[] } }
            or
            { "group": { ... } }
          ]
        }
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiFilterService"/> class.
    /// </summary>
    public AiFilterService() { }

    /// <summary>
    /// Creates a filter plan from a natural language prompt using the configured AI provider.
    /// </summary>
    /// <param name="naturalLanguagePrompt">
    /// The user-provided prompt in plain English (e.g., "Show female employees with rating ≥ 8 and salary > 5000").
    /// </param>
    public async Task<FilterPlan?> CreateFilterPlanAsync(string naturalLanguagePrompt)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguagePrompt)) return null;

        if (string.IsNullOrWhiteSpace(AzureBaseService.Endpoint) ||
            string.IsNullOrWhiteSpace(AzureBaseService.DeploymentName) ||
            string.IsNullOrWhiteSpace(AzureBaseService.Key))
        {
            return null;
        }

        var system = "You convert plain English filters into strictly valid JSON filter plans for a data grid.";
        var user = $"Grid schema:\n{SchemaText}\n\nUser query:\n{naturalLanguagePrompt}\n\nReturn JSON only.";

        try
        {
            var content = await CallAzureAsync(system, user);
            var json = ExtractJsonObject(content);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return ParseFilterPlanFromJson(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a JSON object string from a text response.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>

    private static string? ExtractJsonObject(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var value = content.Trim();
        if (value.StartsWith("```"))
        {
            value = value.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                 .Replace("```", string.Empty)
                 .Trim();
        }

        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');
        if (start >= 0 && end >= start)
        {
            return value.Substring(start, end - start + 1).Trim();
        }

        return value;
    }

    /// <summary>
    /// This method validates the JSON structure and delegates detailed parsing to <see cref="ParsePlanElement"/>.
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    private static FilterPlan? ParseFilterPlanFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParsePlanElement(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a JSON element that represents a filter plan and converts it into a <see cref="FilterPlan"/>.
    /// </summary>
    /// <param name="el"></param>
    /// <returns></returns>
    private static FilterPlan? ParsePlanElement(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var logic = el.TryGetProperty("logic", out var lg) && lg.ValueKind == JsonValueKind.String ? lg.GetString() ?? "and" : "and";
        var plan = new FilterPlan { logic = logic, conditions = new List<FilterNode>() };

        if (!el.TryGetProperty("conditions", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var node in arr.EnumerateArray())
        {
            // Case 1: wrapper object with "condition"
            if (node.TryGetProperty("condition", out var cobj))
            {
                var cond = ParseCondition(cobj);
                if (cond != null) plan.conditions.Add(new FilterNode { condition = cond });
                continue;
            }
            // Case 2: wrapper object with "group"
            if (node.TryGetProperty("group", out var gobj))
            {
                var sub = ParsePlanElement(gobj);
                if (sub != null) plan.conditions.Add(new FilterNode { group = sub });
                continue;
            }
            // Case 3: direct condition fields at top-level
            var direct = ParseCondition(node);
            if (direct != null) plan.conditions.Add(new FilterNode { condition = direct });
        }

        return plan.conditions.Count > 0 ? plan : null;
    }

    /// <summary>
    /// Parses a JSON element representing a single filter condition and converts it into a<see cref="Condition"/> object.
    /// </summary>
    /// <param name="el"></param>
    /// <returns></returns>
    private static Condition? ParseCondition(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string field = el.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() ?? string.Empty : string.Empty;
        string op = el.TryGetProperty("op", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() ?? string.Empty : string.Empty;
        string? value = el.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        List<string>? values = null;
        if (el.TryGetProperty("values", out var vs) && vs.ValueKind == JsonValueKind.Array)
        {
            values = new List<string>();
            foreach (var item in vs.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) values.Add(item.GetString() ?? string.Empty);
                else values.Add(item.ToString());
            }
        }

        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op))
        {
            return null;
        }

        return new Condition { field = field, operate = op, value = value, values = values };
    }

    /// <summary>
    /// Calls Azure OpenAI Chat Completions to transform a system + user prompt into a response.
    /// Configured to return strictly JSON (via response_format) for deterministic parsing in AI filtering.
    /// </summary>
    /// <param name="system"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private async Task<string?> CallAzureAsync(string system, string user)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(AzureBaseService.Endpoint.TrimEnd('/') + "/") };
            http.DefaultRequestHeaders.Add("api-key", AzureBaseService.Key);

            var apiVersion = "2024-06-01";
            var url = $"openai/deployments/{AzureBaseService.DeploymentName}/chat/completions?api-version={apiVersion}";

            var payload = new
            {
                response_format = new { type = "json_object" },
                temperature = 0,
                top_p = 0,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };

            var resp = await http.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content;
        }
        catch
        {
            return null;
        }
    }
}
