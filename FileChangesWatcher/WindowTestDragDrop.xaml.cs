using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FileChangesWatcher {
    /// <summary>
    /// Interaction logic for WindowTestDragDrop.xaml
    /// </summary>
    public partial class WindowTestDragDrop  : Window, INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string propertyName) {
            // Если кто-то на него подписан, то вызывем его
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public WindowTestDragDrop() {
            InitializeComponent();
            this.AllowDrop = true;
            DataContext = this;
            FileName = "Drop file here";
        }

        private string _FileName;

        public string FileName {
            get {
                return _FileName;
            }
            set {
                _FileName = value;
                RaisePropertyChanged(nameof(FileName));
            }
        }


        private void event_MouseDown(object sender, MouseButtonEventArgs e) {
            //((MenuItem)sender).StaysOpenOnClick = true;
            Debug.WriteLine($"MenuItem PreviewMouseDown.");
            MouseEventHandler evh = null;
            evh = (_sender, _args) => {
                //((MenuItem)_sender).StaysOpenOnClick = true;
                Debug.WriteLine($"MenuItem MouseMove. Start Dragging");
                try {
                    this.MouseMove -= evh;
                    this.QueryContinueDrag += (_server1, args1) => {
                        Debug.WriteLine($"MenuItem QueryContinueDrag.");
                        args1.Action = DragAction.Continue;
                    };

                    Debug.WriteLine($"MenuItem MouseMove. Remove.");
                    //this.AllowDrop = true;
                    // https://stackoverflow.com/questions/3040415/drag-and-drop-to-desktop-explorer
                    //var data_object = new DataObject();
                    this.Hide();
                    var data_object = new DataObject(DataFormats.FileDrop, new string[] { @"F:\Enternet\2021\21.06.16\Безымянный.png" }, true);
                    //DragDrop.AddDragOverHandler(mi, (s2, a2) => {
                    //    a2.Handled = true;
                    //});
                    // Позволяет не закрывать меню в начале DoDragDrop. https://stackoverflow.com/questions/1558932/wpf-listbox-drag-drop-interferes-with-contextmenu
                    bool cm = this.CaptureMouse();
                    Debug.WriteLine($"MenuItem MouseMove. cm = {cm}");
                    DragDropEffects drop_res = DragDrop.DoDragDrop(this, data_object, DragDropEffects.All);
                    Debug.WriteLine($"Drop result: {drop_res.ToString()}");
                } catch(Exception _ex) {
                    Debug.WriteLine($"MenuItem MouseMove. Exception: {_ex.Message}");
                } finally {
                    this.Show();
                    this.ReleaseMouseCapture();
                    Debug.WriteLine($"MenuItem MouseMove. Finally");
                }
            };
            this.MouseMove += evh;
        }

        private void dropmenuitem_OnMouseDown(object sender, MouseButtonEventArgs e) {
            MouseEventHandler evh = null;
            MenuItem mi = sender as MenuItem;
            if(mi!=null) {
                evh = (_sender, _args) => {
                    this.Hide();
                    //((MenuItem)_sender).StaysOpenOnClick = true;
                    Debug.WriteLine($"MenuItem MouseMove. Start Dragging");
                    try {
                        this.MouseMove -= evh;
                        this.QueryContinueDrag += (_server1, args1) => {
                            Debug.WriteLine($"MenuItem QueryContinueDrag.");
                            args1.Action = DragAction.Continue;
                        };

                        Debug.WriteLine($"MenuItem MouseMove. Remove.");
                        this.AllowDrop = true;
                        // https://stackoverflow.com/questions/3040415/drag-and-drop-to-desktop-explorer
                        //var data_object = new DataObject();
                        var data_object = new DataObject(DataFormats.FileDrop, new string[] { @"F:\Enternet\2021\21.06.16\Безымянный.png" }, true);
                        //DragDrop.AddDragOverHandler(mi, (s2, a2) => {
                        //    a2.Handled = true;
                        //});
                        // Позволяет не закрывать меню в начале DoDragDrop. https://stackoverflow.com/questions/1558932/wpf-listbox-drag-drop-interferes-with-contextmenu
                        bool cm = this.CaptureMouse();
                        Debug.WriteLine($"MenuItem MouseMove. cm = {cm}");
                        DragDropEffects drop_res = DragDrop.DoDragDrop(this, data_object, DragDropEffects.All);
                        Debug.WriteLine($"Drop result: {drop_res.ToString()}");
                    } catch(Exception _ex) {
                        Debug.WriteLine($"MenuItem MouseMove. Exception: {_ex.Message}");
                    } finally {
                        this.ReleaseMouseCapture();
                        this.Show();
                        Debug.WriteLine($"MenuItem MouseMove. Finally");
                    }
                };
                this.MouseMove += evh;
            } else {
                Debug.WriteLine($"mi is null");
            }
        }

        private void window_DropEvent(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
                string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                FileName = droppedFilePaths[0];
            }
        }
    }
}
