using System.Windows;
using Monitoring.Client.Services;
using Monitoring.Shared.Models;

namespace Monitoring.Client
{
    // partal : MainWindow 클래스의 정의가 여러 파일에 걸쳐 있을 수 있음을 나타냄
    public partial class MainWindow : Window
    {
        private readonly TcpClientService _clientService = new();

        private const string ServerIp = "127.0.0.1";
        private const int ServerPort = 5000;

        private DeviceSummary? _currentDevice;

        public MainWindow()
        {
            InitializeComponent();

            _clientService.LogReceived += ClientService_LogReceived;
            _clientService.ConnectionChanged += ClientService_ConnectionChanged;
            _clientService.MessageReceived += ClientService_MessageReceived;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        // MainWindow_Loaded 이벤트 핸들러는 MainWindow가 로드될 때 호출되며, 장비 이름을 표시하고 서버에 연결을 시도
        private async void MainWindow_Loaded(object sender,RoutedEventArgs e)
        {
            bool connected = await _clientService.ConnectAsync(
                ServerIp,
                ServerPort);

            if (connected)
            {
                await _clientService.RequestDeviceListAsync();
            }
        }

        // MainWindow_Closing 이벤트 핸들러는 MainWindow가 닫힐 때 호출되며, 서버와의 연결을 끊음
        private void MainWindow_Closing(object? sender,System.ComponentModel.CancelEventArgs e)
        {
            _clientService.Disconnect();
        }

        // ClientService_LogReceived 이벤트 핸들러는 서버로부터 로그 메시지를 수신할 때 호출되며, LogListBox에 로그를 추가하고 스크롤을 최신 로그로 이동
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

        // ClientService_ConnectionChanged 이벤트 핸들러는 서버와의 연결 상태가 변경될 때 호출되며, ServerConnectionText에 연결 상태를 표시하고 색상을 변경
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

        // ClientService_MessageReceived 이벤트 핸들러는 서버로부터 메시지를 수신할 때 호출되며, PingRequest 메시지를 수신하면 PingResponse 메시지를 서버로 전송
        private async void ClientService_MessageReceived(NetworkMessage message)
        {
            if (message.Type == MessageType.DeviceListResponse)
            {
                Dispatcher.Invoke(() =>
                {
                    DeviceComboBox.ItemsSource = message.Devices;
                });

                return;
            }

            // 장비를 선택·등록하기 전에는 Ping이나 상태 응답을 보낼 수 없다.
            if (_currentDevice is null)
            {
                return;
            }

            if (message.Type == MessageType.PingResponse)
            {
                return;
            }

            if (message.Type == MessageType.PingRequest)
            {
                NetworkMessage response = new()
                {
                    Type = MessageType.PingResponse,
                    DeviceId = _currentDevice.DeviceId,
                    DeviceName = _currentDevice.DeviceName,
                    RequestId = message.RequestId,
                    SentAt = DateTime.Now
                };

                await _clientService.SendAsync(response);

                return;
            }

            if (message.Type == MessageType.SystemInfoRequest)
            {
                NetworkMessage response = new()
                {
                    Type = MessageType.SystemInfoResponse,
                    DeviceId = _currentDevice.DeviceId,
                    DeviceName = _currentDevice.DeviceName,
                    RequestId = message.RequestId,
                    SentAt = DateTime.Now,

                    Status = "정상",
                    Temperature = 32.4,
                    Voltage = 24.1,
                    Battery = 84
                };

                await _clientService.SendAsync(response);
            }
        }

        private async void RegisterButton_Click(object sender,RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem
                is not DeviceSummary selectedDevice)
            {
                LogListBox.Items.Add("장비를 선택하세요.");
                return;
            }

            _currentDevice = selectedDevice;

            await _clientService.RegisterAsync(
                _currentDevice.DeviceId,
                _currentDevice.DeviceName);

            DeviceNameText.Text =
                $"장비 이름: {_currentDevice.DeviceId}";

            DeviceComboBox.IsEnabled = false;
            RegisterButton.IsEnabled = false;
        }
    }
}