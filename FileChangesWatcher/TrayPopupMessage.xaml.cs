using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FileChangesWatcher
{
    /// <summary>
    /// Interaction logic for TrayPopup_test.xaml
    /// </summary>
    public partial class TrayPopupMessage : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string propertyName) {
            // Если кто-то на него подписан, то вызывем его
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }



        public string path = null;
        public WatchingObjectType wType;
        public TaskbarIcon tb = null;
        private System.Timers.Timer timeout_Timer = null;
        private bool _IsDragging;

        public bool IsDragging {
            get {
                return _IsDragging;
            }
            set {
                _IsDragging = value;
            }
        }

        //public Image item_image = null;

        public void StopTimeoutTimer() {
            Debug.WriteLine("StopTimeoutTimer");
            tb.ResetBalloonCloseTimer();
            timeout_Timer.Stop();
        }

        public void RestartTimeoutTimer() {
            tb.ResetBalloonCloseTimer();
            timeout_Timer.Enabled = false;
            timeout_Timer.Stop();
            timeout_Timer.Enabled = true;
            timeout_Timer.Start();

        }

        public enum ControlButtons
        {
            None=0, Clipboard_Text=1, Clipboard_File=2, File_Run=3, File_Delete=4, Popup_Close=5
        };

        public string TextMessage {
            get {
                return text_message.Text;
            }
            set {
                text_message.Text = value;
                timeout_Timer.Stop();
                timeout_Timer.Start();
            }
        }

        private System.Windows.Visibility _Button_Copy_File_To_Clipboard_Visibility;

        public System.Windows.Visibility Button_Copy_File_To_Clipboard_Visibility {
            get {
                return _Button_Copy_File_To_Clipboard_Visibility;
            }
            set {
                _Button_Copy_File_To_Clipboard_Visibility = value;
                RaisePropertyChanged(nameof(Button_Copy_File_To_Clipboard_Visibility));
            }
        }

        private System.Windows.Visibility _Button_Move_File_To_Clipboard_Visibility;

        public System.Windows.Visibility Button_Move_File_To_Clipboard_Visibility {
            get {
                return _Button_Move_File_To_Clipboard_Visibility;
            }
            set {
                _Button_Move_File_To_Clipboard_Visibility = value;
                RaisePropertyChanged(nameof(Button_Move_File_To_Clipboard_Visibility));
            }
        }

        private System.Windows.Visibility _Button_Delete_Visibility;

        public System.Windows.Visibility Button_Delete_Visibility {
            get {
                return _Button_Delete_Visibility;
            }
            set {
                _Button_Delete_Visibility = value;
                RaisePropertyChanged(nameof(Button_Delete_Visibility));
            }
        }

        private System.Windows.Visibility _Btn_Copy_Path_To_Clipboard_Visibility;

        public System.Windows.Visibility Btn_Copy_Path_To_Clipboard_Visibility {
            get {
                return _Btn_Copy_Path_To_Clipboard_Visibility;
            }
            set {
                _Btn_Copy_Path_To_Clipboard_Visibility = value;
                RaisePropertyChanged(nameof(Btn_Copy_Path_To_Clipboard_Visibility));
            }
        }


        private bool _Button_Copy_To_Clipboard_Enabled;

        public bool Button_Copy_To_Clipboard_Enabled {
            get {
                return _Button_Copy_To_Clipboard_Enabled;
            }
            set {
                _Button_Copy_To_Clipboard_Enabled = value;
                RaisePropertyChanged(nameof(Button_Copy_To_Clipboard_Enabled));
            }
        }

        private bool _Button_Copy_File_To_Clipboard;

        public bool Button_Copy_File_To_Clipboard {
            get {
                return _Button_Copy_File_To_Clipboard;
            }
            set {
                _Button_Copy_File_To_Clipboard = value;
                RaisePropertyChanged(nameof(Button_Copy_File_To_Clipboard));
            }
        }

        private bool _Button_Move_File_To_Clipboard;

        public bool Button_Move_File_To_Clipboard {
            get {
                return _Button_Move_File_To_Clipboard;
            }
            set {
                _Button_Move_File_To_Clipboard = value;
                RaisePropertyChanged(nameof(Button_Move_File_To_Clipboard));
            }
        }


        private bool _Button_Execute_Enabled;

        public bool Button_Execute_Enabled {
            get {
                return _Button_Execute_Enabled;
            }
            set {
                _Button_Execute_Enabled = value;
                RaisePropertyChanged(nameof(Button_Execute_Enabled));
            }
        }

        private bool _Button_Delete_Enabled;

        public bool Button_Delete_Enabled {
            get {
                return _Button_Delete_Enabled;
            }
            set {
                _Button_Delete_Enabled = value;
                RaisePropertyChanged(nameof(Button_Delete_Enabled));
            }
        }

        private string _Button_Copy_Path_To_Clipboard_Color;

        public string Button_Copy_Path_To_Clipboard_Color {
            get {
                return _Button_Copy_Path_To_Clipboard_Color;
            }
            set {
                _Button_Copy_Path_To_Clipboard_Color = value;
                RaisePropertyChanged(nameof(Button_Copy_Path_To_Clipboard_Color));
            }
        }

        private string _Button_Copy_File_To_Clipboard_Color;

        public string Button_Copy_File_To_Clipboard_Color {
            get {
                return _Button_Copy_File_To_Clipboard_Color;
            }
            set {
                _Button_Copy_File_To_Clipboard_Color = value;
                RaisePropertyChanged(nameof(Button_Copy_File_To_Clipboard_Color));
            }
        }

        private string _Button_Move_File_To_Clipboard_Color;

        public string Button_Move_File_To_Clipboard_Color {
            get {
                return _Button_Move_File_To_Clipboard_Color;
            }
            set {
                _Button_Move_File_To_Clipboard_Color = value;
                RaisePropertyChanged(nameof(Button_Move_File_To_Clipboard_Color));
            }
        }




        private void init(string _path, WatchingObjectType _wType, TaskbarIcon _tb, Image _item_image, ControlButtons _buttons, Type _type)
        {
            tb = _tb;
            item_image = _item_image;
            Image img = ((Image)this.FindName("item_image"));
            if (_item_image != null) {
                img.Source = _item_image.Source;
            }

            this.path = _path;
            this.text_message.Text = _path;
            this.wType = _wType;

            timeout_Timer = new System.Timers.Timer();
            timeout_Timer.Interval = 2000;
            timeout_Timer.Elapsed += new System.Timers.ElapsedEventHandler(customballoon_close);

            Button_Copy_Path_To_Clipboard_Color = "Black";
            Button_Copy_File_To_Clipboard_Color = "Black";
            Button_Move_File_To_Clipboard_Color = "Black";


            //Button btn_copy_clipboard = ((Button)this.FindName("btn_copy_clipboard"));
            //btn_copy_clipboard.Click += (sender, args) =>
            //{
            //    if (tb.CustomBalloon != null) {
            //        tb.CustomBalloon.IsOpen = false;
            //    }
            //    App.copy_clipboard_with_popup(_path);
            //};

            Button_Copy_To_Clipboard_Enabled = true;
            Btn_Copy_Path_To_Clipboard_Visibility = Visibility.Visible;
            if ((_buttons & ControlButtons.Clipboard_Text) == 0)
            {
                //btn_copy_clipboard.Visibility = Visibility.Hidden;
                Button_Copy_To_Clipboard_Enabled = false;
                Btn_Copy_Path_To_Clipboard_Visibility = Visibility.Hidden;
            }

            Button_Copy_File_To_Clipboard_Visibility = Visibility.Visible;
            if ((_buttons & ControlButtons.Clipboard_File) == 0)
            {
                Button_Copy_File_To_Clipboard_Visibility = Visibility.Hidden;
            }

            Button_Move_File_To_Clipboard_Visibility = Visibility.Visible;
            if ((_buttons & ControlButtons.Clipboard_File) == 0)
            {
                Button_Move_File_To_Clipboard_Visibility = Visibility.Hidden;
            }
            //Button btn_execute_file = ((Button)this.FindName("btn_execute_file"));
            //btn_execute_file.Click += (sender, args) =>
            //{
            //    if (tb.CustomBalloon != null) {
            //        tb.CustomBalloon.IsOpen = false;
            //    }
            //    if(File.Exists(_path)==true) {
            //        Process.Start(_path);
            //    } else {
            //        App.ShowMessage($"File {_path} do not exists.");
            //    }
            //};
            Button_Execute_Enabled = true;
            if ((_buttons & ControlButtons.File_Run) == 0)
            {
                //btn_execute_file.Visibility = Visibility.Hidden;
                Button_Execute_Enabled = false;
            }

            Button_Delete_Enabled = true;
            Button_Delete_Visibility= Visibility.Visible;
            if ((_buttons & ControlButtons.File_Delete) == 0){
                //btn_execute_file.Visibility = Visibility.Hidden;
                Button_Delete_Visibility = Visibility.Hidden;
                Button_Delete_Enabled = false;
            }



            this.MouseEnter += (sender, args) =>
            {
                tb.ResetBalloonCloseTimer();
                this.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x04, 0x7A, 0x95) );
                //this.Visibility = Visibility.Hidden;
                if (timeout_Timer.Enabled){
                    timeout_Timer.Stop();
                }
            };
            this.MouseLeave += (sender, args) =>
            {
                this.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0xAB, 0xD1));
                timeout_Timer.Start();
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
                timeout_Timer.Stop();
                //Console.WriteLine("Tooltip Closing");
            };

            if(_type==Type.Text) {
                this.MouseDown += (sender, args) => {
                    if(App.NotifyIcon.CustomBalloon != null) {
                        App.NotifyIcon.CustomBalloon.IsOpen = false;
                    }
                    //App.ShowMessage("Initial settings.\n" + init_text_message.ToString());
                    App.ShowMessage(_path);
                };
            } else if(_type==Type.PathDelete) {
                this.MouseDown += (sender, args) => {
                    if(App.NotifyIcon.CustomBalloon != null) {
                        App.NotifyIcon.CustomBalloon.IsOpen = false;
                    }
                };
            } else if(_type==Type.PathCreateChangeRename) {
                this.Cursor = Cursors.Hand;
                this.MouseDown += (sender, args) => {
                    MouseEventHandler evh = null;
                    string start_textMessage = this.TextMessage;
                    evh = (_sender, _args) => {
                        if(_args.LeftButton==MouseButtonState.Pressed) {
                            this.MouseMove -= evh;
                            //popup.AllowDrop = true;
                            this.TextMessage = $"{start_textMessage}\nDrag and drop this file to application";
                            try {
                                this.IsDragging = true;
                                //bool cm = popup.CaptureMouse();
                                // https://stackoverflow.com/questions/3040415/drag-and-drop-to-desktop-explorer
                                DragDropEffects drop_res = DragDrop.DoDragDrop(this, new DataObject(DataFormats.FileDrop, new string[] { this.path }), DragDropEffects.Copy);
                                //Debug.WriteLine($"Drop result: {drop_res.ToString()}");
                                this.RestartTimeoutTimer();
                                this.TextMessage = start_textMessage;
                            } catch(Exception _ex) {
                            } finally {
                                this.IsDragging = false;
                                //popup.ReleaseMouseCapture();
                            }
                        }
                    };
                    this.MouseMove += evh;
                    this.Drop += (_sender1, _args1) => {
                        //App.NotifyIcon.ResetBalloonCloseTimer
                        this.TextMessage = start_textMessage;
                    };
                };
                this.MouseUp += (sender, args) => {
                    if(App.NotifyIcon.CustomBalloon != null) {
                        App.NotifyIcon.CustomBalloon.IsOpen = false;
                    }
                    App.gotoPathByWindowsExplorer(this.path, this.wType);
                };
            } else {
                string str_error = $"{MCodes._000}. Call Developer.";
                Console.WriteLine(str_error);
                throw new Exception(str_error);
            }
        }

        public enum Type{
            Text,
            PathDelete,
            PathCreateChangeRename
        };

        /*
        public TrayPopupMessage(string _path, WatchingObjectType _wType, TaskbarIcon _tb, ControlButtons _buttons)
        {
            InitializeComponent();
            init(_path, _wType, _tb, _buttons);
        }
        //*/

        private TrayPopupMessage() {
            throw new Exception("Do not Use!!!");
        }

        public TrayPopupMessage(string _path, string _title, WatchingObjectType _wType, TaskbarIcon _tb, Image _item_image, ControlButtons _buttons, Type _type)
        {
            InitializeComponent();
            DataContext = this;
            Button_Copy_Path_To_Clipboard_Color = "";
            this.title.Text = _title;
            init(_path, _wType, _tb, _item_image, _buttons, _type);
        }


        public void customballoon_close(object sender, ElapsedEventArgs e)
        {
            if(this.IsDragging==false) {
                Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                {
                    // popup_test.Visibility = Visibility.Hidden;
                    //this.Visibility = Visibility.Hidden;
                    if (tb.CustomBalloon != null) {
                        tb.CustomBalloon.IsOpen = false;
                    }
                });
                System.Timers.Timer temp = ((System.Timers.Timer)sender);
                temp.Stop();
            }
        }

        private void Button_Close_Window_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void Button_Copy_Path_To_Clipboard_Click(object sender, RoutedEventArgs e) {
            //if(tb.CustomBalloon != null) {
            //    tb.CustomBalloon.IsOpen = false;
            //}
            //App.copy_clipboard_with_popup(path);
            bool res = false;
            try {
                System.Windows.Forms.Clipboard.SetText(path);
                Button_Copy_Path_To_Clipboard_Color = "Green";
                Button_Copy_File_To_Clipboard_Color = "Black";
                Button_Move_File_To_Clipboard_Color = "Black";
                res = true;
            }catch(Exception _ex) {

            }

            if(sender is System.Windows.Controls.Button) {
                System.Windows.Controls.Button btn = (Button)sender;
                Color startColor = (res == false ? Colors.Red : Colors.LimeGreen);
                ColorAnimation animation;
                animation = new ColorAnimation();
                animation.From = startColor;
                animation.To = Colors.Black;
                animation.Duration = new Duration(TimeSpan.FromSeconds(1));
                btn.Foreground = new SolidColorBrush(startColor);
                btn.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }

        }

        private void Button_Copy_File_To_Clipboard_Click(object sender, RoutedEventArgs e) {
            bool res = false;
            try {
                StringCollection paths = new StringCollection();
                paths.Add(path);
                Clipboard.SetFileDropList(paths);
                Button_Copy_Path_To_Clipboard_Color = "Black";
                Button_Copy_File_To_Clipboard_Color = "Green";
                Button_Move_File_To_Clipboard_Color = "Black";
                res = true;
                //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "File copied into a clipboard", BalloonIcon.Info);
            }catch(Exception _ex) {

            }

            if(sender is System.Windows.Controls.Button) {
                System.Windows.Controls.Button btn = (Button)sender;
                Color startColor = (res == false ? Colors.Red : Colors.LimeGreen);
                ColorAnimation animation;
                animation = new ColorAnimation();
                animation.From = startColor;
                animation.To = Colors.Black;
                animation.Duration = new Duration(TimeSpan.FromSeconds(1));
                btn.Foreground = new SolidColorBrush(startColor);
                btn.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }

        }

        private void Button_Move_File_To_Clipboard_Click(object sender, RoutedEventArgs e) {
            bool res = false;
            try {
                StringCollection paths = new StringCollection();
                //paths.Add(path);
                //Clipboard.SetFileDropList(paths);
                App.MoveFileToClipboard(path);
                Button_Copy_Path_To_Clipboard_Color = "Black";
                Button_Copy_File_To_Clipboard_Color = "Black";
                Button_Move_File_To_Clipboard_Color = "Green";
                res = true;
                //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "File copied into a clipboard", BalloonIcon.Info);
            }catch(Exception _ex) {

            }

            if(sender is System.Windows.Controls.Button) {
                System.Windows.Controls.Button btn = (Button)sender;
                Color startColor = (res == false ? Colors.Red : Colors.LimeGreen);
                ColorAnimation animation;
                animation = new ColorAnimation();
                animation.From = startColor;
                animation.To = Colors.Black;
                animation.Duration = new Duration(TimeSpan.FromSeconds(1));
                btn.Foreground = new SolidColorBrush(startColor);
                btn.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }

        }

        private void Button_Execute_File_Click(object sender, RoutedEventArgs e) {
            if(File.Exists(path)==true) {
                Process.Start(path);
            } else {
                App.ShowMessage($"File {path} do not exists.");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e) {

        }

        private void Button_Delete_File_Click(object sender, RoutedEventArgs e) {
            if(File.Exists(path)==true) {
                File.Delete(path);
            } else {
                App.ShowMessage($"File {path} do not exists.");
            }
        }

        private void Button_Delete_File_To_Trash_Click(object sender, RoutedEventArgs e) {
            if(File.Exists(path)==true) {
                File.Delete(path);
            } else {
                App.ShowMessage($"File {path} do not exists.");
            }
        }
    }
}
