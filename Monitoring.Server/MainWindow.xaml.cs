using System.Windows;
using Monitoring.Server.Services;
using Monitoring.Shared.Models;
using Monitoring.Server.Models;

namespace Monitoring.Server
{
    public partial class MainWindow : Window
    {

        // DeviceDatabaseService 클래스의 인스턴스를 생성하여 장비 데이터베이스와 관련된 기능을 제공.
        // 이 클래스는 장비 등록, 조회, 삭제 등의 기능을 수행하며, MainWindow에서 장비 데이터베이스와 상호작용할 때 사용됨. 현재 코드에서는 사용되지 않지만, 향후 장비 데이터베이스 기능을 확장할 때 활용 가능
        private readonly DeviceDatabaseService _deviceDatabase = new();

        // TcpServerService 클래스의 인스턴스를 생성하여 서버 기능을 제공.
        // 이 클래스는 클라이언트 연결 수락, 메시지 수신 및 전송, 장비 등록 및 연결 해제 처리 등의 기능을 수행. MainWindow에서 서버와 관련된 이벤트를 처리하고 UI를 업데이트하는 데 사용됨
        private readonly TcpServerService _serverService;

        // Dictionary를 사용하여 장비 ID와 최신 시스템 정보를 관리. Dictionary는 키-값 쌍으로 데이터를 저장하며, 장비 ID를 키로 사용하여 최신 시스템 정보를 빠르게 조회 가능
        private readonly Dictionary<string, NetworkMessage> _latestSystemInfo = new();

        // 선택된 장비 ID를 저장하는 변수. 사용자가 DeviceListBox에서 장비를 선택하면 해당 장비의 ID가 이 변수에 저장되어 이후 명령 전송 시 사용됨. 선택된 장비가 없으면 null이 될 수 있음
        private string? _selectedDeviceId;

        // HashSet을 사용하여 현재 연결된 장비 ID를 관리. HashSet은 중복된 값을 허용하지 않으며, 장비 연결 상태를 빠르게 확인할 수 있음. 장비가 연결되면 ID를 추가하고, 연결이 종료되면 ID를 제거하여 현재 연결 상태를 효율적으로 추적 가능
        private readonly HashSet<string> _connectedDeviceIds = new();
        

        public MainWindow()
        {
            InitializeComponent();

            _serverService = new TcpServerService(_deviceDatabase.GetDeviceSummariesAsync);
            _serverService.LogReceived += ServerService_LogReceived;
            _serverService.DeviceRegistered += ServerService_DeviceRegistered;
            _serverService.MessageReceived += ServerService_MessageReceived;
            _serverService.DeviceDisconnected += ServerService_DeviceDisconnected;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        // MainWindow_Loaded 이벤트 핸들러는 MainWindow가 로드될 때 호출되며, 장비 데이터베이스를 초기화하고 서버를 시작. 서버는 5000 포트에서 클라이언트 연결을 수락하도록 설정되어 있음.
        // 이 메서드는 비동기적으로 실행되며, 장비 데이터베이스 초기화와 서버 시작이 완료될 때까지 기다림. 이를 통해 서버가 준비된 상태에서 클라이언트 연결을 처리할 수 있도록 보장
        private async void MainWindow_Loaded(object sender,RoutedEventArgs e)
        {
            await _deviceDatabase.InitializeAsync();

            await _deviceDatabase.ResetConnectionStatesAsync();

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
        private async void ServerService_DeviceRegistered(NetworkMessage message)
        {
            // 장비가 등록되면 SQLite DB에서 해당 장비의 연결 상태를 true로 업데이트.
            // 이를 통해 장비가 연결된 상태임을 데이터베이스에 반영.
            // 이 메서드는 비동기적으로 실행되며, DB 업데이트가 완료될 때까지 기다림
            await _deviceDatabase.UpdateConnectionAsync(message.DeviceId,true);

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

                // 장비가 등록되면 DeviceListBox에서 해당 장비 ID를 찾아 글자색을 검은색으로 변경하여 연결 상태를 시각적으로 표시.
                // 이미 존재하는 경우에도 글자색을 검은색으로 변경하여 연결 상태를 명확히 함
                System.Windows.Controls.ListBoxItem? deviceItem =
                DeviceListBox.Items
                .OfType<System.Windows.Controls.ListBoxItem>()
                .FirstOrDefault(x =>
                x.Content?.ToString() == message.DeviceId);

                if (deviceItem is not null)
                {
                    deviceItem.Foreground =
                        System.Windows.Media.Brushes.Black;
                }
            });
        }

