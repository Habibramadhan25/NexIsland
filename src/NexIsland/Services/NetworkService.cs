using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace NexIsland.Services
{
    public class NetworkStatusInfo
    {
        public bool IsConnected { get; set; }
        public string ConnectionType { get; set; } = "Unknown";
        public string StatusMessage { get; set; } = "Disconnected";
    }

    public class NetworkService
    {
        private static NetworkService? _instance;
        public static NetworkService Instance => _instance ??= new NetworkService();

        private bool _lastState;
        public NetworkStatusInfo CurrentStatus { get; private set; } = new();

        public event Action<NetworkStatusInfo>? NetworkStatusChanged;
        public event Action<string>? NetworkAlert;

        public NetworkService()
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += (s, e) => _ = RefreshStatusAsync();
        }

        public async Task StartAsync()
        {
            await RefreshStatusAsync();
            _lastState = CurrentStatus.IsConnected;
        }

        private async void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            await RefreshStatusAsync();
        }

        public async Task RefreshStatusAsync()
        {
            bool isAvailable = NetworkInterface.GetIsNetworkAvailable();
            string connType = "Unknown";

            if (isAvailable)
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        connType = nic.NetworkInterfaceType switch
                        {
                            NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                            NetworkInterfaceType.Ethernet => "Ethernet",
                            NetworkInterfaceType.Wman or NetworkInterfaceType.Wwanpp => "Cellular",
                            _ => nic.NetworkInterfaceType.ToString()
                        };
                        break;
                    }
                }
            }

            // Quick internet verification check
            bool hasInternet = false;
            if (isAvailable)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync("1.1.1.1", 1000);
                    hasInternet = reply.Status == IPStatus.Success;
                }
                catch
                {
                    hasInternet = isAvailable; // Fallback to local network status
                }
            }

            CurrentStatus = new NetworkStatusInfo
            {
                IsConnected = hasInternet,
                ConnectionType = connType,
                StatusMessage = hasInternet ? $"{connType} Connected" : (isAvailable ? "No Internet Access" : "Disconnected")
            };

            if (_lastState != CurrentStatus.IsConnected)
            {
                _lastState = CurrentStatus.IsConnected;
                var alert = CurrentStatus.IsConnected
                    ? $"Internet connection restored ({connType})"
                    : "Internet connection disconnected";
                NetworkAlert?.Invoke(alert);
            }

            NetworkStatusChanged?.Invoke(CurrentStatus);
        }
    }
}
