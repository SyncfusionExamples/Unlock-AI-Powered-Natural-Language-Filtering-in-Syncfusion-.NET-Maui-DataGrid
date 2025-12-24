namespace DataGridAIFilterSample
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
            viewModel.FilterChanged += (sender, args) =>
            {
                DataGrid.View!.Filter = viewModel.BuildPredicate();
                DataGrid.View.RefreshFilter();
            };
        }  
    }
}
