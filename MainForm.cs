using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NoNetApp
{
    public class MainForm : Form
    {
        private ListView devicesListView;
        private Button btnCutAll;
        private Button btnRefresh;
        private Label lblStatus;
        private List<Device> currentDevices = new List<Device>();
        private Timer refreshTimer;

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
            BindEvents();
            StartAutoRefresh();
        }

        private void InitializeComponent()
        {
            this.Text = "No Net - Network Monitor";
            this.Size = new System.Drawing.Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
        }

        private void SetupUI()
        {
            btnCutAll = new Button();
            btnCutAll.Text = "قطع الإنترنت عن الجميع (Kill All)";
            btnCutAll.Location = new Point(10, 10);
            btnCutAll.Size = new Size(220, 40);
            btnCutAll.Font = new Font("Arial", 10, FontStyle.Bold);
            btnCutAll.BackColor = Color.FromArgb(200, 50, 50);
            btnCutAll.ForeColor = Color.White;
            btnCutAll.FlatStyle = FlatStyle.Flat;
            this.Controls.Add(btnCutAll);

            btnRefresh = new Button();
            btnRefresh.Text = "تحديث القائمة";
            btnRefresh.Location = new Point(240, 10);
            btnRefresh.Size = new Size(120, 40);
            btnRefresh.Font = new Font("Arial", 10);
            btnRefresh.BackColor = Color.FromArgb(50, 100, 200);
            btnRefresh.ForeColor = Color.White;
            btnRefresh.FlatStyle = FlatStyle.Flat;
            this.Controls.Add(btnRefresh);

            lblStatus = new Label();
            lblStatus.Text = "جاري المسح...";
            lblStatus.Location = new Point(380, 22);
            lblStatus.Font = new Font("Arial", 10, FontStyle.Regular);
            lblStatus.AutoSize = true;
            this.Controls.Add(lblStatus);

            devicesListView = new ListView();
            devicesListView.Location = new Point(10, 60);
            devicesListView.Size = new Size(860, 490);
            devicesListView.View = View.Details;
            devicesListView.FullRowSelect = true;
            devicesListView.GridLines = true;
            devicesListView.BackColor = Color.FromArgb(40, 40, 40);
            devicesListView.ForeColor = Color.LightGray;

            devicesListView.Columns.Add("IP Address", 150);
            devicesListView.Columns.Add("MAC Address", 150);
            devicesListView.Columns.Add("Device Name / Vendor", 250);
            devicesListView.Columns.Add("Status / Actions", 280);

            this.Controls.Add(devicesListView);
        }

        private void BindEvents()
        {
            btnCutAll.Click += BtnCutAll_Click;
            btnRefresh.Click += BtnRefresh_Click;
        }

        private void StartAutoRefresh()
        {
            refreshTimer = new Timer();
            refreshTimer.Interval = 15000;
            refreshTimer.Tick += (s, e) => _ = ScanNetworkAsync();
            refreshTimer.Start();
            _ = ScanNetworkAsync();
        }

        private async void BtnRefresh_Click(object sender, EventArgs e)
        {
            await ScanNetworkAsync();
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("127."))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch {}
            return "192.168.1.100";
        }

        private async Task PingAndDetect(string ipAddress)
        {
            try
            {
                using (Ping pingSender = new Ping())
                {
                    PingOptions options = new PingOptions(64, true);
                    byte[] buffer = new byte[32];
                    int timeout = 500;
                    PingReply reply = await pingSender.SendPingAsync(ipAddress, timeout, buffer, options);

                    if (reply.Status == IPStatus.Success)
                    {
                        lock (currentDevices)
                        {
                            if (!currentDevices.Any(d => d.IpAddress == ipAddress))
                            {
                                currentDevices.Add(new Device
                                {
                                    IpAddress = ipAddress,
                                    MacAddress = "00:1A:2B:3C:4D:5E",
                                    Name = $"Device-{ipAddress.Split('.').Last()}"
                                });
                            }
                        }
                    }
                }
            }
            catch {}
        }

        private async Task ScanNetworkAsync()
        {
            lblStatus.Text = "جاري المسح...";
            
            try
            {
                string myIp = GetLocalIpAddress();
                if (string.IsNullOrEmpty(myIp)) return;

                string[] parts = myIp.Split('.');
                string baseSubnet = $"{parts[0]}.{parts[1]}.{parts[2]}.";

                List<Task> tasks = new List<Task>();
                for (int i = 1; i <= 254; i++)
                {
                    string ipToPing = baseSubnet + i;
                    tasks.Add(PingAndDetect(ipToPing));
                }

                await Task.WhenAll(tasks);

                if (this.IsDisposed || !this.IsHandleCreated) return;

                this.Invoke(new Action(() =>
                {
                    devicesListView.Items.Clear();
                    foreach (var device in currentDevices)
                    {
                        ListViewItem item = new ListViewItem(device.IpAddress);
                        item.SubItems.Add(device.MacAddress);
                        item.SubItems.Add(device.Name);
                        item.SubItems.Add(device.IsBlocked ? "مقطوع (Blocked)" : "متصل (Connected)");
                        
                        devicesListView.Items.Add(item);
                    }
                    lblStatus.Text = $"تم العثور على {currentDevices.Count} جهاز متصل حالياً";
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCutAll_Click(object sender, EventArgs e)
        {
            foreach (var device in currentDevices)
            {
                device.IsBlocked = true;
                KillDevice(device);
            }
            MessageBox.Show("تمت محاكاة حظر جميع الأجهزة بنجاح!");
            _ = ScanNetworkAsync();
        }

        private void KillDevice(Device device)
        {
            device.IsBlocked = true;
        }
    }
}