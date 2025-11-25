namespace DataGridAIFilteringSample
{
    public partial class MainPage : ContentPage
    {
        private readonly EmployeesViewModel _vm;

        public MainPage()
        {
            InitializeComponent();

            var aiSettings = new AiSettings
            {
                Provider = AiProvider.Local
            };

            var aiService = new AiFilterService(aiSettings);
            _vm = new EmployeesViewModel(aiService);
            BindingContext = _vm;

            _vm.FilterChanged += (_, __) =>
            {
                DataGrid.View?.Filter = _vm.BuildPredicate();
                DataGrid.View?.RefreshFilter();
            };
        }
    }
}
