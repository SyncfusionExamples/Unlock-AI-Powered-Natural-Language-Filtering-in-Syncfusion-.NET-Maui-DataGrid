using DataGridAIFilteringSample.ViewModel;

namespace DataGridAIFilteringSample
{
    public partial class MainPage : ContentPage
    {
        private readonly EmployeesViewModel viewModel;

        public MainPage()
        {
            InitializeComponent();
            var aiService = new AiFilterService();
            viewModel = new EmployeesViewModel(aiService);
            BindingContext = viewModel;
            viewModel.FilterChanged += (_, __) =>
            {
                DataGrid.View?.Filter = viewModel.BuildPredicate();
                DataGrid.View?.RefreshFilter();
            };
        }
    }
}
