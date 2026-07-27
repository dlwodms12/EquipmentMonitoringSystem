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
using Monitoring.Server.Services;
using Monitoring.Shared.Models;

namespace Monitoring.Server
{
    public partial class MainWindow : Window
    {
        private readonly TcpServerService _serverService = new();

        private string? _selectedDeviceId;

        public MainWindow()
        {
            InitializeComponent();

            _serverService.LogReceived += ServerService_LogReceived;
            _serverService.DeviceRegistered += ServerService_DeviceRegistered;
            _serverService.MessageReceived += ServerService_MessageReceived;
            _serverService.DeviceDisconnected += ServerService_DeviceDisconnected;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender,RoutedEventArgs e)
        {
            await _serverService.StartAsync(5000);
        }

        private void MainWindow_Closing(object? sender,System.ComponentModel.CancelEventArgs e)
        {
            _serverService.Stop();
        }

        private void ServerService_LogReceived(string log)
        {
            Dispatcher.Invoke(() =>
            {
                LogListBox.Items.Add(
                    $"{DateTime.Now:HH:mm:ss} {log}");

                LogListBox.ScrollIntoView(
                    LogListBox.Items[LogListBox.Items.Count - 1]);
            });
        }

        private void ServerService_DeviceRegistered(NetworkMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                bool alreadyExists = DeviceListBox.Items
                    .OfType<System.Windows.Controls.ListBoxItem>()
                    .Any(x => x.Content?.ToString() == message.DeviceId);

                if (!alreadyExists)
                {
                    DeviceListBox.Items.Add(
                        new System.Windows.Controls.ListBoxItem
                        {
                            Content = message.DeviceId
                        });
                }
            });
        }

        private void DeviceListBox_SelectionChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DeviceListBox.SelectedItem
                is System.Windows.Controls.ListBoxItem item)
            {
                _selectedDeviceId = item.Content?.ToString();

                SelectedDeviceText.Text =
                    $"현재 선택된 장비: {_selectedDeviceId}";

                ConnectionText.Text = "통신: Connected";
            }
        }

        private void ServerService_MessageReceived(NetworkMessage message)
        {
            if (message.Type == MessageType.PingRequest)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_selectedDeviceId == message.DeviceId)
                    {
                        ConnectionText.Text = "통신: Connected";
                    }
                });
            }
        }

        private void ServerService_DeviceDisconnected(string deviceId)
        {
            Dispatcher.Invoke(() =>
            {
                System.Windows.Controls.ListBoxItem? deviceItem =
                    DeviceListBox.Items
                        .OfType<System.Windows.Controls.ListBoxItem>()
                        .FirstOrDefault(x =>
                            x.Content?.ToString() == deviceId);

                if (deviceItem is not null)
                {
                    deviceItem.Foreground =
                        System.Windows.Media.Brushes.Red;
                }

                if (_selectedDeviceId == deviceId)
                {
                    ConnectionText.Text = "통신: Disconnected";
                }

                LogListBox.Items.Add(
                    $"{DateTime.Now:HH:mm:ss} {deviceId} 연결이 종료되었습니다.");
            });
        }

        private async void SendButton_Click(object sender,RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedDeviceId))
            {
                LogListBox.Items.Add(
                    $"{DateTime.Now:HH:mm:ss} 장비를 먼저 선택하세요.");

                return;
            }

            NetworkMessage command;

            if (PingRadioButton.IsChecked == true)
            {
                command = new NetworkMessage
                {
                    Type = MessageType.PingRequest,
                    DeviceId = _selectedDeviceId,
                    RequestId = Guid.NewGuid().ToString(),
                    SentAt = DateTime.Now
                };
            }
            else if (SystemInfoRadioButton.IsChecked == true)
            {
                command = new NetworkMessage
                {
                    Type = MessageType.SystemInfoRequest,
                    DeviceId = _selectedDeviceId,
                    RequestId = Guid.NewGuid().ToString(),
                    SentAt = DateTime.Now
                };
            }
            else
            {
                LogListBox.Items.Add(
                    $"{DateTime.Now:HH:mm:ss} 전송할 명령을 선택하세요.");

                return;
            }

            await _serverService.SendAsync(_selectedDeviceId, command);
        }
    }
}