using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace DataGridAIFilteringSample
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public int Rating { get; set; }
        public DateTime BirthDate { get; set; }
        public string Gender { get; set; } = "";
        public decimal Salary { get; set; }
    }

    public static class EmployeeRepository
    {
        public static List<Employee> GetEmployees(int count = 200)
        {
            var rnd = new Random(7);
            var titles = new[] { "Production Technician - WC50", "Marketing Assistant", "Tool Designer",
            "Marketing Specialist", "Production Supervisor - WC60" };
            var genders = new[] { "Male", "Female" };
            var names = new[] { "Kim Abercrombie", "Ramona Antrim", "Carla Adams", "James Aguilar",
            "Milton Albury", "Thomas Armstrong", "Emilio Alvaro", "Francois Ferrier", "Tom Johnston",
            "Kyley Arbeleaz" };

            return Enumerable.Range(1, count).Select(i => new Employee
            {
                EmployeeId = 1000 + i,
                Name = names[rnd.Next(names.Length)],
                Title = titles[rnd.Next(titles.Length)],
                Rating = rnd.Next(1, 10),
                BirthDate = new DateTime(rnd.Next(1975, 1990), rnd.Next(1, 12), rnd.Next(1, 28)),
                Gender = genders[rnd.Next(2)],
                Salary = (decimal)(rnd.NextDouble() * 6000 + 300)
            }).ToList();
        }
    }

    public class FilterPlan
    {
        public string logic { get; set; } = "and";
        public List<FilterNode> conditions { get; set; } = new();
    }

    public class FilterNode
    {
        public Condition? condition { get; set; }
        public FilterPlan? group { get; set; }
    }

    public class Condition
    {
        public string field { get; set; } = "";
        public string op { get; set; } = "";
        public string? value { get; set; }
        public List<string>? values { get; set; }
    }
}