        // DeviceListBox_SelectionChanged 이벤트 핸들러는 사용자가 DeviceListBox에서 장비를 선택할 때 호출되며, 선택된 장비의 정보를 SQLite DB에서 읽어와 화면에 표시.
        // 선택된 장비가 없으면 아무 작업도 수행하지 않음. 또한 선택된 장비의 ID를 _selectedDeviceId에 저장하여 이후 명령 전송 시 사용 가능. 이 메서드는 비동기적으로 실행되며, DB 조회가 완료될 때까지 기다림
        private async void DeviceListBox_SelectionChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DeviceListBox.SelectedItem
                is not System.Windows.Controls.ListBoxItem selectedItem)
            {
                return;
            }

            string? deviceId = selectedItem.Content?.ToString();

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            _selectedDeviceId = deviceId;

            SelectedDeviceText.Text =
                $"현재 선택된 장비: {_selectedDeviceId}";

            // SQLite DB에서 선택 장비의 마지막 저장 상태를 읽는다.
            DeviceRecord? device =
                await _deviceDatabase.GetDeviceAsync(deviceId);

            if (device is null)
            {
                StatusText.Text = "상태: 장비 정보를 찾을 수 없습니다.";
                return;
            }

            // DB를 읽는 동안 다른 장비를 클릭했을 수 있으므로 확인
            if (_selectedDeviceId != deviceId)
            {
                return;
            }

            DisplayDeviceRecord(device);
        }

        //  ServerService_MessageReceived 이벤트 핸들러는 서버로부터 메시지를 수신할 때 호출되며, PingRequest 메시지를 수신하면 선택된 장비와 일치하는 경우 ConnectionText를 "통신: Connected"로 업데이트
        private async void ServerService_MessageReceived(NetworkMessage message)
        {
            if (message.Type == MessageType.PingRequest ||
                message.Type == MessageType.PingResponse)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_selectedDeviceId == message.DeviceId)
                    {
                        DisplayConnectionState(true);
                    }
                });

                return;
            }

            if (message.Type == MessageType.SystemInfoResponse)
            {
                // SQLite DB에 수신한 시스템 정보를 업데이트. 이 메서드는 비동기적으로 실행되며, DB 업데이트가 완료될 때까지 기다림.
                // 이를 통해 장비의 최신 시스템 정보를 데이터베이스에 반영
                await _deviceDatabase.UpdateSystemInfoAsync(message);

                // UI 스레드에서 최신 시스템 정보를 Dictionary에 저장하고, 선택된 장비와 일치하면 화면에 표시.
                // Dispatcher.Invoke를 사용하여 UI 스레드에서 안전하게 UI 요소를 업데이트 가능.
                Dispatcher.Invoke(() =>
                {
                    _latestSystemInfo[message.DeviceId] = message;

                    if (_selectedDeviceId == message.DeviceId)
                    {
                        DisplaySystemInfo(message);
                    }
                });
            }
        }

        // ServerService_DeviceDisconnected 이벤트 핸들러는 장비 연결이 종료될 때 호출되며, DeviceListBox에서 해당 장비 ID를 찾아 글자색을 빨간색으로 변경하고, 선택된 장비가 연결 종료된 경우 ConnectionText를 "통신: Disconnected"로 업데이트. 또한 로그에 연결 종료 메시지를 추가
        private async void ServerService_DeviceDisconnected(string deviceId)
        {
            // 장비 연결이 종료되면 SQLite DB에서 해당 장비의 연결 상태를 false로 업데이트.
            await _deviceDatabase.UpdateConnectionAsync(deviceId,false);

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
                    DisplayConnectionState(false);
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
        }

        // DisplayConnectionState 메서드는 선택된 장비의 연결 상태를 UI에 표시하며, 연결 상태에 따라 텍스트와 글자색을 업데이트.
        // 연결되어 있으면 "통신: Connected"를 녹색으로 표시하고, 연결이 끊어져 있으면 "통신: Disconnected"를 빨간색으로 표시
        private void DisplayConnectionState(bool isConnected)
        {
            ConnectionText.Text = isConnected
                ? "통신: Connected"
                : "통신: Disconnected";

            ConnectionText.Foreground = isConnected
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;
        }

        // DisplayDeviceRecord 메서드는 선택된 장비의 DeviceRecord 정보를 UI에 표시하며, 상태, 온도, 전압, 배터리 잔량, 통신 상태를 업데이트.
        // 이 메서드는 DeviceRecord 객체를 매개변수로 받아 해당 장비의 정보를 화면에 출력.
        // 연결 상태에 따라 텍스트와 글자색을 업데이트하여 사용자가 장비 상태를 쉽게 확인할 수 있도록 함
        private void DisplayDeviceRecord(DeviceRecord device)
        {
            StatusText.Text = $"상태: {device.Status}";
            TemperatureText.Text =
                $"온도: {device.Temperature:F1} °C";
            VoltageText.Text =
                $"전압: {device.Voltage:F1} V";
            BatteryText.Text =
                $"배터리: {device.Battery}%";

            DisplayConnectionState(device.IsConnected);
        }
    }
}