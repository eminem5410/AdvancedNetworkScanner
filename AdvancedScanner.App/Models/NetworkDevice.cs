using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AdvancedScanner.App.Models
{
    public class NetworkDevice : INotifyPropertyChanged
    {
        private string _ip = string.Empty;
        private string _hostName = string.Empty;
        private string _macAddress = string.Empty;
        private string _manufacturer = string.Empty;
        private string _status = "Offline";
        private int _latency = 0;
        private string _publicIP = string.Empty;
        
        // NUEVO: Tipo de dispositivo
        private string _deviceType = "Unknown";

        public string IP
        {
            get => _ip;
            set { _ip = value; OnPropertyChanged(); }
        }

        public string HostName
        {
            get => _hostName;
            set { _hostName = value; OnPropertyChanged(); }
        }

        public string MACAddress
        {
            get => _macAddress;
            set { _macAddress = value; OnPropertyChanged(); }
        }

        public string Manufacturer
        {
            get => _manufacturer;
            set { _manufacturer = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }
        
        public int Latency
        {
            get => _latency;
            set { _latency = value; OnPropertyChanged(); }
        }

        public string PublicIP
        {
            get => _publicIP;
            set { _publicIP = value; OnPropertyChanged(); }
        }

        public string DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
        }
    }
}