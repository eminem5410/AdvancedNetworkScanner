using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO; // Agregado para guardar archivos
using System.Runtime.CompilerServices;
using System.Text; // Agregado para generar el texto del CSV
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32; // Agregado para el diálogo de guardar
// Eliminé el using System.Windows; duplicado
using AdvancedScanner.App.Models;
using AdvancedScanner.App.Services;

namespace AdvancedScanner.App.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly NetworkScannerService _scannerService;
        private bool _isScanning;
        private string _scanButtonText = "INICIAR ESCANEO";
        private string _networkPublicIP = "Detecting...";

        // Propiedades de Rango de IP
        private string _startIP = "192.168.0.1";
        private string _endIP = "192.168.0.254";

        public ObservableCollection<NetworkDevice> Devices { get; set; }
        
        public string ScanButtonText
        {
            get => _scanButtonText;
            set { _scanButtonText = value; OnPropertyChanged(); }
        }

        public string NetworkPublicIP
        {
            get => _networkPublicIP;
            set { _networkPublicIP = value; OnPropertyChanged(); }
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set { _isScanning = value; OnPropertyChanged(); }
        }

        public string StartIP
        {
            get => _startIP;
            set { _startIP = value; OnPropertyChanged(); }
        }

        public string EndIP
        {
            get => _endIP;
            set { _endIP = value; OnPropertyChanged(); }
        }

        // Comandos Existentes
        public ICommand ScanCommand { get; }
        public ICommand RemoteDesktopCommand { get; }

        // Comandos para acciones
        public ICommand OpenBrowserCommand { get; }
        public ICommand OpenSMBCommand { get; }
        public ICommand WakeOnLanCommand { get; }
        public ICommand CopyIPCommand { get; }
        public ICommand PingCommand { get; }

        // NUEVO: Comando de Exportación
        public ICommand ExportCommand { get; }

        public MainViewModel()
        {
            _scannerService = new NetworkScannerService();
            Devices = new ObservableCollection<NetworkDevice>();
            
            // Inicializar Comandos Existentes
            ScanCommand = new RelayCommand(async () => await OnScanAsync());
            RemoteDesktopCommand = new RelayCommand((param) => OnRemoteDesktop(param));
            
            // Inicializar Comandos de Acción
            OpenBrowserCommand = new RelayCommand((param) => ExecuteAction(param, "browser"));
            OpenSMBCommand = new RelayCommand((param) => ExecuteAction(param, "smb"));
            WakeOnLanCommand = new RelayCommand((param) => ExecuteAction(param, "wol"));
            CopyIPCommand = new RelayCommand((param) => ExecuteAction(param, "copy"));
            PingCommand = new RelayCommand((param) => ExecuteAction(param, "ping"));

            // Inicializar Comando de Exportación
            ExportCommand = new RelayCommand(ExportToCsv);

            ConfigureSorting();
            LoadDefaultNetworks(); 
        }

        private void LoadDefaultNetworks()
        {
            try
            {
                string subnet = _scannerService.GetLocalSubnet(); 
                StartIP = $"{subnet}.1";
                EndIP = $"{subnet}.254";
            }
            catch { }
        }

        private void ConfigureSorting()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(Devices);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription("Status", ListSortDirection.Descending));
            view.SortDescriptions.Add(new SortDescription("IP", ListSortDirection.Ascending));
            view.Refresh();
        }

        private async Task OnScanAsync()
        {
            if (IsScanning) return;
            
            IsScanning = true;
            ScanButtonText = "ESCANEANDO..."; 
            Devices.Clear(); 

            try 
            {
                NetworkPublicIP = await _scannerService.GetPublicIPAsync();

                var (baseStart, startNum) = ParseIP(StartIP);
                var (baseEnd, endNum) = ParseIP(EndIP);

                if (baseStart == null || baseEnd == null)
                {
                    System.Windows.MessageBox.Show("Formato de IP inválido. Use: 192.168.0.1");
                    IsScanning = false;
                    ScanButtonText = "INICIAR ESCANEO";
                    return;
                }

                if (baseStart != baseEnd)
                {
                    System.Windows.MessageBox.Show("Las IPs deben pertenecer a la misma subred (ej: 192.168.0.x).");
                    IsScanning = false;
                    ScanButtonText = "INICIAR ESCANEO";
                    return;
                }

                if (startNum > endNum)
                {
                    int temp = startNum; startNum = endNum; endNum = temp;
                }

                var results = await _scannerService.ScanRangeAsync(baseStart, startNum, endNum, NetworkPublicIP);

                foreach (var device in results)
                {
                    Devices.Add(device);
                }
                ConfigureSorting();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                ScanButtonText = "INICIAR ESCANEO";
            }
        }

        private (string? baseIp, int lastOctet) ParseIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return (null, 0);
            string[] parts = ip.Split('.');
            if (parts.Length != 4) return (null, 0);
            if (!int.TryParse(parts[3], out int lastOctet)) return (null, 0);
            string baseIp = $"{parts[0]}.{parts[1]}.{parts[2]}";
            return (baseIp, lastOctet);
        }

        private void ExecuteAction(object? parameter, string actionType)
        {
            if (parameter is NetworkDevice device)
            {
                try
                {
                    switch (actionType)
                    {
                        case "browser":
                            _scannerService.OpenInBrowser(device.IP);
                            break;
                        case "smb":
                            _scannerService.OpenSharedFolder(device.IP);
                            break;
                        case "wol":
                            if (string.IsNullOrEmpty(device.MACAddress) || device.MACAddress.Contains("00:00:00"))
                            {
                                System.Windows.MessageBox.Show("No se puede enviar WOL: La dirección MAC no es válida.", "Error WOL");
                                return;
                            }
                            bool success = _scannerService.WakeOnLan(device.MACAddress);
                            System.Windows.MessageBox.Show(success ? $"Paquete WOL enviado a {device.IP}" : "Error al enviar paquete WOL.", "Wake-on-LAN");
                            break;
                        case "copy":
                            Clipboard.SetText(device.IP);
                            System.Windows.MessageBox.Show($"IP {device.IP} copiada al portapapeles.", "Copiado");
                            break;
                        case "ping":
                            System.Windows.MessageBox.Show($"Iniciando ping a {device.IP}...", "Ping");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }

        private void OnRemoteDesktop(object? parameter)
        {
            if (parameter is NetworkDevice device && device.Status == "Online")
            {
                bool success = _scannerService.OpenRemoteDesktop(device.IP);
                
                if (!success)
                {
                    System.Windows.MessageBox.Show("No se pudo iniciar la conexión RDP desde la lista principal.", "Error RDP");
                }
            }
            else
            {
                System.Windows.MessageBox.Show("El dispositivo está Offline.", "Estado Offline");
            }
        }

        // NUEVO: Método para exportar a CSV
        private void ExportToCsv()
        {
            try
            {
                // Crear el diálogo para guardar archivo
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    Title = "Guardar Reporte de Escaneo",
                    FileName = $"NetworkScan_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Crear el contenido del CSV
                    var csvContent = new StringBuilder();
                    // Cabeceras
                    csvContent.AppendLine("IP Address,Host Name,MAC Address,Device Type,Status,Latency (ms),Public IP");

                    // Filas
                    foreach (var device in Devices)
                    {
                        csvContent.AppendLine($"{device.IP},{device.HostName},{device.MACAddress},{device.DeviceType},{device.Status},{device.Latency},{device.PublicIP}");
                    }

                    // Escribir en disco
                    File.WriteAllText(saveFileDialog.FileName, csvContent.ToString());
                    
                    MessageBox.Show("Reporte exportado correctamente.", "Éxito");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action? _execute;
        private readonly Action<object?>? _executeParam;
        public event EventHandler? CanExecuteChanged;
        public RelayCommand(Action execute) => _execute = execute;
        public RelayCommand(Action<object?> executeParam) => _executeParam = executeParam;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter)
        {
            if (_execute != null) _execute();
            else _executeParam?.Invoke(parameter);
        }
    }
}