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

        // MainWindow_Loaded 이벤트 핸들러는 MainWindow가 로드될 때 호출되며, 서버를 시작하고 지정된 포트에서 클라이언트 연결을 수락
        private async void MainWindow_Loaded(object sender,RoutedEventArgs e)
        {
            await _serverService.StartAsync(5000);
        }

        // MainWindow_Closing 이벤트 핸들러는 MainWindow가 닫힐 때 호출되며, 서버를 중지
        private void MainWindow_Closing(object? sender,System.ComponentModel.CancelEventArgs e)
        {
            _serverService.Stop();
        }

        // ServerService_LogReceived 이벤트 핸들러는 서버로부터 로그 메시지를 수신할 때 호출되며, LogListBox에 로그를 추가하고 스크롤을 최신 로그로 이동
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

        // ServerService_DeviceRegistered 이벤트 핸들러는 장비가 등록될 때 호출되며, DeviceListBox에 장비 ID를 추가. 이미 존재하는 경우 중복 추가 방지
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

        // DeviceListBox_SelectionChanged 이벤트 핸들러는 DeviceListBox에서 선택된 장비가 변경될 때 호출되며, 선택된 장비 ID를 저장하고 SelectedDeviceText와 ConnectionText를 업데이트
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

        //  ServerService_MessageReceived 이벤트 핸들러는 서버로부터 메시지를 수신할 때 호출되며, PingRequest 메시지를 수신하면 선택된 장비와 일치하는 경우 ConnectionText를 "통신: Connected"로 업데이트
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

        // ServerService_DeviceDisconnected 이벤트 핸들러는 장비 연결이 종료될 때 호출되며, DeviceListBox에서 해당 장비 ID를 찾아 글자색을 빨간색으로 변경하고, 선택된 장비가 연결 종료된 경우 ConnectionText를 "통신: Disconnected"로 업데이트. 또한 로그에 연결 종료 메시지를 추가
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

        // SendButton_Click 이벤트 핸들러는 SendButton이 클릭될 때 호출되며, 선택된 장비가 없으면 로그에 메시지를 추가하고, PingRequest 또는 SystemInfoRequest 명령을 생성하여 서버로 전송. 선택된 명령이 없으면 로그에 메시지를 추가하고 종료
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