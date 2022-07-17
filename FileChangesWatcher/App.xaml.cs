using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;

using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

using System.Reflection;
using System.IO;
using System.Windows.Media.Imaging;

using System.Web;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics.Eventing.Reader;
using System.Xml;
using System.Threading;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Controls.Primitives;
using System.Timers;
using System.Resources;
using SharepointSync;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Specialized;

namespace FileChangesWatcher {

    /// <summary>
    /// Exposes the Mime Mapping method that Microsoft hid from us.
    /// </summary>
    public static class MimeMappingStealer {
        // The get mime mapping method info
        private static readonly MethodInfo _getMimeMappingMethod = null;

        /// <summary>
        /// Static constructor sets up reflection.
        /// </summary>
        static MimeMappingStealer() {
            // Load hidden mime mapping class and method from System.Web
            var assembly = Assembly.GetAssembly(typeof(HttpApplication));
            Type mimeMappingType = assembly.GetType("System.Web.MimeMapping");
            _getMimeMappingMethod = mimeMappingType.GetMethod("GetMimeMapping",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        }

        /// <summary>
        /// Exposes the hidden Mime mapping method.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>The mime mapping.</returns>
        public static string GetMimeMapping(string fileName) {
            return (string)_getMimeMappingMethod.Invoke(null /*static method*/, new[] { fileName });
        }
    }

    // Существуют только два типа наблюдаемых объектов - файл и каталог.
    public enum WatchingObjectType {
        File, Folder, Unknown, Log
    }

    // Используется при передаче пути в функцию, чтобы не потерять о каком типе объекта идёт речь - файле или каталоге. Ведь
    // они могут называться одинаково!
    class Path_ObjectType {
        public string path;
        public WatchingObjectType wType;
        public DateTime dateTime;
        public Path_ObjectType(string _path, WatchingObjectType _wType) {
            path = _path;
            wType = _wType;
            dateTime = DateTime.Now;
        }
        public Path_ObjectType(string _path, WatchingObjectType _wType, DateTime _dateTime) {
            path = _path;
            wType = _wType;
            dateTime = _dateTime;
        }
    }

    public class MenuItemData {
        // Кешируемые иконки по расширениям файлов:
        public static Dictionary<string, BitmapImage> icons_map = new Dictionary<string, BitmapImage>();

        /// <summary>
        /// To short long paths. Max length of menu item. If line exceed tins value it will be shrinked
        /// <image url="$(SolutionDir)images\38.png" scale="0.2"/>
        /// </summary>
        public static int menuitem_header_length = 30;

        public DateTime event_date_time = DateTime.Now; // Дата/время регистрации события.
        public MenuItem mi;
        public WatchingObjectType wType;

        protected MenuItemData(WatchingObjectType _wType, DateTime _dt) {
            this.event_date_time = _dt; // DateTime.Now;
            this.wType = _wType;
        }
    }

    /// <summary>
    /// Элементы меню для Файла/Каталога для режима CreateChangeRenameDelete
    /// </summary>
    public class MenuItemData_CCRD : MenuItemData  // CreateChangeRenameDelete
    {
        public FileSystemEventArgs e;

