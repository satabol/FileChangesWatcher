using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window, INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string propertyName) {
            // Если кто-то на него подписан, то вызывем его
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        private string _About_Text;

        public string About_Text {
            get { return _About_Text; }
            set {
                _About_Text = value;
                RaisePropertyChanged(nameof(About_Text));
            }
        }

        private string _AppVersion;

        public string AppVersion {
            get { return _AppVersion; }
            set { 
                _AppVersion = value; 
                RaisePropertyChanged(nameof(AppVersion));
            }
        }


        public About()
        {
            InitializeComponent();
            DataContext = this;
            //this.text_version.Text = "Version: "+Assembly.GetExecutingAssembly().GetName().Version.ToString();
            this.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            About_Text = System.Text.Encoding.UTF8.GetString(FileChangesWatcher.Properties.Resources.readme).Replace("vvvvvvv", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            AppVersion = " v." + FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigateWithVersion(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo( e.Uri.AbsoluteUri + ",version:" + Assembly.GetExecutingAssembly().GetName().Version.ToString()));
            e.Handled = true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo( e.Uri.AbsoluteUri ));
            e.Handled = true;
        }
    }
}
