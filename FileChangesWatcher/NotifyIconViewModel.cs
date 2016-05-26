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

namespace Stroiproject
{
    class NotifyIconViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(name));
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

     /*
    public class OpenBrowser : ICommand
    {
        public void Execute(object parameter)
        {
            Object[] arrObject = (object[])parameter;
            MainWindow mv = (MainWindow)arrObject[0];
        }
    }
    */
}
