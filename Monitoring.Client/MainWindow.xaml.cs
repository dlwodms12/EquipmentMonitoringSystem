using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Monitoring.Client.Services;
using Monitoring.Shared.Models;

namespace Monitoring.Client
{
    public partial class MainWindow : Window
    {
        private readonly TcpClientService _clientService = new();

        private const string ServerIp = "127.0.0.1";
        private const int ServerPort = 5000;

        private const string DeviceId = "Device-001";
        private const string DeviceName = "장비 1";

        public MainWindow()
        {
            InitializeComponent();

            _clientService.LogReceived += ClientService_LogReceived;
            _clientService.ConnectionChanged += ClientService_ConnectionChanged;
            _clientService.MessageReceived += ClientService_MessageReceived;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DeviceNameText.Text = $"장비 이름: {DeviceId}";

            await _clientService.ConnectAsync(
                ServerIp,
                ServerPort,
                DeviceId,
                DeviceName);
        }

        private void MainWindow_Closing(object? sender,System.ComponentModel.CancelEventArgs e)
        {
            _clientService.Disconnect();
        }

        private void ClientService_LogReceived(string log)
        {
            Dispatcher.Invoke(() =>
            {
                LogListBox.Items.Add(
                    $"{DateTime.Now:HH:mm:ss} {log}");

                LogListBox.ScrollIntoView(
                    LogListBox.Items[LogListBox.Items.Count - 1]);
            });
        }

        private void ClientService_ConnectionChanged(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                ServerConnectionText.Text = isConnected
                    ? "서버가 연결되었습니다."
                    : "서버 연결이 끊어졌습니다.";

                ServerConnectionText.Foreground = isConnected
                    ? System.Windows.Media.Brushes.Green
                    : System.Windows.Media.Brushes.Red;
            });
        }

        private async void ClientService_MessageReceived(NetworkMessage message)
        {
            if (message.Type == MessageType.PingResponse)
            {
                return;
            }

            if (message.Type == MessageType.PingRequest)
            {
                NetworkMessage response = new()
                {
                    Type = MessageType.PingResponse,
                    DeviceId = DeviceId,
                    DeviceName = DeviceName,
                    RequestId = message.RequestId,
                    SentAt = DateTime.Now
                };

                await _clientService.SendAsync(response);
            }
        }
    }
}