using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace DataGridAIFilteringSample
{
    public class EmployeesViewModel : BindableObject
    {
        private readonly IAiFilterService _ai;

        public ObservableCollection<Employee> Employees { get; } =
            new(EmployeeRepository.GetEmployees());

        // Holds manual text (ComboBox.Text)
        private string _prompt = "";
        public string Prompt
        {
            get => _prompt;
            set { if (_prompt == value) return; _prompt = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> PromptSuggestions { get; } = new()
        {
            "Employees with rating >= 8 and salary > 5000",
            "Show only Female employees born before 1990",
            "Name contains 'Tom' or Title contains 'Supervisor'",
            "BirthDate between 01/01/1980 and 12/31/1989",
            "Salary between 3000 and 4000",
            "Gender in [Male, Female] and Rating > 5",
            "EmployeeId between 1005 and 1015"
        };

        private string? _selectedSuggestion;
        public string? SelectedSuggestion
        {
            get => _selectedSuggestion;
            set
            {
                if (_selectedSuggestion == value) return;
                _selectedSuggestion = value;
                OnPropertyChanged();
                // Keep Prompt in sync when a suggestion is chosen
                if (!string.IsNullOrWhiteSpace(value))
                    Prompt = value;
            }
        }

        public FilterPlan? CurrentPlan { get; private set; }

        public ICommand ExecutePromptCommand { get; }
        public ICommand ResetCommand { get; }

        public event EventHandler? FilterChanged;

        public EmployeesViewModel(IAiFilterService ai)
        {
            _ai = ai;

            ExecutePromptCommand = new Command(async () => await ExecuteAsync());

            ResetCommand = new Command(() =>
            {
                CurrentPlan = null;
                SelectedSuggestion = null;
                Prompt = string.Empty;
                FilterChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        private async Task ExecuteAsync()
        {
            // Prefer manual text (Prompt). If empty, fall back to selected suggestion.
            var toRun = !string.IsNullOrWhiteSpace(Prompt) ? Prompt : (SelectedSuggestion ?? string.Empty);
            toRun = toRun.Trim();

            if (string.IsNullOrWhiteSpace(toRun))
            {
                CurrentPlan = null;
                FilterChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            CurrentPlan = await _ai.CreateFilterPlanAsync(toRun);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        public Predicate<object>? BuildPredicate()
        {
            if (CurrentPlan is null) return null;
            return rowObj => EvalPlan(CurrentPlan, (Employee)rowObj);
        }

        private static bool EvalPlan(FilterPlan plan, Employee e)
        {
            bool Combine(bool a, bool b, string logic) =>
                logic.Equals("or", StringComparison.OrdinalIgnoreCase) ? a || b : a && b;

            bool result = false;
            bool first = true;

            foreach (var node in plan.conditions)
            {
                bool local = false;
                if (node.group != null)
                {
                    local = EvalPlan(node.group, e);
                }
                else if (node.condition != null)
                {
                    local = EvalCondition(node.condition, e);
                }

                result = first ? local : Combine(result, local, plan.logic);
                first = false;
            }
            return result;
        }

        private static bool EvalCondition(Condition c, Employee e)
        {
            string op = c.op.ToLowerInvariant();
            object? lhs = c.field switch
            {
                "EmployeeId" => e.EmployeeId,
                "Name" => e.Name,
                "Title" => e.Title,
                "Rating" => e.Rating,
                "BirthDate" => e.BirthDate,
                "Gender" => e.Gender,
                "Salary" => e.Salary,
                _ => null
            };
            if (lhs is null) return false;

            (string? s, decimal? dec, int? i, DateTime? dt) Parse(string? v)
            {
                if (v is null) return (null, null, null, null);
                if (DateTime.TryParse(v, out var dtmp)) return (v, null, null, dtmp);
                if (int.TryParse(v, out var itmp)) return (v, null, itmp, null);
                if (decimal.TryParse(v, out var dctmp)) return (v, dctmp, null, null);
                return (v, null, null, null);
            }

            bool StrCmp(Func<string, bool> pred) => lhs is string ss && pred(ss);
            bool NumCmp(Func<decimal, bool> pred) => (lhs is int ii && pred(ii)) || (lhs is decimal dd && pred(dd));
            bool DateCmp(Func<DateTime, bool> pred) => lhs is DateTime d && pred(d);

            var (sv, dv, iv, dtv) = Parse(c.value);

            return op switch
            {
                "eq" => lhs switch
                {
                    string => StrCmp(x => string.Equals(x, sv, StringComparison.OrdinalIgnoreCase)),
                    int or decimal => NumCmp(x => x == (iv ?? dv ?? 0)),
                    DateTime => DateCmp(x => dtv.HasValue && x.Date == dtv.Value.Date),
                    _ => false
                },
                "ne" => lhs switch
                {
                    string => StrCmp(x => !string.Equals(x, sv, StringComparison.OrdinalIgnoreCase)),
                    int or decimal => NumCmp(x => x != (iv ?? dv ?? 0)),
                    DateTime => DateCmp(x => !dtv.HasValue || x.Date != dtv.Value.Date),
                    _ => false
                },
                "gt" => NumCmp(x => x > (iv ?? dv ?? 0)) || DateCmp(x => dtv.HasValue && x > dtv.Value),
                "gte" => NumCmp(x => x >= (iv ?? dv ?? 0)) || DateCmp(x => dtv.HasValue && x >= dtv.Value),
                "lt" => NumCmp(x => x < (iv ?? dv ?? 0)) || DateCmp(x => dtv.HasValue && x < dtv.Value),
                "lte" => NumCmp(x => x <= (iv ?? dv ?? 0)) || DateCmp(x => dtv.HasValue && x <= dtv.Value),
                "before" => DateCmp(x => dtv.HasValue && x < dtv.Value),
                "after" => DateCmp(x => dtv.HasValue && x > dtv.Value),
                "contains" => StrCmp(x => x.Contains(sv ?? "", StringComparison.OrdinalIgnoreCase)),
                "startswith" => StrCmp(x => x.StartsWith(sv ?? "", StringComparison.OrdinalIgnoreCase)),
                "endswith" => StrCmp(x => x.EndsWith(sv ?? "", StringComparison.OrdinalIgnoreCase)),
                "between" => (c.values?.Count ?? 0) >= 2 && (
                    lhs is DateTime d && DateTime.TryParse(c.values![0], out var d1) && DateTime.TryParse(c.values![1], out var d2) && d >= d1 && d <= d2 ||
                    lhs is int ii && int.TryParse(c.values![0], out var i1) && int.TryParse(c.values![1], out var i2) && ii >= i1 && ii <= i2 ||
                    lhs is decimal dd && decimal.TryParse(c.values![0], out var m1) && decimal.TryParse(c.values![1], out var m2) && dd >= m1 && dd <= m2
                ),
                "in" => c.values != null && (lhs switch
                {
                    string ss => c.values.Any(v => string.Equals(v, ss, StringComparison.OrdinalIgnoreCase)),
                    int ii => c.values.Any(v => int.TryParse(v, out var x) && x == ii),
                    decimal dd => c.values.Any(v => decimal.TryParse(v, out var x) && x == dd),
                    _ => false
                }),
                _ => false
            };
        }
    }
}