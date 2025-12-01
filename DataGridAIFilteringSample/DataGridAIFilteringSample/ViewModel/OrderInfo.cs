using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;


namespace DataGridAIFilteringSample
{
    /// <summary>
    /// Represents an employee record with basic details such as ID, name, title, rating, birth date, gender, and salary.
    /// </summary>
    public class Employee
    {
        /// <summary>
        /// Gets or sets the unique identifier for the employee.
        /// </summary>
        public int EmployeeId { get; set; }

        /// <summary>
        /// Gets or sets the employee's name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the employee's job title.
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Gets or sets the employee's performance rating.
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// Gets or sets the employee's birth date.
        /// </summary>
        public DateTime BirthDate { get; set; }

        /// <summary>
        /// Gets or sets the employee's gender.
        /// </summary>
        public string Gender { get; set; } = "";

        /// <summary>
        /// Gets or sets the employee's salary.
        /// </summary>
        public decimal Salary { get; set; }
    }

    /// <summary>
    /// Provides methods to generate sample employee data for demonstration purposes.
    /// </summary>
    public static class EmployeeRepository
    {
        /// <summary>
        /// Generates a list of employees with random data for testing and demo scenarios.
        /// </summary>
        /// <param name="count">The number of employees to generate. Default is 200.</param>
        /// <returns>A list of <see cref="Employee"/> objects with randomized details.</returns>
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

    /// <summary>
    /// Represents a filter plan consisting of logical operators and conditions for filtering data.
    /// </summary>
    public class FilterPlan
    {
        /// <summary>
        /// Gets or sets the logical operator used to combine conditions ("and" or "or").
        /// </summary>
        public string logic { get; set; } = "and";

        /// <summary>
        /// Gets or sets the list of filter nodes (conditions or nested groups).
        /// </summary>
        public List<FilterNode> conditions { get; set; } = new();
    }

    /// <summary>
    /// Represents a node in a filter plan, which can be either a single condition or a nested group of conditions.
    /// </summary>
    public class FilterNode
    {
        /// <summary>
        /// Gets or sets the condition for this node, if applicable.
        /// </summary>
        public Condition? condition { get; set; }

        /// <summary>
        /// Gets or sets the nested filter group for this node, if applicable.
        /// </summary>
        public FilterPlan? group { get; set; }
    }

    /// <summary>
    /// Represents a single filter condition with a field, operator, and value(s).
    /// </summary>
    public class Condition
    {
        /// <summary>
        /// Gets or sets the field name to apply the condition on.
        /// </summary>
        public string field { get; set; } = "";

        /// <summary>
        /// Gets or sets the operator for the condition (e.g., eq, gt, lt, contains).
        /// </summary>
        public string op { get; set; } = "";

        /// <summary>
        /// Gets or sets the single value for the condition, if applicable.
        /// </summary>
        public string? value { get; set; }

        /// <summary>
        /// Gets or sets the list of values for the condition, if applicable (e.g., for "in" or "between").
        /// </summary>
        public List<string>? values { get; set; }
    }
}
