using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AdvancedScanner.App.Models;
using AdvancedScanner.App.ViewModels;
using AdvancedScanner.App.Views; // Necesario para la ventana de acciones

namespace AdvancedScanner.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Conectamos el ViewModel con la Vista (ESTO ES VITAL PARA QUE EL ESCANEO FUNCIONE)
            this.DataContext = new MainViewModel();
        }

        // NUEVO: Método para manejar el doble clic en la tabla
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Verificamos que quien disparó el evento sea el DataGrid
            if (sender is DataGrid grid)
            {
                // Obtenemos el dispositivo seleccionado
                if (grid.SelectedItem is NetworkDevice selectedDevice)
                {
                    // Creamos y abrimos la ventana de acciones
                    var actionWindow = new DeviceActionWindow(selectedDevice);
                    
                    // Hacemos que esta ventana sea "dueña" de la nueva
                    actionWindow.Owner = this;
                    
                    // Mostramos la ventana
                    actionWindow.ShowDialog();
                }
            }
        }
    }
}