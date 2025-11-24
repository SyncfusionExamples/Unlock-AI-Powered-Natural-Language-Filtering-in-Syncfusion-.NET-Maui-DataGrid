# **AI-Driven Natural Language Filtering in .NET MAUI DataGrid**

**Meta Title:** AI-Driven Natural Language Filtering in .NET MAUI DataGrid | Smart Data Interaction  
**Meta Description:** Learn how to implement AI-powered natural language filtering in .NET MAUI DataGrid using Azure OpenAI and Syncfusion controls. Enable intuitive, conversational filtering across Android, iOS, Windows, and macOS.  
**Keywords:** .NET MAUI DataGrid, AI Filtering, Natural Language Processing, Syncfusion MAUI DataGrid, Azure OpenAI, Semantic Kernel, Cross-Platform DataGrid, Smart Filtering  
**Author:** Shalini Suresh

***

## **Introduction**

Modern applications demand intuitive ways for users to interact with data. Instead of complex filter menus, imagine typing:

    Show orders above $500 from Alice

…and instantly seeing the filtered results. This is possible by combining **Syncfusion’s .NET MAUI DataGrid** with **AI-powered natural language processing using OpenAI**.

In this guide, we’ll cover how to implement **AI-driven natural language filtering** in a .NET MAUI DataGrid.

***

## **Why Natural Language Filtering?**

Traditional filtering requires users to know column names and conditions. Natural language filtering:

*   ✅ Improves user experience
*   ✅ Handles complex queries easily
*   ✅ Leverages AI to interpret intent

***

## **Architecture Overview**

The solution consists of:

*   **UI Layer:** `SfDataGrid` bound to an `ObservableCollection<Employee>`.
*   **ViewModel:** Handles prompt execution and builds filter predicates.
*   **AI Service:** Converts natural language queries into structured filter plans using OpenAI.

### **Workflow**

1.  User enters query or selects a suggestion.
2.  AI service generates a `FilterPlan` (JSON schema).
3.  ViewModel converts `FilterPlan` into a `Predicate<object>`.
4.  DataGrid applies the filter dynamically.

***

## **Step 1: Configure AI Settings in `MauiProgram.cs`**

Register AI settings and services in the MAUI app builder:

```csharp
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"); // or secure storage

builder.Services.AddSingleton(new AiSettings
{
    Provider = AiProvider.OpenAI, // or AiProvider.AzureOpenAI
    OpenAiApiKey = openAiKey,
    OpenAiModel = "gpt-4o-mini", // choose any chat model with JSON output
    AzureEndpoint = "", // For Azure OpenAI
    AzureApiKey = "",
    AzureDeployment = ""
});

builder.Services.AddSingleton<IAiFilterService, AiFilterService>();
builder.Services.AddSingleton<EmployeesViewModel>();
builder.Services.AddTransient<MainPage>();
```

***

## **Step 2: Bind DataGrid and ViewModel in `MainPage.xaml.cs`**

```csharp
public partial class MainPage : ContentPage
{
    private readonly EmployeesViewModel _vm;

    public MainPage()
    {
        InitializeComponent();

        var aiSettings = new AiSettings { Provider = AiProvider.Local }; // or OpenAI/Azure
        var aiService = new AiFilterService(aiSettings);
        _vm = new EmployeesViewModel(aiService);
        BindingContext = _vm;

        _vm.FilterChanged += (_, __) =>
        {
            DataGrid.View.Filter = _vm.BuildPredicate();
            DataGrid.View.RefreshFilter();
        };
    }
}
```

***

## **Step 3: ViewModel Logic (`EmployeesViewModel.cs`)**

*   **Prompt Suggestions:**
    *   “Employees with rating ≥ 8 and salary > 5000”
    *   “Show only Female employees born before 1990”

*   **Commands:**
    *   `ExecutePromptCommand` → Calls AI service
    *   `ResetCommand` → Clears filters

```csharp
private async Task ExecuteAsync()
{
    var toRun = !string.IsNullOrWhiteSpace(SelectedSuggestion) ? SelectedSuggestion! : Prompt;
    if (string.IsNullOrWhiteSpace(toRun))
    {
        CurrentPlan = null;
        FilterChanged?.Invoke(this, EventArgs.Empty);
        return;
    }

    CurrentPlan = await _ai.CreateFilterPlanAsync(toRun);
    FilterChanged?.Invoke(this, EventArgs.Empty);
}
```

***

## **Step 4: AI Service (`AiFilterService.cs`)**

*   Sends prompt to OpenAI/Azure OpenAI with schema instructions.
*   Receives JSON filter plan like:

```json
{
  "logic": "and",
  "conditions": [
    { "condition": { "field": "Rating", "op": "gte", "value": "8" } },
    { "condition": { "field": "Salary", "op": "gt", "value": "5000" } }
  ]
}
```

*   Converts JSON into `FilterPlan` for evaluation.

**Local mode** uses regex to parse common phrases like:

*   “salary between 3000 and 4000”
*   “gender in \[Male, Female]”
*   “birthdate before 1990”

***

## **Step 5: Apply Filter**

`BuildPredicate()` converts `FilterPlan` into a predicate:

```csharp
public Predicate<object>? BuildPredicate()
{
    if (CurrentPlan is null) return null;
    return rowObj => EvalPlan(CurrentPlan, (Employee)rowObj);
}
```

`EvalCondition()` supports operators:

*   **Numeric:** gt, gte, lt, lte, between
*   **String:** contains, startswith, endswith, in
*   **Date:** before, after

***

## **Conclusion**

Thanks for reading! In this blog, we’ve seen how to implement **AI-driven Natural Language Filtering** in .NET MAUI DataGrid. Check out our Release Notes and What’s New pages to see the other updates in this release and leave your feedback in the comments section below. 
For current Syncfusion customers, the newest version of Essential Studio is available from the license and downloads page. If you are not yet a customer, you can try our 30-day free trial to check out these new features. 
For questions, you can contact us through our support forums, feedback portal, or support portal. We are always happy to assist you!
