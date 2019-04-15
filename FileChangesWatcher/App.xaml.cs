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

//using NotificationsExtensions.Tiles;

// Сообщение при запуске: http://serv-japp.stpr.ru:27080/images/ImageCRUD?_id=57dbee8386b57cb9283e5b56
// Общий вид программы:  http://serv-japp.stpr.ru:27080/images/ImageCRUD?_id=57dbeee586b57cb9283e5b59
// Репозитрий: http://serv-japp.stpr.ru:7080/FileChangesWatcher/FileChangesWatcher

namespace FileChangesWatcher
{

    /// <summary>
    /// Exposes the Mime Mapping method that Microsoft hid from us.
    /// </summary>
    public static class MimeMappingStealer
    {
        // The get mime mapping method info
        private static readonly MethodInfo _getMimeMappingMethod = null;

        /// <summary>
        /// Static constructor sets up reflection.
        /// </summary>
        static MimeMappingStealer()
        {
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
        public static string GetMimeMapping(string fileName)
        {
            return (string)_getMimeMappingMethod.Invoke(null /*static method*/, new[] { fileName });
        }
    }

    // Существуют только два типа наблюдаемых объектов - файл и каталог.
    public enum WatchingObjectType
    {
        File, Folder, Unknown, Log
    }

    // Используется при передаче пути в функцию, чтобы не потерять о каком типе объекта идёт речь - файле или каталоге. Ведь
    // они могут называться одинаково!
    class Path_ObjectType
    {
        public string path;
        public WatchingObjectType wType;
        public DateTime dateTime;
        public Path_ObjectType(string _path, WatchingObjectType _wType)
        {
            path = _path;
            wType = _wType;
            dateTime = DateTime.Now;
        }
        public Path_ObjectType(string _path, WatchingObjectType _wType, DateTime _dateTime)
        {
            path = _path;
            wType = _wType;
            dateTime = _dateTime;
        }
    }

    public class MenuItemData
    {
        public enum Type
        {
            file_folder, removed_items, log_record
        }
        private static int _i = 0;
        public static int i
        {
            get
            {
                _i++;
                return _i;
            }
        }

        public WatcherChangeTypes watcherChangeType; // Тип события в системе, для которого создаётся меню.
        public DateTime date_time = DateTime.Now; // Дата/время регистрации события.
        public Int32 index;
        public string path;
        public MenuItem mi;
        public string log_record_text; // для записей типа log_record
        public BalloonIcon ballonIcon;
        public Type type;  // Тип записи.
                           // file_folder - пункт меню содержит ссылку на файл/каталог
                           // removed_items - для отображения диалога со списком удалённых объектов

        public MenuItemData()
        {
            this.index = MenuItemData.i; // index
        }

        public MenuItemData(MenuItem menuItem)
        {
            this.mi = menuItem;
            this.index = MenuItemData.i; // index
        }
        public MenuItemData(string _path, MenuItem menuItem, MenuItemData.Type _type)
        {
            this.path = _path;
            this.mi = menuItem;
            this.index = MenuItemData.i; // index
            this.type = _type;
            this.date_time = DateTime.Now;
        }

        public static MenuItemData CreateLogRecord(MenuItem menuItem, BalloonIcon _ballonIcon, string logText)
        {
            MenuItemData mid = new MenuItemData();
            mid.index = MenuItemData.i; // index
            mid.mi = menuItem;
            mid.type = MenuItemData.Type.log_record;
            mid.ballonIcon = _ballonIcon;
            mid.log_record_text = logText;
            mid.date_time = DateTime.Now;
            return mid;
        }
    }

    public class _MenuItemData
    {
        // Кешируемые иконки по расширениям файлов:
        public static Dictionary<String, BitmapImage> icons_map = new Dictionary<string, BitmapImage>();

        public static int menuitem_header_length = 30;
        private static int _i = 0;
        public static int i
        {
            get
            {
                _i++;
                return _i;
            }
        }

        public DateTime date_time = DateTime.Now; // Дата/время регистрации события.
        public Int32 index;
        public MenuItem mi;
        public WatchingObjectType wType;

        protected _MenuItemData(WatchingObjectType _wType, DateTime _dt)
        {
            this.date_time = _dt; // DateTime.Now;
            this.wType = _wType;
            this.index = _MenuItemData.i; // index
        }
    }

    // Элементы меню для Файла/Каталога для режима CreateChangeRenameDelete
    public class MenuItemData_CCRD:_MenuItemData  // CreateChangeRenameDelete
    {
        public FileSystemEventArgs e;

        // Проверить, а есть ли файл, который указан в этом меню.
        public void CheckPath()
        {
            // Log не обрабатывается:
            if (this.wType == WatchingObjectType.Log)
            {
                return;
            }
            // Если элемент меню является объектом, который был удалён, то не надо вместо него делать модификации пункта меню,
            // т.к. удаление - это уже состоявшееся событие:
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                return;
            }
            bool bool_object_exists = false;
            // Если объект существует, то активировать его меню:
            if (this.wType == WatchingObjectType.File)
            {
                if (LongFile.Exists(e.FullPath))
                {
                    bool_object_exists = true;
                }
            }else if (this.wType == WatchingObjectType.Folder)
            {
                if (LongDirectory.Exists(e.FullPath))
                {
                    bool_object_exists = true;
                }
            }