        // Проверить, а есть ли файл, который указан в этом меню.
        public void CheckPath() {
            // Log не обрабатывается:
            // Skip changes in log files
            if(this.wType == WatchingObjectType.Log) {
                return;
            }
            // Если элемент меню является объектом, который был удалён, то не надо вместо него делать модификации пункта меню,
            // т.к. удаление - это уже состоявшееся событие:
            if(e.ChangeType == WatcherChangeTypes.Deleted) {
                return;
            }
            bool bool_object_exists = false;
            // Если объект существует, то активировать его меню:
            // If object exists then item of menu has to be actived.
            if(this.wType == WatchingObjectType.File) {
                if(LongFile.Exists(e.FullPath)) {
                    bool_object_exists = true;
                }
            } else if(this.wType == WatchingObjectType.Folder) {
                if(LongDirectory.Exists(e.FullPath)) {
                    bool_object_exists = true;
                }
            }

            {
                Grid grid = (Grid)mi.Template.FindName("mi_grid", mi);
                if(grid != null) {
                    foreach(Object _obj in grid.Children) {
                        if(_obj is MenuItem) {
                            MenuItem mi_tmp = (MenuItem)_obj;
                            if(mi_tmp != null) {
                                switch(mi_tmp.Name) {
                                    case "mi_main":
                                    case "mi_copy_file_to_clipboard":
                                    case "mi_move_file_to_clipboard":
                                    case "mi_file_delete":
                                    case "mi_enter":
                                        //mi_tmp.Background = System.Windows.Media.Brushes.DarkGray;
                                        mi_tmp.IsEnabled = bool_object_exists;
                                        //mi_tmp.Visibility =  Visibility.Hidden;
                                        break;
                                    case "mi_clipboard":
                                        break;
                                }

                                if(bool_object_exists==false) {
                                    switch(mi_tmp.Name) {
                                        //case "mi_main":
                                        case "mi_copy_file_to_clipboard":
                                        case "mi_move_file_to_clipboard":
                                        case "mi_file_delete":
                                        case "mi_enter":
                                            //mi_tmp.Background = System.Windows.Media.Brushes.DarkGray;
                                            //mi_tmp.IsEnabled = bool_object_exists;
                                            mi_tmp.Visibility =  Visibility.Hidden;
                                            break;
                                        case "mi_clipboard":
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Обработка события наведения курсора на пункт меню (TODO: can I do preview?)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotFocus(Object sender, EventArgs e) {
            int i = 0;
        }

        // fastest_generation - генерировать пункт меню без всякий дополнительных опций, чтобы ускорить сам процесс.
        //                      Применять при массовых файловых операциях, когда события валятся десятками и сотнями в секунду.
        //                      Пользователь всё равно не будет успевать их видеть. Как только события прекратят валиться с такой скоростью
        //                      программа пересоздаст все пункты меню со всеми "плюшками".
        //                      На логировании этот параметр не сказывается и все пути пишутся в файл как и раньше.
        public MenuItemData_CCRD(FileSystemEventArgs _e, WatchingObjectType _wType, DateTime _dt, bool fastest_generation = true) : base(_wType, _dt) {
            this.e = _e;
            if(fastest_generation == true) {
                return;
            }

            if(_e is RenamedEventArgs) {
                // То известен OldPath и OldName
            }
            mi = new MenuItem() {
                Name = "mi_main"
            };
            mi.GotFocus += this.GotFocus;
            switch(_e.ChangeType) {
                case WatcherChangeTypes.Deleted: {
                    //string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss Deleted ") + _e.FullPath;
                    mi.ToolTip = "Copy path to clipboard: " + _e.FullPath;
                    mi.Header = _dt.ToString("yyyy/MM/dd HH:mm:ss.fff") + " [" + _wType.ToString() + " " + _e.ChangeType.ToString()+"]\n    " + App.ShortText(_e.FullPath); //mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
                                                                                                                                                                            // Еле-еле выставил иконку для меню программно и то без ресурса. Использовать иконку из ресурса не получается. http://www.telerik.com/forums/how-to-set-icon-from-codebehind
                    mi.Icon = new System.Windows.Controls.Image { Source = new BitmapImage(new Uri("pack://application:,,,/Icons/deleted.ico", UriKind.Absolute)) };
                    mi.Command = App.CustomRoutedCommand_CopyTextToClipboard;
                    mi.CommandParameter = _e.FullPath;
                    mi.Foreground = System.Windows.Media.Brushes.Gray;
                }
                break;
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Renamed: {
                    mi.ToolTip = "Go to " + _e.FullPath;
                    string file_size = "";
                    {
                        FileInfo fi = new FileInfo(_e.FullPath);
                        if(fi.Exists==true) {
                            mi.Cursor = Cursors.Hand;
                            NumberFormatInfo nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                            nfi.NumberGroupSeparator = " ";
                            file_size = $"[{fi.Length.ToString("#,0", nfi)} byte]";
                        }

                    }
                    mi.Header = _dt.ToString("yyyy/MM/dd HH:mm:ss.fff")+ " ["+ _wType.ToString() + " " + _e.ChangeType.ToString() + $"] {file_size}\n    " + App.ShortText(_e.FullPath); // mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
                    if(_e.ChangeType == WatcherChangeTypes.Renamed) {
                        RenamedEventArgs _ee = (RenamedEventArgs)_e;
                        string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _ee, "oldFullPath");
                    }
                    mi.Command = App.CustomRoutedCommand;
                    mi.CommandParameter = new Path_ObjectType(_e.FullPath, wType);

                    if(_wType==WatchingObjectType.File) {
                        // Получить иконку файла для вывода в меню:
                        // http://www.codeproject.com/Articles/29137/Get-Registered-File-Types-and-Their-Associated-Ico
                        // Загрузить иконку файла в меню: http://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap?answertab=votes#tab-top
                        // Как-то зараза не грузится простым присваиванием.
                        string file_ext = Path.GetExtension(_e.FullPath);
                        Icon mi_icon = null;
                        BitmapImage bitmapImage = null;

                        // Кешировать иконки для файлов:
                        if(icons_map.TryGetValue(file_ext, out bitmapImage) == true && bitmapImage != null) {
                        } else {
                            ushort uicon = 0;
                            StringBuilder strB = new StringBuilder(_e.FullPath);
                            try {
                                // На сетевых путях выдаёт Exception. Поэтому к сожалению не годиться.
                                //mi_icon = Icon.ExtractAssociatedIcon(_path);// getIconByExt(file_ext);

                                // Этот метод работает: http://stackoverflow.com/questions/1842226/how-to-get-the-associated-icon-from-a-network-share-file?answertab=votes#tab-top
                                IntPtr handle = App.ExtractAssociatedIcon(IntPtr.Zero, strB, out uicon);
                                mi_icon = Icon.FromHandle(handle);
                            } catch(Exception ex) {
                                mi_icon = null;
                                icons_map.Add(file_ext, null);
                            }

                            if(mi_icon != null) {
                                using(MemoryStream memory = new MemoryStream()) {
                                    Bitmap bitmap = mi_icon.ToBitmap();
                                    bitmap.Save(memory, ImageFormat.Png);
                                    memory.Position = 0;

                                    bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    // Принудительный resize иконки, потому что под xp WPF не умеет автоматически масштабировать иконки в меню.
                                    // Windows XP has a scale problem, so I have resize it manually.
                                    bitmapImage.DecodePixelHeight = 16;
                                    bitmapImage.DecodePixelWidth = 16;
                                    bitmapImage.StreamSource = memory;
                                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad; //.Default; //.OnLoad;
                                    bitmapImage.EndInit();
                                    if(strB.ToString().ToLower().EndsWith("shell32.dll") == true && uicon == 1) {
                                        // Это означает, что нужно отобразить иконку для не существующего файла или иконка не найдена user32.Dll,1
                                        // http://forum.vingrad.ru/topic-26161.html
                                        // https://msdn.microsoft.com/ru-ru/library/windows/desktop/ms648067%28v=vs.85%29.aspx?f=255&MSPPError=-2147217396
                                        // 
                                    } else {
                                        icons_map.Add(file_ext, bitmapImage);
                                    }
                                }
                            }
                        }
                        if(bitmapImage != null) {
                            mi.Icon = new System.Windows.Controls.Image {
                                Source = bitmapImage
                            };
                        } else {
                            mi.Icon = null;
                        }
                    } else if(_wType == WatchingObjectType.Folder) {
                        mi.Icon = new System.Windows.Controls.Image {
                            Source = new BitmapImage(
                            new Uri("pack://application:,,,/Icons/folder-horizontal-open.png"))
                        };
                    }

                    {
                        MenuItem _mi = new MenuItem();
                        Grid mi_grid = null; // new Grid();

                        // Эксперимент сделать шаблон из ресурса, а не парсить строку:
                        ControlTemplate ct = (ControlTemplate)System.Windows.Application.Current.Resources["MenuItemFileForContextMenu"];
                        _mi.Template = ct;

                        if(_mi.ApplyTemplate()) {
                            mi_grid = (Grid)ct.FindName("mi_grid", _mi);
                        }
                        MenuItem mi_clipboard = (MenuItem)ct.FindName("mi_clipboard", _mi);
                        mi_clipboard.Command = App.CustomRoutedCommand_CopyTextToClipboard;
                        mi_clipboard.CommandParameter = _e.FullPath;

                        MenuItem mi_copy_file_to_clipboard = (MenuItem)ct.FindName("mi_copy_file_to_clipboard", _mi);
                        mi_copy_file_to_clipboard.Command = App.CustomRoutedCommand_CopyFileToClipboard;
                        mi_copy_file_to_clipboard.CommandParameter = _e.FullPath;

                        MenuItem mi_move_file_to_clipboard = (MenuItem)ct.FindName("mi_move_file_to_clipboard", _mi);
                        mi_move_file_to_clipboard.Command = App.CustomRoutedCommand_MoveFileToClipboard;
                        mi_move_file_to_clipboard.CommandParameter = _e.FullPath;

                        MenuItem mi_file_delete = (MenuItem)ct.FindName("mi_file_delete", _mi);
                        mi_file_delete.Command = App.CustomRoutedCommand_DeleteFile;
                        mi_file_delete.CommandParameter = _e.FullPath;

                        MenuItem mi_enter = (MenuItem)ct.FindName("mi_enter", _mi);
                        // Если объект удалён, то нельзя его выполнить
                        // If file has removed you can't execute it or start associated application.
                        if(_e.ChangeType != WatcherChangeTypes.Deleted) {
                            mi_enter.ToolTip = "Execute file";
                            mi_enter.Command = App.CustomRoutedCommand_ExecuteFile;
                            mi_enter.CommandParameter = new Path_ObjectType(_e.FullPath, wType);
                        }

                        Grid.SetColumn(mi, 0);
                        Grid.SetRow(mi, 0);
                        mi_grid.Children.Add(mi);
#if(!_Evgeniy)
                        mi_clipboard.Visibility = Visibility.Visible;
#endif
                        // Добавить кнопку для файла "Запустить файл" в правом столбце:
                        if(wType == WatchingObjectType.File) {
                            mi_enter.Visibility = Visibility.Visible;
                            mi_copy_file_to_clipboard.Visibility = Visibility.Visible;
                            mi_move_file_to_clipboard.Visibility = Visibility.Visible;
                            mi_file_delete.Visibility = Visibility.Visible;
                        }
                        
                        // Append Drag&Drop
                        App.NotifyIcon.ContextMenu.StaysOpen = true;
                        mi.StaysOpenOnClick = true;
                        App.NotifyIcon.ContextMenu.PreviewMouseWheel += (s2, a2) => {
                        };
                        mi.PreviewMouseRightButtonDown += (_sender, _args) => {
                            try {
                                ShellContextMenu scm = new ShellContextMenu();
                                FileInfo[] files = new FileInfo[1];
                                files[0] = new FileInfo(_e.FullPath);
                                scm.ShowContextMenu(files, System.Windows.Forms.Cursor.Position);
                                Thread.Sleep(2000);
                            } catch(Exception _ex) {
                                Console.WriteLine($"{MCodes._0001.mfem()}. PreviewMouseRightButtonDown: {_ex.Message}");
                            }
                        };
                        mi.PreviewMouseLeftButtonDown += (_sender, _args) => {
                            if(_args.LeftButton==MouseButtonState.Pressed) {
                                MouseEventHandler evh = null;
                                evh = (_sender0, _args0) => {
                                    bool is_capture_mouse_set = false;
                                    try {
                                        mi.MouseMove -= evh;
                                        // Есть глюк для контекстного меню NotifyIcon. Если потащить DragDrop из меню,
                                        // и провести его над Windows Toolbar, остановить над каким-либо приложением, которое активируется
                                        // и станет активным, то контекстное меню закрывается и процесс DragDrop
                                        // прерывается, но делает это некорректно. Если после прерываения попытаться запустить
                                        // контекстное меню правой кнопкой мыши, то оно самостоятельно закроется в момент открытия.
                                        // Потом будет снова работать нормально, но эффект выглядит некрасиво.
                                        // Поэтому в момент закрытия контекстного меню требуется остановить DragDrop по этому признаку,
                                        // а это можно сделать только когда DragDrop поинтересуется, можно ли ему продолжать. А случится это
                                        // в событии QueryContinueDrag. Т.е., когда App.NotifyIcon.ContextMenu.Closed срабатывает
                                        // то выставляет признак, чтобы не обрабатывать DragDrop, а когда DragDrop вызывает событие 
                                        // QueryContinueDrag, то тут-то я ему и сообщаю, что я в его услугах больше не нуждаюсь.
                                        bool dragdrop_continue = true;
                                        //DragDrop.AddQueryContinueDragHandler(mi, (_sender1, _args1) => {
                                        //    if(dragdrop_continue==false) {
                                        //        _args1.Action = DragAction.Cancel;
                                        //        _args1.Handled=true;
                                        //    }
                                        //});
                                        mi.QueryContinueDrag += (_sender2, _args2) => {
                                            if(dragdrop_continue==false) {
                                                _args2.Action = DragAction.Cancel;
                                                _args2.Handled=true;
                                                {
                                                    // last chance for user - on stop drag'n'drop - open notify message
//                                                    TrayPopupMessage popup = new TrayPopupMessage(e.FullPath, mi.Header.ToString(), _wType, App.NotifyIcon, null,
//#if(!_Evgeniy)
//                                                            TrayPopupMessage.ControlButtons.Clipboard |
//#endif
//                                                            TrayPopupMessage.ControlButtons.Run,
//                                                            TrayPopupMessage.Type.PathCreateChangeRename
//                                                        );

//                                                    App.NotifyIcon.ShowCustomBalloon(popup, PopupAnimation.None, 4000);
                                                }
                                            }
                                        };
                                        App.NotifyIcon.ContextMenu.ContextMenuClosing += (_sender2, _args2) => {
                                        };
                                        App.NotifyIcon.ContextMenu.Closed += (_sender2, _args2) => {
                                            dragdrop_continue = false;
                                        };
                                        App.NotifyIcon.ContextMenu.Opened += (_sender2, _args2) => {
                                            dragdrop_continue = true;
                                        };
                                        mi.AllowDrop = true;
                                        // https://stackoverflow.com/questions/3040415/drag-and-drop-to-desktop-explorer
                                        DataObject data_object = new DataObject(DataFormats.FileDrop, new string[] { _e.FullPath }, true);
                                        // DoDragDrop обычно закрывает меню, как только вызывается.
                                        // Позволяет не закрывать контекстно меню в NotifyIcon в начале DoDragDrop. https://stackoverflow.com/questions/1558932/wpf-listbox-drag-drop-interferes-with-contextmenu
                                        is_capture_mouse_set = mi.CaptureMouse();
                                        DragDropEffects drop_res = DragDrop.DoDragDrop(mi, data_object, DragDropEffects.All);
                                    } catch(Exception _ex) {
                                        Debug.WriteLine($"MenuItem MouseMove. Exception: {_ex.Message}");
                                    } finally {
                                        if(is_capture_mouse_set==true) {
                                            mi.ReleaseMouseCapture();
                                        }
                                    }
                                };
                                mi.MouseMove += evh;
                            }
                        };

                        mi = _mi;
                    }
                }
                break;
            }
        }
    }


    /// <summary>
    /// Элемент меню для записи Log (для отображения при нажатии текстого сообщения)
    /// </summary>
    public class MenuItemData_Log : MenuItemData  // Rename
    {
        public string log_record_text;

        public MenuItemData_Log(BalloonIcon _ballonIcon, string logText, DateTime _dt) : base(WatchingObjectType.Log, _dt) {
            this.log_record_text = logText;

            mi = new MenuItem();
            string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss ") + logText.Split('\n')[0];
            mi.Header = mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
            mi.ToolTip = "message from program:\n" + logText;
            mi.Command = App.CustomRoutedCommand_ShowMessage;
            mi.CommandParameter = logText;

            MenuItemData first_menuItemData = null;
            if(App.stackPaths.Count > 0) {
                first_menuItemData = App.stackPaths.OrderBy(d => d.event_date_time).Last();
            }
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        // Добавление пользовательских меню выполнено на основе: https://msdn.microsoft.com/ru-ru/library/ms752070%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396

        public App() {
            //windowTestDragDrop = new WindowTestDragDrop();
            //windowTestDragDrop.Show();
            process_stack();
        }

        public static WindowTestDragDrop windowTestDragDrop = null;

        // Классификация элементов контекстного меню с файловыми операциями:
        enum ContextMenu_Group_Names {
            group_files, group_folders, group_logs
        }

        Dictionary<ContextMenu_Group_Names, List<MenuItemData>> context_menu_dict = new Dictionary<ContextMenu_Group_Names, List<MenuItemData>>(){
            { ContextMenu_Group_Names.group_files, new List<MenuItemData>() },
            { ContextMenu_Group_Names.group_folders, new List<MenuItemData>() },
            { ContextMenu_Group_Names.group_logs, new List<MenuItemData>() },
        };

        // Список пользовательских пунктов меню, в которые записываются пути изменяемых файлов:
        public static List<MenuItemData> stackPaths = new List<MenuItemData>();

        // Получить список путей в контекстном меню, перед копированием в буфер обмена.
        public static String GetStackPathsAsString() {
            StringBuilder sb = new StringBuilder();
            //foreach (var obj in stackPaths.FindAll(d=> (d is MenuItemData_CCRD)).OrderBy(d => d.index).Reverse().ToArray())
            foreach(MenuItemData obj in stackPaths.FindAll(d => (d is MenuItemData_CCRD)).OrderBy(d => d.event_date_time).Reverse().ToArray()) {
                if(sb.Length > 0) {
                    sb.Append("\n");
                }
                MenuItemData_CCRD _obj = (MenuItemData_CCRD)obj;
                sb.Append(_obj.e.FullPath);
            }
            return sb.ToString();
        }

        public static string ShortText(string text) {
            string result = text.Length > (menuitem_header_length * 2 + 5) ? text.Substring(0, menuitem_header_length) + " ... " + text.Substring(text.Length - menuitem_header_length) : text;
            return result;
        }

        // Пользовательская команда:
        public static RoutedCommand CustomRoutedCommand_ExecuteFile = new RoutedCommand();
        private void ExecutedCustomCommand_ExecuteFile(object sender, ExecutedRoutedEventArgs e) {
            Path_ObjectType obj = (Path_ObjectType)e.Parameter;
            Process.Start(obj.path, "");
        }

        /// <summary>
        /// Copy text to clipboard
        /// </summary>
        public static RoutedCommand CustomRoutedCommand_CopyTextToClipboard = new RoutedCommand();
        public static RoutedCommand CustomRoutedCommand_CopyFileToClipboard = new RoutedCommand();
        public static RoutedCommand CustomRoutedCommand_MoveFileToClipboard = new RoutedCommand();
        public static RoutedCommand CustomRoutedCommand_DeleteFile= new RoutedCommand();
        private void ExecutedCustomCommand_CopyTextToClipboard(object sender, ExecutedRoutedEventArgs e) {
            try {
                String text = (string)e.Parameter;
                copy_clipboard_with_popup(text);
            } catch(Exception _ex) { 
            }
        }

        private void ExecutedCustomCommand_CopyFileToClipboard(object sender, ExecutedRoutedEventArgs e) {
            bool res = false;
            try {
                StringCollection paths = new StringCollection();
                String path = (string)e.Parameter;
                if(File.Exists(path)==true) {
                    paths.Add(path);
                    Clipboard.SetFileDropList(paths);
                    res = true;
                }
                //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "File copied into a clipboard", BalloonIcon.Info);
            }catch(Exception _ex) {

            }
        }

        private void ExecutedCustomCommand_MoveFileToClipboard(object sender, ExecutedRoutedEventArgs e) {
            try {
                String path = (string)e.Parameter;
                if(File.Exists(path)==true) {
                    // https://stackoverflow.com/questions/2077981/cut-files-to-clipboard-in-c-sharp
                    StringCollection files = new StringCollection();
                    files.Add(path);

                    byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
                    MemoryStream dropEffect = new MemoryStream();
                    dropEffect.Write(moveEffect, 0, moveEffect.Length);

                    DataObject data = new DataObject();
                    data.SetFileDropList(files);
                    data.SetData("Preferred DropEffect", dropEffect);

                    Clipboard.Clear();
                    Clipboard.SetDataObject(data, true);
                }
                //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "File copied into a clipboard", BalloonIcon.Info);
            }catch(Exception _ex) {

            }
        }

        public static void MoveFileToClipboard(string path) {
            if(File.Exists(path)==true) {
                // https://stackoverflow.com/questions/2077981/cut-files-to-clipboard-in-c-sharp
                StringCollection files = new StringCollection();
                files.Add(path);

                byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
                MemoryStream dropEffect = new MemoryStream();
                dropEffect.Write(moveEffect, 0, moveEffect.Length);

                DataObject data = new DataObject();
                data.SetFileDropList(files);
                data.SetData("Preferred DropEffect", dropEffect);

                Clipboard.Clear();
                Clipboard.SetDataObject(data, true);
            }
        }

        private void ExecutedCustomCommand_DeleteFile(object sender, ExecutedRoutedEventArgs e) {
            bool res = false;
            try {
                StringCollection paths = new StringCollection();
                String path = (string)e.Parameter;
                if(File.Exists(path)==true) {
                    File.Delete(path);
                    res = true;
                }
                //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "File copied into a clipboard", BalloonIcon.Info);
            }catch(Exception _ex) {

            }
        }

        public static void copy_clipboard_with_popup(string text) {
            System.Windows.Forms.Clipboard.SetText(text);
            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Message copied into a clipboard", BalloonIcon.Info);
        }

        // Пользовательская комманда отображения окна с текстовым содержанием:
        public static RoutedCommand CustomRoutedCommand_ShowMessage = new RoutedCommand();
        private void ExecutedCustomCommand_ShowMessage(object sender, ExecutedRoutedEventArgs e) {
            string text = (string)e.Parameter;
            ShowMessage(text);
        }

        public static void ShowMessage(string text) {
            DialogListingDeletedFiles window = new DialogListingDeletedFiles();
            window.txtListFiles.Text = text;
            window.Show();
            DLLImport.ActivateWindow(new WindowInteropHelper(window).Handle);
        }

        // Пользовательская комманда открытия диалога стёртых объектов:
        public static RoutedCommand CustomRoutedCommand_DialogListingDeletedFiles = new RoutedCommand();
        private void ExecutedCustomCommand_DialogListingDeletedFiles(object sender, ExecutedRoutedEventArgs e) {
            List<Dictionary<string, string>> list_files = (List<Dictionary<string, string>>)e.Parameter;
            DialogListingDeletedFiles(list_files);
        }

        private static void DialogListingDeletedFiles(List<Dictionary<string, string>> list_files) {
            List<string> txt = new List<string>();
            int i = 0;
            txt.Add(String.Format("{0})\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", "N", "TimeCreated", "ObjectType", "SubjectDomainName", "SubjectUserName", "ObjectName", "ProcessName"));
            foreach(Dictionary<string, string> rec in list_files) {
                string ObjectType = null;
                rec.TryGetValue("ObjectType", out ObjectType);
                string SubjectUserName = null;
                rec.TryGetValue("SubjectUserName", out SubjectUserName);
                string SubjectDomainName = null;
                rec.TryGetValue("SubjectDomainName", out SubjectDomainName);
                string ObjectName = null;
                rec.TryGetValue("ObjectName", out ObjectName);
                string ProcessName = null;
                rec.TryGetValue("ProcessName", out ProcessName);
                string TimeCreated = null;
                rec.TryGetValue("TimeCreated", out TimeCreated);
                i++;

                //string txt_rec = String.Format("{0})\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", i, TimeCreated, ObjectType, SubjectDomainName, SubjectUserName, ObjectName, ProcessName);
                string txt_rec = $"{i})\t{TimeCreated}\t{ObjectType}\t{SubjectDomainName}\t{SubjectUserName}\t{ObjectName}\t{ProcessName}";
                txt.Add(txt_rec);
            }

            DialogListingDeletedFiles window = new DialogListingDeletedFiles();

            window.txtListFiles.Text = String.Join("\n", txt.ToArray());
            window.Show();
        }


        // Пользовательская команда:
        public static RoutedCommand CustomRoutedCommand = new RoutedCommand();
        private void ExecutedCustomCommand(object sender, ExecutedRoutedEventArgs e) {
            Path_ObjectType obj = (Path_ObjectType)e.Parameter;
            gotoPathByWindowsExplorer(obj.path, obj.wType);
        }

        public static void gotoPathByWindowsExplorer(string _path, WatchingObjectType wType) {
            if(wType==WatchingObjectType.File) {
                Process.Start("explorer.exe", "/select,\"" + _path + "\"");
            } else {
                Process.Start("explorer.exe", "\"" + _path + "\"");
            }
        }

        // CanExecuteRoutedEventHandler that only returns true if the source is a control.
        private void CanExecuteCustomCommand(object sender, CanExecuteRoutedEventArgs e) {
            Control target = e.Source as Control;
            e.CanExecute = true;
        }

        static System.Timers.Timer reloadCustomMenuItems_timer = null;
        protected static void reloadCustomMenuItems() {
            // Повесить таймер на 0.1 секунду, чтобы перестраивать меню не чаще 10 раз в сек (бывает, что события валятся сотнями и нет необходимости их все выводить синхронно).
            if(reloadCustomMenuItems_timer == null) {
                reloadCustomMenuItems_timer = new System.Timers.Timer(100);
                reloadCustomMenuItems_timer.Elapsed += ReloadCustomMenuItems_timer_Elapsed;
                ;
                reloadCustomMenuItems_timer.AutoReset = false;
                reloadCustomMenuItems_timer.Enabled = true;
            }
        }

        private static void ReloadCustomMenuItems_timer_Elapsed(object sender, ElapsedEventArgs e) {
            _reloadCustomMenuItems();
            reloadCustomMenuItems_timer = null;
        }

        // Пересоздать пункты контекстного меню, которые указывают на файлы.
        protected static void _reloadCustomMenuItems() {
            if(Application.Current != null) {
                Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                {
                    string[] lst_items_name = { "menu_separator", "menu_settings", "menu_exit" };

                    lock(_notifyIcon.ContextMenu.Items) {
                        for(int i = _notifyIcon.ContextMenu.Items.Count - 1; i >= 0; i--) {
                            if(Array.IndexOf(lst_items_name, ((Control)_notifyIcon.ContextMenu.Items.GetItemAt(i)).Name) < 0) {
                                _notifyIcon.ContextMenu.Items.RemoveAt(i);
                            }
                        }

                        // Максимальное количество файлов в списке должно быть не больше указанного максимального значения:
                        while(stackPaths.Count > Settings.log_contextmenu_size) {
                            // Удалить самый старый элемент из списка путей и из меню
                            //_MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).First();
                            MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.event_date_time).First();
                            _notifyIcon.ContextMenu.Items.Remove(first_menuItemData.mi);
                            stackPaths.Remove(first_menuItemData);
                        }

                        List<MenuItemData> _stackPaths = new List<MenuItemData>();
                        // Преобразовать сокращённые элементы меню в полные:
                        foreach(MenuItemData md in stackPaths) {
                            MenuItemData _md = md;

                            // Если запись предназначена для файла, то сделать меню полным:
                            if(md is MenuItemData_CCRD) {
                                MenuItemData_CCRD md_ccrd = ((MenuItemData_CCRD)md);
                                _md = new MenuItemData_CCRD(md_ccrd.e, md_ccrd.wType, md_ccrd.event_date_time, false);
                            }
                            _stackPaths.Add(_md);
                        }
                        stackPaths = _stackPaths;



                        // Заполнить новые пункты:
#if (_Evgeniy)
                        _MenuItemData last_menuItemData_file = null;
                        _MenuItemData last_menuItemData_folder = null;
                        //foreach (_MenuItemData obj in stackPaths.OrderBy(d => d.index).ToArray())
                        foreach (WatchingObjectType wType in new WatchingObjectType[] { WatchingObjectType.Folder, WatchingObjectType.File, }) {
                            foreach (_MenuItemData obj in stackPaths.Where(d => d.wType == wType).OrderBy(d => d.date_time).ToArray()) {
                            if (obj is MenuItemData_CCRD) {
                                    ((MenuItemData_CCRD)obj).CheckPath();
                                    obj.mi.FontWeight = FontWeights.Normal;
                                    _notifyIcon.ContextMenu.Items.Insert(0, obj.mi);
                                } else {
                                    obj.mi.FontWeight = FontWeights.Normal;
                                    _notifyIcon.ContextMenu.Items.Insert(0, obj.mi);
                                }
                                if (wType == WatchingObjectType.Folder) {
                                    obj.mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 232, 143, 28));
                                }
                                if (wType == WatchingObjectType.File) {
                                    last_menuItemData_file = obj;
                                }
                                if (wType == WatchingObjectType.Folder) {
                                    last_menuItemData_folder = obj;
                                }
                            }
                        }
                        if (last_menuItemData_file != null) {
                            last_menuItemData_file.mi.FontWeight = FontWeights.Bold;
                        }
                        if (last_menuItemData_folder != null) {
                            last_menuItemData_folder.mi.FontWeight = FontWeights.Bold;
                        }
#else
                        MenuItemData last_menuItemData = null;
                        foreach(MenuItemData obj in stackPaths.OrderBy(d => d.event_date_time).ToArray()) {
                            WatchingObjectType wType = obj.wType;
                            if(obj is MenuItemData_CCRD) {
                                ((MenuItemData_CCRD)obj).CheckPath();
                                obj.mi.FontWeight = FontWeights.Normal;
                                _notifyIcon.ContextMenu.Items.Insert(0, obj.mi);
                            } else {
                                obj.mi.FontWeight = FontWeights.Normal;
                                _notifyIcon.ContextMenu.Items.Insert(0, obj.mi);
                            }
                            if(wType == WatchingObjectType.Folder) {
                                obj.mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 232, 143, 28));
                            }
                            last_menuItemData = obj;
                        }
                        if(last_menuItemData != null) {
                            last_menuItemData.mi.FontWeight = FontWeights.Bold;
                        }
#endif
                    }
                });
            }
        }

        private static TaskbarIcon _notifyIcon = null;
        public static TaskbarIcon NotifyIcon {
            get {
                return _notifyIcon;
            }
        }

        protected override void OnStartup(StartupEventArgs e) {
            Process proc = Process.GetCurrentProcess();
            int count = Process.GetProcessesByName(proc.ProcessName).Where(p => p.ProcessName == proc.ProcessName).Count();
            if(count > 1) {
                MessageBox.Show("Already an instance is running...");
                App.Current.Shutdown();
                return;
            }

            IsUserAdministrator();

            base.OnStartup(e);

            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

            /*
            not fired. Look: https://stackoverflow.com/questions/5139691/wpf-scroll-without-focus
            _notifyIcon.MouseWheel+= (sender, args) => {
                _notifyIcon.ContextMenu.IsOpen = true;
            };
            _notifyIcon.ContextMenu.MouseWheel+=(sender, args) => {
                int x=0;
            };
            */

            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand, ExecutedCustomCommand, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_ExecuteFile, ExecutedCustomCommand_ExecuteFile, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_CopyTextToClipboard, ExecutedCustomCommand_CopyTextToClipboard, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_CopyFileToClipboard, ExecutedCustomCommand_CopyFileToClipboard, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_MoveFileToClipboard, ExecutedCustomCommand_MoveFileToClipboard, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_DeleteFile, ExecutedCustomCommand_DeleteFile, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_DialogListingDeletedFiles, ExecutedCustomCommand_DialogListingDeletedFiles, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_ShowMessage, ExecutedCustomCommand_ShowMessage, CanExecuteCustomCommand));

#if (_Evgeniy)
            foreach(Control _mi in _notifyIcon.ContextMenu.Items)
            {
                if( _mi.Name == "menu_settings")
                {
                    _notifyIcon.ContextMenu.Items.Remove(_mi);
                    break;
                }
            }
#endif
            initApplication(e);
        }

