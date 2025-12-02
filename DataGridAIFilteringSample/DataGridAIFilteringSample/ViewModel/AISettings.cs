using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DataGridAIFilteringSample;


/// <summary>
/// Specifies the AI provider options available for integration.
/// </summary>
public enum AiProvider
{
    /// <summary>
    /// Use OpenAI as the AI provider.
    /// </summary>
    OpenAI,

    /// <summary>
    /// Use Azure OpenAI as the AI provider.
    /// </summary>
    AzureOpenAI,

    /// <summary>
    /// Use a local AI implementation.
    /// </summary>
    Local
}

/// <summary>
/// Represents configuration settings for AI integration in the application.
/// </summary>
public class AiSettings
{
    /// <summary>
    /// Gets or sets the AI provider to use (OpenAI, AzureOpenAI, or Local).
    /// </summary>
    public AiProvider Provider { get; set; } = AiProvider.Local;

    /// <summary>
    /// Gets or sets the API key for OpenAI services.
    /// </summary>
    public string? OpenAiApiKey { get; set; }

    /// <summary>
    /// Gets or sets the OpenAI model name (e.g., gpt-4o-mini).
    /// </summary>
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Gets or sets the Azure OpenAI endpoint URL.
    /// </summary>
    public string? AzureEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the API key for Azure OpenAI services.
    /// </summary>
    public string? AzureApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Azure OpenAI deployment name.
    /// </summary>
    public string? AzureDeployment { get; set; }
}

/// <summary>
/// Defines the contract for an AI-based filter service that converts natural language prompts into structured filter plans.
/// </summary>
public interface IAiFilterService
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
/// Converts user prompts into structured <see cref="FilterPlan"/> objects using OpenAI, Azure OpenAI, or local parsing.
/// </summary>
public class AiFilterService : IAiFilterService
{
    private readonly AiSettings _settings;

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
    /// Initializes a new instance of the <see cref="AiFilterService"/> class with the specified AI settings.
    /// </summary>
    /// <param name="settings">The AI configuration settings.</param>
    public AiFilterService(AiSettings settings) => _settings = settings;