            {
                Grid grid = (Grid)mi.Template.FindName("mi_grid", mi);
                if (grid != null)
                {
                    foreach (Object _obj in grid.Children)
                    {
                        if (_obj is MenuItem)
                        {
                            MenuItem mi_tmp = (MenuItem)_obj;
                            if (mi_tmp != null)
                            {
                                switch (mi_tmp.Name)
                                {
                                    case "mi_main":
                                    case "mi_enter":
                                        //mi_tmp.Background = System.Windows.Media.Brushes.DarkGray;
                                        mi_tmp.IsEnabled = bool_object_exists;
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

        // Обработка события наведения курсора на пункт меню (TODO: может preview какое сделать?)
        private void GotFocus( Object sender, EventArgs e ) {
            int i = 0;
        }

        // fastest_generation - генерировать пункт меню без всякий дополнительных опций, чтобы ускорить сам процесс.
        //                      Применять при массовых файловых операциях, когда события валятся десятками и сотнями в секунду.
        //                      Пользователь всё равно не будет успевать их видеть. Как только события прекратят валиться с такой скоростью
        //                      программа пересоздаст все пункты меню со всеми "плюшками".
        //                      На логировании этот параметр не сказывается и все пути пишутся в файл как и раньше.
        public MenuItemData_CCRD(FileSystemEventArgs _e, WatchingObjectType _wType, DateTime _dt, bool fastest_generation=true) :base(_wType, _dt)
        {
            this.e = _e;
            if (fastest_generation == true) {
                return;
            }

            if ( _e is RenamedEventArgs)
            {
                // То известен OldPath и OldName
            }
            mi = new MenuItem()
            {
                Name = "mi_main"
            };
            mi.GotFocus += this.GotFocus;
            switch (_e.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    {
                        //string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss Deleted ") + _e.FullPath;
                        mi.ToolTip = "Copy to clipboard: " + _e.FullPath;
                        mi.Header = _dt.ToString("yyyy/MM/dd HH:mm:ss.fff") + " [" + _wType.ToString() + " " + _e.ChangeType.ToString()+"]\n    " + App.ShortText(_e.FullPath); //mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
                        // Еле-еле выставил иконку для меню программно и то без ресурса. Использовать иконку из ресурса не получается. http://www.telerik.com/forums/how-to-set-icon-from-codebehind
                        mi.Icon = new System.Windows.Controls.Image { Source = new BitmapImage(new Uri("pack://application:,,,/Icons/deleted.ico", UriKind.Absolute))};
                        mi.Command = App.CustomRoutedCommand_CopyTextToClipboard;
                        mi.CommandParameter = _e.FullPath;
                    }
                    break;
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Renamed:
                    {
                        mi.ToolTip = "Go to " + _e.FullPath;
                        mi.Header = _dt.ToString("yyyy/MM/dd HH:mm:ss.fff")+ " ["+ _wType.ToString() + " " + _e.ChangeType.ToString() + "]\n    " + App.ShortText(_e.FullPath); // mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
                        if(_e.ChangeType == WatcherChangeTypes.Renamed)
                        {
                            RenamedEventArgs _ee = (RenamedEventArgs)_e;
                            string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _ee, "oldFullPath");

                            //try {
                            //    mi.Header += "\n    " + App.ShortText(_ee.OldFullPath) + " [OldFullPath]";
                            //}
                            //catch (PathTooLongException) {
                            //    mi.Header += "\n    " + App.ShortText(_ee.OldName) + " [OldName]";
                            //}
                        }
                        mi.Command = App.CustomRoutedCommand;
                        mi.CommandParameter = new Path_ObjectType(_e.FullPath, wType);

                        if(_wType==WatchingObjectType.File){
                            // Получить иконку файла для вывода в меню:
                            // http://www.codeproject.com/Articles/29137/Get-Registered-File-Types-and-Their-Associated-Ico
                            // Загрузить иконку файла в меню: http://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap?answertab=votes#tab-top
                            // Как-то зараза не грузится простым присваиванием.
                            String file_ext = Path.GetExtension(_e.FullPath);
                            Icon mi_icon = null;
                            BitmapImage bitmapImage = null;

                            // Кешировать иконки для файлов:
                            if (icons_map.TryGetValue(file_ext, out bitmapImage) == true && bitmapImage != null) {
                            }
                            else {
                                ushort uicon=0;
                                StringBuilder strB = new StringBuilder(_e.FullPath);
                                try {
                                    // На сетевых путях выдаёт Exception. Поэтому к сожалению не годиться.
                                    //mi_icon = Icon.ExtractAssociatedIcon(_path);// getIconByExt(file_ext);

                                    // Этот метод работает: http://stackoverflow.com/questions/1842226/how-to-get-the-associated-icon-from-a-network-share-file?answertab=votes#tab-top
                                    IntPtr handle = App.ExtractAssociatedIcon(IntPtr.Zero, strB, out uicon);
                                    mi_icon = Icon.FromHandle(handle);
                                }
                                catch (Exception ex) {
                                    mi_icon = null;
                                    icons_map.Add(file_ext, null);
                                }

                                if (mi_icon != null) {
                                    using (MemoryStream memory = new MemoryStream()) {
                                        //{  // https://social.msdn.microsoft.com/Forums/vstudio/en-US/d3181462-6923-4b0d-b9cc-987a252c4202/access-violation-after-creating-a-bitmap-with-pixel-data?forum=csharpgeneral
                                        //    Bitmap bmp = new Bitmap(mi_icon.Width, mi_icon.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                        //    Rectangle rect = new Rectangle(0, 0, mi_icon.Width, mi_icon.Height);
                                        //    BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
                                        //    System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, bmpData.Scan0,
                                        //        this.Stride * this.Height);
                                        //    bmp.UnlockBits(bmpData);
                                        //}

                                        Bitmap bitmap = mi_icon.ToBitmap();
                                        bitmap.Save(memory, ImageFormat.Png);

                                        //mi_icon.Save(memory);
                                        memory.Position = 0;

                                        //Bitmap Logo = new Bitmap(mi_icon.ToBitmap());
                                        ////Logo.MakeTransparent(Logo.GetPixel(1, 1));
                                        //Logo.Save(memory, ImageFormat.Png);
                                        //memory.Position = 0;

                                        bitmapImage = new BitmapImage();
                                        bitmapImage.BeginInit();
                                        // Принудительный resize иконки, потому что под xp WPF не умеет автоматически масштабировать иконки в меню.
                                        bitmapImage.DecodePixelHeight = 16;
                                        bitmapImage.DecodePixelWidth = 16;
                                        bitmapImage.StreamSource = memory;
                                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad; //.Default; //.OnLoad;
                                        bitmapImage.EndInit();
                                        if (strB.ToString().ToLower().EndsWith("shell32.dll") == true && uicon == 1) {
                                            // Это означает, что нужно отобразить иконку для несуществующего файла или иконка не найдена user32.Dll,1
                                            // http://forum.vingrad.ru/topic-26161.html
                                            // https://msdn.microsoft.com/ru-ru/library/windows/desktop/ms648067%28v=vs.85%29.aspx?f=255&MSPPError=-2147217396
                                            // 
                                        }
                                        else {
                                            icons_map.Add(file_ext, bitmapImage);
                                        }
                                    }
                                }
                            }
                            if (bitmapImage != null) {
                                mi.Icon = new System.Windows.Controls.Image {
                                    Source = bitmapImage
                                };
                            }
                            else {
                                mi.Icon = null;
                            }
                        }else if (_wType == WatchingObjectType.Folder) {
                            mi.Icon = new System.Windows.Controls.Image {
                                Source = new BitmapImage(
                                new Uri("pack://application:,,,/Icons/folder-horizontal-open.png"))
                            };
                        }

                        {
                            // Так определять Grid гораздо проще: http://stackoverflow.com/questions/5755455/how-to-set-control-template-in-code
                            //    string str_template = @"
                            //    <ControlTemplate 
                            //                        xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                            //                        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                            //                        xmlns:tb='http://www.hardcodet.net/taskbar'
                            //                        xmlns:local='clr-namespace:FileChangesWatcher'
                            //     >
                            //        <Grid x:Name='mi_grid'>
                            //            <Grid.ColumnDefinitions>
                            //                <ColumnDefinition Width='*'/>
                            //                <ColumnDefinition Width='20'/>
                            //                <ColumnDefinition Width='20'/>
                            //            </Grid.ColumnDefinitions>
                            //        </Grid>
                            //    </ControlTemplate>
                            //";
                            MenuItem _mi = new MenuItem();
                            Grid mi_grid = null; // new Grid();
                            //ControlTemplate ct = (ControlTemplate)XamlReader.Parse(str_template);

                            // Эксперимент сделать шаблон из ресурса, а не парсить строку:
                            ControlTemplate ct = (ControlTemplate)System.Windows.Application.Current.Resources["MenuItemFileForContextMenu"];
                            _mi.Template = ct;

                            if (_mi.ApplyTemplate())
                            {
                                mi_grid = (Grid)ct.FindName("mi_grid", _mi);
                            }
                            //MenuItem mi_clipboard = new MenuItem()
                            //{
                            //    Name = "mi_clipboard"
                            //};
                            //mi_clipboard.Icon = new System.Windows.Controls.Image
                            //{
                            //    Source = new BitmapImage(
                            //    new Uri("pack://application:,,,/Icons/Clipboard.ico"))
                            //};
                            //mi_clipboard.ToolTip = "Copy path to clipboard";
                            MenuItem mi_clipboard = (MenuItem)ct.FindName("mi_clipboard", _mi);
                            mi_clipboard.Command = App.CustomRoutedCommand_CopyTextToClipboard;
                            mi_clipboard.CommandParameter = _e.FullPath;
                            //MenuItem mi_enter = new MenuItem()
                            //{
                            //    Name = "mi_enter"
                            //};
                            //mi_enter.Icon = new System.Windows.Controls.Image
                            //{
                            //    Source = new BitmapImage(
                            //    new Uri("pack://application:,,,/Icons/Enter.ico"))
                            //};

                            MenuItem mi_enter = (MenuItem)ct.FindName("mi_enter", _mi);
                            // Если объект удалён, то нельзя его выполнить
                            if (_e.ChangeType != WatcherChangeTypes.Deleted)
                            {
                                mi_enter.ToolTip = "Execute file";
                                mi_enter.Command = App.CustomRoutedCommand_ExecuteFile;
                                mi_enter.CommandParameter = new Path_ObjectType(_e.FullPath, wType);
                            }

                            Grid.SetColumn(mi, 0);
                            Grid.SetRow(mi, 0);
                            mi_grid.Children.Add(mi);
                            //Grid.SetColumn(mi_clipboard, 1);
                            //Grid.SetRow(mi_clipboard, 0);
#if (!_Evgeniy)
                            //mi_grid.Children.Add(mi_clipboard);
                            mi_clipboard.Visibility = Visibility.Visible;
#endif
                            // Добавить кнопку для файла "Запустить файл" в правом столбце:
                            if (wType == WatchingObjectType.File)
                            {
                                //Grid.SetColumn(mi_enter, 2);
                                //Grid.SetRow(mi_enter, 0);
                                //mi_grid.Children.Add(mi_enter);
                                mi_enter.Visibility = Visibility.Visible;
                            }
                            mi = _mi;
                        }
                    }
                    break;
            }
        }
    }

    /*
    // Элементы меню для Файла/Каталога для режима Rename
    public class MenuItemData_Rename : _MenuItemData  // Rename
    {
        public RenamedEventArgs e;
        public MenuItemData_Rename(MenuItem _menuItem, BalloonIcon _ballonIcon, RenamedEventArgs _e, WatchingObjectType _wType) :base(_menuItem, _ballonIcon, _wType)
        {
            this.e = _e;
        }
    }
    //*/

    // Элемент меню для записи Log (для отображения при нажатии текстого сообщения)
    public class MenuItemData_Log: _MenuItemData  // Rename
    {
        public string log_record_text;

        public MenuItemData_Log(BalloonIcon _ballonIcon, string logText, DateTime _dt):base(WatchingObjectType.Log, _dt)
        {
            this.log_record_text = logText;

            mi = new MenuItem();
            string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss ") + logText.Split('\n')[0];
            mi.Header = mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
            mi.ToolTip = "message from program:\n" + logText;
            mi.Command = App.CustomRoutedCommand_ShowMessage;
            mi.CommandParameter = logText;

            _MenuItemData first_menuItemData = null;
            if(App.stackPaths.Count > 0)
            {
                //first_menuItemData = App.stackPaths.OrderBy(d => d.index).Last();
                first_menuItemData = App.stackPaths.OrderBy(d => d.date_time).Last();
            }
            /*
            if (first_menuItemData == null || first_menuItemData == this)
            {
                TrayPopupMessage popup = new TrayPopupMessage(logText, "Initial initialization", WatchingObjectType.File, App.NotifyIcon, TrayPopupMessage.ControlButtons.Clipboard);
                popup.MouseDown += (sender, args) =>
                {
                    App.NotifyIcon.CustomBalloon.IsOpen = false;
                    App.ShowMessage(logText);
                };
                App.NotifyIcon.ShowCustomBalloon(popup, PopupAnimation.Fade, 4000);
            }
            */
        }
    }

    /*
    class MenuItemData_FileSystem : MenuItemData
    {
        public string path;
        public MenuItemData_FileSystem(string _path, MenuItem menuItem): base(menuItem)
        {
            this.path = _path;
        }
    }
    class MenuItemData_LogRecord : MenuItemData
    {
        public string log_record_text; // для записей типа log_record
        public MenuItemData_LogRecord(MenuItem menuItem, BalloonIcon _ballonIcon, string logText)
        {

        }
    }
    //*/

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : Application
    {
        // Добавление пользовательских меню выполнено на основе: https://msdn.microsoft.com/ru-ru/library/ms752070%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396


        // Классификация элементов контекстного меню с файловыми операциями:
        enum ContextMenu_Group_Names
        {
            group_files, group_folders, group_logs
        }

        Dictionary<ContextMenu_Group_Names, List<_MenuItemData>> context_menu_dict = new Dictionary<ContextMenu_Group_Names, List<_MenuItemData>>()
        {
            { ContextMenu_Group_Names.group_files, new List<_MenuItemData>() },
            { ContextMenu_Group_Names.group_folders, new List<_MenuItemData>() },
            { ContextMenu_Group_Names.group_logs, new List<_MenuItemData>() }
        };

        // Список пользовательских пунктов меню, в которые записываются пути изменяемых файлов:
        public static List<_MenuItemData> stackPaths = new List<_MenuItemData>();

        // Получить список путей в контекстном меню, перед копирование в буфер обмена.
        public static String GetStackPathsAsString()
        {
            //List<MenuItemData> sss = new List<MenuItemData>();
            //sss.Exists(x => x.index == 1);
            StringBuilder sb = new StringBuilder();
            //foreach (var obj in stackPaths.FindAll(d=> (d is MenuItemData_CCRD)).OrderBy(d => d.index).Reverse().ToArray())
            foreach (var obj in stackPaths.FindAll(d => (d is MenuItemData_CCRD)).OrderBy(d => d.date_time).Reverse().ToArray())
            {
                if(sb.Length > 0)
                {
                    sb.Append("\n");
                }
                MenuItemData_CCRD _obj = (MenuItemData_CCRD)obj;
                sb.Append(_obj.e.FullPath);
            }
            return sb.ToString();
        }

        public static string ShortText(string text)
        {
            string result = text.Length > (menuitem_header_length * 2 + 5) ? text.Substring(0, menuitem_header_length) + " ... " + text.Substring(text.Length - menuitem_header_length) : text;
            return result;
        }

        // Пользовательская команда:
        public static RoutedCommand CustomRoutedCommand_ExecuteFile = new RoutedCommand();
        private void ExecutedCustomCommand_ExecuteFile(object sender, ExecutedRoutedEventArgs e)
        {
            //MessageBox.Show("Custom Command Executed: "+ e.Parameter);
            Path_ObjectType obj = (Path_ObjectType)e.Parameter;
            Process.Start(obj.path, "");
        }

        // Пользовательская комманда копировать текст в буфер обмена:
        public static RoutedCommand CustomRoutedCommand_CopyTextToClipboard = new RoutedCommand();
        private void ExecutedCustomCommand_CopyTextToClipboard(object sender, ExecutedRoutedEventArgs e)
        {
            String text = (string)e.Parameter;
            copy_clipboard_with_popup(text);
            /*
            System.Windows.Forms.Clipboard.SetText(text);
            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Path copied into a clipboard", BalloonIcon.Info);
            */
        }
        public static void copy_clipboard_with_popup(string text)
        {
            System.Windows.Forms.Clipboard.SetText(text);
            App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Message copied into a clipboard", BalloonIcon.Info);
        }

        // Пользовательская комманда отображения окна с текстовым содержанием:
        public static RoutedCommand CustomRoutedCommand_ShowMessage= new RoutedCommand();
        private void ExecutedCustomCommand_ShowMessage(object sender, ExecutedRoutedEventArgs e)
        {
            String text = (string)e.Parameter;
            ShowMessage(text);
            /*
            DialogListingDeletedFiles window = new DialogListingDeletedFiles();
            window.txtListFiles.Text = text;
            window.Show();
            ActivateWindow(new WindowInteropHelper(window).Handle);
            */
        }

        public static void ShowMessage(string text)
        {
            //String text = (string)e.Parameter;
            DialogListingDeletedFiles window = new DialogListingDeletedFiles();
            window.txtListFiles.Text = text;
            window.Show();
            DLLImport.ActivateWindow(new WindowInteropHelper(window).Handle);
        }

        // Пользовательская комманда открытия диалога стёртых объектов:
        public static RoutedCommand CustomRoutedCommand_DialogListingDeletedFiles = new RoutedCommand();
        private void ExecutedCustomCommand_DialogListingDeletedFiles(object sender, ExecutedRoutedEventArgs e)
        {
            List<Dictionary<string, string>> list_files = (List<Dictionary<string, string>>)e.Parameter;
            DialogListingDeletedFiles(list_files);
        }

        private static void DialogListingDeletedFiles(List<Dictionary<string, string>> list_files)
        {
            List<string> txt = new List<string>();
            int i = 0;
            txt.Add(String.Format("{0})\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", "N", "TimeCreated", "ObjectType", "SubjectDomainName", "SubjectUserName", "ObjectName", "ProcessName"));
            foreach (Dictionary<string, string> rec in list_files)
            {
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

                string txt_rec = String.Format("{0})\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", i, TimeCreated, ObjectType, SubjectDomainName, SubjectUserName, ObjectName, ProcessName);
                txt.Add(txt_rec);
            }

            DialogListingDeletedFiles window = new DialogListingDeletedFiles();

            window.txtListFiles.Text = String.Join("\n", txt.ToArray());
            window.Show();
        }


        // Пользовательская команда:
        public static RoutedCommand CustomRoutedCommand = new RoutedCommand();
        private void ExecutedCustomCommand(object sender, ExecutedRoutedEventArgs e)
        {
            //MessageBox.Show("Custom Command Executed: "+ e.Parameter);
            Path_ObjectType obj = (Path_ObjectType)e.Parameter;
            gotoPathByWindowsExplorer(obj.path, obj.wType);
        }

        public static void gotoPathByWindowsExplorer(string _path, WatchingObjectType wType)
        {
            if (wType==WatchingObjectType.File)
            {
                Process.Start("explorer.exe", "/select,\"" + _path + "\"");
            }
            else
            {
                Process.Start("explorer.exe", "\"" + _path + "\"");
            }
        }

        // CanExecuteRoutedEventHandler that only returns true if the source is a control.
        private void CanExecuteCustomCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            Control target = e.Source as Control;
            e.CanExecute = true;
        }

        static System.Timers.Timer reloadCustomMenuItems_timer = null;
        protected static void reloadCustomMenuItems() {
            // Повесить таймер на 0.1 секунду, чтобы перестраивать меню не чаще 10 раз в сек (бывает, что события валятся сотнями и нет необходимости их все выводить синхронно).
            if (reloadCustomMenuItems_timer == null) {
                reloadCustomMenuItems_timer = new System.Timers.Timer(100);
                reloadCustomMenuItems_timer.Elapsed += ReloadCustomMenuItems_timer_Elapsed; ;
                reloadCustomMenuItems_timer.AutoReset = false;
                reloadCustomMenuItems_timer.Enabled = true;
            }
        }

        private static void ReloadCustomMenuItems_timer_Elapsed( object sender, ElapsedEventArgs e ) {
            _reloadCustomMenuItems();
            reloadCustomMenuItems_timer = null;
        }

        // Пересоздать пункты контекстного меню, которые указывают на файлы.
        protected static void _reloadCustomMenuItems()
        {
            if (Application.Current != null) {
                Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                {
                // TODO: нужно сделать синхронизацию по stackPaths, а то бывает, что изменение стека меню не успевает обновляться, в то время как появляется новое событие. В этом случае вылетает Exception.
                // Примерно понял, как ускорить регенерацию меню. Нужно будет делать отложенную проверку на существование элементов, указанных в меню.
                // Повесить таймер, который будет мониторить окончание работы это функции (reloadCustomMenuItems) и через 0.1 сек (например) запускать
                // проверку существования объектов. Если эта функция снова будет вызвана, то прервать проверку существования объектов. Проверка продолжится автоматически после
                // вызова (reloadCustomMenuItems)

                // Сначала очистить пункты меню с путями к файлам:
                /*
                foreach (var obj in stackPaths )
                {
                    _notifyIcon.ContextMenu.Items.Remove(obj.mi);
                }
                //*/
                    string[] lst_items_name = { "menu_separator", "menu_settings", "menu_exit" };

                    lock (_notifyIcon.ContextMenu.Items) {
                        for (int i = _notifyIcon.ContextMenu.Items.Count - 1; i >= 0; i--) {
                            if (Array.IndexOf(lst_items_name, ((Control)_notifyIcon.ContextMenu.Items.GetItemAt(i)).Name) < 0) {
                                _notifyIcon.ContextMenu.Items.RemoveAt(i);
                            }
                        }

                        /*
                        int i = 0;
                        while(_notifyIcon.ContextMenu.Items.Count > lst_items_name.Length || _notifyIcon.ContextMenu.Items.Count<(i-1)) {
                            if( Array.IndexOf(lst_items_name, ((Control)_notifyIcon.ContextMenu.Items.GetItemAt(i)).Name) >= 0) {
                                _notifyIcon.ContextMenu.Items.RemoveAt(i);
                            }else {
                                i++;
                          }
                        }
                        */

                        // Максимальное количество файлов в списке должно быть не больше указанного максимального значения:
                        while (stackPaths.Count > Settings.log_contextmenu_size) {
                            // Удалить самый старый элемент из списка путей и из меню
                            //_MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).First();
                            _MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.date_time).First();
                            _notifyIcon.ContextMenu.Items.Remove(first_menuItemData.mi);
                            stackPaths.Remove(first_menuItemData);
                        }

                        List<_MenuItemData> _stackPaths = new List<_MenuItemData>();
                        // Преобразовать сокращённые элементы меню в полные:
                        foreach (_MenuItemData md in stackPaths) {
                            _MenuItemData _md = md;

                            // Если запись предназначена для файла, то сделать меню полным:
                            if ( md is MenuItemData_CCRD) {
                                MenuItemData_CCRD md_ccrd = ((MenuItemData_CCRD)md);
                                _md = new MenuItemData_CCRD(md_ccrd.e, md_ccrd.wType, md_ccrd.date_time, false);
                            }
                            _stackPaths.Add(_md);
                        }
                        stackPaths = _stackPaths;



                        // Заполнить новые пункты:
                        //Grid mi = null;
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
                        _MenuItemData last_menuItemData = null;
                        foreach (_MenuItemData obj in stackPaths.OrderBy(d => d.date_time).ToArray()) {
                            WatchingObjectType wType = obj.wType;
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
                                last_menuItemData = obj;
                        }
                        if (last_menuItemData != null) {
                            last_menuItemData.mi.FontWeight = FontWeights.Bold;
                        }
#endif
                    }
                });
            }
        }

        public static MenuItemData currentMenuItem = null;
        private static TaskbarIcon _notifyIcon =null;
        private static bool bool_is_path_tooltip = false;
        private static bool bool_is_ballow_was_shown = false;
        public static TaskbarIcon NotifyIcon
        {
            get
            {
                return _notifyIcon;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Process proc = Process.GetCurrentProcess();
            int count = Process.GetProcessesByName(proc.ProcessName).Where(p =>p.ProcessName == proc.ProcessName).Count();
            if (count > 1)
            {
                MessageBox.Show("Already an instance is running...");
                App.Current.Shutdown();
                return;
            }

            IsUserAdministrator();

            base.OnStartup(e);

            /* TODO: Выяснить почему валится программы при запуске из каталога, в котором у пользователя недостаточно разрешений?
             * Пытаюсь выяснить возможность писать в каталог. Пока безрезультатно. Программа валится при запуске, если у пользователя в каталоге запуска недостаточно прав.
            String exeFilePath = getExeFilePath();
            String appFolder = System.IO.Path.GetDirectoryName(exeFilePath);
            if(HasWritePermission(appFolder) == false)
            {
                if( MessageBox.Show("You have low access permission in folder\n\n"+appFolder+"\n Program cannot run.\nOpen home folder before close?", "Error", MessageBoxButton.YesNo) == MessageBoxResult.OK)
                {
                    Process.Start("explorer.exe", "/select,\"" + exeFilePath + "\"");
                }
                return;
            }
            //*/

            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

            _notifyIcon.MouseWheel+= (sender, args) =>
            {
                //MethodInfo mi = typeof(TaskbarIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                //mi.Invoke(_notifyIcon, null);
                _notifyIcon.ContextMenu.IsOpen = true;
            };

            //notifyIcon.ContextMenu.Items.Insert(0, new Separator() );  // http://stackoverflow.com/questions/4823760/how-to-add-horizontal-separator-in-a-dynamically-created-contextmenu
            //CommandBinding customCommandBinding = new CommandBinding(CustomRoutedCommand, ExecutedCustomCommand, CanExecuteCustomCommand);
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand, ExecutedCustomCommand, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_ExecuteFile, ExecutedCustomCommand_ExecuteFile, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_CopyTextToClipboard, ExecutedCustomCommand_CopyTextToClipboard, CanExecuteCustomCommand));
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
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static String getSettingsFilePath(string ext)
        {
            String iniFilePath = null; // System.IO.Path.GetDirectoryName(Environment.CommandLine.Replace("\"", "")) + "\\Stroiproject.ini";
            string exe_file = typeof(FileChangesWatcher.App).Assembly.Location; // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
            //iniFilePath = Process.GetCurrentProcess().MainModule.FileName;
            iniFilePath = System.IO.Path.GetDirectoryName(exe_file) + "\\" + System.IO.Path.GetFileNameWithoutExtension(exe_file) + ext;
            return iniFilePath;
        }

        static string history_file_name = null;
        static List<string> history_list_items = new List<string>();
        // Файл, где хранятся последние файлы, выведенные в контекстное меню:
        public static String getHistoryFileName()
        {
            if (history_file_name == null)
            {
                string _history_file_name = null; // System.IO.Path.GetDirectoryName(Environment.CommandLine.Replace("\"", "")) + "\\Stroiproject.ini";
                string exe_file = typeof(FileChangesWatcher.App).Assembly.Location; // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
                                                                                    //iniFilePath = Process.GetCurrentProcess().MainModule.FileName;
                _history_file_name = System.IO.Path.GetDirectoryName(exe_file) + "\\" + System.IO.Path.GetFileNameWithoutExtension(exe_file) + ".last_files.txt";
                history_file_name = _history_file_name;
            }
            return history_file_name;
        }
        public static String getExeFilePath()
        {
            // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
            String exeFilePath = typeof(FileChangesWatcher.App).Assembly.Location; // System.IO.Path.GetDirectoryName(Environment.CommandLine.Replace("\"", "")) + "\\Stroiproject.ini";
            //exeFilePath = Process.GetCurrentProcess().MainModule.FileName;
            return exeFilePath;
        }

        public static String getLogFileName()
        {
            DateTime n = DateTime.Now;
            string year = n.Year.ToString();
            string month = (n.Month < 10 ? "0" : "") + n.Month.ToString();
            string day = (n.Day< 10 ? "0" : "") + n.Day.ToString();
            string str_path = Settings.string_log_path + "\\" + Settings.string_log_file_prefix + ""+year+"."+month+"."+day+".log";
            return str_path;
        }


        // Используется для исключения дубликатов в работе FileChangesWatcher
        // http://weblogs.asp.net/ashben/31773
        class struct_log_record
        {
            public DateTime dt;
            public FileSystemEventArgs fsea;
            public WatchingObjectType wType;
            public struct_log_record( DateTime _dt, FileSystemEventArgs _fsea, WatchingObjectType _wType)
            {
                dt = _dt;
                fsea = _fsea;
                wType = _wType;
            }

            public override string ToString()
            {
                string str_record = null;
                string date_time = dt.ToString("yyyy.MM.dd HH:mm:ss.fff");
                if (fsea.ChangeType == WatcherChangeTypes.Renamed)
                {
                    RenamedEventArgs _fsea = (RenamedEventArgs)fsea;
                    string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _fsea, "oldFullPath");
                    str_record = String.Format("\r\n{0}\t{1}\t{2}\t{3}\t{4}", date_time, _fsea.ChangeType.ToString(), wType.ToString(), _fsea.FullPath, OldFullPath); //str_record = String.Format("\r\n{0}\t{1}\t{2}\t{3}\t{4}", date_time, _fsea.ChangeType.ToString(), wType.ToString(), _fsea.FullPath, _fsea.OldFullPath);
                }
                else
                {
                    str_record = String.Format("\r\n{0}\t{1}\t{2}\t{3}", date_time, fsea.ChangeType.ToString(), wType.ToString(), fsea.FullPath);
                }
                return str_record;
            }

            public string ToLogString() {
                string str_record = null;
                string date_time = dt.Ticks.ToString(); //.ToString("yyyy.MM.dd HH:mm:ss.fff");
                if (fsea.ChangeType == WatcherChangeTypes.Renamed) {
                    RenamedEventArgs _fsea = (RenamedEventArgs)fsea;
                    string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _fsea, "oldFullPath");
                    str_record = String.Format("\r\n{0}\t{1}\t{2}\t{3}\t{4}", date_time, _fsea.ChangeType.ToString(), wType.ToString(), _fsea.FullPath, OldFullPath);//str_record = String.Format("\r\n{0}\t{1}\t{2}\t{3}\t{4}", date_time, _fsea.ChangeType.ToString(), wType.ToString(), _fsea.FullPath, _fsea.OldFullPath);
                }
                else {
                    str_record = String.Format("\r\n{0}\t{1}\t{2}\t{3}", date_time, fsea.ChangeType.ToString(), wType.ToString(), fsea.FullPath);
                }
                return str_record;
            }

            // Сравнить объекты. Если значения данных (кроме времени) в объектах одинаковые, то выдать true.
            public bool ObjectAreSame( struct_log_record o)
            {
                bool eq = true;
                if( o==null)
                {
                    eq = false;
                }
                if (eq == true)
                {
                    if( fsea.ChangeType==o.fsea.ChangeType)
                    {
                        if (fsea.ChangeType == WatcherChangeTypes.Renamed)
                        {
                            RenamedEventArgs _fsea = (RenamedEventArgs)fsea;
                            RenamedEventArgs _o_fsea = (RenamedEventArgs)o.fsea;
                            string _o_fsea_OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _fsea, "oldFullPath");
                            if ( _fsea.FullPath==_o_fsea.FullPath && _fsea.OldFullPath == _o_fsea_OldFullPath)
                            {
                            }
                            else
                            {
                                eq = false;
                            }
                        }
                        else
                        {
                            if(fsea.FullPath == o.fsea.FullPath)
                            {
                            }
                            else
                            {
                                eq = false;
                            }
                        }

                    }
                    else
                    {
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
        public static void write_log(DateTime dt, FileSystemEventArgs _e, WatchingObjectType wType)
        {
            lock (lock_write_log) {
                if (Settings.bool_log == false) {
                    return;
                }

                struct_log_record new_log_record = null;
                if (_e != null) {
                    new_log_record = new struct_log_record(dt, _e, wType);
                }

                if (new_log_record == null) {

                }

                string str_file_path = getLogFileName();

                // Если имя файла, в который была сделана предыдущая запись отличается от полученного, то нужно закрыть поток и открыть новый для записи
                // Это так же работает и для начала работы программы, когда запись ещё не производилась.
                if (string_log_file_name != str_file_path) {
                    if (sw_log_file == null) {
                    }
                    else {
                        // Не забыть записать последнюю запись из буфера, если она отличается от новой записи или время между записями больше или равно одной секунды
                        if (last_log_record != null) {
                            if (last_log_record.ObjectAreSame(new_log_record) == false) {
                                sw_log_file.Write(last_log_record.ToString());
                            }
                            else {
                                if ((new_log_record.dt - last_log_record.dt).TotalSeconds >= 1) {
                                    sw_log_file.Write(last_log_record.ToString());
                                }
                            }
                            last_log_record = null;
                        }
                        else {

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

                if (last_log_record != null) {
                    if (last_log_record.ObjectAreSame(new_log_record) == false || (new_log_record.dt - last_log_record.dt).TotalSeconds >= 1) {
                        sw_log_file.Write(new_log_record.ToString());  // TODO: Тут однажды выскочило исключение, что нельзя писать в закрытый файл (при разархивации большого архива). Надо бы сделать проверку. Может быть сработал таймер ниже. (Больше одной секунды).
                    }
                }
                else {
                    sw_log_file.Write(new_log_record.ToString());
                }

                last_log_record = new_log_record;

                // Повесить таймер на одну секунду, чтобы в отсутствии нагрузки на запись в лог сбросить буффер.
                if (flush_buffer_timer != null) {
                    flush_buffer_timer.Stop();
                }
                flush_buffer_timer = new System.Timers.Timer(1000);
                flush_buffer_timer.AutoReset = false;
                flush_buffer_timer.Elapsed += //Flush_buffer_timer_Elapsed;
                    (sender, e) => {
                        // https://stackoverflow.com/questions/2416793/why-is-lock-much-slower-than-monitor-tryenter

                        //if(Monitor.TryEnter(lock_write_log)) 
                        lock (lock_write_log) {
                            if (sw_log_file != null) {
                                sw_log_file.Flush();
                                sw_log_file.Close();
                                sw_log_file = null;
                                string_log_file_name = "";
                            }
                            flush_buffer_timer = null;
                        }
                    };
                flush_buffer_timer.Enabled = true;

                /*
                string date_time = dt.ToString("yyyy.MM.dd HH:mm:ss.fff");

                string str_record = null;
                if (_e.ChangeType == WatcherChangeTypes.Renamed)
                {
                    RenamedEventArgs _ee = (RenamedEventArgs)_e;
                    //mi.Header += "\n    " + App.ShortText(_ee.OldFullPath) + " [OldFullPath]";
                    str_record = String.Format("{0}\t{1}\t{2}\t{3}\n", date_time, _ee.ChangeType.ToString(), _ee.FullPath, _ee.OldFullPath );
                }
                else
                {
                    str_record = String.Format("{0}\t{1}\t{2}\n", date_time, _e.ChangeType.ToString(), _e.FullPath);
                }
                */
                //sw_log_file.Write(str_record);
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FlushFileBuffers( IntPtr handle );
        private static void Flush_buffer_timer_Elapsed( object sender, ElapsedEventArgs e ) {
            lock (lock_write_log) {
                if (sw_log_file != null) {
                    sw_log_file.Flush();
                    sw_log_file.Close();
                    sw_log_file = null;
                    string_log_file_name = "";
                    //((FileStream)(sw_log_file.BaseStream)).Flush();
                    /*
                    if (!FlushFileBuffers(sw_log_file.Handle)) {   // Flush OS file cache to disk.
                        // TODO: подумать, что с этим делать:
                        // Int32 err = Marshal.GetLastWin32Error();
                        // throw new Win32Exception(err, "Win32 FlushFileBuffers returned error for " + stream.Name);
                    }
                    //*/
                }
                //write_history_file();
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

        public static bool IsAppInRegestry()
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            String appName = System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
            if (rk.GetValue(appName) != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void setAutostart()
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                String appName = System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
                rk.SetValue(appName, Process.GetCurrentProcess().MainModule.FileName);

                //// После установки автозапуска отметить это настройкой в конфигурации:
                //FileIniDataParser fileIniDataParser = new FileIniDataParser();
                //IniParser.Model.IniData iniData = new IniParser.Model.IniData();
                //String iniFilePath = App.getSettingsFilePath(".ini");
                //iniData = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                //iniData.Sections["General"].RemoveKey("autostart_on_windows");
                //iniData.Sections["General"].AddKey("autostart_on_windows", "true");
                //UTF8Encoding a = new UTF8Encoding();
                //fileIniDataParser.WriteFile(iniFilePath, iniData, Encoding.UTF8);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        public static void resetAutostart()
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                String appName = System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
                rk.DeleteValue(appName, false);

                // После установки автозапуска отметить это настройкой в конфигурации:
                //FileIniDataParser fileIniDataParser = new FileIniDataParser();
                //IniParser.Model.IniData data = new IniParser.Model.IniData();
                //String iniFilePath = App.getSettingsFilePath(".ini");
                //data = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                //data.Sections["General"].RemoveKey("autostart_on_windows");
                //data.Sections["General"].AddKey("autostart_on_windows", "false");
                //fileIniDataParser.WriteFile(iniFilePath, data, Encoding.UTF8);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }


        public static void initApplication(StartupEventArgs e)
        {
            StringBuilder init_text_message = new StringBuilder();

            // Сбросить всех наблюдателей, установленных ранее (при перезапуске настроек в уже запущенной программе):
            foreach (FileSystemWatcher watcher in list_watcher.ToArray() )
            {
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

            // Проверить существование ini-файла. Если его нет, то создать его:
            //String iniFilePath = getSettingsFilePath(".ini");
            String jsonFilePath = getSettingsFilePath(".js");
            JObject jsonData = new JObject();

            if (LongFile.Exists(jsonFilePath) == false)
            {
                jsonData["General"]                 = new JObject();
                jsonData["Extensions"]              = new JArray();
                jsonData["UserExtensions"]          = new JArray();
                jsonData["FoldersForWatch"]         = new JArray();
                jsonData["FoldersForExceptions"]    = new JArray();
                jsonData["FileNamesExceptions"]     = new JArray();
                jsonData["FoldersForWatch"]         = new JArray();
                jsonData["FoldersForExceptions"]    = new JArray();

                // Пример записи комментария вместе со значением. Отдельно от значения в этом компоненте комментарии не пишутся
                //data.Sections["Comments"].AddKey("key01", "disabled ; Отключить параметр."); 


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
                    // Список расширений, по-умолчанию, за которыми надо "следить". Из них потом будут регулярки^
                    JArray jextensions =  ((JArray)jsonData["Extensions"]);
                    jextensions.Add(new JObject( new JProperty("archivers", ".tar|.jar|.zip|.bzip2|.gz|.tgz|.7z")));
                    jextensions.Add(new JObject( new JProperty("officeexcel", ".xls|.xlt|.xlm|.xlsx|.xlsm|.xltx|.xltm|.xlsb|.xla|.xlam|.xll|.xlw")));
                    jextensions.Add(new JObject( new JProperty("officepowerpoint", ".ppt|.pot|.pptx|.pptm|.potx|.potm|.ppam|.ppsx|.ppsm|.sldx|.sldm")));
                    jextensions.Add(new JObject( new JProperty("officevisio", ".vsd|.vsdx|.vdx|.vsx|.vtx|.vsl|.vsdm")));
                    jextensions.Add(new JObject( new JProperty("autodesk", ".dwg|.dxf|.dwf|.dwt|.dxb|.lsp|.dcl")));
                    jextensions.Add(new JObject( new JProperty("extensions02", ".gif|.png|.jpeg|.jpg|.tiff|.tif|.bmp")));
                    jextensions.Add(new JObject( new JProperty("extensions03", ".cs|.xaml|.config|.ico")));
                    jextensions.Add(new JObject( new JProperty("extensions04", ".gitignore|.md")));
                    jextensions.Add(new JObject( new JProperty("extensions05", ".msg|.ini")));
                    jextensions.Add(".pdf|.html|.xhtml|.txt|.mp3|.aiff|.au|.midi|.wav|.pst|.xml|.java|.js");
                }

                {
                    JArray jUserExtensions =  ((JArray)jsonData["UserExtensions"]);
                    jUserExtensions.Add(new JObject( new JProperty("extensions01", ".json|.md|.js")));
                    jUserExtensions.Add(new JObject( new JProperty("officeword", ".doc|.docx|.docm|.dotx|.dotm|.rtf")));
                }

                {
                    // Список каталогов, за которыми надо следить:
                    JArray jFoldersForWatch = ((JArray)jsonData["FoldersForWatch"]);
                    jFoldersForWatch.Add(new JObject( new JProperty("folder01", @"D:\")));
                    jFoldersForWatch.Add(@"E:\Docs");
                    jFoldersForWatch.Add(@"F:\");
                }

                {
                    // Список каталогов, которые надо исключить из "слежения" (просто будут сравниваться начала имён файлов):
                    JArray jFoldersForExceptions= ((JArray)jsonData["FoldersForExceptions"]);
                    jFoldersForExceptions.Add(new JObject( new JProperty("folder01", "D:\\temp")));
                }

                {
                    JArray jFileNamesExceptions = ((JArray)jsonData["FileNamesExceptions"]);
                    jFileNamesExceptions.Add("~$");
                }
                File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented));
            }
            else
            {
                _notifyIcon.ToolTipText = "FileChangesWatcher. Right-click for menu";
                try
                {
                    jsonData = JObject.Parse(File.ReadAllText(jsonFilePath));
                    _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Icons/FileChangesWatcher.ico", UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    appendLogToDictionary("" + ex.Message + "", BalloonIcon.Error);
                    _notifyIcon.ToolTipText = "FileChangesWatcher not working. Error in ini-file. Open settings in menu.";
                    _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Icons/FileChangesWatcherDisable.ico", UriKind.Absolute));
                    //_notifyIcon.ShowBalloonTip("Error in ini-file. Open settings in menu, please", ""+ex.Message + "", BalloonIcon.Error);
                    return;
                }
            }

            // При первом запуске проверить, если в настройках нет флага, отменяющего автозагрузку,
            // то прописать автозапуск приложения в реестр:
            //if (jsonData["General"]["autostart_on_windows"]==null) {
            //    if( MessageBox.Show("Set autostart with windows?\n\n(if you say no then you can do this in context menu later)", "FileChangesWatcher", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            //    {
            //        setAutostart();
            //    }
            //    else
            //    {
            //        resetAutostart();
            //    }
            //}else 
            //if("true".Equals((bool)jsonData["General"]["autostart_on_windows"])==true){
            //    setAutostart();
            //}else{
            //    resetAutostart();
            //}

            try
            {
                // Определить количество пунктов подменю:
                Settings.log_contextmenu_size = Convert.ToInt32(jsonData["General"]["log_contextmenu_size"]);
            }
            catch (Exception _ex)
            {
                Console.WriteLine("Ошибка преобразования значения log_contextmenu_size в число. \n"+_ex.ToString() );
            }

            try
            {
                // Активировать ли систему вывода уведомлений (true/false):
                Settings.bool_display_notifications = Convert.ToBoolean(jsonData["General"]["display_notifications"]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка преобразования значения display_notifications в bool. Либо не указано, либо указано не true/false " + ex.Message);
            }

            try
            {
                // Активировать ли систему логирования (true/false):
                Settings.bool_log = Convert.ToBoolean(jsonData["General"]["log"]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка преобразования значения log в bool. Либо не указано, либо указано не true/false "+ex.Message);
            }

            try
            {
                // Каталог, куда складывать файлы логов:
                Settings.string_log_path = Convert.ToString(jsonData["General"]["log_path"]);
                if (Settings.string_log_path ==".")
                {
                    Settings.string_log_path = getExeFilePath();
                    Settings.string_log_path = Path.GetDirectoryName(Settings.string_log_path);
                }
                else
                {
                    if( LongDirectory.Exists(Settings.string_log_path ))
                    {
                    }
                    else
                    {
                        Settings.string_log_path = getExeFilePath();
                        Settings.string_log_path = Path.GetDirectoryName(Settings.string_log_path);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Неверный параметр log_path. " + ex.Message);
            }

            try
            {
                // Префикс файла логов (чтобы не петать с файлами других программ, которые могут писать в общую сетевую папку):
                Settings.string_log_file_prefix = Convert.ToString(jsonData["General"]["log_file_prefix"]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка чтения параметра log_file_prefix: " + ex.Message);
            }

            //_re_extensions = getExtensionsRegEx(iniData, new string[] { "Extensions", "UserExtensions" });
            //_re_user_extensions = getExtensionsRegEx(iniData, new string[] { "UserExtensions" });
            _re_extensions = getExtensionsRegEx(jsonData, new string[] { "Extensions", "UserExtensions" });
            _re_user_extensions = getExtensionsRegEx(jsonData, new string[] { "UserExtensions" });


            // Определить список каталогов, за которыми надо наблюдать:
            ///*
            Settings.list_folders_for_watch = new List<String>();
            //for (int i = 0; i <= iniData.Sections["FoldersForWatch"].Count - 1; i++)
            //{
            //    String folder = iniData.Sections["FoldersForWatch"].ElementAt(i).Value;
            //    if (folder.Length > 0 && Settings.list_folders_for_watch.Contains(folder) == false && LongDirectory.Exists(folder))
            //    {
            //        Settings.list_folders_for_watch.Add(folder);
            //    }
            //    else
            //    {
            //        init_text_message.Append("\nThere is no folder \"" + folder + "\" for watching. Skipped.");
            //    }
            //}
            {
                JArray jFoldersForWatch = (JArray)jsonData["FoldersForWatch"];
                Settings.list_folders_for_watch = getListOfInnerValues(jsonData["FoldersForWatch"]);
            }

            if (e != null)
            {
                for (int i = 0; i <= e.Args.Length - 1; i++)
                {
                    String folder = e.Args[i];
                    if (folder.Length > 0 && Settings.list_folders_for_watch.Contains(folder) == false && LongDirectory.Exists(folder))
                    {
                        Settings.list_folders_for_watch.Add(folder);
                    }
                }
            }

            {
                List<string> folders_for_remove = new List<string>();
                foreach (string folder in Settings.list_folders_for_watch) {
                    if ((folder.Length > 0 && LongDirectory.Exists(folder)) == false) {
                        folders_for_remove.Add(folder);
                    }
                }
                foreach (string folder in folders_for_remove) {
                    Settings.list_folders_for_watch.Remove(folder);
                }
            }

            // Список каталогов с исключениями:
            //List<string> _arr_folders_for_exceptions = new List<String>();
            //for (int i = 0; i <= iniData.Sections["FoldersForExceptions"].Count - 1; i++)
            //{
            //    String folder = iniData.Sections["FoldersForExceptions"].ElementAt(i).Value;
            //    if (folder.Length > 0)
            //    {
            //        _arr_folders_for_exceptions.Add(folder);
            //    }
            //}
            //Settings.arr_folders_for_exceptions = _arr_folders_for_exceptions;
            Settings.arr_folders_for_exceptions = getListOfInnerValues(jsonData["FoldersForExceptions"]);

            // Список файлов с исключениями:
            //List<string> _arr_files_for_exceptions = new List<String>();
            //for (int i = 0; i <= iniData.Sections["FileNamesExceptions"].Count - 1; i++)
            //{
            //    String folder = iniData.Sections["FileNamesExceptions"].ElementAt(i).Value;
            //    if (folder.Length > 0)
            //    {
            //        _arr_files_for_exceptions.Add(folder);
            //    }
            //}
            //Settings.arr_files_for_exceptions = _arr_files_for_exceptions;
            Settings.arr_files_for_exceptions = getListOfInnerValues(jsonData["FileNamesExceptions"]);

            if (Settings.list_folders_for_watch.Count >= 1)
            {
                init_text_message.Append("\n");
                init_text_message.Append( setWatcherForFolderAndSubFolders(Settings.list_folders_for_watch.ToArray()) );
            }
            else
            {
                appendLogToDictionary("No watching for folders. Set folders correctly.", BalloonIcon.Info);
                //_notifyIcon.ShowBalloonTip("Info", "No watching for folders. Set folders correctly.", BalloonIcon.Info);
            }

            // Если пользователь администратор, то проверить наличие групповой политики, включающую регистрацию
            // файловых событий в журнале безопасности windows. Без прав администратора нет возможности
            // прочитать эту политику.
            /*
            if (_IsUserAdministrator == true)
            {
                //Dictionary<AuditPolicy.AuditEventPolicy, AuditPolicy.AuditEventStatus> pol = AuditPolicy.GetPolicies();
                Dictionary<AuditPolicy.AuditEventPolicy, Dictionary<String, AuditPolicy.AuditEventStatus>> sub_pol = AuditPolicy.GetSubcategoryPolicies();
                init_text_message.Append("\n-----------------");
                Dictionary<String, AuditPolicy.AuditEventStatus> object_access_policy = null;
                AuditPolicy.AuditEventStatus file_system_policy_status = AuditPolicy.AuditEventStatus.AUDIT_NONE;
                if (sub_pol.ContainsKey(AuditPolicy.AuditEventPolicy.OBJECT_ACCESS) == true)
                {
                    sub_pol.TryGetValue(AuditPolicy.AuditEventPolicy.OBJECT_ACCESS, out object_access_policy);
                    if (object_access_policy.ContainsKey("FILE_SYSTEM") == true)
                    {
                        object_access_policy.TryGetValue("FILE_SYSTEM", out file_system_policy_status);
                    }
                }
                init_text_message.Append("\n\n");
                init_text_message.Append("File System policy status: " + file_system_policy_status.ToString());
                if (file_system_policy_status == AuditPolicy.AuditEventStatus.AUDIT_SUCCESS ||
                    file_system_policy_status == AuditPolicy.AuditEventStatus.AUDIT_SUCCESS_FAILURE
                  )
                {
                    init_text_message.Append(" - GOOD");
                }
                else
                {
                    init_text_message.Append(" - BAD. You can't catch user name who deleted files because policy FILE_SYSTEM\\AUDIT_SUCCESS is off.");
                }
            }
            else
            {
                init_text_message.Append("\n");
                init_text_message.Append("You have to run this application as system administrator to catch windows security event log to find out who delete files.");
            }
            //*/

            // Прочитать логи от предыдущего сеанса и добавить их в контекстное меню:
            //string history_file_name = - всё таки у меня для этого файла есть глобальная переменная. TODO - проверить и удалить эту строку.
            getHistoryFileName();
            if(LongFile.Exists(history_file_name) == true)
            {
                string[] strings = LongFile.ReadAllLines(history_file_name);
                foreach(string str in strings)
                {
                    if(str.Length > 0)
                    {
                        string[] prms = str.Split('\t');
                        if(prms.Count()==4 || prms.Count() == 5)
                        {
                            string str_unix_time = prms[0];
                            string str_ChangesType = prms[1];
                            string str_wType = prms[2];
                            string FullPath = prms[3];
                            // Проверить, что файл или каталог существует. Нельзя восстанавливать в контекстное меню несуществующие объекты.
                            if( !( LongFile.Exists(FullPath)==true || LongDirectory.Exists(FullPath)==true) )
                            {
                                continue;
                            }
                            long unix_time = Convert.ToInt64(str_unix_time);
                            DateTime dt = UnixTimestampToDateTime(unix_time);
                            WatcherChangeTypes wct = (WatcherChangeTypes)Enum.Parse( typeof(WatcherChangeTypes), str_ChangesType);
                            WatchingObjectType wType = (WatchingObjectType)Enum.Parse(typeof(WatchingObjectType), str_wType);

                            MenuItemData_CCRD ccrd = null;
                            if (str_ChangesType == "Renamed")
                            {
                                string OldFullPath = prms[4];
                                //RenamedEventArgs _re = new RenamedEventArgs(wct, Path.GetDirectoryName(FullPath), Path.GetFileName(FullPath), Path.GetFileName(OldFullPath) );
                                RenamedEventArgs _re = new RenamedEventArgs(wct, LongDirectory.GetDirectoryName(FullPath), Path.GetFileName(FullPath), Path.GetFileName(OldFullPath) );
                                ccrd = new MenuItemData_CCRD(_re, wType, dt);
                            }
                            else
                            {
                                //FileSystemEventArgs _e = new FileSystemEventArgs(wct, Path.GetDirectoryName(FullPath), Path.GetFileName(FullPath));
                                FileSystemEventArgs _e = new FileSystemEventArgs(wct, LongDirectory.GetDirectoryName(FullPath), Path.GetFileName(FullPath));
                                ccrd = new MenuItemData_CCRD(_e, wType, dt);
                            }
                            stackPaths.Add(ccrd);
                        }
                    }
                }
            }

            appendLogToDictionary("Initial settings.\n"+init_text_message.ToString(), BalloonIcon.Info);

            if (Settings.bool_display_notifications == true)
            {
                TrayPopupMessage popup = new TrayPopupMessage("Initial settings.\n" + init_text_message.ToString(), "Initial initialization", WatchingObjectType.File, App.NotifyIcon, null, TrayPopupMessage.ControlButtons.Clipboard);
                popup.MouseDown += (sender, args) =>
                {
                    if (App.NotifyIcon.CustomBalloon != null) {
                        App.NotifyIcon.CustomBalloon.IsOpen = false;
                    }
                    App.ShowMessage("Initial settings.\n" + init_text_message.ToString());
                };
                App.NotifyIcon.ShowCustomBalloon(popup, PopupAnimation.None, 4000);
            }
        }

        //public static Regex getExtensionsRegEx( IniParser.Model.IniData data, string[] sections)
        //{
        //    List<string> arr_extensions_for_filter = new List<String>();
        //    String _extensions = "";
        //    Regex re = new Regex("\\.");
        //    foreach (string section in sections) {
        //        if (data.Sections.ContainsSection(section) == true) {
        //            for (int i = 0; i <= data.Sections[section].Count - 1; i++) {
        //                String folder = data.Sections[section].ElementAt(i).Value;
        //                folder = (new Regex("(^|)|(|$)")).Replace(folder, "");
        //                if (folder.Length > 0) {
        //                    if (_extensions.Length > 0) {
        //                        _extensions += "|";
        //                    }
        //                    _extensions += re.Replace(folder, "\\.");
        //                }
        //            }
        //        }
        //    }
        //    //_extensions = @".*(" + _extensions + ")$";
        //    _extensions = @"$(?<=(" + _extensions + "))";  // Крутое решение по ускорению проверки расширений: http://stackoverflow.com/questions/2081555/testing-for-endswith-efficiently-with-a-regex?answertab=votes#tab-top
        //    _extensions = (new Regex("(\\|\\|)")).Replace(_extensions, "|");
            
        //    return new Regex(_extensions, RegexOptions.IgnoreCase);
        //}

        public static Regex getExtensionsRegEx(JObject data, string[] sections) {
            List<string> arr_extensions_for_filter = new List<String>();
            String _extensions = "";
            Regex re = new Regex("\\.");
            foreach (string section in sections) {
                if (data[section] != null) {
                    List<string> list = getListOfInnerValues(data[section]);

                    foreach(string folder in list){ 
                        if (folder.Length > 0) {
                            if (_extensions.Length > 0) {
                                _extensions += "|";
                            }
                            _extensions += re.Replace(folder, "\\.");
                        }
                    }
                }
            }
            //_extensions = @".*(" + _extensions + ")$";
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
            if (data.Type == JTokenType.String) {
                values.Add((string)data);
            }else if (data.Type == JTokenType.Object) {
                foreach (JProperty prop in ((JObject)data).Properties()) {
                    if (prop.Value.Type == JTokenType.String) {
                        string value = (string)prop.Value;
                        values.Add(value);
                    }else if (prop.Value.Type == JTokenType.Object) {
                        List<string> list = getListOfInnerValues( (JObject)prop.Value );
                        values.AddRange(list);
                    }else if (prop.Value.Type == JTokenType.Array) {
                        List<string> list = getListOfInnerValues( prop.Value );
                        values.AddRange(list);
                    }
                }
            }else if (data.Type == JTokenType.Array) {
                JArray arr = (JArray)data;
                for(int i=0; i<=arr.Count-1; i++) {
                    JToken prop = arr[i];
                    if (prop.Type == JTokenType.String) {
                        string value = (string)prop;
                        values.Add(value);
                    }
                    else if (prop.Type == JTokenType.Object) {
                        List<string> list = getListOfInnerValues( prop );
                        values.AddRange(list);
                    }else if (prop.Type == JTokenType.Array) {
                        List<string> list = getListOfInnerValues( prop );
                        values.AddRange(list);
                    }
                }
            }
            return values;
        }

        static List<FileSystemWatcher> list_watcher = new List<FileSystemWatcher>();
        //static List<FileSystemWatcher> list_watcher_folders = new List<FileSystemWatcher>();

        private static string setWatcherForFolderAndSubFolders(String[] _paths)
        {
            foreach (String _path in _paths)
            {
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
                        //|  NotifyFilters.LastAccess // - не знаю как ловить - Теперь знаю. Нужно в реестре установить атрибут NtfsDisableLastAccessUpdate=0 и перезапустить комп. НО!!! Этот атрибут устанавливается в течении ЧАСА после доступа к файлу по NTFS и СУТОК при доступе к FAT!!! 
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
                //if (false == true)
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

        // Соответствие между именем диска и его физическим именем:
        static Dictionary<string, string> dict_drive_phisical = new Dictionary<string, string>();

        //static Dictionary<String, BitmapImage> icons_map = new Dictionary<string, BitmapImage>();

        static Regex _re_extensions = null; // Расширения, которые логируются программой (являются суммой секций Extensions и UserExtensions )
        static Regex _re_user_extensions = null;  // Расширения, которые выводятся юзеру (только секция UserExtensions).
        public static Regex re_extensions
        {
            get
            {
                return _re_extensions;
            }
        }

        public class Settings {
            // Список каталогов, которые надо исключить из вывода:
            public static List<String> arr_folders_for_exceptions = null;
            public static List<String> arr_files_for_exceptions = null;
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


        // Если _old_path!=null, то надо переименовать имеющиеся пути с _old_path на _path
        private static int menuitem_header_length = 30;
        #region Legasy
        //private static void appendPathToDictionary(string _path, WatcherChangeTypes changedType, WatchingObjectType wType, MenuItemData menuItemData)
        //{
        //    String str = _path;
        //    Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
        //    {
        //        _notifyIcon.CloseBalloon();

        //        //if (changedType == WatcherChangeTypes.Deleted)
        //        //{

        //        //}
        //        //else // остальные случаи: WatcherChangeTypes.Changed, WatcherChangeTypes.Created, WatcherChangeTypes.Renamed
        //        {
        //            // Получить владельца файла:
        //            /*
        //            string user_owner = null;
        //            try
        //            {
        //                // http://stackoverflow.com/questions/7445182/find-out-file-owner-creator-in-c-sharp
        //                user_owner = System.IO.File.GetAccessControl(_path).GetOwner(typeof(System.Security.Principal.NTAccount)).ToString();
        //            }
        //            catch(Exception ex)
        //            {
        //                user_owner = "<unknown>";
        //            }
        //            //*/

        //            //DriveInfo di = new DriveInfo(Path.GetPathRoot(_path));
        //            string s = Path.GetPathRoot(_path);

        //            _notifyIcon.HideBalloonTip();
        //            //_notifyIcon.ShowBalloonTip("go to path:", /*"file owner: "+user_owner+"\n"+*/ _path, BalloonIcon.Info);
        //            //bool_is_path_tooltip = true; // После клика или после исчезновения баллона этот флаг будет сброшен.
        //            //bool_is_ballow_was_shown = false;

        //            //_notifyIcon.TrayBalloonTipClicked
        //            // Если такой путь уже есть в логе, то нужно его удалить. Это позволит переместить элемент наверх списка.
        //            //if (stackPaths.ContainsKey(_path) == true)
        //            // TODO: Если существующий индекс стоит в самой первой позиции, то не надо сверкать балоном. Ещё не реализовано.
        //            bool show_balloon = stackPaths.FindIndex(x => x.path == _path) == 0;
        //            // while (stackPaths.Exists(x => x.path == _path) == true)
        //            while(stackPaths.Exists(x => x.path == _path) == true && stackPaths.ElementAt(stackPaths.Count-1).path==_path )
        //            {
        //                MenuItemData _id = stackPaths.Find(x => x.path == _path);
        //                _notifyIcon.ContextMenu.Items.Remove(_id.mi);
        //                stackPaths.Remove(_id);
        //            }

        //            //if (stackPaths.ContainsKey(_path) == false)
        //            {
        //                //int max_value = 0;
        //                if (stackPaths.Count > 0)
        //                {
        //                    // i = stackPaths.Last().Value;
        //                    // http://stackoverflow.com/questions/11549580/find-key-with-max-value-from-sorteddictionary
        //                    //max_value = stackPaths.OrderBy(d => d.index).Last().index;
        //                }

        //                // Создать пункт меню и наполнить его смыслом:
        //                MenuItem mi = new MenuItem()
        //                {
        //                    Name="mi_main"
        //                };
        //                if (changedType == WatcherChangeTypes.Deleted)
        //                {
        //                    string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss Deleted ") + _path;
        //                    mi.ToolTip = "Deleted " + _path;
        //                    mi.Header = mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
        //                }
        //                else
        //                {
        //                    string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss ") + _path;
        //                    mi.ToolTip = "Go to " + _path;
        //                    mi.Header = mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
        //                    mi.Command = CustomRoutedCommand;
        //                    mi.CommandParameter = new Path_ObjectType(_path, wType);
        //                }
        //                /* Пробую установить цвет шрифта для разных событий над файлом. Плохая идея, т.к. приложение может несколько раз менять файл во время записи. Даже переименовывать.
        //                if (changedType == WatcherChangeTypes.Changed)
        //                {
        //                    mi.Foreground = new SolidColorBrush( System.Windows.Media.Color.FromArgb(255, 255, 139, 0) );
        //                }
        //                else
        //                if (changedType == WatcherChangeTypes.Created)
        //                {
        //                    mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 255, 0));
        //                }
        //                else
        //                if (changedType == WatcherChangeTypes.Renamed)
        //                {
        //                    mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 255));
        //                }
        //                //*/

        //                // Получить иконку файла для вывода в меню:
        //                // http://www.codeproject.com/Articles/29137/Get-Registered-File-Types-and-Their-Associated-Ico
        //                // Загрузить иконку файла в меню: http://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap?answertab=votes#tab-top
        //                // Как-то зараза не грузится простым присваиванием.
        //                String file_ext = Path.GetExtension(_path);
        //                Icon mi_icon = null;
        //                BitmapImage bitmapImage = null;

        //                // Кешировать иконки для файлов:
        //                if (icons_map.TryGetValue(file_ext, out bitmapImage) == true)
        //                {
        //                }
        //                else
        //                {
        //                    try
        //                    {
        //                        // На сетевых путях выдаёт Exception. Поэтому к сожалению не годиться.
        //                        //mi_icon = Icon.ExtractAssociatedIcon(_path);// getIconByExt(file_ext);

        //                        // Этот метод работает: http://stackoverflow.com/questions/1842226/how-to-get-the-associated-icon-from-a-network-share-file?answertab=votes#tab-top
        //                        ushort uicon;
        //                        StringBuilder strB = new StringBuilder(_path);
        //                        IntPtr handle = ExtractAssociatedIcon(IntPtr.Zero, strB, out uicon);
        //                        mi_icon = Icon.FromHandle(handle);
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        mi_icon = null;
        //                        icons_map.Add(file_ext, null);
        //                    }

        //                    if (mi_icon != null)
        //                    {
        //                        using (MemoryStream memory = new MemoryStream())
        //                        {
        //                            mi_icon.ToBitmap().Save(memory, ImageFormat.Png);
        //                            memory.Position = 0;
        //                            bitmapImage = new BitmapImage();
        //                            bitmapImage.BeginInit();
        //                            bitmapImage.StreamSource = memory;
        //                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        //                            bitmapImage.EndInit();
        //                            icons_map.Add(file_ext, bitmapImage);
        //                        }
        //                    }
        //                }
        //                if (bitmapImage != null)
        //                {
        //                    mi.Icon = new System.Windows.Controls.Image
        //                    {
        //                        Source = bitmapImage
        //                    };
        //                }
        //                else
        //                {
        //                    mi.Icon = null;
        //                }

        //                //if (wType == WatchingObjectType.File)
        //                {
        //                    // Так определять Grid гораздо проще: http://stackoverflow.com/questions/5755455/how-to-set-control-template-in-code
        //                    string str_template = @"
        //                    <ControlTemplate 
        //                                        xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        //                                        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        //                                        xmlns:tb='http://www.hardcodet.net/taskbar'
        //                                        xmlns:local='clr-namespace:FileChangesWatcher'
        //                     >
        //                        <Grid x:Name='mi_grid'>
        //                            <Grid.ColumnDefinitions>
        //                                <ColumnDefinition Width='*'/>
        //                                <ColumnDefinition Width='20'/>
        //                                <ColumnDefinition Width='20'/>
        //                            </Grid.ColumnDefinitions>
        //                        </Grid>
        //                    </ControlTemplate>
        //                ";
        //                    MenuItem _mi = new MenuItem();
        //                    Grid mi_grid = null; // new Grid();
        //                    ControlTemplate ct = (ControlTemplate)XamlReader.Parse(str_template);
        //                    _mi.Template = ct;
        //                    if (_mi.ApplyTemplate())
        //                    {
        //                        mi_grid = (Grid)ct.FindName("mi_grid", _mi);
        //                    }
        //                    MenuItem mi_clipboard = new MenuItem() {
        //                        Name="mi_clipboard"
        //                    };
        //                    mi_clipboard.Icon = new System.Windows.Controls.Image
        //                    {
        //                        Source = new BitmapImage(
        //                        new Uri("pack://application:,,,/Icons/Clipboard.ico"))
        //                    };
        //                    mi_clipboard.ToolTip = "Copy path to clipboard";
        //                    mi_clipboard.Command = CustomRoutedCommand_CopyTextToClipboard;
        //                    mi_clipboard.CommandParameter = _path;
        //                    MenuItem mi_enter = new MenuItem()
        //                    {
        //                        Name="mi_enter"
        //                    };
        //                    mi_enter.Icon = new System.Windows.Controls.Image
        //                    {
        //                        Source = new BitmapImage(
        //                        new Uri("pack://application:,,,/Icons/Enter.ico"))
        //                    };
        //                    // Если объект удалён, то нельзя его выполнить
        //                    if (changedType != WatcherChangeTypes.Deleted)
        //                    {
        //                        mi_enter.ToolTip = "Execute file";
        //                        mi_enter.Command = CustomRoutedCommand_ExecuteFile;
        //                        mi_enter.CommandParameter = new Path_ObjectType(_path, wType);
        //                    }

        //                    Grid.SetColumn(mi, 0);
        //                    Grid.SetRow(mi, 0);
        //                    Grid.SetColumn(mi_clipboard, 1);
        //                    Grid.SetRow(mi_clipboard, 0);
        //                    mi_grid.Children.Add(mi);
        //                    mi_grid.Children.Add(mi_clipboard);

        //                    if (wType == WatchingObjectType.File)
        //                    {
        //                        Grid.SetColumn(mi_enter, 2);
        //                        Grid.SetRow(mi_enter, 0);
        //                        mi_grid.Children.Add(mi_enter);
        //                    }
        //                    mi = _mi;
        //                }

        //                //MenuItemData id = new MenuItemData(_path, mi, MenuItemData.Type.file_folder ); // user_owner
        //                menuItemData.path = _path;
        //                menuItemData.mi = mi;
        //                //menuItemData.type = changedType;
        //                menuItemData.type = MenuItemData.Type.file_folder;
        //                stackPaths.Add(menuItemData);
        //                //_notifyIcon.ContextMenu.Items.Insert(0, id.mi);
        //                currentMenuItem = menuItemData;
        //                /*
        //                _notifyIcon.ShowBalloonTip("go to path:", _path, BalloonIcon.Info);
        //                bool_is_path_tooltip = true; // После клика или после исчезновения баллона этот флаг будет сброшен.
        //                bool_is_ballow_was_shown = false;
        //                //*/

        //                // Если текущий пункт меню является первым в стеке, то можно вывести баллон, иначе - нет, потому что возникло следующее событие
        //                // и его нельзя перекрывать старым баллоном.
        //                MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).Last();
        //                if (first_menuItemData == menuItemData)
        //                {
        //                    TrayPopupMessage popup = null;
        //                    if (changedType == WatcherChangeTypes.Deleted)
        //                    {
        //                        popup = new TrayPopupMessage(_path, "Removed: ", wType, _notifyIcon, TrayPopupMessage.ControlButtons.Clipboard);
        //                        popup.MouseDown += (sender, args) =>
        //                        {
        //                            _notifyIcon.CustomBalloon.IsOpen = false;
        //                            //App.gotoPathByWindowsExplorer(popup.path, popup.wType);
        //                        };
        //                    }
        //                    else
        //                    {
        //                        popup = new TrayPopupMessage(_path, wType, _notifyIcon, TrayPopupMessage.ControlButtons.Clipboard | TrayPopupMessage.ControlButtons.Run);
        //                        popup.MouseDown += (sender, args) =>
        //                        {
        //                            _notifyIcon.CustomBalloon.IsOpen = false;
        //                            App.gotoPathByWindowsExplorer(popup.path, popup.wType);
        //                        };
        //                    }

        //                    _notifyIcon.ShowCustomBalloon(popup, PopupAnimation.Fade, 4000);
        //                }

        //                reloadCustomMenuItems();
        //            }
        //        }
        //    });
        //}
        #endregion 

        public static void customballoon_close(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                // popup_test.Visibility = Visibility.Hidden;
                if (App.NotifyIcon.CustomBalloon != null) {
                    _notifyIcon.CustomBalloon.IsOpen = false;
                }
            });
            System.Timers.Timer temp = ((System.Timers.Timer)sender);
            temp.Stop();
        }

        private static void appendLogToDictionary(String logText, BalloonIcon _ballonIcon)
        {
            _MenuItemData menuItemData = new MenuItemData_Log(_ballonIcon, logText, DateTime.Now);
            stackPaths.Add(menuItemData);
            reloadCustomMenuItems();
        }

        #region Legasy
        //private static void appendLogToDictionary1(String logText, BalloonIcon _ballonIcon)
        //{
        //    Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
        //    {
        //        MenuItem mi = new MenuItem();
        //        string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss ") + logText.Split('\n')[0];
        //        mi.Header = mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
        //        //mi.Header = logText.Split('\n')[0];
        //        mi.ToolTip = "message from program:\n" + logText;
        //        mi.Command = CustomRoutedCommand_ShowMessage;
        //        mi.CommandParameter = logText;
        //        mi.IsEnabled = true;

        //        MenuItemData id = MenuItemData.CreateLogRecord(mi, _ballonIcon, logText); // user_owner
        //        stackPaths.Add(id);
        //        currentMenuItem = id;
        //    //_notifyIcon.ShowBalloonTip("FileChangesWatcher", logText, _ballonIcon);

        //        // Если текущий пункт меню является первым в стеке, то можно вывести баллон, иначе - нет, потому что возникло следующее событие
        //        // и его нельзя перекрывать старым баллоном.
        //        MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).Last();
        //        if (first_menuItemData == id)
        //        {
        //            TrayPopupMessage popup = new TrayPopupMessage(logText, "Initial initialization", WatchingObjectType.File, _notifyIcon, TrayPopupMessage.ControlButtons.Clipboard);
        //            popup.MouseDown += (sender, args) =>
        //            {
        //                _notifyIcon.CustomBalloon.IsOpen = false;
        //                ShowMessage(logText);
        //            };
        //            _notifyIcon.ShowCustomBalloon(popup, PopupAnimation.Fade, 3000);
        //        }                
        //        reloadCustomMenuItems();
        //    });
        //}

        /*
        private static void removePathFromDictionary(String _path)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                //if (stackPaths.TryGetValue(_path, out _id))
                if(stackPaths.Exists(x=>x.path==_path))
                {
                    MenuItemData _id = stackPaths.Find(x => x.path == _path);
                    _notifyIcon.ContextMenu.Items.Remove(_id.mi);
                    stackPaths.Remove(_id);
                    reloadCustomMenuItems();
                }
            });
        }
        //*/
        
        // Определить все события, которые в данный момент находятся в dict_path_time.
        // После определения необходимо стереть все элементы, которые были на момент входа в функцию.
        // Вернуть список событий, которые были получены в результате обработки очереди.
        // https://codewala.net/2013/10/04/reading-event-logs-efficiently-using-c/

        //static List<Dictionary<string, string>> getDeleteInfo(/*string _path*/)
        //{
        //    List<Dictionary<string, string>> list_events = new List<Dictionary<string, string>>();
        //    List<Dictionary<string, string>> event_4653 = new List<Dictionary<string, string>>();
        //    List<Dictionary<string, string>> event_4660 = new List<Dictionary<string, string>>();
        //    List<Dictionary<string, string>> event_4656 = new List<Dictionary<string, string>>();
        //    // Удаление иногда бывает длительным и нужно дождаться, пока стек файлов "опустеет" (наполняется он снаружи этого цикла):
        //    while (dict_path_time.Count > 0)
        //    {

        //        //string result = "неизвестный удалил "+_path;
        //        // Тут будут события, которые взяты из журнала событий (как только они там наступят)
        //        List<Dictionary<string, string>> dict_event_path_object = new List<Dictionary<string, string>>();
        //        try
        //        {
        //            int j = 0;
        //            //string disk_name = Path.GetPathRoot(_path);
        //            //if (!(disk_name == null || disk_name.Length == 0))
        //            {
        //                //disk_name = disk_name.Split('\\')[0];
        //                int i = 0;
        //                EventRecord eventdetail = null;
        //                EventLogReader logReader = null; // new EventLogReader(eventsQuery);
        //                List<KeyValuePair<string, Path_ObjectType>> arr_partial_events = new List<KeyValuePair<string, Path_ObjectType>>();
        //                do
        //                {
        //                    arr_partial_events = dict_path_time.ToList();
        //                    //arr_partial_events.AddRange( dict_path_time.ToList() );
        //                    DateTime curr_time = DateTime.Now;
        //                    int delta_seconds = 60;
        //                    DateTime min_time = DateTime.Now;
        //                    // Сначала отсортировать события, которые уже не подпадают под рассмотрение, т.к. произошли достаточно давно.
        //                    foreach (KeyValuePair<string, Path_ObjectType> path_time in dict_path_time)
        //                    {
        //                        // Если событие зарегистрировано в указанный интервал времени (delta_seconds) от момента входа в функцию,
        //                        // то использовать эту запись в дальнейшем. Если не попадает, то удалить:
        //                        if ((curr_time - path_time.Value.dateTime).TotalSeconds <= delta_seconds)
        //                        {
        //                            if (min_time > path_time.Value.dateTime)
        //                            {
        //                                min_time = path_time.Value.dateTime;
        //                            }
        //                        }
        //                        else
        //                        {
        //                            arr_partial_events.Remove(path_time);
        //                            if (arr_partial_events.Count == 0)
        //                            {
        //                                return dict_event_path_object;
        //                            }
        //                            //DateTime temp = new DateTime();
        //                            Path_ObjectType temp;
        //                            if (dict_path_time.TryRemove(path_time.Key, out temp) == false)
        //                            {
        //                                Console.Write("Удаление ключа " + path_time.Key + " не удалось");
        //                            }
        //                        }
        //                    }
        //                    // Пройтись по всем и выкинуть те, у кого время старше 10 сек от текущего момента:
        //                    /*
        //                    string query = string.Format("*[System/EventID=4656 and System[TimeCreated[@SystemTime >= '{0}']]] and *[System[TimeCreated[@SystemTime <= '{1}']] and EventData[Data[@Name='AccessMask']='0x10000'] ]",
        //                        DateTime.Now.AddSeconds(-1).ToUniversalTime().ToString("o"),
        //                        DateTime.Now.AddSeconds( 1).ToUniversalTime().ToString("o")
        //                        );
        //                        */
        //                    // Запросить все события удаления файлов, которые входят в интервал min_time
        //                    /* Пока не буду анализировать логи события 4660, т.к. на Windows Server 2012 R2 они не возникают.
        //                     * Это не очень хорошо, что я так делаю, т.к. именно 4660 говорит о том, что объект был удалён, 
        //                     * а 4656 всего лишь попытка выполнить удаления (но как правило удачная), но пока на это и расчёт.
        //                    string query = string.Format("*[ System[EventID=4656 or EventID=4663 or EventID=4660] and System[TimeCreated[@SystemTime >= '{0}']] and EventData[Data[@Name='AccessMask']='0x10000'] ]",
        //                        min_time.AddSeconds(-1).ToUniversalTime().ToString("o")
        //                        );
        //                    */
        //                    //string query = string.Format("*[ System[EventID=4656] and System[TimeCreated[@SystemTime >= '{0}']] and EventData[Data[@Name='AccessMask']='0x10000'] ]",
        //                    string query = string.Format("*[ System[EventID=4656] and System[TimeCreated[@SystemTime >= '{0}']] ]",
        //                        min_time.AddSeconds(-1).ToUniversalTime().ToString("o")
        //                        );
        //                    EventLogQuery eventsQuery = new EventLogQuery("Security", PathType.LogName, query);
        //                    logReader = new EventLogReader(eventsQuery);
        //                    eventdetail = logReader.ReadEvent();
        //                    // Если записи из журнала прочитать не удалось, то читать их повторно, но не больше 10 раз (1 сек по 0.1)
        //                    if (i++ > 10 || eventdetail != null)
        //                    {
        //                        break;
        //                    }
        //                    Thread.Sleep(100);
        //                } while (eventdetail == null);

        //                for (; eventdetail != null; eventdetail = logReader.ReadEvent())
        //                {
        //                    // https://techoctave.com/c7/posts/113-c-reading-xml-with-namespace
        //                    XmlDocument doc = new XmlDocument();
        //                    doc.LoadXml(eventdetail.ToXml());
        //                    XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
        //                    nsmgr.AddNamespace("def", "http://schemas.microsoft.com/win/2004/08/events/event");
        //                    XmlNode root = doc.DocumentElement;

        //                    Dictionary<string, string> dict_event_object = new Dictionary<string, string>();

        //                    XmlNode node_EventID = root["System"].SelectSingleNode("def:EventID", nsmgr);
        //                    string str_EventID = node_EventID.InnerText;
        //                    dict_event_object.Add("EventID", str_EventID);

        //                    switch (str_EventID)
        //                    {
        //                        // Не самый корректный способ определяющий удаление файла, но пока оставлю этот.
        //                        // TODO: 4660 - реальное событие удаление, но бывает не во всех ОС. Надо разобраться в чём дело.
        //                        case "4656":
        //                            break;
        //                        default:
        //                            // чтобы не тратить время на остальные проверки переходить к следующему событию.
        //                            continue;
        //                    }

        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_EventRecordID = root["System"].SelectSingleNode("def:EventRecordID", nsmgr);
        //                        string str_EventRecordID = node_EventRecordID.InnerText;
        //                        dict_event_object.Add("EventRecordID", str_EventRecordID);
        //                    }

        //                    if (str_EventID == "4656")
        //                    {
        //                        // Строки настраиваемых форматов даты и времени: https://msdn.microsoft.com/ru-ru/library/8kb3ddd4%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
        //                        // http://stackoverflow.com/questions/3075659/what-is-the-time-format-of-windows-event-log
        //                        XmlNode node_TimeCreated_SystemTime = root["System"].SelectSingleNode("def:TimeCreated[@SystemTime]", nsmgr);
        //                        string str_TimeCreated_SystemTime = node_TimeCreated_SystemTime.Attributes.GetNamedItem("SystemTime").InnerText;
        //                        DateTime dateValue = DateTime.Parse(str_TimeCreated_SystemTime, null, DateTimeStyles.None);
        //                        string str_TimeCreated = dateValue.ToString();
        //                        dict_event_object.Add("TimeCreated", str_TimeCreated);
        //                    }

        //                    string str_ObjectType = "";
        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_ObjectType = root["EventData"].SelectSingleNode("def:Data[@Name='ObjectType']", nsmgr);
        //                        str_ObjectType = node_ObjectType.InnerText;
        //                        dict_event_object.Add("ObjectType", str_ObjectType);
        //                    }

        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_SubjectUserName = root["EventData"].SelectSingleNode("def:Data[@Name='SubjectUserName']", nsmgr);
        //                        string str_SubjectUserName = node_SubjectUserName.InnerText;
        //                        dict_event_object.Add("SubjectUserName", str_SubjectUserName);
        //                    }

        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_SubjectDomainName = root["EventData"].SelectSingleNode("def:Data[@Name='SubjectDomainName']", nsmgr);
        //                        string str_SubjectDomainName = node_SubjectDomainName.InnerText;
        //                        dict_event_object.Add("SubjectDomainName", str_SubjectDomainName);
        //                    }

        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_ProcessName = root["EventData"].SelectSingleNode("def:Data[@Name='ProcessName']", nsmgr);
        //                        string str_ProcessName = node_ProcessName.InnerText;
        //                        dict_event_object.Add("ProcessName", str_ProcessName);
        //                    }

        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_HandleId = root["EventData"].SelectSingleNode("def:Data[@Name='HandleId']", nsmgr);
        //                        string str_HandleId = node_HandleId.InnerText;
        //                        dict_event_object.Add("HandleId", str_HandleId);
        //                    }

        //                    string str_AccessMask = "";
        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_HandleId = root["EventData"].SelectSingleNode("def:Data[@Name='AccessMask']", nsmgr);
        //                        str_AccessMask = node_HandleId.InnerText;
        //                        dict_event_object.Add("AccessMask", str_AccessMask);
        //                        int value = (int)new System.ComponentModel.Int32Converter().ConvertFromString(str_AccessMask);
        //                        if( (value & 0x10000) != 0x10000)
        //                        {
        //                            continue;
        //                        }
        //                    }

        //                    if (str_EventID == "4656")
        //                    {
        //                        XmlNode node_ObjectName = root["EventData"].SelectSingleNode("def:Data[@Name='ObjectName']", nsmgr);
        //                        string str_ObjectName = node_ObjectName.InnerText;
        //                        dict_event_object.Add("ObjectName", str_ObjectName);

        //                        // Дружественное название имени объекта в списке зарегистрированных событий:
        //                        string user_friendly_path = str_ObjectName;
        //                        foreach (KeyValuePair<string, string> drive_phisical in dict_drive_phisical)
        //                        {
        //                            string drive = drive_phisical.Key;
        //                            string phisical = drive_phisical.Value;
        //                            if (str_ObjectName.StartsWith(phisical) == true || str_ObjectName.StartsWith(drive) == true)
        //                            {
        //                                user_friendly_path = str_ObjectName.Replace(phisical, drive + "\\");
        //                                bool bool_is_path_watchable = false;
        //                                // https://www.ultimatewindowssecurity.com/securitylog/encyclopedia/event.aspx?eventID=4663
        //                                // Object Type: "File" for file or folder but can be other types of objects such as Key, SAM, SERVICE OBJECT, etc.
        //                                bool_is_path_watchable = check_path_is_in_watchable(user_friendly_path, WatchingObjectType.Folder, _re_extensions); // "Folder"); // После удаления нет возможности отличить каталог от файла. Поэтому буду проверять путь только на соответствие каталогу. str_ObjectType);
        //                                if (bool_is_path_watchable == true)
        //                                {
        //                                    dict_event_object.Add("_user_friendly_path", user_friendly_path);
        //                                    dict_event_path_object.Add(dict_event_object);
        //                                }
        //                                break;
        //                            }
        //                        }
        //                        // Т.к. событие обработано, то больше эта запись из журнала регистрации событий в программе не понадобиться. Удалить её совсем:
        //                        //DateTime temp = new DateTime();
        //                        Path_ObjectType temp;
        //                        if (dict_path_time.TryRemove(user_friendly_path, out temp) == false)
        //                        {
        //                            Console.Write("Удаление ключа " + user_friendly_path + " не удалось");
        //                        }
        //                    }
        //                }
        //                // Очистить главный стек событий стёртых файлов от обработанных событий:
        //                foreach (Dictionary<string, string> o in dict_event_path_object.ToArray() )
        //                {
        //                    string _path = null;
        //                    if( o.TryGetValue("_user_friendly_path", out _path) == true)
        //                    {
        //                        //DateTime temp = new DateTime();
        //                        Path_ObjectType temp;
        //                        if (dict_path_time.TryRemove(_path, out temp) == false)
        //                        {
        //                            Console.Write("Удаление ключа " + _path + " не удалось");
        //                        }
        //                    }
        //                }
        //                // Read Event details
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("Error while reading the event logs");
        //            StackTrace st = new StackTrace(ex, true);
        //            StackFrame st_frame = st.GetFrame(st.FrameCount - 1);
        //            appendLogToDictionary("in: " + st_frame.GetFileName() + ":(" + st_frame.GetFileLineNumber() + "," + st_frame.GetFileColumnNumber() + ")" + "\n" + ex.Message, BalloonIcon.Error);
        //            //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "in: " + st_frame.GetFileName() + ":(" + st_frame.GetFileLineNumber() + "," + st_frame.GetFileColumnNumber() + ")" + "\n" + ex.Message, BalloonIcon.Error);
        //        }
        //        List<Dictionary<string, string>> events = dict_event_path_object;
        //        // Если на остальные объекты не нашлось событий в журнале безопасности windows, то сообщить о том, что информации на них нет.
        //        // Это хотя бы уведомит, что файл удалён:
        //        foreach(KeyValuePair<string, Path_ObjectType> o in dict_path_time.ToArray() )
        //        {
        //            // TODO: проверить, что объект является наблюдаемым. Но пока это невозможно, т.к. неизвесен тип объекта (файл или каталог).
        //            string str_ObjectType = o.Value.wType == WatchingObjectType.File ? "File" : "Folder";
        //            bool bool_is_path_watchable = false;
        //            bool_is_path_watchable = check_path_is_in_watchable(o.Value.path.ToString(), WatchingObjectType.Folder, _re_extensions); // "Folder"); // После удаления нет возможности отличить каталог от файла. Поэтому буду проверять путь только на соответствие каталогу. str_ObjectType);
        //            if (bool_is_path_watchable==true)
        //            {
        //                Dictionary<string, string> file_event = new Dictionary<string, string>();
        //                file_event.Add("_user_friendly_path", o.Key);
        //                file_event.Add("TimeCreated", o.Value.dateTime.ToString());
        //                file_event.Add("ObjectType", "[unknown]");
        //                file_event.Add("SubjectUserName", "[unknown]");
        //                file_event.Add("SubjectDomainName", "[unknown]");
        //                file_event.Add("ProcessName", "[unknown]");
        //                file_event.Add("HandleId", "[unknown]");
        //                file_event.Add("ObjectName", o.Key);
        //                //file_data.Add("", "[unknown]");
        //                events.Add(file_event);
        //            }
        //            //DateTime temp=DateTime.Now;
        //            Path_ObjectType temp;
        //            dict_path_time.TryRemove(o.Key, out temp);
        //        }
        //        // Если нашлись хоть какие-то события удаления файлов, удовлетворяющие условиям наблюдения, то вывести добавить их в ответку:
        //        if (events.Count > 0)
        //        {
        //            list_events.AddRange(events);
        //        }
        //    }
        //    return list_events;
        //}

        /*
        private static void renamePathFromDictionary(String _old_path, string _new_path, WatcherChangeTypes cType)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                foreach( KeyValuePair<string, MenuItemData> mid in stackPaths.ToArray())
                {
                    if(mid.Key.StartsWith(_old_path) == true)
                    {
                        string _Key = mid.Key.Replace(_old_path, _new_path);
                        //Grid grid = mid.Value.mi;
                        MenuItem mi = mid.Value.mi;
                        //MenuItem mi_command = grid.Children.Cast<MenuItem>().First(e => Grid.GetRow(e) == 0 && Grid.GetColumn(e) == 0);//mid.Value.mi;
                        MenuItem mi_command = mid.Value.mi;
                        mi_command.Header = _Key.Length > (menuitem_header_length * 2 + 5) ? _Key.Substring(0, menuitem_header_length) + " ... " + _Key.Substring(_new_path.Length - menuitem_header_length) : _Key;
                        mi_command.ToolTip = _Key;
                        mi_command.CommandParameter = _Key;
                        stackPaths.Remove(mid.Key);
                        //MenuItemData id = new MenuItemData(grid, mid.Value.index);
                        MenuItemData id = new MenuItemData(mi, mid.Value.index);
                        stackPaths.Add(_Key, id);
                    }
                }
                reloadCustomMenuItems();
            });
        }
        //*/
        #endregion

        // Проверить, что путь удовлетворяет наблюдаемым правилам:
        public static bool check_path_is_in_watchable(string user_friendly_path, WatchingObjectType wType, Regex _extensions) // string str_ObjectType)
        {
            bool bool_is_path_watchable = true;

            //foreach (string _watch_folder in list_folders_for_watch)
            //{
            //    string watch_folder = _watch_folder;
            //    // Если путь в настройках заканчивается на обратную дробь, то удалить её. Если не удалить, то это повлияет на (1)
            //    if (watch_folder.EndsWith("\\") == true)
            //    {
            //        watch_folder = watch_folder.Substring(0, watch_folder.Length - 1);
            //    }
            //    if (user_friendly_path.Replace(watch_folder, "") == "" ||
            //        user_friendly_path.Replace(watch_folder, "")[0] == Path.DirectorySeparatorChar /*(1)*/)
            //    {
            //        bool_is_path_watchable = true;
            //        break;
            //    }
            //    else
            //    {
            //        continue;
            //    }
            //}

            // Проверить, а не начинается ли путь с исключения:
            foreach (string ex_path in Settings.arr_folders_for_exceptions)
            {
                if (user_friendly_path.StartsWith(ex_path) == true &&
                    (
                        user_friendly_path.Replace(ex_path, "") == "" ||
                        user_friendly_path.Replace(ex_path, "")[0] == Path.DirectorySeparatorChar
                    )
                )
                {
                    bool_is_path_watchable = false;
                    break;
                }
            }

            if (bool_is_path_watchable == true) {
                // Проверить, а не начинается ли имя файла с исключения:
                foreach (string ex_start_with in Settings.arr_files_for_exceptions) {
                    if (Path.GetFileNameWithoutExtension(user_friendly_path).StartsWith(ex_start_with) == true) {
                        bool_is_path_watchable = false;
                        break;
                    }
                }
            }

            if (bool_is_path_watchable == true) {
                // Проверить, а не является ли объект файлом с наблюдаемым расширением:
                if (bool_is_path_watchable == true && wType == WatchingObjectType.File) // str_ObjectType == "File")
                {
                    //if (_re_extensions.IsMatch(user_friendly_path) == false) {
                    if (check_path_is_in_watchable_re(user_friendly_path, _extensions) == false) {
                            bool_is_path_watchable = false;
                    }
                }
            }

            return bool_is_path_watchable;
        }

        public static bool check_path_is_in_watchable_re( string user_friendly_path, Regex _extensions ) {
            //return _re_extensions.IsMatch(user_friendly_path);
            return _extensions.IsMatch(user_friendly_path);
        }

        #region Legasy
        //private static void ShowPopupDeletePath(object sender, DoWorkEventArgs e)
        //{
        //    try
        //    {
        //        MenuItemData menuItemData = (MenuItemData)e.Argument;
        //        /*
        //        Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
        //        {
        //            mi = new MenuItem();
        //        });
        //        //*/

        //            List<Dictionary<string, string>> list_events = getDeleteInfo();
        //        // Если нашлись хоть какие-то события удаления файлов, удовлетворяющие условиям наблюдения, то вывести баллон:
        //        if (list_events.Count > 0)
        //        {
        //            //appendLogToDictionary("removed " + list_events.Count + " " + DateTime.Now, BalloonIcon.Warning);
        //            //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "removed " + list_events.Count + " " + DateTime.Now /* + "\nlast: " + SubjectDomainName + "\\\\" + SubjectUserName + "\n" + str_path*/, BalloonIcon.Warning);
        //        }
        //        else
        //        {
        //            return;
        //        }

        //        string str_path = null;
        //        list_events.First().TryGetValue("ObjectName", out str_path);
        //        string SubjectUserName = null;
        //        list_events.First().TryGetValue("SubjectUserName", out SubjectUserName);
        //        string SubjectDomainName = null;
        //        list_events.First().TryGetValue("SubjectDomainName", out SubjectDomainName);

        //        Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
        //        {
        //            MenuItem mi = new MenuItem();
        //            string str_path_short = str_path.Length > (menuitem_header_length * 2 + 5) ? str_path.Substring(0, menuitem_header_length) + " ... " + str_path.Substring(str_path.Length - menuitem_header_length) : str_path;
        //            string str_path_prefix = menuItemData.date_time.ToString("yyyy/MM/dd HH:mm:ss ")+"removed " +list_events.Count +" object(s). Last one:";
        //            mi.Header = str_path_prefix + "\n" + str_path_short;
        //            mi.ToolTip = "Open dialog for listing of deleted objects.\n"+str_path;
        //            mi.Command = CustomRoutedCommand_DialogListingDeletedFiles;
        //            mi.CommandParameter = list_events;
        //            mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 139, 0));
        //            //mi.MouseRightButtonUp

        //            //MenuItemData id = new MenuItemData(str_path, mi, MenuItemData.Type.removed_items); // user_owner
        //            menuItemData.path = str_path;
        //            menuItemData.mi = mi;
        //            menuItemData.type = MenuItemData.Type.removed_items;
        //            stackPaths.Add(menuItemData);
        //            currentMenuItem = menuItemData;

        //            // Если текущий пункт меню является первым в стеке, то можно вывести баллон, иначе - нет, потому что возникло следующее событие
        //            // и его нельзя перекрывать старым баллоном.
        //            MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).Last();
        //            if (first_menuItemData == menuItemData)
        //            {
        //                // Взять первые три файла из списка и вывести их в диалоговое окно:
        //                //List<string> text = new List<string>();

        //                List<Dictionary<string,string>> temp_list =  list_events.GetRange(0, list_events.Count<=3 ? list_events.Count : 3 );
        //                List<string> temp_file_names = new List<string>();
        //                foreach(Dictionary<string, string> rec in temp_list)
        //                {
        //                    string ObjectName = null;
        //                    rec.TryGetValue("ObjectName", out ObjectName);
        //                    temp_file_names.Add(ObjectName);
        //                }
        //                string str_for_message = String.Join("\n", temp_file_names);
        //                TrayPopupMessage popup = new TrayPopupMessage("Open dialog for listing of deleted objects\n\n"+ str_for_message, "removed " + list_events.Count + " " + DateTime.Now, WatchingObjectType.File, _notifyIcon, TrayPopupMessage.ControlButtons.Clipboard);
        //                popup.MouseDown+= (_sender, args) =>
        //                {
        //                    _notifyIcon.CustomBalloon.IsOpen = false;
        //                    DialogListingDeletedFiles(list_events);
        //                };
        //                // TODO: Подумать, как уведомлять пользователя об удалении файлов другим методом, потому что возможно прямо сейчас висит баллон,
        //                // который показывает пользователю, что файл поменялся, а это важнее, чем, например удаление файлов блокировки .git
        //                //_notifyIcon.ShowCustomBalloon(popup, PopupAnimation.Fade, 4000);
        //            }

        //            reloadCustomMenuItems();
        //        });
        //    }
        //    catch( Exception ex )
        //    {
        //        Console.WriteLine("Error while reading the event logs");
        //        StackTrace st = new StackTrace(ex, true);
        //        StackFrame st_frame = st.GetFrame(st.FrameCount - 1);
        //        string logText = "in: " + st_frame.GetFileName() + ":(" + st_frame.GetFileLineNumber() + "," + st_frame.GetFileColumnNumber() + ")" + "\n" + ex.Message;
        //        appendLogToDictionary(logText, BalloonIcon.Error);
        //        //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "in: "+st_frame.GetFileName()+":(" + st_frame.GetFileLineNumber()+","+st_frame.GetFileColumnNumber()+")" + "\n" + ex.Message, BalloonIcon.Error);
        //    }
        //}
        #endregion

        // http://stackoverflow.com/questions/12570324/c-sharp-run-a-thread-every-x-minutes-but-only-if-that-thread-is-not-running-alr
        private static BackgroundWorker worker = new BackgroundWorker(); // Класс BackgroundWorker позволяет выполнить операцию в отдельном, выделенном потоке. https://msdn.microsoft.com/ru-ru/library/system.componentmodel.backgroundworker%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396

        // Путь к файлу и время, когда вызвано событие (Искать в журнале будем с учётом этого времени -1с)
        // Валидны только те записи, которые не старше 60 сек от момента проверки и только те пути,
        // которые есть у наблюдателя (в логах пишутся много событий удаления)
        static ConcurrentDictionary<string, Path_ObjectType> dict_path_time = new ConcurrentDictionary<string, Path_ObjectType>();

        // Параметр, позволяющие выводить сообщение не быстрее 10 раз в сек
        static DateTime dt_prev_show_custom_ballon = new DateTime();

        // Обязательно ознакомиться с http://weblogs.asp.net/ashben/31773
        private static void OnChanged_file(object source, FileSystemEventArgs e)
        {
            try {
                DateTime dt = DateTime.Now;
                // Пропускать события файлов логов, иначе он только о себе и будет писать:
                if (e.FullPath == getLogFileName() || e.FullPath == getHistoryFileName()) {
                    return;
                }

                WatchingObjectType wType = WatchingObjectType.File;
                DateTime dt0 = DateTime.Now;

                if (e.ChangeType == WatcherChangeTypes.Deleted) {
                    // При удалении файла я всегда знаю, что это именно файла
                    // Заодно и экономим, что при удалении не надо проверять тип объекта:
                    wType = WatchingObjectType.File;
                } else {
                    if (LongDirectory.Exists(e.FullPath)) {
                        wType = WatchingObjectType.Folder;
                    }
                }

                if (wType != WatchingObjectType.File) {
                    return;
                }

                DateTime dt1 = DateTime.Now;

                if (e.ChangeType == WatcherChangeTypes.Changed && wType == WatchingObjectType.Folder) {
                    return;
                }
                if (check_path_is_in_watchable(e.FullPath, wType, _re_extensions) == false) {
                    return;
                }

                // Отображать этот элемент в меню при условии, что он в списке расширений пользователя:
                if (check_path_is_in_watchable(e.FullPath, wType, _re_user_extensions) == true) {
                //if (check_path_is_in_watchable(e.FullPath, wType, _re_extensions) == true) {
                    // Застолбить место в меню:
                    _MenuItemData menuItemData = null;
                    if (Application.Current != null) {
                        Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                        {
                            try {
                                menuItemData = new MenuItemData_CCRD(e, wType, dt);
                            //OnChanged(source, e, WatchingObjectType.File, menuItemData);
                            _MenuItemData last_menuItemData = null;
                                if (App.stackPaths.Count > 0) {
                                //last_menuItemData = App.stackPaths.OrderBy(d => d.index).Last();
                                last_menuItemData = App.stackPaths.OrderBy(d => d.date_time).Last();
                                }

                            // Проверить, насколько давно выводилось предыдущее окно с сообщением:
                            long elapsedTicks = DateTime.Now.Ticks - dt_prev_show_custom_ballon.Ticks;
                                TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);

                                if (last_menuItemData != null && last_menuItemData.wType != WatchingObjectType.Log) {
                                    MenuItemData_CCRD last_menuItemData_ccrd = ((MenuItemData_CCRD)last_menuItemData);
                                // Бывает, что некоторое приложение так "увлекается", что генерирует некоторые события по нескольку раз.
                                // 
                                // Решил не показывать быстровозникающие события одинакового типа над одним объектом:
                                if (last_menuItemData_ccrd.e.ChangeType == e.ChangeType     // Типы совпадают?
                                            && last_menuItemData_ccrd.e.FullPath == e.FullPath        // Путь совпадает?
                                            && last_menuItemData_ccrd.wType == WatchingObjectType.File) // Полное имя совпадает?
                                        {
                                    // Проверить разницу во времени с предыдущим последним событием и если она меньше одной секунды,
                                    // то удалить предыдущее событие:
                                    double totalSeconds = (DateTime.Now - last_menuItemData_ccrd.date_time).TotalSeconds;
                                        if (totalSeconds <= 1.0) {
                                            stackPaths.Remove(last_menuItemData);
                                            _notifyIcon.ContextMenu.Items.Remove(last_menuItemData.mi);
                                            last_menuItemData = null;
                                        }
                                    }
                                }
                                stackPaths.Add(menuItemData);
                            //if (last_menuItemData == null) // || last_menuItemData == menuItemData)
                            if (Settings.bool_display_notifications == true && elapsedSpan.TotalMilliseconds > 100) {
                                    TrayPopupMessage popup = null;

                                    BitmapImage bitmapImage = null;
                                    System.Windows.Controls.Image popup_image = null;
                                    String file_ext = Path.GetExtension(e.FullPath);
                                // Кешировать иконки для файлов:
                                if (_MenuItemData.icons_map.TryGetValue(file_ext, out bitmapImage) == true && bitmapImage != null) {
                                        popup_image = new System.Windows.Controls.Image();
                                        popup_image.Source = bitmapImage;

                                    }
                                    if (e.ChangeType == WatcherChangeTypes.Deleted) {
                                        popup = new TrayPopupMessage(e.FullPath, menuItemData.wType.ToString() + " " + e.ChangeType.ToString(), menuItemData.wType, App.NotifyIcon, popup_image,
#if (!_Evgeniy)
                                                TrayPopupMessage.ControlButtons.Clipboard
#else
                                                TrayPopupMessage.ControlButtons.None
#endif
                                        );
                                        popup.MouseDown += ( sender, args ) => {
                                            if (App.NotifyIcon.CustomBalloon != null) {
                                                App.NotifyIcon.CustomBalloon.IsOpen = false;
                                            }
                                        };
                                    } else {
                                        popup = new TrayPopupMessage(e.FullPath, menuItemData.wType.ToString() + " " + e.ChangeType.ToString(), menuItemData.wType, App.NotifyIcon, popup_image,
#if (!_Evgeniy)
                                        TrayPopupMessage.ControlButtons.Clipboard |
#endif
                                        TrayPopupMessage.ControlButtons.Run);

                                        popup.MouseDown += ( sender, args ) => {
                                            if (App.NotifyIcon.CustomBalloon != null) {
                                                App.NotifyIcon.CustomBalloon.IsOpen = false;
                                            }
                                            App.gotoPathByWindowsExplorer(popup.path, popup.wType);
                                        };
                                    }
                                    App.NotifyIcon.ShowCustomBalloon(popup, PopupAnimation.None, 4000);
                                    dt_prev_show_custom_ballon = DateTime.Now;
                                }
                            } catch (Exception _ex) {
                                _ex = _ex;
                            }
                        });
                    }
                }
                write_log(dt, e, wType);
                reloadCustomMenuItems();
            } catch (Exception _ex) {
                _ex = _ex;
            }
        }
        private static void OnCreated_file(object source, FileSystemEventArgs e)
        {
            #region Legasy
            /*
            // Застолбить место в меню:
            //OnChanged(source, e, WatchingObjectType.File, menuItemData);
            _MenuItemData menuItemData = new MenuItemData_CCRD(e, WatchingObjectType.File);
            stackPaths.Add(menuItemData);
            reloadCustomMenuItems();
            */
            #endregion
        }

        private static void OnRenamed_file(object source, RenamedEventArgs e)
        {
            #region Legasy
            // Застолбить место в меню:
            //MenuItemData menuItemData = new MenuItemData();
            //OnRenamed(source, e, WatchingObjectType.File, menuItemData);
            #endregion
        }

        private static void OnDeleted_file(object source, FileSystemEventArgs e)
        {
            #region Legasy
            //// Застолбить место в меню:
            //MenuItemData menuItemData = new MenuItemData();
            //OnChanged(source, e, WatchingObjectType.File, menuItemData);
            #endregion
        }

        private static void OnChanged_folder(object source, FileSystemEventArgs e)
        {
            try {
                DateTime dt = DateTime.Now;
                WatchingObjectType wType = WatchingObjectType.Unknown;  // При удалении указывается этот тип, т.к. после удаления уже неизвестно, что это было.
                if (e.ChangeType == WatcherChangeTypes.Deleted) {
                    // При удалени каталога я всегда знаю, что это именно каталог
                    // Заодно и экономим, что при удалении не надо проверять тип объекта методом Exists (метод затратный):
                    wType = WatchingObjectType.Folder;
                } else {
                    wType = WatchingObjectType.File;
                    DateTime dt0 = DateTime.Now;
                    if (LongDirectory.Exists(e.FullPath)) {
                        wType = WatchingObjectType.Folder;
                    }
                    if (wType != WatchingObjectType.Folder) {
                        return;
                    }

                    if (e.ChangeType == WatcherChangeTypes.Changed && wType == WatchingObjectType.Folder) {
                        return;
                    }
                }

                //if (check_path_is_in_watchable(e.FullPath, wType) == false)
                if (check_path_is_in_watchable(e.FullPath, WatchingObjectType.Folder, _re_extensions) == false) {
                    return;
                }

                // Застолбить место в меню:
                _MenuItemData menuItemData = null;
                if (Application.Current != null) {
                    Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                    {
                        try {
                            menuItemData = new MenuItemData_CCRD(e, WatchingObjectType.Folder, dt);
                            stackPaths.Add(menuItemData);

                        // Проверить, насколько давно выводилось предыдущее окно с сообщением:
                        long elapsedTicks = DateTime.Now.Ticks - dt_prev_show_custom_ballon.Ticks;
                            TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
                            System.Windows.Controls.Image popup_image = new System.Windows.Controls.Image();
                            {
                                BitmapImage bi = new BitmapImage();
                                bi.BeginInit();
                                bi.UriSource = new Uri(@"Icons\folder-horizontal-open.png", UriKind.Relative);
                                bi.EndInit();
                                popup_image.Source = bi;
                            }
                            if (Settings.bool_display_notifications == true && elapsedSpan.TotalMilliseconds > 100) {
                                TrayPopupMessage popup = null;
                                if (e.ChangeType == WatcherChangeTypes.Deleted) {
                                    popup = new TrayPopupMessage(e.FullPath, menuItemData.wType.ToString() + " " + e.ChangeType.ToString(), menuItemData.wType, App.NotifyIcon, popup_image,
#if (!_Evgeniy)
                            TrayPopupMessage.ControlButtons.Clipboard
#else
                            TrayPopupMessage.ControlButtons.None
#endif
                        );
                                    popup.MouseDown += ( sender, args ) => {
                                        if (App.NotifyIcon.CustomBalloon != null) {
                                            App.NotifyIcon.CustomBalloon.IsOpen = false;
                                        }
                                    };
                                } else {
                                    popup = new TrayPopupMessage(e.FullPath, menuItemData.wType.ToString() + " " + e.ChangeType.ToString(), menuItemData.wType, App.NotifyIcon, popup_image,
#if (!_Evgeniy)
                            TrayPopupMessage.ControlButtons.Clipboard
#else
                            TrayPopupMessage.ControlButtons.None
#endif
                        );
                                    popup.MouseDown += ( sender, args ) => {
                                        if (App.NotifyIcon.CustomBalloon != null) {
                                            App.NotifyIcon.CustomBalloon.IsOpen = false;
                                        }
                                        App.gotoPathByWindowsExplorer(popup.path, popup.wType);
                                    };
                                }
                                App.NotifyIcon.ShowCustomBalloon(popup, PopupAnimation.None, 4000);
                                dt_prev_show_custom_ballon = DateTime.Now;
                            }
                        } catch (Exception _ex) {
                            _ex = _ex;
                        }
                    });
                }

                write_log(dt, e, wType);
                reloadCustomMenuItems();
            } catch (Exception _ex) {
                _ex = _ex;
            }
        }
        private static void OnCreated_folder(object source, FileSystemEventArgs e)
        {
            // Застолбить место в меню:
            //MenuItemData menuItemData = new MenuItemData();
            //OnChanged(source, e, WatchingObjectType.Folder, menuItemData);
        }

        private static void OnRenamed_folder(object source, RenamedEventArgs e)
        {
            // Застолбить место в меню:
            //MenuItemData menuItemData = new MenuItemData();
            //OnRenamed(source, e, WatchingObjectType.Folder, menuItemData);
        }

        private static void OnDeleted_folder(object source, FileSystemEventArgs e)
        {
            // Застолбить место в меню:
            //MenuItemData menuItemData = new MenuItemData();
            //OnChanged(source, e, WatchingObjectType.Folder, menuItemData);
        }

        #region Legasy
        //private static void OnChanged(object source, FileSystemEventArgs e, WatchingObjectType wType, MenuItemData menuItemData)
        //{
        //    //if(e.ChangeType == WatcherChangeTypes.Deleted)
        //    //{
        //    //    if (stackPaths.Exists(x=>x.path==e.FullPath) == true)
        //    //    {
        //    //        reloadCustomMenuItems();
        //    //    }

        //    //    DateTime oldDateTime = DateTime.Now;
        //    //    //dict_path_time.AddOrUpdate(e.FullPath, DateTime.Now, (key, oldValue) => oldDateTime);
        //    //    Path_ObjectType path_object_type = new Path_ObjectType(e.FullPath, wType, DateTime.Now);
        //    //    dict_path_time.AddOrUpdate(e.FullPath, path_object_type, (key, oldValue) => path_object_type);

        //    //    // Обработку списка удалённых файлов отправить в фон, если фона ещё нет. Если фон есть,
        //    //    // то не надо ничего запускать. Фоновый процесс сам мониторит изменения в очереди.
        //    //    if ( !worker.IsBusy)
        //    //    {
        //    //        worker = new BackgroundWorker();
        //    //        worker.DoWork += new DoWorkEventHandler(ShowPopupDeletePath);
        //    //        worker.RunWorkerAsync(menuItemData);
        //    //    }
        //    //    else
        //    //    {
        //    //        Console.Write("skip");
        //    //    }
        //    //    return;
        //    //}

        //    /*
        //    if(File.Exists(e.FullPath) == true)
        //    {
        //        wType = WatchingObjectType.File;
        //    }
        //    else if(Directory.Exists(e.FullPath)==true)
        //    {
        //        wType = WatchingObjectType.Folder;
        //    }
        //    else
        //    {
        //        // WTF???
        //        //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "Object " + e.FullPath + " not Exist!", BalloonIcon.Error);
        //        return;
        //    }
        //    //*/

        //    // Проверить, а не начинается ли путь с исключения:
        //    for (int i = 0; i <= arr_folders_for_exceptions.Count - 1; i++)
        //    {
        //        if (e.FullPath.StartsWith(arr_folders_for_exceptions.ElementAt(i)) == true &&
        //                (
        //                    e.FullPath.Replace(arr_folders_for_exceptions.ElementAt(i), "") == "" ||
        //                    e.FullPath.Replace(arr_folders_for_exceptions.ElementAt(i), "")[0] == Path.DirectorySeparatorChar
        //                )
        //            )
        //        {
        //            return;
        //        }
        //    }

        //    // Если изменяемым является только каталог, то не регистрировать это изменение.
        //    // Я заметил возникновение этого события, когда я меняю что-то непосредственно в подкаталоге 
        //    // (например, переименовываю его подфайл или подкаталога)
        //    // Не регистрировать изменения каталога (это не переименование)
        //    if ( wType==WatchingObjectType.Folder && e.ChangeType!=WatcherChangeTypes.Created)
        //    {
        //        //return;
        //    }

        //    if ( wType==WatchingObjectType.File)
        //    {
        //        String file_name = Path.GetFileName(e.FullPath);
        //        if (_re_extensions.IsMatch(file_name) == false)
        //        {
        //            return;
        //        }
        //        // Проверить, а не начинается ли имя файла с исключения:
        //        for (int i = 0; i <= arr_files_for_exceptions.Count - 1; i++)
        //        {
        //            if (Path.GetFileNameWithoutExtension(e.FullPath).StartsWith(arr_files_for_exceptions.ElementAt(i)) == true)
        //            {
        //                return;
        //            }
        //        }
        //    }
        //    appendPathToDictionary(e.FullPath, e.ChangeType, wType, menuItemData);
        //}

        //private static void OnRenamed(object source, RenamedEventArgs e, WatchingObjectType wType, MenuItemData menuItemData)
        //{
        //    // Проверить, а не начинается ли путь с исключения:
        //    foreach (string ex_path in arr_folders_for_exceptions)
        //    {
        //        if (e.FullPath.StartsWith(ex_path) == true &&
        //                (
        //                    e.FullPath.Replace(ex_path, "") == "" ||
        //                    e.FullPath.Replace(ex_path, "")[0] == Path.DirectorySeparatorChar
        //                )
        //            )
        //        {
        //            return;
        //        }
        //    }

        //    if (File.Exists(e.FullPath) == true)
        //    {
        //        wType = WatchingObjectType.File;
        //    }
        //    else if (Directory.Exists(e.FullPath) == true)
        //    {
        //        wType = WatchingObjectType.Folder;
        //    }
        //    else
        //    {
        //        // WTF???
        //        //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "Object " + e.FullPath + " not Exist!", BalloonIcon.Error);
        //        return;
        //    }

        //    // Проверить, а не является ли расширение наблюдаемым?
        //    if (wType==WatchingObjectType.File)
        //    {
        //        String new_file_name = Path.GetFileName(e.FullPath);
        //        bool bool_new_is_exception = false;
        //        String old_file_name = Path.GetFileName(e.OldFullPath);
        //        bool bool_old_is_exception = false;
        //        foreach (string ex_path in arr_files_for_exceptions )
        //        {
        //            if (old_file_name.StartsWith(ex_path) == true)
        //            {
        //                bool_old_is_exception=true;
        //                break;
        //            }
        //        }

        //        // Проверить, а не начинается ли имя файла с исключения для имён файлов:
        //        foreach (string ex_path in arr_files_for_exceptions)
        //        {
        //            if (new_file_name.StartsWith(ex_path) == true)
        //            {
        //                bool_new_is_exception = true;
        //                break;
        //            }
        //        }

        //        // Если старый путь не был исключением, то перестроить меню (т.к. возможно старый путь там был)
        //        if (bool_old_is_exception == false)
        //        {
        //            reloadCustomMenuItems();
        //        }
        //        if (bool_new_is_exception == true)
        //        {
        //            return;
        //        }

        //        if (_re_extensions.IsMatch(new_file_name) == false)
        //        {
        //            return;
        //        }
        //    }

        //    appendPathToDictionary(e.FullPath, e.ChangeType, wType, menuItemData);
        //}
        #endregion

        public static bool IsObjectWachable(string path, WatchingObjectType wType)
        {
            bool bool_wachable = false;
            try {
                // Проверить, а не начинается ли путь с исключения:
                for (int i = 0; i <= Settings.arr_folders_for_exceptions.Count - 1; i++) {
                    if (path.StartsWith(Settings.arr_folders_for_exceptions.ElementAt(i)) == true &&
                            (
                                path.Replace(Settings.arr_folders_for_exceptions.ElementAt(i), "") == "" ||
                                path.Replace(Settings.arr_folders_for_exceptions.ElementAt(i), "")[0] == Path.DirectorySeparatorChar
                            )
                        ) {
                        return false;
                    }
                }

                if (wType == WatchingObjectType.File) {
                    String file_name = Path.GetFileName(path);
                    if (_re_extensions.IsMatch(file_name) == false) {
                        return false;
                    }
                    // Проверить, а не начинается ли имя файла с исключения:
                    for (int i = 0; i <= Settings.arr_files_for_exceptions.Count - 1; i++) {
                        if (Path.GetFileNameWithoutExtension(path).StartsWith(Settings.arr_files_for_exceptions.ElementAt(i)) == true) {
                            return false;
                        }
                    }
                }
            }
            catch(Exception _ex) {
                _ex = _ex;
            }
            return true;
        }

        // Конец параметров для отслеживания изменений в файловой системе. =======================================

        // http://stackoverflow.com/questions/249760/how-to-convert-a-unix-timestamp-to-datetime-and-vice-versa
        // Unix -> DateTime
        public static DateTime UnixTimestampToDateTime(long unixTime)
        {
            DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Local);
            long unixTimeStampInTicks = (long)(unixTime);
            return new DateTime(unixStart.Ticks + unixTimeStampInTicks, System.DateTimeKind.Local);
        }

        // DateTime -> Unix
        public static long DateTimeToUnixTimestamp(DateTime dateTime)
        {
            DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Local);
            long unixTimeStampInTicks = (dateTime.ToLocalTime() - unixStart).Ticks;
            return unixTimeStampInTicks;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            if (sw_log_file != null) {
                //write_log(DateTime.Now, null, WatchingObjectType.Unknown);  // Нужно скинуть то, что осталось в буфере, поэтому Unknown не имеет значения
                sw_log_file.Flush();
                sw_log_file.Close();
            }else{
                // Сохранить данные контекстного меню для последующего восстановления при перезапуске:
                //foreach (_MenuItemData obj in stackPaths.OrderBy(d => d.index).ToArray())
                string file_name_last_files = getHistoryFileName();
                string record_all = "";

                foreach (_MenuItemData obj in stackPaths.OrderBy(d => d.date_time).ToArray())
                {
                    if (obj is MenuItemData_CCRD)
                    {
                        string record = null;
                        MenuItemData_CCRD ccrd = ((MenuItemData_CCRD)obj);
                        long unix_time = DateTimeToUnixTimestamp(ccrd.date_time);
                        WatcherChangeTypes wct = ccrd.e.ChangeType;
                        if (wct == WatcherChangeTypes.Renamed)
                        {
                            RenamedEventArgs _ee = (RenamedEventArgs)ccrd.e;
                            string FullPath = _ee.FullPath;
                            //string OldFullPath = _ee.OldFullPath;
                            string OldFullPath = (string)AppUtility.GetInstanceField(typeof(RenamedEventArgs), _ee, "oldFullPath");
                            record = String.Format("{0}\t{1}\t{2}\t{3}\t{4}\n", unix_time, wct.ToString(), ccrd.wType.ToString(), FullPath, OldFullPath);
                        }
                        else
                        {
                            string FullPath = ccrd.e.FullPath;
                            record = String.Format("{0}\t{1}\t{2}\t{3}\n", unix_time, wct.ToString(), ccrd.wType.ToString(), FullPath);
                        }
                        //string header = (string)ccrd.mi.Header;
                        record_all += record;
                    }
                    else
                    {
                    }
                }
                LongFile.WriteAllText(file_name_last_files, record_all, Encoding.UTF8);

                //FileSystemEventArgs _e = new FileSystemEventArgs(WatcherChangeTypes.Changed, "", "");
            }
            if (_notifyIcon != null)
            {
                _notifyIcon.Dispose();
            }
        }

        public class EventUnit
        {
            public string Message;
            public string User;
            public string File;
        }

        public static EventUnit DisplayEventAndLogInformation(string fileToSearch, DateTime actionTime)
        {
            StringBuilder sb = new StringBuilder();
            const string queryString = @"<QueryList>
              <Query Id=""0"" Path=""Security"">
                <Select Path=""Security"">*</Select>
              </Query>
            </QueryList>";
            EventLogQuery eventsQuery = new EventLogQuery("Security", PathType.LogName, queryString);
            eventsQuery.ReverseDirection = true;
            EventLogReader logReader = new EventLogReader(eventsQuery);
            EventUnit e=null;
            bool isStop = false;
            for (EventRecord eventInstance = logReader.ReadEvent(); null != eventInstance; eventInstance = logReader.ReadEvent())
            {
                foreach (var VARIABLE in eventInstance.Properties)
                    if (VARIABLE.Value.ToString().ToLower().Contains(fileToSearch.ToLower()) && actionTime.ToString("dd/MM/yyyy HH:mm:ss") == eventInstance.TimeCreated.Value.ToString("dd/MM/yyyy HH:mm:ss"))
                    {
                        foreach (var VARIABLE2 in eventInstance.Properties)
                            sb.AppendLine(VARIABLE2.Value.ToString());
                        e = new EventUnit();
                        e.Message = sb.ToString();
                        e.User = (eventInstance.Properties.Count > 1) ? eventInstance.Properties[1].Value.ToString() : "n/a";
                        e.File = fileToSearch;
                        isStop = true;
                        break;
                    }
                if (isStop) break;
                try
                {
                    //    Console.WriteLine("Description: {0}", eventInstance.FormatDescription());
                }
                catch (Exception e2)
                {
                }
            }
            return e;
        }

        public static bool HasWritePermission(string dir)
        {
            bool Allow = false;
            bool Deny = false;
            DirectorySecurity acl = null;
            try
            {
                acl = Directory.GetAccessControl(dir);
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                throw new Exception("DirectoryNotFoundException");
            }
            if (acl == null)
            {
                return false;
            }
            AuthorizationRuleCollection arc = acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
            if (arc == null)
            {
                return false;
            }
            foreach (FileSystemAccessRule rule in arc)
            {
                if ((FileSystemRights.Write & rule.FileSystemRights) != FileSystemRights.Write)
                {
                    continue;
                }
                if (rule.AccessControlType == AccessControlType.Allow)
                {
                    Allow = true;
                }
                else if (rule.AccessControlType == AccessControlType.Deny)
                {
                    Deny = true;
                }
            }
            return Allow && !Deny;
        }

        //  регистрацией компонента для контекстного меню
        // https://artemgrygor.wordpress.com/2010/10/06/register-shell-extension-context-menu-also-on-windows-x64-part-2/
        public static void registerDLL(string dllPath)
        {
            try
            {
                if (!LongFile.Exists(dllPath))
                    return;
                Assembly asm = Assembly.LoadFile(dllPath);
                var reg = new RegistrationServices();

                // Для нормальной работы регистрации/разрегистрации в x64 нужно предварительно в проекте снять флаг Properties\Build\prefer x32-bit
                // http://serv-japp.stpr.ru:27080/images/ImageCRUD?_id=574b489886b57c9b6268635a
                // Идею нашёл в http://www.advancedinstaller.com/forums/viewtopic.php?t=7837

                if (reg.RegisterAssembly(asm, AssemblyRegistrationFlags.SetCodeBase))
                {
                    //MessageBox.Show("Registered!");
                    //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Successfully registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                    appendLogToDictionary("Successfully registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                }
                else
                {
                    //MessageBox.Show("Not Registered!");
                    //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                    appendLogToDictionary("Failed registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed\n"+ex.Message, BalloonIcon.Error);
                appendLogToDictionary("Failed register FileChangesWatcher in Windows Context Menu.\n" + ex.Message, BalloonIcon.Error);
            }
        }

        // https://artemgrygor.wordpress.com/2010/10/06/register-shell-extension-context-menu-also-on-windows-x64-part-2/
        public static void unregisterDLL(string dllPath)
        {
            try
            {
                if (!LongFile.Exists(dllPath))
                    return;
                Assembly asm = Assembly.LoadFile(dllPath);
                var reg = new RegistrationServices();

                // Для нормальной работы регистрации/разрегистрации в x64 нужно предварительно в проекте снять флаг Properties\Build\prefer x32-bit
                // http://serv-japp.stpr.ru:27080/images/ImageCRUD?_id=574b489886b57c9b6268635a
                // Идею нашёл в http://www.advancedinstaller.com/forums/viewtopic.php?t=7837

                if (reg.UnregisterAssembly(asm))
                {
                    //MessageBox.Show("UnRegistered!");
                    //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Successfully unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                    appendLogToDictionary("Successfully unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                }
                else
                {
                    //MessageBox.Show("Not UnRegistered!");
                    //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                    appendLogToDictionary("Failed unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                //App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed\n" + ex.Message, BalloonIcon.Error);
                appendLogToDictionary("Exception on unregistering FileChangesWatcher in Windows Context Menu.\n"+ex.Message, BalloonIcon.Error);
            }
        }
        //*/

        public static bool _IsUserAdministrator = false;
        // http://stackoverflow.com/questions/1089046/in-net-c-test-if-process-has-administrative-privileges
        public static bool IsUserAdministrator()
        {
            //bool value to hold our return value
            bool isAdmin;
            WindowsIdentity user = null;
            try
            {
                //get the currently logged in user
                user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (UnauthorizedAccessException ex)
            {
                isAdmin = false;
            }
            catch (Exception ex)
            {
                isAdmin = false;
            }
            finally
            {
                if (user != null)
                    user.Dispose();
            }
            _IsUserAdministrator = isAdmin;
            return isAdmin;
        }

        /* Для тестов над toasts для Windows 10
        public static void TestToast()
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
            var notification = new TileNotification(xml_doc);

            String APP_ID = "Microsoft.Samples.DesktopToastsSample";
            ToastNotification toast = new ToastNotification(xml_doc);
            ToastNotificationManager.CreateToastNotifier(APP_ID).Show(toast);
        }
        //*/

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
        private static List<string> GetPhysicalDriveList(string drive_letter_without_slash, string start_with)
        {
            uint returnSize = 0;
            // Arbitrary initial buffer size
            int maxResponseSize = 100;

            IntPtr response = IntPtr.Zero;

            string allDevices = null;
            string[] devices = null;

            while (returnSize == 0)
            {
                // Allocate response buffer for native call
                response = Marshal.AllocHGlobal(maxResponseSize);

                // Check out of memory condition
                if (response != IntPtr.Zero)
                {
                    try
                    {
                        // List DOS devices
                        returnSize = QueryDosDevice( drive_letter_without_slash/*null*/, response, maxResponseSize);

                        // List success
                        if (returnSize != 0)
                        {
                            // Result is returned as null-char delimited multistring
                            // Dereference it from ANSI charset
                            allDevices = Marshal.PtrToStringAnsi(response, maxResponseSize);
                        }
                        // The response buffer is too small, reallocate it exponentially and retry
                        else if (Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
                        {
                            maxResponseSize = (int)(maxResponseSize * 5);
                        }
                        // Fatal error has occured, throw exception
                        else
                        {
                            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
                        }
                    }
                    finally
                    {
                        // Always free the allocated response buffer
                        Marshal.FreeHGlobal(response);
                    }
                }
                else
                {
                    throw new OutOfMemoryException("Out of memory when allocating space for QueryDosDevice command!");
                }
            }

            // Split zero-character delimited multi-string
            devices = allDevices.Split('\0');
            // QueryDosDevices lists alot of devices, return only PhysicalDrives
            return devices.Where(device => device.StartsWith(start_with /*"PhysicalDrive"*/)).ToList <string> ();
        }
    }

}

