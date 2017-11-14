using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FileChangesWatcher {
    /// <summary>
    /// Interaction logic for MenuItemFileForTrayContextMenu.xaml
    /// </summary>
    public partial class MenuItemFileForTrayContextMenu : UserControl {

        public MenuItem mi_clipboard = null;
        public MenuItem mi_enter = null;
        public MenuItemFileForTrayContextMenu() {
            InitializeComponent();

            mi_clipboard    = (MenuItem)this.FindName("mi_clipboard");
            mi_enter        = (MenuItem)this.FindName("mi_enter");
        }
    }
}
