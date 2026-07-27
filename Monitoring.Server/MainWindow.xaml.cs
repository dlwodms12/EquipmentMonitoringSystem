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

namespace Monitoring.Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? _selectedDevice;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void DeviceListBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DeviceListBox.SelectedItem is System.Windows.Controls.ListBoxItem item)
            {
                _selectedDevice = item.Content?.ToString();

                SelectedDeviceText.Text = $"현재 선택된 장비: {_selectedDevice}";
                StatusText.Text = "상태: 정상";
                TemperatureText.Text = "온도: 32.4 °C";
                VoltageText.Text = "전압: 24.1 V";
                BatteryText.Text = "배터리: 84%";
                ConnectionText.Text = "통신: Connected";
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedDevice))
            {
                LogListBox.Items.Add("장비를 먼저 선택하세요.");
                return;
            }

            if (PingRadioButton.IsChecked == true)
            {
                LogListBox.Items.Add(
                    $"{DateTime.Now:HH:mm:ss} TX {_selectedDevice} PING_REQUEST");
            }
            else if (SystemInfoRadioButton.IsChecked == true)
            {
                LogListBox.Items.Add(
                    $"{DateTime.Now:HH:mm:ss} TX {_selectedDevice} SYSTEM_INFO_REQUEST");
            }
            else
            {
                LogListBox.Items.Add("전송할 명령을 선택하세요.");
            }
        }
    }
}