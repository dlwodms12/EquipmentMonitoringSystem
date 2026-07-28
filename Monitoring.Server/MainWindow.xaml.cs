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
        // TcpServerService 클래스의 인스턴스를 생성하여 서버 기능을 제공. 이 클래스는 클라이언트 연결 수락, 메시지 수신 및 전송, 장비 등록 및 연결 해제 처리 등의 기능을 수행. MainWindow에서 서버와 관련된 이벤트를 처리하고 UI를 업데이트하는 데 사용됨
        private readonly TcpServerService _serverService = new();
        // Dictionary를 사용하여 장비 ID와 최신 시스템 정보를 관리. Dictionary는 키-값 쌍으로 데이터를 저장하며, 장비 ID를 키로 사용하여 최신 시스템 정보를 빠르게 조회 가능. 이를 통해 여러 장비의 상태를 효율적으로 관리하고 UI에 표시할 수 있음
        private readonly Dictionary<string, NetworkMessage> _latestSystemInfo = new();
        // 선택된 장비 ID를 저장하는 변수. 사용자가 DeviceListBox에서 장비를 선택하면 해당 장비의 ID가 이 변수에 저장되어 이후 명령 전송 시 사용됨. 선택된 장비가 없으면 null이 될 수 있음
        private string? _selectedDeviceId;
        // HashSet을 사용하여 현재 연결된 장비 ID를 관리. HashSet은 중복된 값을 허용하지 않으며, 장비 연결 상태를 빠르게 확인할 수 있음. 장비가 연결되면 ID를 추가하고, 연결이 종료되면 ID를 제거하여 현재 연결 상태를 효율적으로 추적 가능
        private readonly HashSet<string> _connectedDeviceIds = new();

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
                // 장비가 등록되면 연결된 장비 ID를 HashSet에 추가하여 현재 연결 상태를 업데이트
                _connectedDeviceIds.Add(message.DeviceId);

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
            // ListBox에서 선택된 항목을 가져온다.
            if (DeviceListBox.SelectedItem
                is not System.Windows.Controls.ListBoxItem selectedItem)
            {
                return;
            }

            // ListBoxItem의 Content에는 Device-001 같은 장비 ID가 들어 있다.
            string? deviceId = selectedItem.Content?.ToString();

            // 선택된 장비 ID가 null이거나 공백이면 아무 작업도 하지 않는다.
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            // 현재 선택 장비로 기억한다.
            _selectedDeviceId = deviceId;

            // 화면 상단의 선택 장비 텍스트를 변경한다.
            SelectedDeviceText.Text =
                $"현재 선택된 장비: {_selectedDeviceId}";

            // 연결 상태를 표시한다.
            bool isConnected =
                _connectedDeviceIds.Contains(_selectedDeviceId);

            ConnectionText.Text = isConnected
                ? "통신: Connected"
                : "통신: Disconnected";

            ConnectionText.Foreground = isConnected
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;

            // 이전에 이 장비로부터 받은 시스템 정보가 있다면 표시한다.
            if (_latestSystemInfo.TryGetValue(
                _selectedDeviceId,
                out NetworkMessage? systemInfo))
            {
                DisplaySystemInfo(systemInfo);
            }
            else
            {
                // 아직 Show System을 요청하지 않은 장비라면 빈 상태로 표시한다.
                StatusText.Text = "상태: 정보 없음";
                TemperatureText.Text = "온도: -";
                VoltageText.Text = "전압: -";
                BatteryText.Text = "배터리: -";
            }
        }

        //  ServerService_MessageReceived 이벤트 핸들러는 서버로부터 메시지를 수신할 때 호출되며, PingRequest 메시지를 수신하면 선택된 장비와 일치하는 경우 ConnectionText를 "통신: Connected"로 업데이트
        private void ServerService_MessageReceived(NetworkMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                if (message.Type == MessageType.PingRequest ||
                    message.Type == MessageType.PingResponse)
                {
                    if (_selectedDeviceId == message.DeviceId)
                    {
                        ConnectionText.Text = "통신: Connected";
                    }

                    return;
                }

                if (message.Type == MessageType.SystemInfoResponse)
                {
                    _latestSystemInfo[message.DeviceId] = message;

                    if (_selectedDeviceId == message.DeviceId)
                    {
                        DisplaySystemInfo(message);
                    }
                }
            });
        }

        // ServerService_DeviceDisconnected 이벤트 핸들러는 장비 연결이 종료될 때 호출되며, DeviceListBox에서 해당 장비 ID를 찾아 글자색을 빨간색으로 변경하고, 선택된 장비가 연결 종료된 경우 ConnectionText를 "통신: Disconnected"로 업데이트. 또한 로그에 연결 종료 메시지를 추가
        private void ServerService_DeviceDisconnected(string deviceId)
        {
            Dispatcher.Invoke(() =>
            {
                // 연결 종료된 장비 ID를 HashSet에서 제거하여 현재 연결 상태를 업데이트
                _connectedDeviceIds.Remove(deviceId);

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
        // DisplaySystemInfo 메서드는 선택된 장비의 시스템 정보를 UI에 표시하며, 상태, 온도, 전압, 배터리 잔량, 통신 상태를 업데이트
        private void DisplaySystemInfo(NetworkMessage message)
        {
            StatusText.Text = $"상태: {message.Status}";
            TemperatureText.Text = $"온도: {message.Temperature:F1} °C";
            VoltageText.Text = $"전압: {message.Voltage:F1} V";
            BatteryText.Text = $"배터리: {message.Battery}%";
            ConnectionText.Text = "통신: Connected";
        }
    }
}