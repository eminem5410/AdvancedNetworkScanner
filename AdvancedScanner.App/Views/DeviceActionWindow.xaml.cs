using System.Collections.Generic;
using System.Windows;
using AdvancedScanner.App.Models;
using AdvancedScanner.App.Services;

namespace AdvancedScanner.App.Views
{
    public partial class DeviceActionWindow : Window
    {
        private readonly NetworkDevice _device;
        private readonly NetworkScannerService _service;

        public DeviceActionWindow(NetworkDevice device)
        {
            InitializeComponent();
            _device = device;
            _service = new NetworkScannerService();
            
            // Asignar el dispositivo al contexto de datos
            this.DataContext = _device;
        }

        private void OpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            _service.OpenInBrowser(_device.IP);
        }

        private void OpenSMB_Click(object sender, RoutedEventArgs e)
        {
            _service.OpenSharedFolder(_device.IP);
        }

        private void WakeOnLan_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_device.MACAddress) || _device.MACAddress.Contains("00:00:00"))
            {
                MessageBox.Show("No se puede enviar WOL: La dirección MAC no es válida.", "Error");
                return;
            }
            bool success = _service.WakeOnLan(_device.MACAddress);
            MessageBox.Show(success ? "Paquete WOL enviado." : "Error al enviar WOL.", "Wake-on-LAN");
        }

        private void OpenRDP_Click(object sender, RoutedEventArgs e)
        {
            if (_device.Status == "Online")
            {
                bool success = _service.OpenRemoteDesktop(_device.IP);
                if (!success) MessageBox.Show("No se pudo iniciar RDP.", "Error RDP");
            }
            else
            {
                MessageBox.Show("El dispositivo está Offline.", "Estado Offline");
            }
        }

        private void CopyIP_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(_device.IP);
            MessageBox.Show($"IP {_device.IP} copiada.", "Copiado");
        }

        // NUEVO: Manejador del escaneo de puertos
        private async void ScanPorts_Click(object sender, RoutedEventArgs e)
        {
            if (_device.Status != "Online")
            {
                MessageBox.Show("El dispositivo está Offline. No se pueden escanear puertos.", "Error");
                return;
            }

            // Deshabilitar botón y limpiar lista mientras escanea
            BtnScanPorts.Content = "Escaneando...";
            BtnScanPorts.IsEnabled = false;
            PortsListBox.Items.Clear();
            PortsListBox.Items.Add("Conectando a puertos...");

            try
            {
                // Ejecutar escaneo asíncrono
                List<string> openPorts = await _service.ScanCommonPortsAsync(_device.IP);

                PortsListBox.Items.Clear();

                if (openPorts.Count > 0)
                {
                    foreach (string port in openPorts)
                    {
                        PortsListBox.Items.Add(port);
                    }
                }
                else
                {
                    PortsListBox.Items.Add("No se detectaron puertos comunes abiertos.");
                }
            }
            catch (System.Exception ex)
            {
                PortsListBox.Items.Clear();
                PortsListBox.Items.Add($"Error: {ex.Message}");
            }
            finally
            {
                // Restaurar botón
                BtnScanPorts.Content = "Escanear Puertos Comunes";
                BtnScanPorts.IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}