        /// <summary>
        /// Получить путь к файлу настроек программы, который лежит в том же каталоге, где и программа.
        /// <br/>
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static String getSettingsFilePath(string ext) {
            String iniFilePath = null;
            string exe_file = typeof(FileChangesWatcher.App).Assembly.Location; // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
            iniFilePath = System.IO.Path.ChangeExtension(exe_file, ext);
            return iniFilePath;
        }

        static string history_file_name = null;
        static List<string> history_list_items = new List<string>();
        // Файл, где хранятся последние файлы, выведенные в контекстное меню:
        /// <summary>
        /// file to keep files in context menu (to show them on start next time)
        /// </summary>
        /// <returns></returns>
        public static String getHistoryFileName() {
            if(history_file_name == null) {
                string _history_file_name = null;
                string exe_file = typeof(FileChangesWatcher.App).Assembly.Location; // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
                                                                                    //iniFilePath = Process.GetCurrentProcess().MainModule.FileName;
                                                                                    //_history_file_name = System.IO.Path.GetDirectoryName(exe_file) + "\\" + System.IO.Path.GetFileNameWithoutExtension(exe_file) + ".last_files.txt";
                _history_file_name = System.IO.Path.ChangeExtension(exe_file, ".last_files.txt");
                history_file_name = _history_file_name;
            }
            return history_file_name;
        }

