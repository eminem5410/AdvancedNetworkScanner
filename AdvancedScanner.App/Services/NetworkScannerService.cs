using System;
using System.Collections.Generic;
using System.Linq; 
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AdvancedScanner.App.Models;

namespace AdvancedScanner.App.Services
{
    public class NetworkScannerService
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int destIp, int srcIp, byte[] mac, ref int macLen);

        public string GetLocalSubnet()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        if (!ipStr.StartsWith("127"))
                        {
                            int lastDot = ipStr.LastIndexOf('.');
                            if (lastDot > 0) return ipStr.Substring(0, lastDot);
                        }
                    }
                }
            }
            catch { }
            return "192.168.1";
        }

        public async Task<string> GetPublicIPAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    return await client.GetStringAsync("https://api.ipify.org");
                }
            }
            catch { return "Unknown"; }
        }

        public async Task<List<NetworkDevice>> ScanRangeAsync(string baseIp, int start, int end, string publicIp)
        {
            var devices = new List<NetworkDevice>();
            var tasks = new List<Task<NetworkDevice>>();

            for (int i = start; i <= end; i++)
            {
                string ip = $"{baseIp}.{i}";
                tasks.Add(ScanSingleIpAsync(ip, publicIp));
            }

            var results = await Task.WhenAll(tasks);
            devices.AddRange(results);
            return devices;
        }

        private async Task<NetworkDevice> ScanSingleIpAsync(string ip, string publicIp)
        {
            var device = new NetworkDevice { IP = ip, Status = "Offline", PublicIP = publicIp };

            try
            {
                using (Ping ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ip, 50);

                    if (reply.Status == IPStatus.Success)
                    {
                        device.Status = "Online";
                        device.Latency = (int)reply.RoundtripTime;
                        
                        try { device.HostName = (await Dns.GetHostEntryAsync(ip)).HostName; } 
                        catch { device.HostName = "Unknown"; }
                        
                        string mac = GetMacAddress(ip);
                        device.MACAddress = mac;
                        
                        // Identificar tipo de dispositivo
                        device.DeviceType = GetDeviceType(mac);
                    }
                }
            }
            catch { }
            return device;
        }

        private string GetMacAddress(string ipAddress)
        {
            try
            {
                byte[] mac = new byte[6];
                int len = mac.Length;
                uint ip = BitConverter.ToUInt32(IPAddress.Parse(ipAddress).GetAddressBytes(), 0);
                int res = SendARP((int)ip, 0, mac, ref len);
                if (res == 0 && len == 6) return string.Join(":", (IEnumerable<byte>)mac).ToUpper();
            }
            catch { }
            return "00:00:00:00:00:00";
        }

        // Lógica simple de identificación por prefijo MAC (OUI)
        private string GetDeviceType(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac == "00:00:00:00:00:00") return "Unknown";

            string prefix = mac.Substring(0, 8).ToUpper();

            // Routers / APs
            if (prefix.StartsWith("00:0C:43") || prefix.StartsWith("00:0E:8C") || 
                prefix.StartsWith("00:1A:A1") || // Cisco/Linksys
                prefix.StartsWith("00:26:B9") || // Netgear
                prefix.StartsWith("00:27:22") || // TP-Link
                prefix.StartsWith("F0:B4:29") || // Xiaomi Router
                prefix.StartsWith("D4:6E:0E") || // Netgear Nighthawk
                prefix.StartsWith("E0:63:DA"))   // Tenda
                return "Router/AP";

            // Smartphones
            if (prefix.StartsWith("AC:87:A3") || // Xiaomi Mobile
                prefix.StartsWith("F8:FF:C2") || // Apple
                prefix.StartsWith("3C:A9:F4") || // Google
                prefix.StartsWith("34:23:BA") || // Xiaomi
                prefix.StartsWith("48:D2:24") || // Samsung
                prefix.StartsWith("CC:B2:55") || // Huawei
                prefix.StartsWith("78:11:DC"))   // Asus
                return "Smartphone/Tablet";

            // Smart TVs / Consolas
            if (prefix.StartsWith("00:15:99") || // Samsung
                prefix.StartsWith("00:04:5F") || // LG
                prefix.StartsWith("D4:3F:8B") || // Sony
                prefix.StartsWith("A0:38:BF"))   // Hisense
                return "Smart TV";

            // PC / Laptop
            if (prefix.StartsWith("00:1B:21") || // Intel
                prefix.StartsWith("00:E0:4C") || // Realtek
                prefix.StartsWith("BC:5F:F4") || // Intel
                prefix.StartsWith("F4:4D:FB"))   // Intel
                return "PC/Laptop";

            return "Unknown Device";
        }

        public bool OpenRemoteDesktop(string ip)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mstsc.exe",
                    Arguments = $"/v:{ip}",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(processInfo);
                return true;
            }
            catch (Exception) { return false; }
        }

        // --- ACCIONES AVANZADAS ---

        // 1. Abrir en Navegador
        public void OpenInBrowser(string ip)
        {
            try
            {
                var url = $"http://{ip}";
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error Browser: {ex.Message}"); }
        }

        // 2. Abrir Carpetas Compartidas (SMB)
        public void OpenSharedFolder(string ip)
        {
            try
            {
                var path = $@"\\{ip}";
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error SMB: {ex.Message}"); }
        }

        // 3. Wake-on-LAN (Encender equipo remoto)
        public bool WakeOnLan(string macAddress)
        {
            try
            {
                macAddress = macAddress.Replace(":", "").Replace("-", "").Replace(".", "").ToUpper();
                if (macAddress.Length != 12) return false;

                byte[] packet = new byte[102];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;

                for (int i = 6; i < 102; i += 6)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        packet[i + j] = Convert.ToByte(macAddress.Substring(j * 2, 2), 16);
                    }
                }

                using (System.Net.Sockets.UdpClient client = new System.Net.Sockets.UdpClient())
                {
                    client.Connect(System.Net.IPAddress.Broadcast, 9);
                    client.Send(packet, packet.Length);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error WOL: {ex.Message}");
                return false;
            }
        }

        // CORREGIDO: Escáner de Puertos con Timeout seguro
        public async Task<List<string>> ScanCommonPortsAsync(string ip)
        {
            var openPorts = new List<string>();
            
            var portsToScan = new Dictionary<int, string>
            {
                { 21, "FTP" },
                { 22, "SSH" },
                { 23, "Telnet" },
                { 53, "DNS" },
                { 80, "HTTP" },
                { 135, "RPC" },
                { 139, "NetBIOS" },
                { 443, "HTTPS" },
                { 445, "SMB" },
                { 3306, "MySQL" },
                { 3389, "RDP" },
                { 5432, "PostgreSQL" },
                { 5900, "VNC" },
                { 8080, "HTTP-Alt" }
            };

            var tasks = portsToScan.Select(async portPair =>
            {
                int port = portPair.Key;
                string service = portPair.Value;
                
                try
                {
                    using (var tcpClient = new System.Net.Sockets.TcpClient())
                    {
                        // CORRECCIÓN: Usamos Task.WhenAny para simular el timeout
                        var connectTask = tcpClient.ConnectAsync(ip, port);
                        var timeoutTask = Task.Delay(200); // 200ms timeout

                        var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                        if (completedTask == connectTask)
                        {
                            // Si la conexión se completó antes del timeout, verificamos si fue exitosa
                            await connectTask; 
                            return $"{port} - {service} (Open)";
                        }
                        else
                        {
                            // Si ganó el timeout, el puerto está cerrado o filtrado
                            return null; 
                        }
                    }
                }
                catch
                {
                    return null; // Cualquier error (conexión rechazada, etc) se considera cerrado
                }
            });

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result != null)
                {
                    openPorts.Add(result);
                }
            }

            return openPorts;
        }
    }
}