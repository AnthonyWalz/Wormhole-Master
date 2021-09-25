using System.Windows;
using Wormhole.ViewModels;
using Wormhole.Views.Converters;

namespace Wormhole.Views
{
    public partial class DestinationsEditor : Window
    {
        private readonly GateViewModel _viewModel;

        public DestinationsEditor()
        {
            InitializeComponent();
        }

        public DestinationsEditor(GateViewModel gateViewModel) : this()
        {
            DataContext = gateViewModel;
            _viewModel = gateViewModel;
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
        {
            ButtonsPanel.Visibility = Visibility.Hidden;
            AddConfirmationPanel.Visibility = Visibility.Visible;
        }

        private void ButtonDelete_OnClick(object sender, RoutedEventArgs e)
        {
            if (ElementsDataGrid.SelectedItem is DestinationViewModel item)
                _viewModel.Destinations.Remove(item);
        }

        private void ButtonConfirm_OnClick(object sender, RoutedEventArgs e)
        {
            if (TypeComboBox.SelectedItem is not ValueDescription destinationType)
                return;

            var viewModel = DestinationViewModel.Create((DestinationType)destinationType.Value);
            _viewModel.Destinations.Add(viewModel);
            ElementsDataGrid.SelectedItem = viewModel;
            ButtonsPanel.Visibility = Visibility.Visible;
            AddConfirmationPanel.Visibility = Visibility.Hidden;
        }
    }
}