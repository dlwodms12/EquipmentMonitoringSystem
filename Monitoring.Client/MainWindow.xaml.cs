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
    // partal : MainWindow 클래스의 정의가 여러 파일에 걸쳐 있을 수 있음을 나타냄
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

        // MainWindow_Loaded 이벤트 핸들러는 MainWindow가 로드될 때 호출되며, 장비 이름을 표시하고 서버에 연결을 시도
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DeviceNameText.Text = $"장비 이름: {DeviceId}";

            await _clientService.ConnectAsync(
                ServerIp,
                ServerPort,
                DeviceId,
                DeviceName);
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