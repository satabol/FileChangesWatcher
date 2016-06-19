using mshtml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FileChangesWatcher
{
    /// <summary>
    /// Interaction logic for WebBrowserDialog.xaml
    /// </summary>
    public partial class WebBrowserDialog : Window
    {
        public WebBrowserDialog()
        {
            InitializeComponent();
            webBrowser.LoadCompleted += WebBrowser_LoadCompleted;
        }

        public void WebBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            //throw new NotImplementedException();
            mshtml.HTMLDocument doc = webBrowser.Document as mshtml.HTMLDocument;
            mshtml.IHTMLElementCollection coll = (mshtml.IHTMLElementCollection)(doc.getElementsByTagName("button"));
            mshtml.IHTMLElement coll_id_start_explorer = (mshtml.IHTMLElement)(doc.getElementById("id_start_explorer"));
            mshtml.IHTMLElement coll_id_start_dialog = (mshtml.IHTMLElement)(doc.getElementById("id_start_dialog"));

            // https://codevomit.wordpress.com/2015/06/15/wpf-webbrowser-control-part-2/
            /*
            foreach (mshtml.IHTMLElement obj in coll)
            {
                HTMLButtonElementEvents_Event htmlButtonEvent = obj as HTMLButtonElementEvents_Event;
                htmlButtonEvent.onclick += clickElementHandler;
            }
            //*/

            if (coll_id_start_dialog != null)
            {
                HTMLButtonElementEvents_Event htmlButtonEvent = coll_id_start_dialog as HTMLButtonElementEvents_Event;
                htmlButtonEvent.onclick += start_dialog;
            }
            if (coll_id_start_explorer != null)
            {
                HTMLButtonElementEvents_Event htmlButtonEvent = coll_id_start_explorer as HTMLButtonElementEvents_Event;
                htmlButtonEvent.onclick += start_explorer;
            }
        }

        public bool clickElementHandler()
        {
            //System.Windows.MessageBox.Show("Кнопка нажалась");
            //Console.Write("Кнопка нажалась.");
            Process.Start("explorer.exe", "");
            return false;
        }
        public bool start_dialog()
        {
            System.Windows.MessageBox.Show("Кнопка нажалась");
            return false;
        }
        public bool start_explorer()
        {
            Process.Start("explorer.exe", "");
            return false;
        }
    }
}
