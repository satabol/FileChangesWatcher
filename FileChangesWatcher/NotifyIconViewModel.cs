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

using System.Net.Http;
using Hardcodet.Wpf.TaskbarNotification;
using System.Runtime.InteropServices;
using System.Diagnostics;
using FileChangesWatcher;
using Microsoft.Win32;
using IniParser;
using System.Reflection;
using System.Windows.Interop;

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
        public ICommand ApplyIniFile
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        App.initApplication(null);
                        OnPropertyChanged("SaveToDbOneChecked");
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
                        String iniFilePath = null; // System.IO.Path.GetDirectoryName(Environment.CommandLine.Replace("\"", "")) + "\\Stroiproject.ini";
                        iniFilePath = Process.GetCurrentProcess().MainModule.FileName;
                        iniFilePath = System.IO.Path.GetDirectoryName(iniFilePath) + "\\" + System.IO.Path.GetFileNameWithoutExtension(iniFilePath) + ".ini";
                        if (File.Exists(iniFilePath) == false)
                        {
                            MessageBox.Show("Файл настроек не обнаружен.");
                        }else
                        {
                            System.Diagnostics.Process.Start(iniFilePath);
                        }
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand OpenHelp
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        System.Diagnostics.Process.Start("http://serv-japp.stpr.ru:4070/support/Soft/FileChangesWatcher.html");
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
                        System.Diagnostics.Process.Start("https://sourceforge.net/projects/filechangeswatcher/");
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
                            //MessageBox.Show( this, "Error on access to register FileChangesWatcher as Windows Context Menu", "FileChangesWatcher", MessageBoxButton.OK, MessageBoxImage.Error);
                            //App.Sh
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
                            //MessageBox.Show( this, "Error on access to register FileChangesWatcher as Windows Context Menu", "FileChangesWatcher", MessageBoxButton.OK, MessageBoxImage.Error);
                            //App.Sh
                            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "No paths to set", BalloonIcon.Error);
                        }
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        /*
        public ICommand TestToast
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = () =>
                    {
                        // In a real app, these would be initialized with actual data
                        string from = "Jennifer Parker";
                        string subject = "Photos from our trip";
                        string body = "Check out these awesome photos I took while in New Zealand!";


                        // Construct the tile content
                        TileContent content = new TileContent()
                        {
                            Visual = new TileVisual()
                            {
                                TileMedium = new TileBinding()
                                {
                                    Content = new TileBindingContentAdaptive()
                                    {
                                        Children =
                                            {
                                                new TileText()
                                                {
                                                    Text = from
                                                },

                                                new TileText()
                                                {
                                                    Text = subject,
                                                    Style = TileTextStyle.CaptionSubtle
                                                },

                                                new TileText()
                                                {
                                                    Text = body,
                                                    Style = TileTextStyle.CaptionSubtle
                                                }
                                            }
                                    }
                                },

                                TileWide = new TileBinding()
                                { 
                                    Content = new TileBindingContentAdaptive()
                                    {
                                        Children =
                                            {
                                                new TileText()
                                                {
                                                    Text = from,
                                                    Style = TileTextStyle.Subtitle
                                                },

                                                new TileText()
                                                {
                                                    Text = subject,
                                                    Style = TileTextStyle.CaptionSubtle
                                                },

                                                new TileText()
                                                {
                                                    Text = body,
                                                    Style = TileTextStyle.CaptionSubtle
                                                }
                                            }
                                    }
                                }
                            }
                        };

                        Windows.Data.Xml.Dom.XmlDocument xml_doc = new Windows.Data.Xml.Dom.XmlDocument();
                        xml_doc.LoadXml(content.GetContent());
                        //var notification = new TileNotification( xml_doc );
                        String APP_ID = "Microsoft.Samples.DesktopToastsSample";
                        ToastNotification toast = new ToastNotification(xml_doc);
                        ToastNotificationManager.CreateToastNotifier(APP_ID).Show(toast);

                    },
                    CanExecuteFunc = () => true
                };
            }
        }
        //*/

        public ICommand TestWebBrowser
        {
            get
            {
                return new DelegateCommand
                {
                    //CanExecuteFunc = () => Application.Current.MainWindow == null,
                    CommandAction = () =>
                    {
                        string result = Properties.Resources.main_html_page;

                        WebBrowserDialog browser = new WebBrowserDialog();
                        //browser.webBrowser.NavigateToString("<!DOCTYPE html><html><meta http-equiv=\"content-type\" content=\"text/html; charset=UTF-8\"/><body>Hello!!!<br/><br/>"+
                        //                                    "<hr/><button>start explorer.exe</button><br/><hr/></body></html>");
                        browser.webBrowser.NavigateToString(result);
                        browser.Show();
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
                    //CanExecuteFunc = () => Application.Current.MainWindow == null,
                    CommandAction = () =>
                    {
                        About window = new About();
                        window.Show();
                        App.ActivateWindow(new WindowInteropHelper(window).Handle);
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        private static void ShowWebBrowser(object sender, DoWorkEventArgs e)
        {
            WebBrowserDialog browser = new WebBrowserDialog();
            browser.webBrowser.Refresh(true);
            browser.webBrowser.NavigateToString("<!DOCTYPE html><html><meta http-equiv=\"content-type\" content=\"text/html; charset=UTF-8\"/><body>Hello!!!<br/><br/>"+
                                                "<hr/><button>start explorer.exe</button><br/><hr/></body></html>");
            browser.Show();
        }

        public ICommand ExitApplicationCommand
        {
            get
            {
                //return new DelegateCommand { CommandAction = () => { Application.Current.Shutdown(); } };
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
