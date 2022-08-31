using CommonDataClient;
using PingClient.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Security.Permissions;
using System.Windows.Forms;

namespace PingClient
{
    public partial class MainForm : Form
    {
        readonly EventClient client = new EventClient();
        ClientConnectionStatus status = ClientConnectionStatus.Closed;
        DateTime pingservicetime = DateTime.Now;
        public bool masterExit;
        MemIniFile mif;
        MessageForm messageForm;

        public MainForm()
        {
            InitializeComponent();
        }

        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        private static Process RunningInstance()
        {
            var current = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(current.ProcessName);
            // Просматриваем все процессы
            return processes.Where(process => process.Id != current.Id).
                FirstOrDefault(process => Assembly.GetExecutingAssembly().
                    Location.Replace("/", "\\") == current.MainModule.FileName);
            // нет, таких процессов не найдено
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            #region Защита от повторного запуска
            var process = RunningInstance();
            if (process != null) { Application.Exit(); return; }
            #endregion
            notifyIcon.Visible = true;
            FormBorderStyle = FormBorderStyle.None;
            Size = new System.Drawing.Size(1, 0);
            mif = new MemIniFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PingClient.ini"));
            var commonDataServiceIp = mif.ReadString("CommonDataService", "IpAddress", "127.0.0.1");
            var commonDataServicePort = mif.ReadInteger("CommonDataService", "IpPort", 80);
            client.Connect(commonDataServiceIp, commonDataServicePort, new[] { "Ping", "ImLive" }, PropertyUpdate, ShowError, ClientFileReceived, UpdateLocalConnectionStatus);
            client.SubscribeValues();
        }

        private void UpdateLocalConnectionStatus(Guid clientId, string ipaddr, ClientConnectionStatus stat)
        {
            status = stat;
        }

        private void ShowError(string errormessage)
        {
            //Text = errormessage;
        }

        private void PropertyUpdate(DateTime servertime, string category, string pointname, string propname, string value)
        {
            var method = new MethodInvoker(() =>
            {
                switch (category)
                {
                    case "Ping":
                        if (mif.SectionExists(value))
                        {
                            var fileName = mif.ReadString(value, propname, "");
                            if (File.Exists(fileName))
                                PlayFile(fileName);
                        }
                        if (messageForm != null)
                        {
                            messageForm.Close();
                            messageForm.Dispose();
                            messageForm = null;
                        }
                        if (value == "TimedOut")
                        {
                            var message = mif.ReadString("TimedOutMessages", propname, $"{propname} {value}");
                            messageForm = new MessageForm();
                            messageForm.Show();
                            messageForm.labUserMessage.Text = message;
                            messageForm.BringToFront();
                        }
                        break;
                    case "ImLive":
                        break;
                }
            });
            if (InvokeRequired)
                BeginInvoke(method);
            else
                method();
        }

        private void timerPingServiceLiveTimeout_Tick(object sender, EventArgs e)
        {

        }

        private void ClientFileReceived(string tarfilename, int percent, bool complete)
        {
            //
        }

        private void UploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            masterExit = true;
            Close();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (masterExit)
            {
                e.Cancel = false;
                client.Disconnect();
            }
            else
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void tsmiShow_Click(object sender, EventArgs e)
        {
            if (Application.OpenForms.OfType<StatusForm>().Count() > 0)
                return;
            var statusForm = new StatusForm();
            statusForm.ShowDialog();
        }

        private void PlayFile(string url)
        {
            var Player = new WMPLib.WindowsMediaPlayer();
            Player.PlayStateChange += Player_PlayStateChange;
            Player.URL = url;
            Player.controls.play();
        }

        private void Player_PlayStateChange(int NewState)
        {
            if ((WMPLib.WMPPlayState)NewState == WMPLib.WMPPlayState.wmppsStopped)
            {
                //Actions on stop
            }
        }
    }
}