    /// <summary>
    /// Creates a filter plan from a natural language prompt using the configured AI provider.
    /// </summary>
    /// <param name="naturalLanguagePrompt">
    /// The user-provided prompt in plain English (e.g., "Show female employees with rating ≥ 8 and salary > 5000").
    /// </param>
    public async Task<FilterPlan?> CreateFilterPlanAsync(string naturalLanguagePrompt)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguagePrompt)) return null;

        // Choose best available route
        var useLocal =
            _settings.Provider == AiProvider.Local ||
            (_settings.Provider == AiProvider.OpenAI && string.IsNullOrWhiteSpace(_settings.OpenAiApiKey)) ||
            (_settings.Provider == AiProvider.AzureOpenAI &&
                (string.IsNullOrWhiteSpace(_settings.AzureEndpoint) ||
                 string.IsNullOrWhiteSpace(_settings.AzureApiKey) ||
                 string.IsNullOrWhiteSpace(_settings.AzureDeployment)));

        if (useLocal)
            return CreateLocalPlan(naturalLanguagePrompt);

        var system = "You convert plain English filters into strictly valid JSON filter plans for a data grid.";
        var user = $"Grid schema:\n{SchemaText}\n\nUser query:\n{naturalLanguagePrompt}\n\nReturn JSON only.";

        try
        {
            var content = _settings.Provider == AiProvider.OpenAI
                ? await CallOpenAiAsync(system, user)
                : await CallAzureOpenAiAsync(system, user);

            if (string.IsNullOrWhiteSpace(content)) return null;
            return JsonSerializer.Deserialize<FilterPlan>(content, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Provides a mapping of common field aliases to their canonical names for filter parsing.
    /// </summary>
    private static readonly Dictionary<string, string> FieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "employeeid", "EmployeeId" }, { "id", "EmployeeId" },
        { "name", "Name" }, { "title", "Title" },
        { "rating", "Rating" },
        { "birthdate", "BirthDate" }, { "dob", "BirthDate" },
        { "gender", "Gender" },
        { "salary", "Salary" },
    };

    /// <summary>
    /// Creates a <see cref="FilterPlan"/> from a natural language prompt using local regex-based parsing.
    /// </summary>
    /// <param name="prompt">
    /// A natural language query describing filter conditions (e.g., "show female employees with rating ≥ 8 and salary > 5000").
    /// </param>
    private static FilterPlan? CreateLocalPlan(string prompt)
    {
        var p = prompt.Trim();
        if (string.IsNullOrEmpty(p)) return null;

        // logic: prefer "or" only if OR appears and AND does not; else AND
        var hasOr = Regex.IsMatch(p, @"(?i)\bor\b");
        var hasAnd = Regex.IsMatch(p, @"(?i)\band\b");
        var logic = hasOr && !hasAnd ? "or" : "and";

        var plan = new FilterPlan { logic = logic, conditions = new List<FilterNode>() };

        // Helper to add a condition
        void Add(string field, string op, string? value = null, IEnumerable<string>? values = null)
        {
            plan.conditions.Add(new FilterNode
            {
                condition = new Condition
                {
                    field = field,
                    op = op,
                    value = value,
                    values = values?.ToList()
                }
            });
        }

        static bool TryField(string raw, out string field)
        {
            if (FieldAliases.TryGetValue(raw.Trim(), out var mapped))
            {
                field = mapped; return true;
            }
            var s = raw.Trim();
            if (FieldAliases.Values.Contains(s, StringComparer.OrdinalIgnoreCase))
            {
                field = s; return true;
            }
            field = s; return false;
        }

        // 1) between: "<field> between a and b"
        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)\s+between\s+([^\s,]+)\s+and\s+([^\s,]+)"))
        {
            if (TryField(m.Groups[1].Value, out var f))
                Add(f, "between", values: new[] { m.Groups[2].Value, m.Groups[3].Value });
        }

        // 2) in: "<field> in [a, b]" or "(a, b)"
        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)\s+in\s*[\(\[\{]\s*([^\]\)\}]+)\s*[\]\)\}]"))
        {
            if (TryField(m.Groups[1].Value, out var f))
            {
                var items = m.Groups[2].Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().Trim('\'', '"'));
                Add(f, "in", values: items);
            }
        }

        // 3) contains/starts/ends
        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)\s+contains\s+['""]?(.+?)['""]?(?=\s*(?:and|or|$))"))
            if (TryField(m.Groups[1].Value, out var f)) Add(f, "contains", m.Groups[2].Value.Trim());

        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)\s+starts\s*with\s+['""]?(.+?)['""]?(?=\s*(?:and|or|$))"))
            if (TryField(m.Groups[1].Value, out var f)) Add(f, "startsWith", m.Groups[2].Value.Trim());

        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)\s+ends\s*with\s+['""]?(.+?)['""]?(?=\s*(?:and|or|$))"))
            if (TryField(m.Groups[1].Value, out var f)) Add(f, "endsWith", m.Groups[2].Value.Trim());

        // 4) before/after (with optional "born")
        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)?\s*(?:born\s+)?before\s+(\d{1,2}/\d{1,2}/\d{2,4}|\d{4})"))
        {
            var field = m.Groups[1].Success && TryField(m.Groups[1].Value, out var f1) ? f1 : "BirthDate";
            var raw = m.Groups[2].Value;
            var value = Regex.IsMatch(raw, @"^\d{4}$") ? $"01/01/{raw}" : raw;
            Add(field, "before", value);
        }
        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)?\s*(?:born\s+)?after\s+(\d{1,2}/\d{1,2}/\d{2,4}|\d{4})"))
        {
            var field = m.Groups[1].Success && TryField(m.Groups[1].Value, out var f1) ? f1 : "BirthDate";
            var raw = m.Groups[2].Value;
            var value = Regex.IsMatch(raw, @"^\d{4}$") ? $"01/01/{raw}" : raw;
            Add(field, "after", value);
        }

        // 5) comparisons: "<field> >= 5", etc.
        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)\s*(>=|<=|>|<|=|==|!=)\s*([^\s,]+)"))
        {
            if (!TryField(m.Groups[1].Value, out var f)) continue;
            var op = m.Groups[2].Value switch
            {
                ">=" => "gte",
                "<=" => "lte",
                ">" => "gt",
                "<" => "lt",
                "=" or "==" => "eq",
                "!=" => "ne",
                _ => "eq"
            };
            Add(f, op, m.Groups[3].Value);
        }

        // 6) word comparisons: "rating gte 8"
        foreach (Match m in Regex.Matches(p, @"(?i)\b(\w+)\s*(gte|lte|gt|lt|eq|ne)\s*([^\s,]+)"))
            if (TryField(m.Groups[1].Value, out var f))
                Add(f, m.Groups[2].Value.ToLowerInvariant(), m.Groups[3].Value);

        // 7) gender keywords without explicit "gender": "show only female employees"
        if (Regex.IsMatch(p, @"(?i)\bfemale\b")) Add("Gender", "eq", "Female");
        if (Regex.IsMatch(p, @"(?i)\bmale\b")) Add("Gender", "eq", "Male");

        // 8) fallback for simple "Name contains Tom" without quotes
        foreach (Match m in Regex.Matches(p, @"(?i)\b(name|title)\s+contains?\s+([A-Za-z0-9]+)"))
            if (TryField(m.Groups[1].Value, out var f)) Add(f, "contains", m.Groups[2].Value);

        // If still nothing matched, try splitting by and/or and parse each chunk with a minimal rule
        if (plan.conditions.Count == 0)
        {
            var parts = Regex.Split(p, @"\s+(?:and|or)\s+", RegexOptions.IgnoreCase)
                             .Where(s => !string.IsNullOrWhiteSpace(s));
            foreach (var part in parts)
            {
                var s = part.Trim();

                var m1 = Regex.Match(s, @"(?i)\b(name|title)\b\s+(.+)");
                if (m1.Success && TryField(m1.Groups[1].Value, out var f1))
                {
                    Add(f1, "contains", m1.Groups[2].Value.Trim('\'', '"', ' '));
                }
            }
        }

        return plan.conditions.Count > 0 ? plan : null;
    }

    /// <summary>
    /// Calls the OpenAI Chat Completions API to generate a JSON-based filter plan from natural language input.
    /// </summary>
    /// <param name="system">
    /// The system instruction that defines schema, constraints, and expected JSON output format.
    /// </param>
    /// <param name="user">
    /// The user's natural language prompt describing filter conditions.
    /// </param>
    private async Task<string?> CallOpenAiAsync(string system, string user)
    {
        if (string.IsNullOrWhiteSpace(_settings.OpenAiApiKey))
            throw new InvalidOperationException("OPENAI_API_KEY is not configured.");

        using var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.OpenAiApiKey);

        var payload = new
        {
            model = _settings.OpenAiModel,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
            new { role = "system", content = system },
            new { role = "user", content = user }
            }
        };

        var resp = await http.PostAsync(
            "v1/chat/completions",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }

    /// <summary>
    /// Calls the Azure OpenAI Chat Completions API to generate a JSON-based filter plan from natural language input.
    /// </summary>
    /// <param name="system">
    /// The system instruction that defines schema, constraints, and expected JSON output format.
    /// </param>
    /// <param name="user">
    /// The user's natural language prompt describing filter conditions.
    /// </param>
    private async Task<string?> CallAzureOpenAiAsync(string system, string user)
    {
        if (string.IsNullOrWhiteSpace(_settings.AzureEndpoint) ||
            string.IsNullOrWhiteSpace(_settings.AzureApiKey) ||
            string.IsNullOrWhiteSpace(_settings.AzureDeployment))
            throw new InvalidOperationException("Azure OpenAI settings are not configured.");

        using var http = new HttpClient { BaseAddress = new Uri(_settings.AzureEndpoint!.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Add("api-key", _settings.AzureApiKey);

        var apiVersion = "2024-06-01";
        var url = $"openai/deployments/{_settings.AzureDeployment}/chat/completions?api-version={apiVersion}";

        var payload = new
        {
            response_format = new { type = "json_object" },
            messages = new object[]
            {
            new { role = "system", content = system },
            new { role = "user", content = user }
            }
        };

        var resp = await http.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }
}