        public static String getExeFilePath() {
            // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
            String exeFilePath = typeof(FileChangesWatcher.App).Assembly.Location;
            //exeFilePath = Process.GetCurrentProcess().MainModule.FileName;
            return exeFilePath;
        }

        public static String getLogFileName() {
            DateTime n = DateTime.Now;
            //string year = n.Year.ToString();
            //string month = (n.Month < 10 ? "0" : "") + n.Month.ToString();
            //string day = (n.Day< 10 ? "0" : "") + n.Day.ToString();
            //string str_path = Settings.string_log_path + "\\" + Settings.string_log_file_prefix + ""+year+"."+month+"."+day+".log";
            //string str_path = System.IO.Path.Combine(Settings.string_log_path, Settings.string_log_file_prefix, $"{year}.{month}.{day}.log");
            string str_path = System.IO.Path.Combine(Settings.string_log_path, Settings.string_log_file_prefix, $"{n.ToString("yyyy.MM.dd")}.log");
            return str_path;
        }


        // Используется для исключения дубликатов в работе FileChangesWatcher
        // http://weblogs.asp.net/ashben/31773
        class struct_log_record {
            public DateTime dt;
            public FileSystemEventArgs fsea;
            public WatchingObjectType wType;
            public struct_log_record(DateTime _dt, FileSystemEventArgs _fsea, WatchingObjectType _wType) {
                dt = _dt;
                fsea = _fsea;
                wType = _wType;
            }

            public override string ToString() {
                string str_record = null;
                string date_time = dt.ToString("yyyy.MM.dd HH:mm:ss.fff");
                if(fsea.ChangeType == WatcherChangeTypes.Renamed) {
                    RenamedEventArgs _fsea = (RenamedEventArgs)fsea;
                    string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _fsea, "oldFullPath");
                    str_record = String.Format($"\r\n{date_time}\t{_fsea.ChangeType.ToString()}\t{wType.ToString()}\t{_fsea.FullPath}\t{OldFullPath}");
                } else {
                    str_record = String.Format($"\r\n{date_time}\t{fsea.ChangeType.ToString()}\t{wType.ToString()}\t{fsea.FullPath}");
                }
                return str_record;
            }

            public string ToLogString() {
                string str_record = null;
                string date_time = dt.Ticks.ToString(); //.ToString("yyyy.MM.dd HH:mm:ss.fff");
                if(fsea.ChangeType == WatcherChangeTypes.Renamed) {
                    RenamedEventArgs _fsea = (RenamedEventArgs)fsea;
                    string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _fsea, "oldFullPath");
                    str_record = $"\r\n{date_time}\t{_fsea.ChangeType.ToString()}\t{wType.ToString()}\t{_fsea.FullPath}\t{OldFullPath}";
                } else {
                    str_record = $"\r\n{date_time}\t{fsea.ChangeType.ToString()}\t{wType.ToString()}\t{fsea.FullPath}";
                }
                return str_record;
            }

