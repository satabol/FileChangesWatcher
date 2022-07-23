using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Hardcodet.Wpf.TaskbarNotification;
using System.Runtime.InteropServices;
using System.Diagnostics;
using FileChangesWatcher;
using Microsoft.Win32;
using System.Reflection;
using System.Windows.Interop;
using System.Collections.Generic;
using SharepointSync;

//using Windows.UI.Notifications;
//using NotificationsExtensions.Tiles; // NotificationsExtensions.Win10

namespace FileChangesWatcher
{
    class NotifyIconViewModel : INotifyPropertyChanged
    {

        public bool SaveToDbOneChecked
        {
            get {
                return App.IsAppInRegestry();
            }
            set {
                OnPropertyChanged("SaveToDbOneChecked");
            }
        }

        // ==============================
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public ICommand MessRight
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        /*
                        switch(MessageBox.Show("Right : ", "", MessageBoxButton.OKCancel))
                        {
                            case MessageBoxResult.OK:
                                SaveToDbOneChecked = SaveToDbOneChecked;
                                break;
                            default:
                                SaveToDbOneChecked = !SaveToDbOneChecked;
                                break;
                        }
                        //*/
                        App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Вы нажали правую кнопку меню", BalloonIcon.Info);
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand MessLeft
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Вы нажали левую кнопку меню", BalloonIcon.Info);
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand AutostartSetter
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        if( App.IsAppInRegestry()==true)
                        {
                            App.resetAutostart();
                        }
                        else
                        {
                            App.setAutostart();
                        }
                        OnPropertyChanged("SaveToDbOneChecked");
                    },
                    CanExecuteFunc = () => true
                };
            }
        }
        public ICommand ReloadSettings
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        App.initApplication(null);
                        OnPropertyChanged( nameof(ReloadSettings) );
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand OpenIniFile
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        String iniFilePath = null;
                        iniFilePath = Process.GetCurrentProcess().MainModule.FileName;
                        //iniFilePath = System.IO.Path.GetDirectoryName(iniFilePath) + "\\" + System.IO.Path.GetFileNameWithoutExtension(iniFilePath) + ".ini";
                        iniFilePath = System.IO.Path.ChangeExtension(iniFilePath, ".ini");
                        if (LongFile.Exists(iniFilePath) == false)
                        {
                            //MessageBox.Show("Файл настроек не обнаружен.");
                            MessageBox.Show("File of settings not found.");
                        }else
                        {
                            Process.Start(iniFilePath);
                        }
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand OpenSettingsFileJS {
            get {
                return new DelegateCommand {
                    CommandAction = () => {
                        String jsFilePath = null;
                        jsFilePath = Process.GetCurrentProcess().MainModule.FileName;
                        jsFilePath = System.IO.Path.ChangeExtension(jsFilePath, ".js");
                        if (LongFile.Exists(jsFilePath) == false) {
                            MessageBox.Show( $"File of settings must have name '{jsFilePath}'. Not found.");
                        }
                        else {
                            Process.Start("explorer.exe", "/select,\""+jsFilePath+"\"");
                        }
                    },
                    CanExecuteFunc = () => true
                };
            }
        }
        public ICommand OpenWindowTestDragDrop{
            get {
                return new DelegateCommand {
                    CommandAction = () => {
                        if(App.windowTestDragDrop!=null) {
                            App.windowTestDragDrop.Close();
                        }
                        App.windowTestDragDrop = new WindowTestDragDrop();
                        App.windowTestDragDrop.Show();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand OpenHomePage
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        System.Diagnostics.Process.Start(App.Settings.OpenHomePage);
                    },
                    CanExecuteFunc = () => true
                };
            }
        }
        public ICommand OpenHomeFolder
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        String exe_folder = App.getExeFilePath();
                        Process.Start("explorer.exe", "/select,\"" + exe_folder + "\"");
                    },
                    CanExecuteFunc = () => true
                };
            }
        }
        public ICommand ShareMeByEmail
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        String exe_folder = App.getExeFilePath();
                        Process.Start("mailto:?subject=look at this website&body=Hi,I found this website https://sourceforge.net/projects/filechangeswatcher/");
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand RegisterWindowsExplorerContextMenu
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        if(FileChangesWatcher.App._IsUserAdministrator == true)
                        {
                            App.registerDLL(App.getExeFilePath());
                        }
                        else
                        {
                            //MessageBox.Show("Error on access to register FileChangesWatcher as Windows Context Menu", "FileChangesWatcher", MessageBoxButton.OK, MessageBoxImage.Error);
                            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Error on access to register FileChangesWatcher in Windows Context Menu. You have to be administrator", BalloonIcon.Error);
                        }
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand UnRegisterWindowsExplorerContextMenu
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        if (FileChangesWatcher.App._IsUserAdministrator == true)
                        {
                            App.unregisterDLL(App.getExeFilePath());
                        }
                        else
                        {
                            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Error on access to register FileChangesWatcher in Windows Context Menu. You have to be administrator", BalloonIcon.Error);
                        }
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand CopyPathsToClipboard
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        String paths = App.GetStackPathsAsString();
                        if (paths.Length>0)
                        {
                            System.Windows.Forms.Clipboard.SetText(paths);
                            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Clipboard set with paths", BalloonIcon.Info);
                        }
                        else
                        {
                            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "No paths to set", BalloonIcon.Error);
                        }
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand OpenAbout
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        About window = new About();
                        window.Show();
                        DLLImport.ActivateWindow(new WindowInteropHelper(window).Handle);
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand ResetIconsCache {
            get {
                return new DelegateCommand {
                    CommandAction = () => {
                        MenuItemData.icons_map = new Dictionary<string, BitmapImage>();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand ExitApplicationCommand
        {
            get
            {
                return new DelegateCommand { CommandAction = () => { App.Current.Shutdown(0); } };
            }
        }
    }

    /// <summary>
    /// Simplistic delegate command for the demo.
    /// </summary>
    public class DelegateCommand : ICommand
    {
        public Action CommandAction { get; set; }
        public Func<bool> CanExecuteFunc { get; set; }

        public void Execute(object parameter)
        {
            CommandAction();
        }

        public bool CanExecute(object parameter)
        {
            return CanExecuteFunc == null || CanExecuteFunc();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
    
}
