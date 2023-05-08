//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Telegram.Stub
{
    class BridgeApplicationContext : ApplicationContext
    {
        private AppServiceConnection _connection = null;
        private NotifyIcon _notifyIcon = null;

        //private InterceptKeys _intercept;

        public BridgeApplicationContext()
        {
            //_intercept = new InterceptKeys();

            MenuItem openMenuItem = new MenuItem("Open Unigram", new EventHandler(OpenApp));
            MenuItem exitMenuItem = new MenuItem("Quit Unigram", new EventHandler(Exit));
            openMenuItem.DefaultItem = true;

            _notifyIcon = new NotifyIcon();
            _notifyIcon.DoubleClick += new EventHandler(OpenApp);
            _notifyIcon.Icon = Properties.Resources.Default;
            _notifyIcon.ContextMenu = new ContextMenu(new MenuItem[] { openMenuItem, exitMenuItem });
#if DEBUG
            _notifyIcon.Text = "Telegram";
#else
            _notifyIcon.Text = "Unigram";
#endif

            _notifyIcon.Visible = true;

            try
            {
                var local = ApplicationData.Current.LocalSettings;
                if (local.Values.TryGetValue("IsLaunchMinimized", out object minimizedV) && minimizedV is bool minimized && !minimized)
                {
                    OpenApp(null, null);
                }
                else
                {
                    Connect();
                }
            }
            catch
            {
                _notifyIcon.Visible = true;
            }
        }

        private async void OpenApp(object sender, EventArgs e)
        {
            try
            {
                var appListEntries = await Package.Current.GetAppListEntriesAsync();
                await appListEntries.First().LaunchAsync();
            }
            catch { }

            Connect();
        }

        private async void Exit(object sender, EventArgs e)
        {
            if (_connection != null)
            {
                try
                {
                    _connection.ServiceClosed -= Connection_ServiceClosed;
                    await _connection.SendMessageAsync(new ValueSet { { "Exit", string.Empty } });
                }
                catch { }
            }

            _notifyIcon.Dispose();
            Application.Exit();
        }

        private async void Connect()
        {
            if (_connection != null)
            {
                return;
            }

            _connection = new AppServiceConnection
            {
                PackageFamilyName = Package.Current.Id.FamilyName,
                AppServiceName = "org.telegram.bridge"
            };

            _connection.RequestReceived += Connection_RequestReceived;
            _connection.ServiceClosed += Connection_ServiceClosed;

            var connectionStatus = await _connection.OpenAsync();
            if (connectionStatus != AppServiceConnectionStatus.Success)
            {
                //MessageBox.Show("Status: " + connectionStatus.ToString());
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public FlashWindow dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        public enum FlashWindow : uint
        {
            /// <summary>
            /// Stop flashing. The system restores the window to its original state.
            /// </summary>    
            FLASHW_STOP = 0,

            /// <summary>
            /// Flash the window caption
            /// </summary>
            FLASHW_CAPTION = 1,

            /// <summary>
            /// Flash the taskbar button.
            /// </summary>
            FLASHW_TRAY = 2,

            /// <summary>
            /// Flash both the window caption and taskbar button.
            /// This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
            /// </summary>
            FLASHW_ALL = 3,

            /// <summary>
            /// Flash continuously, until the FLASHW_STOP flag is set.
            /// </summary>
            FLASHW_TIMER = 4,

            /// <summary>
            /// Flash continuously until the window comes to the foreground.
            /// </summary>
            FLASHW_TIMERNOFG = 12
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.TryGetValue("FlashWindow", out object flash))
            {
                //#if DEBUG
                //                var handle = FindWindow("ApplicationFrameWindow", "Telegram");
                //#else
                //                var handle = FindWindow("ApplicationFrameWindow", "Unigram");
                //#endif

                //                FLASHWINFO info = new FLASHWINFO();
                //                info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
                //                info.hwnd = handle;
                //                info.dwFlags = FlashWindow.FLASHW_ALL;
                //                info.dwTimeout = 0;
                //                info.uCount = 1;
                //                FlashWindowEx(ref info);
            }
            else if (args.Request.Message.TryGetValue("UnreadCount", out object unread) && args.Request.Message.TryGetValue("UnreadUnmutedCount", out object unreadUnmuted))
            {
                if (unread is int unreadCount && unreadUnmuted is int unreadUnmutedCount)
                {
                    if (unreadCount > 0 || unreadUnmutedCount > 0)
                    {
                        _notifyIcon.Icon = unreadUnmutedCount > 0 ? Properties.Resources.Unmuted : Properties.Resources.Muted;
                    }
                    else
                    {
                        _notifyIcon.Icon = Properties.Resources.Default;
                    }
                }
            }
            else if (args.Request.Message.TryGetValue("LoopbackExempt", out object loopbackExempt))
            {
                AddLocalhostExemption();
            }
            else if (args.Request.Message.TryGetValue("Exit", out object exit))
            {
                _connection.ServiceClosed -= Connection_ServiceClosed;
                _connection.Dispose();

                _notifyIcon.Dispose();
                Application.Exit();
            }
        }

        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            _connection.ServiceClosed -= Connection_ServiceClosed;
            _connection.Dispose();
            _connection = null;

            //Application.Exit();
            Connect();

            //MessageBox.Show(args.Status.ToString());
        }

        private static void AddLocalhostExemption()
        {
            var familyName = Package.Current.Id.FamilyName;
            var info = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = "CheckNetIsolation.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "LoopbackExempt -a -n=" + familyName
            };

            try
            {
                Process process = Process.Start(info);
                process.WaitForExit();
                process.Dispose();
            }
            catch { }
        }
    }
}