            // Сравнить объекты. Если значения данных (кроме времени) в объектах одинаковые, то выдать true.
            public bool ObjectAreSame(struct_log_record o) {
                bool eq = true;
                if(o==null) {
                    eq = false;
                }
                if(eq == true) {
                    if(fsea.ChangeType==o.fsea.ChangeType) {
                        if(fsea.ChangeType == WatcherChangeTypes.Renamed) {
                            RenamedEventArgs _fsea = (RenamedEventArgs)fsea;
                            RenamedEventArgs _o_fsea = (RenamedEventArgs)o.fsea;
                            string _o_fsea_OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _fsea, "oldFullPath");
                            if(_fsea.FullPath==_o_fsea.FullPath && _fsea.OldFullPath == _o_fsea_OldFullPath) {
                            } else {
                                eq = false;
                            }
                        } else {
                            if(fsea.FullPath == o.fsea.FullPath) {
                            } else {
                                eq = false;
                            }
                        }

                    } else {
                        eq = false;
                    }

                }
                return eq;
            }
        }

        // Таймер, который периодически сбрасывает файловый кеш лога через указанное количество секунд. После последней записи.
        // Если за указанный интервал времени произошла новая запись, то отодвинуть запуск таймена ещё на этот интервал:

        static System.Timers.Timer flush_buffer_timer = null;

        // Блокировщик write_log для другого потока:
        private readonly static object lock_write_log = new object();  // https://www.codeproject.com/Tips/758494/Smarter-than-lock-Cleaner-than-TryEnter "You should always lock on a private readonly object"

        // Имя файла лога, в который была сделана последняя запись:
        static string string_log_file_name = "";
        // Поток, в который была сделана последняя запись (нужно закрывать при автоматическом изменении string_log_file_name)
        static StreamWriter sw_log_file = null; // Закрывается на выходе приложения вместе с отключением иконки.
        static struct_log_record last_log_record = null; // Последняя запись, которая записывается в файл. Если это null, то запись является первой после запуска программы и её не надо записывать
        public static void write_log(DateTime dt, FileSystemEventArgs _e, WatchingObjectType wType) {
            lock(lock_write_log) {
                if(Settings.bool_log == false) {
                    return;
                }

                struct_log_record new_log_record = null;
                if(_e != null) {
                    new_log_record = new struct_log_record(dt, _e, wType);
                }

                if(new_log_record == null) {

                }

                string str_file_path = getLogFileName();

                // Если имя файла, в который была сделана предыдущая запись отличается от полученного, то нужно закрыть поток и открыть новый для записи
                // Это так же работает и для начала работы программы, когда запись ещё не производилась.
                if(string_log_file_name != str_file_path) {
                    if(sw_log_file == null) {
                    } else {
                        // Не забыть записать последнюю запись из буфера, если она отличается от новой записи или время между записями больше или равно одной секунды
                        if(last_log_record != null) {
                            if(last_log_record.ObjectAreSame(new_log_record) == false) {
                                sw_log_file.Write(last_log_record.ToString());
                            } else {
                                if((new_log_record.dt - last_log_record.dt).TotalSeconds >= 1) {
                                    sw_log_file.Write(last_log_record.ToString());
                                }
                            }
                            last_log_record = null;
                        } else {

                        }
                        sw_log_file.Flush();
                        sw_log_file.Close();
                    }
                    // Создать новый файл для записи:
                    // Оптимизация записи в файл: http://stackoverflow.com/questions/16191591/what-consumes-less-resources-and-is-faster-file-appendtext-or-file-writealltext
                    const int BufferSize = 65536;  // 64 Kilobytes
                    string_log_file_name = str_file_path;
                    sw_log_file = new StreamWriter(string_log_file_name, true, Encoding.UTF8, BufferSize);
                }

                if(last_log_record != null) {
                    if(last_log_record.ObjectAreSame(new_log_record) == false || (new_log_record.dt - last_log_record.dt).TotalSeconds >= 1) {
                        sw_log_file.Write(new_log_record.ToString());  // TODO: Тут однажды выскочило исключение, что нельзя писать в закрытый файл (при разархивации большого архива). Надо бы сделать проверку. Может быть сработал таймер ниже. (Больше одной секунды).
                    }
                } else {
                    sw_log_file.Write(new_log_record.ToString());
                }

                last_log_record = new_log_record;

                // Повесить таймер на одну секунду, чтобы сохранить буффер только при отсутствии нагрузки на файловые операции.
                if(flush_buffer_timer != null) {
                    flush_buffer_timer.Stop();
                }
                flush_buffer_timer = new System.Timers.Timer(1000);
                flush_buffer_timer.AutoReset = false;
                flush_buffer_timer.Elapsed += //Flush_buffer_timer_Elapsed;
                    (sender, e) => {
                        // https://stackoverflow.com/questions/2416793/why-is-lock-much-slower-than-monitor-tryenter

                        //if(Monitor.TryEnter(lock_write_log)) 
                        lock(lock_write_log) {
                            if(sw_log_file != null) {
                                sw_log_file.Flush();
                                sw_log_file.Close();
                                sw_log_file = null;
                                string_log_file_name = "";
                            }
                            flush_buffer_timer = null;
                        }
                    };
                flush_buffer_timer.Enabled = true;
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FlushFileBuffers(IntPtr handle);
        private static void Flush_buffer_timer_Elapsed(object sender, ElapsedEventArgs e) {
            lock(lock_write_log) {
                if(sw_log_file != null) {
                    sw_log_file.Flush();
                    sw_log_file.Close();
                    sw_log_file = null;
                    string_log_file_name = "";
                }
                flush_buffer_timer = null;
            }
        }

        // Записать историю: TODO: оставить на будущее. Сейчас пока не нужна.
        private static void write_history_file() {
            const int BufferSize = 65536;  // 64 Kilobytes
            string string_history_file_name = getHistoryFileName();
            StreamWriter sw_history_file = new StreamWriter(string_history_file_name, false, Encoding.UTF8, BufferSize);
            sw_history_file.Write(String.Join("\n", history_list_items));
            sw_history_file.Flush();
            sw_history_file.Close();
            history_list_items.Clear();
        }

        public static bool IsAppInRegestry() {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            String appName = System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
            if(rk.GetValue(appName) != null) {
                return true;
            } else {
                return false;
            }
        }

        public static void setAutostart() {
            try {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                string appName = System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
                rk.SetValue(appName, Process.GetCurrentProcess().MainModule.FileName);
            } catch(Exception e) {
                MessageBox.Show(e.Message);
            }
        }

        public static void resetAutostart() {
            try {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                string appName = System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
                rk.DeleteValue(appName, false);
            } catch(Exception e) {
                MessageBox.Show(e.Message);
            }
        }


        public static void initApplication(StartupEventArgs e) {
            StringBuilder init_text_message = new StringBuilder();

            // Сбросить всех наблюдателей, установленных ранее (при перезапуске настроек в уже запущенной программе):
            foreach(FileSystemWatcher watcher in list_watcher.ToArray()) {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= new FileSystemEventHandler(OnChanged_file);
                watcher.Changed -= new FileSystemEventHandler(OnChanged_folder);

                watcher.Created -= new FileSystemEventHandler(OnChanged_file);
                watcher.Created -= new FileSystemEventHandler(OnChanged_folder);

                watcher.Deleted -= new FileSystemEventHandler(OnChanged_file);
                watcher.Deleted -= new FileSystemEventHandler(OnChanged_folder);

                watcher.Renamed -= new RenamedEventHandler(OnChanged_file);
                watcher.Renamed -= new RenamedEventHandler(OnChanged_folder);

                list_watcher.Remove(watcher);
                watcher.Dispose();
            }

            // Проверить существование файла настроек. Если его нет, то создать его:
            string jsonFilePath = getSettingsFilePath(".js");
            JObject jsonData = new JObject();

            if(LongFile.Exists(jsonFilePath) == false) {
                jsonData["General"]                 = new JObject();
                jsonData["Extensions"]              = new JArray();
                jsonData["UserExtensions"]          = new JArray();
                jsonData["FoldersForWatch"]         = new JArray();
                jsonData["FoldersForExceptions"]    = new JArray();
                jsonData["FileNamesExceptions"]     = new JArray();
                jsonData["FoldersForWatch"]         = new JArray();
                jsonData["FoldersForExceptions"]    = new JArray();

                // Количество файлов, видимое в меню:
                jsonData["General"]["log_contextmenu_size"] = 10;
                // Отображать уведомления (всплывающие балоны):
                jsonData["General"]["display_notifications"] = true;
                // Активация логирования в файл:
                jsonData["General"]["log"] = true;
                // Путь файлов логирования:
                jsonData["General"]["log_path"] = ".";
                // Префикс файлов логирования. Если программа будет записывать файлы логов на общий сетевой каталог, то нужно, чтобы такие файлы не пересекались с файлами других компьюеров
                jsonData["General"]["log_file_prefix"] = "";

                {
                    // Список расширений, по-умолчанию, за которыми надо "следить". Из них потом будут регулярки:
                    JArray jextensions = ((JArray)jsonData["Extensions"]);
                    jextensions.Add(new JObject(new JProperty("archivers", ".tar|.jar|.zip|.bzip2|.gz|.tgz|.7z")));
                    jextensions.Add(new JObject(new JProperty("officeexcel", ".xls|.xlt|.xlm|.xlsx|.xlsm|.xltx|.xltm|.xlsb|.xla|.xlam|.xll|.xlw")));
                    jextensions.Add(new JObject(new JProperty("officepowerpoint", ".ppt|.pot|.pptx|.pptm|.potx|.potm|.ppam|.ppsx|.ppsm|.sldx|.sldm")));
                    jextensions.Add(new JObject(new JProperty("officevisio", ".vsd|.vsdx|.vdx|.vsx|.vtx|.vsl|.vsdm")));
                    jextensions.Add(new JObject(new JProperty("autodesk", ".dwg|.dxf|.dwf|.dwt|.dxb|.lsp|.dcl")));
                    jextensions.Add(new JObject(new JProperty("extensions02", ".gif|.png|.jpeg|.jpg|.tiff|.tif|.bmp")));
                    jextensions.Add(new JObject(new JProperty("extensions03", ".cs|.xaml|.config|.ico")));
                    jextensions.Add(new JObject(new JProperty("extensions04", ".gitignore|.md")));
                    jextensions.Add(new JObject(new JProperty("extensions05", ".msg|.ini")));
                    jextensions.Add(".pdf|.html|.xhtml|.txt|.mp3|.aiff|.au|.midi|.wav|.pst|.xml|.java|.js");
                }

                {
                    JArray jUserExtensions = ((JArray)jsonData["UserExtensions"]);
                    jUserExtensions.Add(new JObject(new JProperty("extensions01", ".json|.md|.js")));
                    jUserExtensions.Add(new JObject(new JProperty("officeword", ".doc|.docx|.docm|.dotx|.dotm|.rtf")));
                    jUserExtensions.Add(new JObject(new JProperty("visual_studio", ".csproj|.sln")));
                    jUserExtensions.Add(new JObject(new JProperty("blender", ".blend[0-9]*|.py")));
                    jUserExtensions.Add(new JObject(new JProperty("other", ".svg|.xaml|.[0-9]+|.ifc|.obj|.exe|.com|.dll")));
                    jUserExtensions.Add(new JObject(new JProperty("other1", ".bmp|.gif|.jpg|.jpeg|.tiff|.tif|.js|.cs|.java|.exe|.dwg|.dxf|.rar|.mp4|.avi|.png")));
                }

                {
                    // Список каталогов, за которыми надо следить:
                    JArray jFoldersForWatch = ((JArray)jsonData["FoldersForWatch"]);
                    jFoldersForWatch.Add(new JObject(new JProperty("folder01", @"D:\")));
                    jFoldersForWatch.Add(@"E:\Docs");
                    jFoldersForWatch.Add(@"F:\");
                }

                {
                    // Список каталогов, которые надо исключить из "слежения" (просто будут сравниваться начала имён файлов):
                    JArray jFoldersForExceptions = ((JArray)jsonData["FoldersForExceptions"]);
                    jFoldersForExceptions.Add(new JObject(new JProperty("folder01", "D:\\temp")));
                }

                {
                    JArray jFileNamesExceptions = ((JArray)jsonData["FileNamesExceptions"]);
                    jFileNamesExceptions.Add("~$");
                }
                File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented));
            } else {
                //_notifyIcon.ToolTipText = "FileChangesWatcher. Right-click for menu";
                _notifyIcon.ToolTipText = $"FileChangesWatcher {Assembly.GetExecutingAssembly().GetName().Version.ToString()}. Right-click for menu";
                try {
                    jsonData = JObject.Parse(File.ReadAllText(jsonFilePath));
                    _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Icons/FileChangesWatcher.ico", UriKind.Absolute));
                } catch(Exception ex) {
                    appendLogToDictionary("" + ex.Message + "", BalloonIcon.Error);
                    _notifyIcon.ToolTipText = "FileChangesWatcher not working. Error in setting file. Open settings in menu.";
                    _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Icons/FileChangesWatcherDisable.ico", UriKind.Absolute));
                    return;
                }
            }

            try {
                // Определить количество пунктов подменю:
                Settings.log_contextmenu_size = Convert.ToInt32(jsonData["General"]["log_contextmenu_size"]);
            } catch(Exception _ex) {
                //Console.WriteLine("Ошибка преобразования значения log_contextmenu_size в число. \n"+_ex.ToString());
                Console.WriteLine("Error convert log_contextmenu_size into bool. Check it is exists and has true/false value: \n"+_ex.ToString());
            }

            try {
                // Активировать ли систему вывода уведомлений (true/false):
                Settings.bool_display_notifications = Convert.ToBoolean(jsonData["General"]["display_notifications"]);
            } catch(Exception ex) {
                //Console.WriteLine("Ошибка преобразования значения display_notifications в bool. Либо не указано, либо указано не true/false " + ex.Message);
                Console.WriteLine("Error convert display_notifications into bool. Check it is exists and has true/false value" + ex.Message);
            }

            try {
                // Активировать ли систему логирования (true/false):
                Settings.bool_log = Convert.ToBoolean(jsonData["General"]["log"]);
            } catch(Exception ex) {
                //Console.WriteLine("Ошибка преобразования значения log в bool. Либо не указано, либо указано не true/false "+ex.Message);
                Console.WriteLine("Error convert log into bool. Check it is exists and has true/false value "+ex.Message);
            }

            try {
                // Каталог, куда складывать файлы логов:
                Settings.string_log_path = Convert.ToString(jsonData["General"]["log_path"]);
                if(Settings.string_log_path ==".") {
                    Settings.string_log_path = getExeFilePath();
                    Settings.string_log_path = Path.GetDirectoryName(Settings.string_log_path);
                } else {
                    if(LongDirectory.Exists(Settings.string_log_path)) {
                    } else {
                        Settings.string_log_path = getExeFilePath();
                        Settings.string_log_path = Path.GetDirectoryName(Settings.string_log_path);
                    }
                }
            } catch(Exception ex) {
                //Console.WriteLine("Неверный параметр log_path. " + ex.Message);
                Console.WriteLine("Error parameter log_path. " + ex.Message);
            }

            try {
                // Префикс файла логов (чтобы не путать с файлами других программ, которые могут писать в общую сетевую папку):
                Settings.string_log_file_prefix = Convert.ToString(jsonData["General"]["log_file_prefix"]);
            } catch(Exception ex) {
                //Console.WriteLine("Ошибка чтения параметра log_file_prefix: " + ex.Message);
                Console.WriteLine("Error param log_file_prefix: " + ex.Message);
            }

            _re_extensions = getExtensionsRegEx(jsonData, new string[] { "Extensions", "UserExtensions" });
            _re_user_extensions = getExtensionsRegEx(jsonData, new string[] { "UserExtensions" });


            // Определить список каталогов, за которыми надо наблюдать:
            Settings.list_folders_for_watch = new List<String>();
            {
                JArray jFoldersForWatch = (JArray)jsonData["FoldersForWatch"];
                Settings.list_folders_for_watch = getListOfInnerValues(jsonData["FoldersForWatch"]);
            }

            if(e != null) {
                for(int i = 0; i <= e.Args.Length - 1; i++) {
                    String folder = e.Args[i];
                    if(folder.Length > 0 && Settings.list_folders_for_watch.Contains(folder) == false && LongDirectory.Exists(folder)) {
                        Settings.list_folders_for_watch.Add(folder);
                    }
                }
            }

            {
                List<string> folders_for_remove = new List<string>();
                foreach(string folder in Settings.list_folders_for_watch) {
                    if((folder.Length > 0 && LongDirectory.Exists(folder)) == false) {
                        folders_for_remove.Add(folder);
                    }
                }
                foreach(string folder in folders_for_remove) {
                    Settings.list_folders_for_watch.Remove(folder);
                }
            }

            // Список каталогов с исключениями:
            Settings.arr_folders_for_exceptions = getListOfInnerValues(jsonData["FoldersForExceptions"]);

            // Список файлов с исключениями:
            Settings.arr_files_for_exceptions = getListOfInnerValues(jsonData["FileNamesExceptions"]);

            if(Settings.list_folders_for_watch.Count >= 1) {
                init_text_message.Append("\n");
                init_text_message.Append(setWatcherForFolderAndSubFolders(Settings.list_folders_for_watch.ToArray()));
            } else {
                appendLogToDictionary("No watching for folders. Set folders correctly.", BalloonIcon.Info);
            }

            // Прочитать логи от предыдущего сеанса и добавить их в контекстное меню:
            //string history_file_name = - всё таки у меня для этого файла есть глобальная переменная. TODO - проверить и удалить эту строку.
            getHistoryFileName();
            if(LongFile.Exists(history_file_name) == true) {
                string[] strings = LongFile.ReadAllLines(history_file_name);
                foreach(string str in strings) {
                    if(str.Length > 0) {
                        string[] prms = str.Split('\t');
                        if(prms.Count()==4 || prms.Count() == 5) {
                            string str_unix_time = prms[0];
                            string str_ChangesType = prms[1];
                            string str_wType = prms[2];
                            string FullPath = prms[3];
                            // Проверить, что файл или каталог существует. Нельзя восстанавливать в контекстное меню несуществующие объекты.
                            if(!(LongFile.Exists(FullPath)==true || LongDirectory.Exists(FullPath)==true)) {
                                continue;
                            }
                            long unix_time = Convert.ToInt64(str_unix_time);
                            DateTime dt = UnixTimestampToDateTime(unix_time);
                            WatcherChangeTypes wct = (WatcherChangeTypes)Enum.Parse(typeof(WatcherChangeTypes), str_ChangesType);
                            WatchingObjectType wType = (WatchingObjectType)Enum.Parse(typeof(WatchingObjectType), str_wType);

                            MenuItemData_CCRD ccrd = null;
                            if(str_ChangesType == "Renamed") {
                                string OldFullPath = prms[4];
                                RenamedEventArgs _re = new RenamedEventArgs(wct, LongDirectory.GetDirectoryName(FullPath), Path.GetFileName(FullPath), Path.GetFileName(OldFullPath));
                                ccrd = new MenuItemData_CCRD(_re, wType, dt);
                            } else {
                                FileSystemEventArgs _e = new FileSystemEventArgs(wct, LongDirectory.GetDirectoryName(FullPath), Path.GetFileName(FullPath));
                                ccrd = new MenuItemData_CCRD(_e, wType, dt);
                            }
                            stackPaths.Add(ccrd);
                        }
                    }
                }
            }

            appendLogToDictionary("Initial settings.\n"+init_text_message.ToString(), BalloonIcon.Info);

            if(Settings.bool_display_notifications == true) {
                string text = $"Initial settings.\nVersion: {Assembly.GetExecutingAssembly().GetName().Version.ToString()}\n{init_text_message.ToString()}";
                TrayPopupMessage popup = new TrayPopupMessage(text, "Initial initialization", WatchingObjectType.File, App.NotifyIcon, null, TrayPopupMessage.ControlButtons.Clipboard_Text, TrayPopupMessage.Type.Text);
                //popup.MouseDown += (sender, args) => {
                //    if(App.NotifyIcon.CustomBalloon != null) {
                //        App.NotifyIcon.CustomBalloon.IsOpen = false;
                //    }
                //    //App.ShowMessage("Initial settings.\n" + init_text_message.ToString());
                //    App.ShowMessage(text);
                //};
                App.NotifyIcon.ShowCustomBalloon(popup, PopupAnimation.None, 4000);
            }
        }

        public static Regex getExtensionsRegEx(JObject data, string[] sections) {
            string _extensions = "";
            Regex re = new Regex("\\.");
            foreach(string section in sections) {
                if(data[section] != null) {
                    List<string> list = getListOfInnerValues(data[section]);

                    foreach(string folder in list) {
                        if(folder.Length > 0) {
                            if(_extensions.Length > 0) {
                                _extensions += "|";
                            }
                            _extensions += re.Replace(folder, "\\.");
                        }
                    }
                }
            }
            _extensions = @"$(?<=(" + _extensions + "))";  // Крутое решение по ускорению проверки расширений: http://stackoverflow.com/questions/2081555/testing-for-endswith-efficiently-with-a-regex?answertab=votes#tab-top
            _extensions = (new Regex("(\\|\\|)")).Replace(_extensions, "|");

            return new Regex(_extensions, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Получить список строк от вложенных значений потомков или от самих значений:
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sections"></param>
        /// <returns></returns>
        public static List<string> getListOfInnerValues(JToken data) {
            List<string> values = new List<String>();
            if(data.Type == JTokenType.String) {
                values.Add((string)data);
            } else if(data.Type == JTokenType.Object) {
                foreach(JProperty prop in ((JObject)data).Properties()) {
                    if(prop.Value.Type == JTokenType.String) {
                        string value = (string)prop.Value;
                        values.Add(value);
                    } else if(prop.Value.Type == JTokenType.Object) {
                        List<string> list = getListOfInnerValues((JObject)prop.Value);
                        values.AddRange(list);
                    } else if(prop.Value.Type == JTokenType.Array) {
                        List<string> list = getListOfInnerValues(prop.Value);
                        values.AddRange(list);
                    }
                }
            } else if(data.Type == JTokenType.Array) {
                JArray arr = (JArray)data;
                for(int i = 0; i<=arr.Count-1; i++) {
                    JToken prop = arr[i];
                    if(prop.Type == JTokenType.String) {
                        string value = (string)prop;
                        values.Add(value);
                    } else if(prop.Type == JTokenType.Object) {
                        List<string> list = getListOfInnerValues(prop);
                        values.AddRange(list);
                    } else if(prop.Type == JTokenType.Array) {
                        List<string> list = getListOfInnerValues(prop);
                        values.AddRange(list);
                    }
                }
            }
            return values;
        }

        static List<FileSystemWatcher> list_watcher = new List<FileSystemWatcher>();

        private static string setWatcherForFolderAndSubFolders(String[] _paths) {
            foreach(String _path in _paths) {
                // Отслеживание изменения в файловой системе:
                // Для файлов:
                {
                    FileSystemWatcher watcher = new FileSystemWatcher();
                    list_watcher.Add(watcher);
                    watcher.IncludeSubdirectories = true;

                    watcher.Path = _path;
                    /* Watch for changes in LastAccess and LastWrite times, and
                        the renaming of files or directories. */
                    // TODO: требуется разделить на файлы и каталоги: http://stackoverflow.com/questions/3336637/net-filesystemwatcher-was-it-a-file-or-a-directory?answertab=votes#tab-top
                    watcher.NotifyFilter = //NotifyFilters.LastAccess
                        NotifyFilters.FileName
                     |  NotifyFilters.LastWrite
                        //|  NotifyFilters.LastAccess // - не знаю как ловить - Теперь знаю. Нужно в реестре установить атрибут NtfsDisableLastAccessUpdate=0 и
                        //перезапустить комп. НО!!! Этот атрибут устанавливается в течении ЧАСА после доступа к файлу по NTFS и СУТОК при доступе к FAT!!! 
                        // http://serverfault.com/questions/351777/how-can-i-know-when-a-file-was-last-read-or-accessed-on-windows
                        // http://help.migraven.com/en/last-access-time-old-data/
                        // >> Resolution of file last accessed date is 1 hour for NTFS and 1 day for FAT.
                        //      http://www.febooti.com/products/filetweak/online-help/file-last-accessed-date.html

                        //| NotifyFilters.DirectoryName  // Оказывается, что если watcher наблюдает за файлами и каталогами одновременно, 
                        // то событие удаления каталога перекрывает события удаления файлов в его подкаталогах!
                        // https://social.msdn.microsoft.com/Forums/vstudio/en-US/b7612249-eb32-4005-9d6b-7f291c218326/filesystemwatcher-service-doesnot-detect-changes-when-folder-containing-the-file-is-deleted?forum=netfxbcl
                        ;
                    // Only watch text files.
                    // watcher.Filter = "*.*";
                    watcher.Filter = "";

                    // Add event handlers.

                    // The Changed event is raised when changes are made to the size, system attributes, 
                    // last write time, last access time, or security permissions of a file or directory 
                    // in the directory being monitored.
                    // https://msdn.microsoft.com/ru-ru/library/system.io.filesystemwatcher.changed%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
                    watcher.Changed += new FileSystemEventHandler(OnChanged_file);

                    // https://msdn.microsoft.com/ru-ru/library/system.io.filesystemwatcher.created(v=vs.110).aspx
                    // Some common occurrences, such as copying or moving a file or directory, do not correspond directly to an event, 
                    // but these occurrences do cause events to be raised.When you copy a file or directory, the system raises a Created 
                    // event in the directory to which the file was copied, if that directory is being watched.If the directory from which 
                    // you copied was being watched by another instance of FileSystemWatcher, no event would be raised.For example, you create 
                    // two instances of FileSystemWatcher.FileSystemWatcher1 is set to watch "C:\My Documents", and FileSystemWatcher2 is set 
                    // to watch "C:\Your Documents".If you copy a file from "My Documents" into "Your Documents", a Created event will be 
                    // raised by FileSystemWatcher2, but no event is raised for FileSystemWatcher1.Unlike copying, moving a file or 
                    // directory would raise two events.From the previous example, if you moved a file from "My Documents" to "Your Documents", 
                    // a Created event would be raised by FileSystemWatcher2 and a Deleted event would be raised by FileSystemWatcher1.

                    watcher.Created += new FileSystemEventHandler(OnChanged_file);
                    watcher.Deleted += new FileSystemEventHandler(OnChanged_file);
                    watcher.Renamed += new RenamedEventHandler(OnChanged_file);

                    // Begin watching:
                    watcher.EnableRaisingEvents = true;
                }
                // Для каталогов:
                {
                    FileSystemWatcher watcher = new FileSystemWatcher();
                    list_watcher.Add(watcher);
                    watcher.IncludeSubdirectories = true;

                    watcher.Path = _path;
                    /* Watch for changes in LastAccess and LastWrite times, and
                        the renaming of files or directories. */
                    // TODO: требуется разделить на файлы и каталоги: http://stackoverflow.com/questions/3336637/net-filesystemwatcher-was-it-a-file-or-a-directory?answertab=votes#tab-top
                    watcher.NotifyFilter =
                        NotifyFilters.DirectoryName
                        // | NotifyFilters.LastAccess
                        // |  NotifyFilters.LastWrite
                        // | NotifyFilters.FileName
                        ;
                    // Only watch text files.
                    // watcher.Filter = "*.*";
                    watcher.Filter = "";

                    // Add event handlers.
                    //watcher.Changed += new FileSystemEventHandler(OnChanged_folder);
                    watcher.Created += new FileSystemEventHandler(OnChanged_folder);
                    watcher.Deleted += new FileSystemEventHandler(OnChanged_folder);
                    watcher.Renamed += new RenamedEventHandler(OnChanged_folder);

                    // Begin watching:
                    watcher.EnableRaisingEvents = true;
                }
            }
            return "Watching folders: \n" + String.Join("\n", _paths.ToArray());
        }

        // Параметры для отлеживания изменений в файлах: ========================================

        /// <summary>
        /// Files extensions for log. It is sum of sections Extensions and UserExtensions in a setting file
        /// </summary>
        static Regex _re_extensions = null; // Расширения, которые логируются программой (являются суммой секций Extensions и UserExtensions )
        /// <summary>
        /// List of file extensions for output to application context menu (only extensions in section UserExtensions).
        /// </summary>
        static Regex _re_user_extensions = null;  // Расширения, которые выводятся юзеру (только секция UserExtensions).
        public static Regex re_extensions {
            get {
                return _re_extensions;
            }
        }

        public static class Settings {
            // Список каталогов, которые надо исключить из вывода:
            public static List<string> arr_folders_for_exceptions = null;
            public static List<string> arr_files_for_exceptions = null;
            // Количество файлов, которые видны в контекстном меню (читается из файла конфигурации):
            public static int log_contextmenu_size = 5;
            // Вывод на экран уведомления о файловых операциях (всплывающие балоны) (читается из файла конфигурации):
            public static bool bool_display_notifications = true;
            // Активировать ли логирование файловых операций в файл логов (читается из файла конфигурации):
            public static bool bool_log = false;
            // Путь файлов логирования. (Точка) - текущий каталог приложения (по-умолчанию). (Читается из файла конфигурации).
            public static string string_log_path = ".";
            // Префикс файлов логирования. Пустой по-умолчанию. (Читается из файла конфигурации)
            public static string string_log_file_prefix = "";

            // Список каталогов, за которыми наблюдает программа:
            public static List<string> list_folders_for_watch = null;

        }


        private static int menuitem_header_length = 30;

        public static void customballoon_close(object sender, ElapsedEventArgs e) {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                if(App.NotifyIcon.CustomBalloon != null) {
                    _notifyIcon.CustomBalloon.IsOpen = false;
                }
            });
            System.Timers.Timer temp = ((System.Timers.Timer)sender);
            temp.Stop();
        }

        private static void appendLogToDictionary(String logText, BalloonIcon _ballonIcon) {
            MenuItemData menuItemData = new MenuItemData_Log(_ballonIcon, logText, DateTime.Now);
            stackPaths.Add(menuItemData);
            reloadCustomMenuItems();
        }

        // Проверить, что путь удовлетворяет наблюдаемым правилам:
        public static bool check_path_is_in_watchable(string user_friendly_path, WatchingObjectType wType, Regex _extensions) // string str_ObjectType)
        {
            bool bool_is_path_watchable = true;

            // Проверить, а не начинается ли путь с исключения:
            foreach(string ex_path in Settings.arr_folders_for_exceptions) {
                if(user_friendly_path.StartsWith(ex_path) == true &&
                    (
                        user_friendly_path.Replace(ex_path, "") == "" ||
                        user_friendly_path.Replace(ex_path, "")[0] == Path.DirectorySeparatorChar
                    )
                ) {
                    bool_is_path_watchable = false;
                    break;
                }
            }

            if(bool_is_path_watchable == true) {
                // Проверить, а не начинается ли имя файла с исключения:
                foreach(string ex_start_with in Settings.arr_files_for_exceptions) {
                    if(Path.GetFileNameWithoutExtension(user_friendly_path).StartsWith(ex_start_with) == true) {
                        bool_is_path_watchable = false;
                        break;
                    }
                }
            }

            if(bool_is_path_watchable == true) {
                // Проверить, а не является ли объект файлом с наблюдаемым расширением:
                if(bool_is_path_watchable == true && wType == WatchingObjectType.File){
                    if(check_path_is_in_watchable_re(user_friendly_path, _extensions) == false) {
                        bool_is_path_watchable = false;
                    }
                }
            }

            return bool_is_path_watchable;
        }

        public static bool check_path_is_in_watchable_re(string user_friendly_path, Regex _extensions) {
            return _extensions.IsMatch(user_friendly_path);
        }

        // See с http://weblogs.asp.net/ashben/31773
        private static void OnChanged_file(object source, FileSystemEventArgs e) {
            try {
                DateTime dt = DateTime.Now;
                // Пропускать события файлов логов, иначе он только о себе и будет писать:
                // Skip a change event of log files
                if(e.FullPath == getLogFileName() || e.FullPath == getHistoryFileName()) {
                    return;
                }

                //Console.WriteLine($"{e.ChangeType}: {e.FullPath}");

                WatchingObjectType wType = WatchingObjectType.File;

                if(e.ChangeType == WatcherChangeTypes.Deleted) {
                    // При удалении файла я всегда знаю, что это именно файла
                    // Заодно и экономим, что при удалении не надо проверять тип объекта:
                    wType = WatchingObjectType.File;
                } else {
                    if(LongDirectory.Exists(e.FullPath)) {
                        wType = WatchingObjectType.Folder;
                    }
                }

                if(wType != WatchingObjectType.File) {
                    return;
                }

                if(e.ChangeType == WatcherChangeTypes.Changed && wType == WatchingObjectType.Folder) {
                    return;
                }
                if(check_path_is_in_watchable(e.FullPath, wType, _re_extensions) == false) {
                    return;
                }

                // Отображать этот элемент в меню при условии, что он в списке расширений пользователя:
                if(check_path_is_in_watchable(e.FullPath, wType, _re_user_extensions) == true) {
                    // Застолбить место в меню:
                    MenuItemData menuItemData = null;
                    if(Application.Current != null) {
                        Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                        {
                            try {
                                menuItemData = new MenuItemData_CCRD(e, wType, dt);
                                MenuItemData last_menuItemData = null;
                                if(App.stackPaths.Count > 0) {
                                    last_menuItemData = App.stackPaths.OrderBy(d => d.event_date_time.Ticks).Last();
                                }

                                if(last_menuItemData != null && last_menuItemData.wType != WatchingObjectType.Log) {
                                    MenuItemData_CCRD last_menuItemData_ccrd = ((MenuItemData_CCRD)last_menuItemData);
                                    // Бывает, что некоторое приложение так "увлекается", что генерирует некоторые события по нескольку раз.
                                    // Решил не показывать быстровозникающие события одинакового типа над одним объектом:
                                    // Restrict popup of several equal events to one object (file or folder) for 1 sec.
                                    if(    last_menuItemData_ccrd.e.ChangeType == e.ChangeType            // Типы совпадают?
                                        && last_menuItemData_ccrd.e.FullPath   == e.FullPath              // Путь совпадает?
                                        && last_menuItemData_ccrd.wType        == WatchingObjectType.File // Полное имя совпадает?
                                        ) {
                                        // Проверить разницу во времени с предыдущим последним событием и если она меньше одной секунды,
                                        // то удалить предыдущее событие:
                                        double totalMilliseconds = (DateTime.Now - last_menuItemData_ccrd.event_date_time).TotalMilliseconds;
                                        if(totalMilliseconds <= 1000.0) {
                                            stackPaths.Remove(last_menuItemData);
                                            _notifyIcon.ContextMenu.Items.Remove(last_menuItemData.mi);
                                            last_menuItemData = null;
                                        }
                                    }
                                }
                                stackPaths.Add(menuItemData);

                                if(Settings.bool_display_notifications == true){ // && elapsedSpan.TotalMilliseconds > 100) {
                                    BitmapImage bitmapImage = null;
                                    System.Windows.Controls.Image popup_image = null;
                                    string file_ext = Path.GetExtension(e.FullPath);
                                    // Кешировать иконки для файлов:
                                    if(MenuItemData.icons_map.TryGetValue(file_ext, out bitmapImage) == true && bitmapImage != null) {
                                        popup_image = new System.Windows.Controls.Image();
                                        popup_image.Source = bitmapImage;
                                    }
                                    FIFO_Stack_Events.Enqueue(new PopupInfo(e, menuItemData, popup_image) );
                                }
                            } catch(Exception _ex) {
                                _ex = _ex;
                            }
                        });
                    }
                }
                write_log(dt, e, wType);
                reloadCustomMenuItems();
            } catch(Exception _ex) {
                _ex = _ex;
            }
        }

        private static void OnChanged_folder(object source, FileSystemEventArgs e) {
            try {
                DateTime dt = DateTime.Now;
                WatchingObjectType wType = WatchingObjectType.Unknown;  // При удалении указывается этот тип, т.к. после удаления уже неизвестно, что это было.
                if(e.ChangeType == WatcherChangeTypes.Deleted) {
                    // При удалении каталога я всегда знаю, что это именно каталог
                    // Заодно и экономим, что при удалении не надо проверять тип объекта методом Exists (метод затратный):
                    wType = WatchingObjectType.Folder;
                } else {
                    wType = WatchingObjectType.File;
                    DateTime dt0 = DateTime.Now;
                    if(LongDirectory.Exists(e.FullPath)) {
                        wType = WatchingObjectType.Folder;
                    }
                    if(wType != WatchingObjectType.Folder) {
                        return;
                    }

                    if(e.ChangeType == WatcherChangeTypes.Changed && wType == WatchingObjectType.Folder) {
                        return;
                    }
                }

                if(check_path_is_in_watchable(e.FullPath, WatchingObjectType.Folder, _re_extensions) == false) {
                    return;
                }

                // Застолбить место в меню:
                MenuItemData menuItemData = null;
                if(Application.Current != null) {
                    Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                    {
                        try {
                            menuItemData = new MenuItemData_CCRD(e, WatchingObjectType.Folder, dt);
                            stackPaths.Add(menuItemData);

                            if(Settings.bool_display_notifications == true){
                                System.Windows.Controls.Image popup_image = new System.Windows.Controls.Image();
                                {
                                    BitmapImage bi = new BitmapImage();
                                    bi.BeginInit();
                                    bi.UriSource = new Uri(@"Icons\folder-horizontal-open.png", UriKind.Relative);
                                    bi.EndInit();
                                    popup_image.Source = bi;
                                }
                                FIFO_Stack_Events.Enqueue(new PopupInfo(e, menuItemData, popup_image) );
                            }
                        } catch(Exception _ex) {
                            _ex = _ex;
                        }
                    });
                }

                write_log(dt, e, wType);
                reloadCustomMenuItems();
            } catch(Exception _ex) {
                _ex = _ex;
            }
        }

        class PopupInfo {
            public FileSystemEventArgs e;
            public MenuItemData mi;
            public System.Windows.Controls.Image popup_image;

            private PopupInfo() {
            }
            public PopupInfo(FileSystemEventArgs _e, MenuItemData _mi, System.Windows.Controls.Image _popup_image) {
                e = _e;
                mi = _mi;
                popup_image = _popup_image;
            }
        }

        private static ConcurrentQueue<PopupInfo> FIFO_Stack_Events = new ConcurrentQueue<PopupInfo>();
        
        private object lockThis = new object();
        /// <summary>
        /// Endless for to output messages
        /// </summary>
        public void process_stack() {
            try {
                System.Threading.Tasks.Task.Factory.StartNew(() => {
                    while (true) {
                        try {
                            if(Monitor.TryEnter(lockThis) == true) {
                                // clear queue and get last message
                                PopupInfo last_event = null;
                                while(FIFO_Stack_Events.Count>0) {
                                    FIFO_Stack_Events.TryDequeue(out last_event);
                                }
                                if(last_event!=null) {
                                    if(Application.Current != null) {
                                        Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                                        {
                                            try {
                                                TrayPopupMessage popup = null;
                                                if (last_event.e.ChangeType == WatcherChangeTypes.Deleted) {
                                                    popup = new TrayPopupMessage(last_event.e.FullPath, last_event.mi.wType.ToString() + " " + last_event.e.ChangeType.ToString(), last_event.mi.wType, App.NotifyIcon, last_event.popup_image,
#if (!_Evgeniy)
                                                    TrayPopupMessage.ControlButtons.Clipboard_Text,
#else
                                                    TrayPopupMessage.ControlButtons.None,
#endif
                                                    TrayPopupMessage.Type.PathDelete
                                                    );
                                                    //popup.MouseDown += (sender, args) => {
                                                    //    if(App.NotifyIcon.CustomBalloon != null) {
                                                    //        App.NotifyIcon.CustomBalloon.IsOpen = false;
                                                    //    }
                                                    //};
                                                } else {
                                                    string ext_text = "";
                                                    if (last_event.mi is MenuItemData_CCRD) {

                                                        if (((MenuItemData_CCRD)last_event.mi).wType==WatchingObjectType.File) {
                                                            if (File.Exists(last_event.e.FullPath)==true) {
                                                                FileInfo fi = new FileInfo(last_event.e.FullPath);
                                                                NumberFormatInfo nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                                                                nfi.NumberGroupSeparator = " ";
                                                                // Иногда файл успевает удалиться, не смотря на проверку условия File.Exists. ))) Редко но бывает. Поэтому этот поток завёрнут в try/catch.
                                                                ext_text = fi.Length.ToString("#,0", nfi);
                                                            }

                                                        }
                                                    }
                                                    popup = new TrayPopupMessage(last_event.e.FullPath, $"{last_event.mi.wType.ToString()} {last_event.e.ChangeType.ToString()} [size: {ext_text} byte]", last_event.mi.wType, App.NotifyIcon, last_event.popup_image,
#if (!_Evgeniy)
                                                    TrayPopupMessage.ControlButtons.Clipboard_Text |
#endif
                                                    TrayPopupMessage.ControlButtons.Clipboard_File | TrayPopupMessage.ControlButtons.File_Run | TrayPopupMessage.ControlButtons.File_Delete,
                                                        TrayPopupMessage.Type.PathCreateChangeRename
                                                        );

                                                    //popup.MouseDown += (sender, args) => {
                                                    //    MouseEventHandler evh = null;
                                                    //    string start_textMessage = popup.TextMessage;
                                                    //    evh = (_sender, _args) => {
                                                    //        if(_args.LeftButton==MouseButtonState.Pressed) {
                                                    //            popup.MouseMove -= evh;
                                                    //            //popup.AllowDrop = true;
                                                    //            popup.TextMessage = $"{start_textMessage}\nDrag and drop this file to application";
                                                    //            try {
                                                    //                popup.IsDragging = true;
                                                    //                //bool cm = popup.CaptureMouse();
                                                    //                // https://stackoverflow.com/questions/3040415/drag-and-drop-to-desktop-explorer
                                                    //                DragDropEffects drop_res = DragDrop.DoDragDrop(popup, new DataObject(DataFormats.FileDrop, new string[]{ popup.path }), DragDropEffects.Copy );
                                                    //                Debug.WriteLine($"Drop result: {drop_res.ToString()}");
                                                    //                popup.RestartTimeoutTimer();
                                                    //                popup.TextMessage = start_textMessage;
                                                    //            }catch(Exception _ex) {
                                                    //            } finally {
                                                    //                popup.IsDragging = false;
                                                    //                //popup.ReleaseMouseCapture();
                                                    //            }
                                                    //        }
                                                    //    };
                                                    //    popup.MouseMove += evh;
                                                    //    popup.Drop += (_sender1, _args1) => {
                                                    //        //App.NotifyIcon.ResetBalloonCloseTimer
                                                    //        popup.TextMessage = start_textMessage;
                                                    //    };
                                                    //};
                                                    //popup.MouseUp += (sender, args) => {
                                                    //    if(App.NotifyIcon.CustomBalloon != null) {
                                                    //        App.NotifyIcon.CustomBalloon.IsOpen = false;
                                                    //    }
                                                    //    App.gotoPathByWindowsExplorer(popup.path, popup.wType);
                                                    //};
                                                }

                                                // Может вылететь, если происходит перезагрузка эксплорера и toolbar отсутствует. Программа после такого исключения вылетает,
                                                // не знаю, почему не ловит try/catch, в который она обёрнута.
                                                App.NotifyIcon.ShowCustomBalloon(popup, PopupAnimation.None, 4000);
                                            } catch (Exception _ex) {
                                                Console.WriteLine("_ex:" + _ex.ToString());
                                            } finally {
                                                try {
                                                    Thread.Sleep(100);
                                                } catch (Exception) {
                                                    //Console.WriteLine("Thread.Sleep _ex:\n" + _ex.ToString());
                                                }
                                            }
                                        });
                                    }
                                }
                            }
                        } catch (Exception _ex) {
                            Console.WriteLine("_ex:" + _ex.ToString());
                        } finally {
                            try {
                                Thread.Sleep(100);
                            } catch (Exception) {
                                //Console.WriteLine("Thread.Sleep _ex:\n" + _ex.ToString());
                            }
                        }
                    }
                });
            } catch(Exception _ex) {
                Console.WriteLine("Error start main for");
            }
        }

        // Конец параметров для отслеживания изменений в файловой системе. =======================================

        // http://stackoverflow.com/questions/249760/how-to-convert-a-unix-timestamp-to-datetime-and-vice-versa
        // Unix -> DateTime
        public static DateTime UnixTimestampToDateTime(long unixTime) {
            DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Local);
            long unixTimeStampInTicks = (long)(unixTime);
            return new DateTime(unixStart.Ticks + unixTimeStampInTicks, System.DateTimeKind.Local);
        }

        // DateTime -> Unix
        public static long DateTimeToUnixTimestamp(DateTime dateTime) {
            DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Local);
            long unixTimeStampInTicks = (dateTime.ToLocalTime() - unixStart).Ticks;
            return unixTimeStampInTicks;
        }

        protected override void OnExit(ExitEventArgs e) {
            base.OnExit(e);
            if(sw_log_file != null) {
                sw_log_file.Flush();
                sw_log_file.Close();
            } else {
                // Сохранить данные контекстного меню для последующего восстановления при перезапуске:
                string file_name_last_files = getHistoryFileName();
                string record_all = "";

                foreach(MenuItemData obj in stackPaths.OrderBy(d => d.event_date_time).ToArray()) {
                    if(obj is MenuItemData_CCRD) {
                        string record = null;
                        MenuItemData_CCRD ccrd = ((MenuItemData_CCRD)obj);
                        long unix_time = DateTimeToUnixTimestamp(ccrd.event_date_time);
                        WatcherChangeTypes wct = ccrd.e.ChangeType;
                        if(wct == WatcherChangeTypes.Renamed) {
                            RenamedEventArgs _ee = (RenamedEventArgs)ccrd.e;
                            string FullPath = _ee.FullPath;
                            string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _ee, "oldFullPath");
                            record = $"{unix_time}\t{wct.ToString()}\t{ccrd.wType.ToString()}\t{FullPath}\t{OldFullPath}\n";
                        } else {
                            string FullPath = ccrd.e.FullPath;
                            record = $"{unix_time}\t{wct.ToString()}\t{ccrd.wType.ToString()}\t{FullPath}\n";
                        }
                        record_all += record;
                    } else {
                    }
                }
                LongFile.WriteAllText(file_name_last_files, record_all, Encoding.UTF8);
            }
            if(_notifyIcon != null) {
                _notifyIcon.Dispose();
            }
        }

        //  регистрацией компонента для контекстного меню
        // https://artemgrygor.wordpress.com/2010/10/06/register-shell-extension-context-menu-also-on-windows-x64-part-2/
        public static void registerDLL(string dllPath) {
            try {
                if(!LongFile.Exists(dllPath))
                    return;
                Assembly asm = Assembly.LoadFile(dllPath);
                var reg = new RegistrationServices();

                // Для нормальной работы регистрации/разрегистрации в x64 нужно предварительно в проекте снять флаг Properties\Build\prefer x32-bit
                // Идею нашёл в http://www.advancedinstaller.com/forums/viewtopic.php?t=7837

                if(reg.RegisterAssembly(asm, AssemblyRegistrationFlags.SetCodeBase)) {
                    appendLogToDictionary("Successfully registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                } else {
                    appendLogToDictionary("Failed registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                }
            } catch(Exception ex) {
                appendLogToDictionary("Failed register FileChangesWatcher in Windows Context Menu.\n" + ex.Message, BalloonIcon.Error);
            }
        }

        // https://artemgrygor.wordpress.com/2010/10/06/register-shell-extension-context-menu-also-on-windows-x64-part-2/
        public static void unregisterDLL(string dllPath) {
            try {
                if(!LongFile.Exists(dllPath))
                    return;
                Assembly asm = Assembly.LoadFile(dllPath);
                var reg = new RegistrationServices();

                // Для нормальной работы регистрации/разрегистрации в x64 нужно предварительно в проекте снять флаг Properties\Build\prefer x32-bit
                // Идею нашёл в http://www.advancedinstaller.com/forums/viewtopic.php?t=7837

                if(reg.UnregisterAssembly(asm)) {
                    appendLogToDictionary("Successfully unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                } else {
                    appendLogToDictionary("Failed unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                }
            } catch(Exception ex) {
                appendLogToDictionary("Exception on unregistering FileChangesWatcher in Windows Context Menu.\n"+ex.Message, BalloonIcon.Error);
            }
        }
        //*/

        public static bool _IsUserAdministrator = false;
        // http://stackoverflow.com/questions/1089046/in-net-c-test-if-process-has-administrative-privileges
        public static bool IsUserAdministrator() {
            //bool value to hold our return value
            bool isAdmin;
            WindowsIdentity user = null;
            try {
                //get the currently logged in user
                user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            } catch(UnauthorizedAccessException ex) {
                isAdmin = false;
            } catch(Exception ex) {
                isAdmin = false;
            } finally {
                if(user != null)
                    user.Dispose();
            }
            _IsUserAdministrator = isAdmin;
            return isAdmin;
        }

        // http://stackoverflow.com/questions/1842226/how-to-get-the-associated-icon-from-a-network-share-file?answertab=votes#tab-top
        [DllImport("shell32.dll")]
        public static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);

        // Native WINAPI functions to retrieve list of devices
        private const int ERROR_INSUFFICIENT_BUFFER = 0x7A;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint QueryDosDevice(string lpDeviceName, IntPtr lpTargetPath, int ucchMax);

        /// <summary>
        /// Retrieves list of all "PhysicalDrive" identifiers on the system, depicting plugged in PhysicalDrives
        /// https://www.virag.si/2010/02/enumerate-physical-drives-in-windows/
        /// </summary>
        /// List of "PhysicalDriveX" strings of plugged in drives
        private static List<string> GetPhysicalDriveList(string drive_letter_without_slash, string start_with) {
            uint returnSize = 0;
            // Arbitrary initial buffer size
            int maxResponseSize = 100;

            IntPtr response = IntPtr.Zero;

            string allDevices = null;
            string[] devices = null;

            while(returnSize == 0) {
                // Allocate response buffer for native call
                response = Marshal.AllocHGlobal(maxResponseSize);

                // Check out of memory condition
                if(response != IntPtr.Zero) {
                    try {
                        // List DOS devices
                        returnSize = QueryDosDevice(drive_letter_without_slash/*null*/, response, maxResponseSize);

                        // List success
                        if(returnSize != 0) {
                            // Result is returned as null-char delimited multistring
                            // Dereference it from ANSI charset
                            allDevices = Marshal.PtrToStringAnsi(response, maxResponseSize);
                        }
                        // The response buffer is too small, reallocate it exponentially and retry
                        else if(Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER) {
                            maxResponseSize = (int)(maxResponseSize * 5);
                        }
                        // Fatal error has occured, throw exception
                        else {
                            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
                        }
                    } finally {
                        // Always free the allocated response buffer
                        Marshal.FreeHGlobal(response);
                    }
                } else {
                    throw new OutOfMemoryException("Out of memory when allocating space for QueryDosDevice command!");
                }
            }

            // Split zero-character delimited multi-string
            devices = allDevices.Split('\0');
            // QueryDosDevices lists alot of devices, return only PhysicalDrives
            return devices.Where(device => device.StartsWith(start_with /*"PhysicalDrive"*/)).ToList<string>();
        }
    }

}