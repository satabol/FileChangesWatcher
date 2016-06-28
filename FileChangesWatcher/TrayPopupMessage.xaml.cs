using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FileChangesWatcher
{
    /// <summary>
    /// Interaction logic for TrayPopup_test.xaml
    /// </summary>
    public partial class TrayPopupMessage : UserControl
    {
        public string path = null;
        public WatchingObjectType wType;
        public TaskbarIcon tb = null;
        private System.Timers.Timer temp = null;

        public enum ControlButtons
        {
            Clipboard=1, Run=2, Close=4
        };

        private void init(string _path, WatchingObjectType _wType, TaskbarIcon _tb, ControlButtons _buttons)
        {
            tb = _tb;

            this.path = _path;
            this.text_message.Text = _path;
            this.wType = _wType;

            temp = new System.Timers.Timer();
            temp.Interval = 2000;
            temp.Elapsed += new System.Timers.ElapsedEventHandler(customballoon_close);

            Button btn_copy_clipboard = ((Button)this.FindName("btn_copy_clipboard"));
            btn_copy_clipboard.Click += (sender, args) =>
            {
                tb.CustomBalloon.IsOpen = false;
                App.copy_clipboard_with_popup(_path);
            };
            if ((_buttons & ControlButtons.Clipboard) == 0)
            {
                btn_copy_clipboard.Visibility = Visibility.Hidden;
            }
            Button btn_execute_file = ((Button)this.FindName("btn_execute_file"));
            btn_execute_file.Click += (sender, args) =>
            {
                tb.CustomBalloon.IsOpen = false;
                Process.Start(_path);
            };
            if ((_buttons & ControlButtons.Run) == 0)
            {
                btn_execute_file.Visibility = Visibility.Hidden;
            }


            this.MouseEnter += (sender, args) =>
            {
                tb.ResetBalloonCloseTimer();
                //this.Visibility = Visibility.Hidden;
                if (temp.Enabled)
                {
                    temp.Stop();
                }
            };
            this.MouseLeave += (sender, args) =>
            {
                temp.Start();
            };
            /*
            this.MouseDown += (sender, args) =>
            {
                tb.CustomBalloon.IsOpen = false;
                //this.Visibility = Visibility.Hidden;
                App.gotoPathByWindowsExplorer(path, wType);
            };
            //*/
            this.ToolTipClosing += (sender, args) =>
            {
                temp.Stop();
            };
        }

        public TrayPopupMessage(string _path, WatchingObjectType _wType, TaskbarIcon _tb, ControlButtons _buttons)
        {
            InitializeComponent();
            init(_path, _wType, _tb, _buttons);
        }
        public TrayPopupMessage(string _path, string _title, WatchingObjectType _wType, TaskbarIcon _tb, ControlButtons _buttons)
        {
            InitializeComponent();
            this.title.Text = _title;
            init(_path, _wType, _tb, _buttons);
        }


        public void customballoon_close(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                // popup_test.Visibility = Visibility.Hidden;
                //this.Visibility = Visibility.Hidden;
                tb.CustomBalloon.IsOpen = false;
            });
            System.Timers.Timer temp = ((System.Timers.Timer)sender);
            temp.Stop();
        }

        private void btn_close_window_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }
    }
}
