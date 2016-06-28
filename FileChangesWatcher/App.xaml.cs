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
using IniParser;
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

//using NotificationsExtensions.Tiles;

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
        File, Folder
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

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : Application
    {
        // Добавление пользовательских меню выполнено на основе: https://msdn.microsoft.com/ru-ru/library/ms752070%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396

        // Список пользовательских пунктов меню, в которые записываются пути изменяемых файлов:
        static List<MenuItemData> stackPaths = new List<MenuItemData>();
        public static String GetStackPathsAsString()
        {
            //List<MenuItemData> sss = new List<MenuItemData>();
            //sss.Exists(x => x.index == 1);
            StringBuilder sb = new StringBuilder();
            foreach (var obj in stackPaths.FindAll(d=>d.type==MenuItemData.Type.file_folder).OrderBy(d => d.index).Reverse().ToArray())
            {
                if(sb.Length > 0)
                {
                    sb.Append("\n");
                }
                sb.Append(obj.path);
            }
            return sb.ToString();
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

        private static void ShowMessage(string text)
        {
            //String text = (string)e.Parameter;
            DialogListingDeletedFiles window = new DialogListingDeletedFiles();
            window.txtListFiles.Text = text;
            window.Show();
            ActivateWindow(new WindowInteropHelper(window).Handle);
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

        // Пересоздать пункты контекстного меню, которые указывают на файлы.
        protected static void reloadCustomMenuItems()
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                // Сначала очистить пункты меню с путями к файлам:
                foreach (var obj in stackPaths)
                {
                    _notifyIcon.ContextMenu.Items.Remove(obj.mi);
                }

                // Максимальное количество файлов в списке должно быть не больше указанного максимального значения:
                while (stackPaths.Count > log_contextmenu_size)
                {
                    // Удалить самый старый элемент из списка путей и из меню
                    MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).First();

                    //MenuItemData _id = null;
                    //if (stackPaths.TryGetValue(first, out _id))
                    if( stackPaths.Exists(x=>x.path==first_menuItemData.path))
                    {
                        _notifyIcon.ContextMenu.Items.Remove(first_menuItemData.mi);
                    }
                    stackPaths.Remove(first_menuItemData);
                }

                // Заполнить новые пункты:
                //Grid mi = null;
                MenuItemData last_menuItemData = null;
                foreach (MenuItemData obj in stackPaths.OrderBy(d => d.index).ToArray())
                {
                    switch (obj.type)
                    {
                        case MenuItemData.Type.file_folder:
                                if ( File.Exists(obj.path) == true || Directory.Exists(obj.path) == true )
                                {
                                    obj.mi.FontWeight = FontWeights.Normal;
                                    _notifyIcon.ContextMenu.Items.Insert(0, obj.mi);
                                }
                                else
                                {
                                    stackPaths.Remove(obj);
                                }
                            break;
                        default:
                                obj.mi.FontWeight = FontWeights.Normal;
                                _notifyIcon.ContextMenu.Items.Insert(0, obj.mi);
                            break;
                    }
                    last_menuItemData = obj;
                }
                /*
                if (last_menuItemData != null)
                {
                    //mi.FontWeight = FontWeights.Bold;
                    //mi.Children.Cast<MenuItem>().First(e => Grid.GetRow(e) == 0 && Grid.GetColumn(e) == 0).FontWeight = FontWeights.Bold;
                    last_menuItemData.mi.FontWeight = FontWeights.Bold;
                    switch (last_menuItemData.type)
                    {
                        case MenuItemData.Type.log_record:
                            currentMenuItem = last_menuItemData;
                            _notifyIcon.ShowBalloonTip("FileChangesWatcher", last_menuItemData.log_record_text, last_menuItemData.ballonIcon);
                            break;
                    }
                }
                //*/
            });
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
            /*
            _notifyIcon.TrayBalloonTipClicked += (sender, args) =>
            {
                //Hardcodet.Wpf.TaskbarNotification.Interop.Point p = _notifyIcon.GetPopupTrayPosition();
                //_notifyIcon.ContextMenu.IsOpen = true;
                if (stackPaths.Count > 0 )// && bool_is_path_tooltip==true)
                {
                    // http://stackoverflow.com/questions/11549580/find-key-with-max-value-from-sorteddictionary
                    //MenuItemData menuItemData = stackPaths.OrderBy(d => d.index).Last();
                    if(currentMenuItem==null)
                    {
                        return;
                    }
                    MenuItemData menuItemData = currentMenuItem;
                    currentMenuItem = null;

                    RoutedCommand command = menuItemData.mi.Command as RoutedCommand;

                    if (command != null)
                    {
                        command.Execute(menuItemData.mi.CommandParameter, menuItemData.mi);
                    }
                    else
                    {
                        try
                        {
                            ((ICommand)command).Execute(menuItemData.mi.CommandParameter);
                        }
                        catch(Exception ex)
                        {
                            NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Ошибка при выполнении команды: " + ex.Message, BalloonIcon.Error);
                        }
                    }

                    //menuItemData.mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, args));
                    //ExecutedRoutedEventArgs ex_param = new ExecutedRoutedEventArgs(menuItemData.mi.CommandParameter);
                    //menuItemData.mi.Command.Execute(menuItemData.mi.CommandParameter);
                    //string path = menuItemData.path;
                    //gotoPathByWindowsExplorer(path);
                    //bool_is_path_tooltip = false;
                }
            };
            //*/

            // Эти два обработчика ошибок появились вынужденно.
            /*
               Проблема в переходе на файл, когда путь к файлу выводится во всплывающем баллоне.
               Проблема в том, как отличить простое сообщение от сообщения, в котором выводиться путь?
               Перед выводом сообщения о пути я выставил флаг bool_is_path_tooltip, чтобы баллон TrayBalloonTipClicked знал,
               что сейчас в нём путь от последнего файла. Потом я хотел сбросить этот флаг в TrayBalloonTipShown, но
               оказалось, что перед отображением баллона всегда вызывается событие TrayBalloonTipClosed, поэтому в обработчике
               TrayBalloonTipClicked переменная bool_is_path_tooltip всегда будет false. Нужно было защитить эту переменную.
               Поэтому я поступил так. Перед выводом баллона выставил защищающую переменную bool_is_ballow_was_shown=true,
               что говорит о том, что что бы не было перед отображением баллона с путём на экране - это не моё и переменная
               bool_is_path_tooltip не сбрасывается. Потом открывается мой баллон и переменная bool_is_ballow_was_shown говорит
               что наконец-то мой баллон был открыт и любое его закрытие (по любой причине - клик или угасание) будет сбрасывать
               оба флага и следующий баллон не будет восприниматься как баллон с путём к файлу.
               Долбанутый алгоритм. Короче просто триггер, чтобы пропустить один hide
             */
             /*
            _notifyIcon.TrayBalloonTipClosed += (sender, args) =>
            {
                if (bool_is_ballow_was_shown == true)
                {
                    bool_is_path_tooltip = false;
                    bool_is_ballow_was_shown = false;
                }
                currentMenuItem = null;
            };

            _notifyIcon.TrayBalloonTipShown += (sender, args) =>
            {
                bool_is_ballow_was_shown = true;
            };
            //*/

            //notifyIcon.ContextMenu.Items.Insert(0, new Separator() );  // http://stackoverflow.com/questions/4823760/how-to-add-horizontal-separator-in-a-dynamically-created-contextmenu
            //CommandBinding customCommandBinding = new CommandBinding(CustomRoutedCommand, ExecutedCustomCommand, CanExecuteCustomCommand);
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand, ExecutedCustomCommand, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_ExecuteFile, ExecutedCustomCommand_ExecuteFile, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_CopyTextToClipboard, ExecutedCustomCommand_CopyTextToClipboard, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_DialogListingDeletedFiles, ExecutedCustomCommand_DialogListingDeletedFiles, CanExecuteCustomCommand));
            _notifyIcon.ContextMenu.CommandBindings.Add(new CommandBinding(CustomRoutedCommand_ShowMessage, ExecutedCustomCommand_ShowMessage, CanExecuteCustomCommand));

            initApplication(e);
        }

        public static String getIniFilePath()
        {
            String iniFilePath = null; // System.IO.Path.GetDirectoryName(Environment.CommandLine.Replace("\"", "")) + "\\Stroiproject.ini";
            string exe_file = typeof(FileChangesWatcher.App).Assembly.Location; // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
            //iniFilePath = Process.GetCurrentProcess().MainModule.FileName;
            iniFilePath = System.IO.Path.GetDirectoryName(exe_file) + "\\" + System.IO.Path.GetFileNameWithoutExtension(exe_file) + ".ini";
            return iniFilePath;
        }
        public static String getExeFilePath()
        {
            // http://stackoverflow.com/questions/4764680/how-to-get-the-location-of-the-dll-currently-executing
            String exeFilePath = typeof(FileChangesWatcher.App).Assembly.Location; // System.IO.Path.GetDirectoryName(Environment.CommandLine.Replace("\"", "")) + "\\Stroiproject.ini";
            //exeFilePath = Process.GetCurrentProcess().MainModule.FileName;
            return exeFilePath;
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

                // После установки автозапуска отметить это настройкой в конфигурации:
                FileIniDataParser fileIniDataParser = new FileIniDataParser();
                IniParser.Model.IniData data = new IniParser.Model.IniData();
                String iniFilePath = App.getIniFilePath();
                data = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                data.Sections["General"].RemoveKey("autostart_on_windows");
                data.Sections["General"].AddKey("autostart_on_windows", "true");
                UTF8Encoding a = new UTF8Encoding();
                fileIniDataParser.WriteFile(iniFilePath, data, Encoding.UTF8);
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
                FileIniDataParser fileIniDataParser = new FileIniDataParser();
                IniParser.Model.IniData data = new IniParser.Model.IniData();
                String iniFilePath = App.getIniFilePath();
                data = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                data.Sections["General"].RemoveKey("autostart_on_windows");
                data.Sections["General"].AddKey("autostart_on_windows", "false");
                fileIniDataParser.WriteFile(iniFilePath, data, Encoding.UTF8);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }


        public static void initApplication(StartupEventArgs e)
        {
            StringBuilder init_text_message = new StringBuilder();

            // Сбросить всех наблюдателей, установленных ранее:
            foreach (FileSystemWatcher watcher in watchers.ToArray() )
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= new FileSystemEventHandler(OnChanged_file);
                watcher.Changed -= new FileSystemEventHandler(OnChanged_folder);

                watcher.Created -= new FileSystemEventHandler(OnChanged_file);
                watcher.Created -= new FileSystemEventHandler(OnChanged_folder);

                watcher.Deleted -= new FileSystemEventHandler(OnChanged_file);
                watcher.Deleted -= new FileSystemEventHandler(OnChanged_folder);

                watcher.Renamed -= new RenamedEventHandler(OnRenamed_file);
                watcher.Renamed -= new RenamedEventHandler(OnRenamed_folder);

                watchers.Remove(watcher);
                watcher.Dispose();
            }

            // Проверить существование ini-файла. Если его нет, то создать его:
            FileIniDataParser fileIniDataParser = new FileIniDataParser();
            IniParser.Model.IniData data = new IniParser.Model.IniData();
            String iniFilePath = getIniFilePath();

            if (File.Exists(iniFilePath) == false)
            {
                data.Sections.AddSection("Comments");
                data.Sections.AddSection("General");
                data.Sections.AddSection("Extensions");
                data.Sections.AddSection("FoldersForWatch");
                data.Sections.AddSection("FoldersForExceptions");
                data.Sections.AddSection("FileNamesExceptions");

                data.Sections["Comments"].AddKey("All extensions in Extensions Section are union in one string. Can be RegEx.");
                data.Sections["Comments"].AddKey("All FoldersForExceptions Section   used in function StartsWith. Not use RegEx.");
                data.Sections["Comments"].AddKey("All FileNamesExceptions Section    used in StartsWith. Not use RegEx.");
                data.Sections["Comments"].AddKey("All keys in sections have to be unique in their sections.");
                // Количество файлов, видимое в меню:
                data.Sections["General"].AddKey("log_contextmenu_size", "7");
                // Список расширений, по-умолчанию, за которыми надо "следить". Из них потом будут регулярки^
                data.Sections["Extensions"].AddKey("archivers", ".tar|.jar|.zip|.bzip2|.gz|.tgz|.7z");
                data.Sections["Extensions"].AddKey("officeword", ".doc|.docx|.docm|.dotx|.dotm|.rtf");
                data.Sections["Extensions"].AddKey("officeexcel", ".xls|.xlt|.xlm|.xlsx|.xlsm|.xltx|.xltm|.xlsb|.xla|.xlam|.xll|.xlw");
                data.Sections["Extensions"].AddKey("officepowerpoint", ".ppt|.pot|.pptx|.pptm|.potx|.potm|.ppam|.ppsx|.ppsm|.sldx|.sldm");
                data.Sections["Extensions"].AddKey("officevisio", ".vsd|.vsdx|.vdx|.vsx|.vtx|.vsl|vsdm");
                data.Sections["Extensions"].AddKey("autodesk", ".dwg|.dxf|.dwf|.dwt|.dxb|.lsp|.dcl");
                data.Sections["Extensions"].AddKey("extensions02", ".gif|.png|.jpeg|.jpg|.tiff|.tif|.bmp");
                data.Sections["Extensions"].AddKey("extensions03", ".cs|.xaml|.config|.ico");
                data.Sections["Extensions"].AddKey("extensions04", ".gitignore|.md");
                data.Sections["Extensions"].AddKey("extensions05", ".msg|.ini");
                data.Sections["Extensions"].AddKey("others", ".pdf|.html|.xhtml|.txt|.mp3|.aiff|.au|.midi|.wav|.pst|.xml|.java|.js");
                //data.Sections["Extensions"].AddKey("", "");
                // Список каталогов, за которыми надо следить:
                data.Sections["FoldersForWatch"].AddKey("folder01", @"D:\");
                data.Sections["FoldersForWatch"].AddKey("folder02", @"E:\Docs");
                data.Sections["FoldersForWatch"].AddKey("folder03", @"F:\");
                // Список каталогов, которые надо исключить из "слежения" (просто будут сравниваться начала имён файлов):
                data.Sections["FoldersForExceptions"].AddKey("folder01", "D:\\temp");
                data.Sections["FileNamesExceptions"].AddKey("file01", "~$");

                fileIniDataParser.WriteFile(iniFilePath, data, Encoding.UTF8);
            }
            else
            {

                _notifyIcon.ToolTipText = "FileChangesWatcher. Right-click for menu";
                try
                {
                    data = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                }
                catch (IniParser.Exceptions.ParsingException ex)
                {
                    appendLogToDictionary("" + ex.Message + "", BalloonIcon.Error);
                    _notifyIcon.ToolTipText = "FileChangesWatcher not working. Error in ini-file. Open settings in menu, please.";
                    //_notifyIcon.ShowBalloonTip("Error in ini-file. Open settings in menu, please", ""+ex.Message + "", BalloonIcon.Error);
                    return;
                }
            }


            // При первом запуске проверить, если в настройках нет флага, отменяющего автозагрузку,
            // то прописать автозапуск приложения в реестр:
            if (data.Sections["General"].GetKeyData("autostart_on_windows") == null) {
                if( MessageBox.Show("Set autostart with windows?\n\n(if you say no then you can do this in context menu later)", "FileChangesWatcher", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    setAutostart();
                }
                else
                {
                    resetAutostart();
                }
            }else if("true".Equals(data.Sections["General"].GetKeyData("autostart_on_windows").Value))
            {
                setAutostart();
            }else
            {
                resetAutostart();
            }

            try
            {
                // Определить количество пунктов подменю:
                log_contextmenu_size = Convert.ToInt32(data.Sections["General"].GetKeyData("log_contextmenu_size").Value);
            }
            catch (OverflowException)
            {
                Console.WriteLine("Ошибка преобразования значения log_contextmenu_size в число");
            }

            _re_extensions = getExtensionsRegEx(data);

            // Определить список каталогов, за которыми надо наблюдать и проверить состояние аудита для этих каталогов:
            ///*
            list_folders_for_watch = new List<String>();
            for (int i = 0; i <= data.Sections["FoldersForWatch"].Count - 1; i++)
            {
                String folder = data.Sections["FoldersForWatch"].ElementAt(i).Value;
                if (folder.Length > 0 && list_folders_for_watch.Contains(folder) == false && Directory.Exists(folder))
                {
                    list_folders_for_watch.Add(folder);
                    // Определить аудит указанного каталога:
                    FileSecurity fSecurity = File.GetAccessControl(folder, AccessControlSections.Audit);
                    AuthorizationRuleCollection col_audit = fSecurity.GetAuditRules(true, true, typeof(SecurityIdentifier) );
                    init_text_message.Append("\nCheck Audit Rules for watching folder \""+folder+"\":");
                    bool is_Audit_Delete = false;
                    bool is_Audit_DeleteSubdirectoriesAndFiles = false;
                    foreach (FileSystemAuditRule ace in col_audit)
                    {
                        // Проверить, что аудит каталога имеет установку на логирование событий DELETE и DeleteSubDirectoriesAndFiles:
                        // https://msdn.microsoft.com/ru-ru/library/system.security.accesscontrol.filesystemrights(v=vs.110).aspx
                        if ( (ace.FileSystemRights & FileSystemRights.Delete)==FileSystemRights.Delete )
                        {
                            is_Audit_Delete = true;
                            init_text_message.Append("\n     Audit Delete is on. GOOD." );
                        }
                        if ((ace.FileSystemRights & FileSystemRights.DeleteSubdirectoriesAndFiles) == FileSystemRights.DeleteSubdirectoriesAndFiles)
                        {
                            is_Audit_DeleteSubdirectoriesAndFiles = true;
                            init_text_message.Append("\n     Audit DeleteSubdirectoriesAndFiles is on. GOOD.");
                        }
                    }
                    if( is_Audit_Delete==false)
                    {
                        init_text_message.Append("\n     Audit Delete is off. BAD. Turn it ON.");
                    }
                    if (is_Audit_DeleteSubdirectoriesAndFiles == false)
                    {
                        init_text_message.Append("\n     Audit DeleteSubdirectoriesAndFiles is off. BAD. Turn it ON.");
                    }
                    /*
                    if( is_Audit_Delete==false || is_Audit_DeleteSubdirectoriesAndFiles == false)
                    {
                    // Вывести окно "свойства" для каталога:
                        ShowFileProperties(folder);
                    }
                    //*/

                    // Определить имя диска и его физическое имя:
                    string drive_name = Path.GetPathRoot(folder);
                    if( !(drive_name==null || drive_name.Length == 0) )
                    {
                        drive_name = drive_name.Split('\\')[0];
                        if(dict_drive_phisical.ContainsKey(drive_name) == false)
                        {
                            List<string> list_phisical_drive_name = GetPhysicalDriveList(drive_name, @"\Device\HarddiskVolume");
                            if(list_phisical_drive_name != null)
                            {
                                string phisical_drive_name = list_phisical_drive_name.First();
                                dict_drive_phisical.Add(drive_name, phisical_drive_name);
                            }
                        }
                    }
                }
                else
                {
                    init_text_message.Append("\nThere is no folder \"" + folder + "\" for watching. Skipped.");
                }
            }
            if (e != null)
            {
                for (int i = 0; i <= e.Args.Length - 1; i++)
                {
                    String folder = e.Args[i];
                    if (folder.Length > 0 && list_folders_for_watch.Contains(folder) == false && Directory.Exists(folder))
                    {
                        list_folders_for_watch.Add(folder);
                    }
                }
            }

            // Список каталогов с исключениями:
            List<string> _arr_folders_for_exceptions = new List<String>();
            for (int i = 0; i <= data.Sections["FoldersForExceptions"].Count - 1; i++)
            {
                String folder = data.Sections["FoldersForExceptions"].ElementAt(i).Value;
                if (folder.Length > 0)
                {
                    _arr_folders_for_exceptions.Add(folder);
                }
            }
            arr_folders_for_exceptions = _arr_folders_for_exceptions;

            // Список файлов с исключениями:
            List<string> _arr_files_for_exceptions = new List<String>();
            for (int i = 0; i <= data.Sections["FileNamesExceptions"].Count - 1; i++)
            {
                String folder = data.Sections["FileNamesExceptions"].ElementAt(i).Value;
                if (folder.Length > 0)
                {
                    _arr_files_for_exceptions.Add(folder);
                }
            }
            arr_files_for_exceptions = _arr_files_for_exceptions;

            if (list_folders_for_watch.Count >= 1)
            {
                init_text_message.Append("\n");
                init_text_message.Append( setWatcherForFolderAndSubFolders(list_folders_for_watch.ToArray()) );
            }
            else
            {
                appendLogToDictionary("No watching for folders. Set folders correctly.", BalloonIcon.Info);
                //_notifyIcon.ShowBalloonTip("Info", "No watching for folders. Set folders correctly.", BalloonIcon.Info);
            }

            // Если пользователь администратор, то проверить наличие групповой политики, включающую регистрацию
            // файловых событий в журнале безопасности windows. Без прав администратора нет возможности
            // прочитать эту политику.
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
            appendLogToDictionary("Initial settings.\n"+init_text_message.ToString(), BalloonIcon.Info);
        }

        public static Regex getExtensionsRegEx(IniParser.Model.IniData data)
        {
            List<string> arr_extensions_for_filter = new List<String>();
            String _extensions = "";
            Regex re = new Regex("\\.");
            for (int i = 0; i <= data.Sections["Extensions"].Count - 1; i++)
            {
                String folder = data.Sections["Extensions"].ElementAt(i).Value;
                folder = (new Regex("(^|)|(|$)")).Replace(folder, "");
                if (folder.Length > 0)
                {
                    if (_extensions.Length > 0)
                    {
                        _extensions += "|";
                    }
                    _extensions += re.Replace(folder, "\\.");
                }
            }
            _extensions = @".*(" + _extensions + ")$";
            _extensions = (new Regex("(\\|\\|)")).Replace(_extensions, "|");
            
            return new Regex(_extensions, RegexOptions.IgnoreCase);
        }

        static List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

        private static string setWatcherForFolderAndSubFolders(String[] _paths)
        {
            foreach (String _path in _paths)
            {
                // Отслеживание изменения в файловой системе:
                // Для файлов:
                {
                    FileSystemWatcher watcher = new FileSystemWatcher();
                    watchers.Add(watcher);
                    watcher.IncludeSubdirectories = true;

                    watcher.Path = _path;
                    /* Watch for changes in LastAccess and LastWrite times, and
                        the renaming of files or directories. */
                    // TODO: требуется разделить на файлы и каталоги: http://stackoverflow.com/questions/3336637/net-filesystemwatcher-was-it-a-file-or-a-directory?answertab=votes#tab-top
                    watcher.NotifyFilter = //NotifyFilters.LastAccess
                          NotifyFilters.LastWrite
                        | NotifyFilters.FileName
                        //| NotifyFilters.DirectoryName
                        ;
                    // Only watch text files.
                    // watcher.Filter = "*.*";
                    watcher.Filter = "";

                    // Add event handlers.
                    watcher.Changed += new FileSystemEventHandler(OnChanged_file);
                    watcher.Created += new FileSystemEventHandler(OnChanged_file);
                    watcher.Deleted += new FileSystemEventHandler(OnChanged_file);
                    watcher.Renamed += new RenamedEventHandler(OnRenamed_file);

                    // Begin watching:
                    watcher.EnableRaisingEvents = true;
                }
                // Для каталогов:
                if(false==true)
                {
                    FileSystemWatcher watcher = new FileSystemWatcher();
                    watchers.Add(watcher);
                    watcher.IncludeSubdirectories = true;

                    watcher.Path = _path;
                    /* Watch for changes in LastAccess and LastWrite times, and
                        the renaming of files or directories. */
                    // TODO: требуется разделить на файлы и каталоги: http://stackoverflow.com/questions/3336637/net-filesystemwatcher-was-it-a-file-or-a-directory?answertab=votes#tab-top
                    watcher.NotifyFilter = //NotifyFilters.LastAccess
                          NotifyFilters.LastWrite
                        //| NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        ;
                    // Only watch text files.
                    // watcher.Filter = "*.*";
                    watcher.Filter = "";

                    // Add event handlers.
                    watcher.Changed += new FileSystemEventHandler(OnChanged_folder);
                    watcher.Created += new FileSystemEventHandler(OnChanged_folder);
                    watcher.Deleted += new FileSystemEventHandler(OnChanged_folder);
                    watcher.Renamed += new RenamedEventHandler(OnRenamed_folder);

                    // Begin watching:
                    watcher.EnableRaisingEvents = true;
                }
            }
            return "Watching folders: \n" + String.Join("\n", _paths.ToArray());
        }

        // Параметры для отлеживания изменений в файлах: ========================================

        // Соответствие между именем диска и его физическим именем:
        static Dictionary<string, string> dict_drive_phisical = new Dictionary<string, string>();
        // Список каталогов, за которыми наблюдает программа:
        static List<string> list_folders_for_watch = null;

        static Dictionary<String, BitmapImage> icons_map = new Dictionary<string, BitmapImage>();

        static Regex _re_extensions = null;
        public static Regex re_extensions
        {
            get
            {
                return _re_extensions;
            }
        }

        // Список каталогов, которые надо исключить из вывода:
        static List<String> arr_folders_for_exceptions = null;
        static List<String> arr_files_for_exceptions = null;
        // Количество файлов, которые видны в контекстном меню:
        static int log_contextmenu_size = 5;

        // Если _old_path!=null, то надо переименовать имеющиеся пути с _old_path на _path
        private static int menuitem_header_length = 30;
        private static void appendPathToDictionary(string _path, WatcherChangeTypes changedType, WatchingObjectType wType, MenuItemData menuItemData)
        {
            String str = _path;
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                _notifyIcon.CloseBalloon();
                // Получить владельца файла:
                /*
                string user_owner = null;
                try
                {
                    // http://stackoverflow.com/questions/7445182/find-out-file-owner-creator-in-c-sharp
                    user_owner = System.IO.File.GetAccessControl(_path).GetOwner(typeof(System.Security.Principal.NTAccount)).ToString();
                }
                catch(Exception ex)
                {
                    user_owner = "<unknown>";
                }
                //*/

                //DriveInfo di = new DriveInfo(Path.GetPathRoot(_path));
                string s = Path.GetPathRoot(_path);

                _notifyIcon.HideBalloonTip();
                //_notifyIcon.ShowBalloonTip("go to path:", /*"file owner: "+user_owner+"\n"+*/ _path, BalloonIcon.Info);
                //bool_is_path_tooltip = true; // После клика или после исчезновения баллона этот флаг будет сброшен.
                //bool_is_ballow_was_shown = false;

                //_notifyIcon.TrayBalloonTipClicked
                // Если такой путь уже есть в логе, то нужно его удалить. Это позволит переместить элемент наверх списка.
                //if (stackPaths.ContainsKey(_path) == true)
                // TODO: Если существующий индекс стоит в самой первой позиции, то не надо сверкать балоном. Ещё не реализовано.
                bool show_balloon = stackPaths.FindIndex(x => x.path == _path)==0;
                while (stackPaths.Exists(x=>x.path==_path) == true)
                {
                    MenuItemData _id = stackPaths.Find(x=>x.path==_path);
                    _notifyIcon.ContextMenu.Items.Remove( _id.mi);
                    stackPaths.Remove(_id);
                }

                //if (stackPaths.ContainsKey(_path) == false)
                {
                    //int max_value = 0;
                    if (stackPaths.Count > 0)
                    {
                        // i = stackPaths.Last().Value;
                        // http://stackoverflow.com/questions/11549580/find-key-with-max-value-from-sorteddictionary
                        //max_value = stackPaths.OrderBy(d => d.index).Last().index;
                    }

                    // Создать пункт меню и наполнить его смыслом:
                    MenuItem mi = new MenuItem();
                    string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss ") + _path;
                    mi.Header = mi_text.Length>(menuitem_header_length*2+5) ? mi_text.Substring(0, menuitem_header_length)+" ... "+ mi_text.Substring(mi_text.Length-menuitem_header_length) : mi_text;
                    /* Пробую установить цвет шрифта для разных событий над файлом. Плохая идея, т.к. приложение может несколько раз менять файл во время записи. Даже переименовывать.
                    if (changedType == WatcherChangeTypes.Changed)
                    {
                        mi.Foreground = new SolidColorBrush( System.Windows.Media.Color.FromArgb(255, 255, 139, 0) );
                    }
                    else
                    if (changedType == WatcherChangeTypes.Created)
                    {
                        mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 255, 0));
                    }
                    else
                    if (changedType == WatcherChangeTypes.Renamed)
                    {
                        mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 255));
                    }
                    //*/
                    mi.ToolTip = "Go to "+_path;
                    mi.Command = CustomRoutedCommand;
                    mi.CommandParameter = new Path_ObjectType(_path, wType);

                    // Получить иконку файла для вывода в меню:
                    // http://www.codeproject.com/Articles/29137/Get-Registered-File-Types-and-Their-Associated-Ico
                    // Загрузить иконку файла в меню: http://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap?answertab=votes#tab-top
                    // Как-то зараза не грузится простым присваиванием.
                    String file_ext = Path.GetExtension(_path);
                    Icon mi_icon=null;
                    BitmapImage bitmapImage = null;

                    // Кешировать иконки для файлов:
                    if( icons_map.TryGetValue(file_ext, out bitmapImage)==true)
                    {
                    }
                    else
                    {
                        try
                        {
                            // На сетевых путях выдаёт Exception. Поэтому к сожалению не годиться.
                            //mi_icon = Icon.ExtractAssociatedIcon(_path);// getIconByExt(file_ext);

                            // Этот метод работает: http://stackoverflow.com/questions/1842226/how-to-get-the-associated-icon-from-a-network-share-file?answertab=votes#tab-top
                            ushort uicon;
                            StringBuilder strB = new StringBuilder(_path);
                            IntPtr handle = ExtractAssociatedIcon(IntPtr.Zero, strB, out uicon);
                            mi_icon = Icon.FromHandle(handle);
                        }
                        catch (Exception ex)
                        {
                            mi_icon = null;
                            icons_map.Add(file_ext, null);
                        }

                        if(mi_icon != null)
                        {
                            using (MemoryStream memory = new MemoryStream())
                            {
                                mi_icon.ToBitmap().Save(memory, ImageFormat.Png);
                                memory.Position = 0;
                                bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.StreamSource = memory;
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.EndInit();
                                icons_map.Add(file_ext, bitmapImage);
                            }
                        }
                    }
                    if (bitmapImage != null)
                    {
                        mi.Icon = new System.Windows.Controls.Image
                        {
                            Source = bitmapImage
                        };
                    }
                    else
                    {
                        mi.Icon = null;
                    }

                    //if (wType == WatchingObjectType.File)
                    {
                        // Так определять Grid гораздо проще: http://stackoverflow.com/questions/5755455/how-to-set-control-template-in-code
                        string str_template = @"
                            <ControlTemplate 
                                                xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                                xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                                xmlns:tb='http://www.hardcodet.net/taskbar'
                                                xmlns:local='clr-namespace:FileChangesWatcher'
                             >
                                <Grid x:Name='mi_grid'>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='*'/>
                                        <ColumnDefinition Width='20'/>
                                        <ColumnDefinition Width='20'/>
                                    </Grid.ColumnDefinitions>
                                </Grid>
                            </ControlTemplate>
                        ";
                        MenuItem _mi = new MenuItem();
                        Grid mi_grid = null; // new Grid();
                        ControlTemplate ct = (ControlTemplate)XamlReader.Parse(str_template);
                        _mi.Template = ct;
                        if (_mi.ApplyTemplate())
                        {
                            mi_grid = (Grid)ct.FindName("mi_grid", _mi);
                        }
                        MenuItem mi_clipboard = new MenuItem();
                        mi_clipboard.Icon = new System.Windows.Controls.Image
                        {
                            Source = new BitmapImage(
                            new Uri("pack://application:,,,/Icons/Clipboard.ico"))
                        };
                        mi_clipboard.ToolTip = "Copy path to clipboard";
                        mi_clipboard.Command = CustomRoutedCommand_CopyTextToClipboard;
                        mi_clipboard.CommandParameter = _path;
                        MenuItem mi_enter = new MenuItem();
                        mi_enter.Icon = new System.Windows.Controls.Image
                        {
                            Source = new BitmapImage(
                            new Uri("pack://application:,,,/Icons/Enter.ico"))
                        };
                        mi_enter.ToolTip = "Execute file";
                        mi_enter.Command = CustomRoutedCommand_ExecuteFile;
                        mi_enter.CommandParameter = new Path_ObjectType(_path, wType);

                        Grid.SetColumn(mi, 0);
                        Grid.SetRow(mi, 0);
                        Grid.SetColumn(mi_clipboard, 1);
                        Grid.SetRow(mi_clipboard, 0);
                        mi_grid.Children.Add(mi);
                        mi_grid.Children.Add(mi_clipboard);

                        if (wType == WatchingObjectType.File)
                        {
                            Grid.SetColumn(mi_enter, 2);
                            Grid.SetRow(mi_enter, 0);
                            mi_grid.Children.Add(mi_enter);
                        }
                        mi = _mi;
                    }

                    //MenuItemData id = new MenuItemData(_path, mi, MenuItemData.Type.file_folder ); // user_owner
                    menuItemData.path = _path;
                    menuItemData.mi = mi;
                    menuItemData.type = MenuItemData.Type.file_folder;
                    stackPaths.Add(menuItemData);
                    //_notifyIcon.ContextMenu.Items.Insert(0, id.mi);
                    currentMenuItem = menuItemData;
                    /*
                    _notifyIcon.ShowBalloonTip("go to path:", _path, BalloonIcon.Info);
                    bool_is_path_tooltip = true; // После клика или после исчезновения баллона этот флаг будет сброшен.
                    bool_is_ballow_was_shown = false;
                    //*/

                    // Если текущий пункт меню является первым в стеке, то можно вывести баллон, иначе - нет, потому что возникло следующее событие
                    // и его нельзя перекрывать старым баллоном.
                    MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).Last();
                    if (first_menuItemData == menuItemData)
                    {

                        TrayPopupMessage popup = new TrayPopupMessage(_path, wType, _notifyIcon, TrayPopupMessage.ControlButtons.Clipboard | TrayPopupMessage.ControlButtons.Run);
                        popup.MouseDown += (sender, args) =>
                        {
                            _notifyIcon.CustomBalloon.IsOpen = false;
                            App.gotoPathByWindowsExplorer(popup.path, popup.wType);
                        };

                        _notifyIcon.ShowCustomBalloon(popup, PopupAnimation.Fade, 4000);
                    }

                    reloadCustomMenuItems();
                }
            });
        }

        public static void customballoon_close(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                // popup_test.Visibility = Visibility.Hidden;
                _notifyIcon.CustomBalloon.IsOpen = false;
            });
            System.Timers.Timer temp = ((System.Timers.Timer)sender);
            temp.Stop();
        }

        private static void appendLogToDictionary(String logText, BalloonIcon _ballonIcon)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                MenuItem mi = new MenuItem();
                string mi_text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss ") + logText.Split('\n')[0];
                mi.Header = mi_text.Length > (menuitem_header_length * 2 + 5) ? mi_text.Substring(0, menuitem_header_length) + " ... " + mi_text.Substring(mi_text.Length - menuitem_header_length) : mi_text;
                //mi.Header = logText.Split('\n')[0];
                mi.ToolTip = "message from program:\n" + logText;
                mi.Command = CustomRoutedCommand_ShowMessage;
                mi.CommandParameter = logText;
                mi.IsEnabled = true;

                MenuItemData id = MenuItemData.CreateLogRecord(mi, _ballonIcon, logText); // user_owner
                stackPaths.Add(id);
                currentMenuItem = id;
            //_notifyIcon.ShowBalloonTip("FileChangesWatcher", logText, _ballonIcon);

                // Если текущий пункт меню является первым в стеке, то можно вывести баллон, иначе - нет, потому что возникло следующее событие
                // и его нельзя перекрывать старым баллоном.
                MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).Last();
                if (first_menuItemData == id)
                {
                    TrayPopupMessage popup = new TrayPopupMessage(logText, "Initial initialization", WatchingObjectType.File, _notifyIcon, TrayPopupMessage.ControlButtons.Clipboard);
                    popup.MouseDown += (sender, args) =>
                    {
                        _notifyIcon.CustomBalloon.IsOpen = false;
                        ShowMessage(logText);
                    };
                    _notifyIcon.ShowCustomBalloon(popup, PopupAnimation.Fade, 3000);
                }                
                reloadCustomMenuItems();
            });
        }

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

        static List<Dictionary<string, string>> getDeleteInfo(/*string _path*/)
        {
            List<Dictionary<string, string>> list_events = new List<Dictionary<string, string>>();
            List<Dictionary<string, string>> event_4653 = new List<Dictionary<string, string>>();
            List<Dictionary<string, string>> event_4660 = new List<Dictionary<string, string>>();
            List<Dictionary<string, string>> event_4656 = new List<Dictionary<string, string>>();
            // Удаление иногда бывает длительным и нужно дождаться, пока стек файлов "опустеет" (наполняется он снаружи этого цикла):
            while (dict_path_time.Count > 0)
            {

                //string result = "неизвестный удалил "+_path;
                // Тут будут события, которые взяты из журнала событий (как только они там наступят)
                List<Dictionary<string, string>> dict_event_path_object = new List<Dictionary<string, string>>();
                try
                {
                    int j = 0;
                    //string disk_name = Path.GetPathRoot(_path);
                    //if (!(disk_name == null || disk_name.Length == 0))
                    {
                        //disk_name = disk_name.Split('\\')[0];
                        int i = 0;
                        EventRecord eventdetail = null;
                        EventLogReader logReader = null; // new EventLogReader(eventsQuery);
                        List<KeyValuePair<string, Path_ObjectType>> arr_partial_events = new List<KeyValuePair<string, Path_ObjectType>>();
                        do
                        {
                            arr_partial_events = dict_path_time.ToList();
                            //arr_partial_events.AddRange( dict_path_time.ToList() );
                            DateTime curr_time = DateTime.Now;
                            int delta_seconds = 60;
                            DateTime min_time = DateTime.Now;
                            // Сначала отсортировать события, которые уже не подпадают под рассмотрение, т.к. произошли достаточно давно.
                            foreach (KeyValuePair<string, Path_ObjectType> path_time in dict_path_time)
                            {
                                // Если событие зарегистрировано в указанный интервал времени (delta_seconds) от момента входа в функцию,
                                // то использовать эту запись в дальнейшем. Если не попадает, то удалить:
                                if ((curr_time - path_time.Value.dateTime).TotalSeconds <= delta_seconds)
                                {
                                    if (min_time > path_time.Value.dateTime)
                                    {
                                        min_time = path_time.Value.dateTime;
                                    }
                                }
                                else
                                {
                                    arr_partial_events.Remove(path_time);
                                    if (arr_partial_events.Count == 0)
                                    {
                                        return dict_event_path_object;
                                    }
                                    //DateTime temp = new DateTime();
                                    Path_ObjectType temp;
                                    if (dict_path_time.TryRemove(path_time.Key, out temp) == false)
                                    {
                                        Console.Write("Удаление ключа " + path_time.Key + " не удалось");
                                    }
                                }
                            }
                            // Пройтись по всем и выкинуть те, у кого время старше 10 сек от текущего момента:
                            /*
                            string query = string.Format("*[System/EventID=4656 and System[TimeCreated[@SystemTime >= '{0}']]] and *[System[TimeCreated[@SystemTime <= '{1}']] and EventData[Data[@Name='AccessMask']='0x10000'] ]",
                                DateTime.Now.AddSeconds(-1).ToUniversalTime().ToString("o"),
                                DateTime.Now.AddSeconds( 1).ToUniversalTime().ToString("o")
                                );
                                */
                            // Запросить все события удаления файлов, которые входят в интервал min_time
                            /* Пока не буду анализировать логи события 4660, т.к. на Windows Server 2012 R2 они не возникают.
                             * Это не очень хорошо, что я так делаю, т.к. именно 4660 говорит о том, что объект был удалён, 
                             * а 4656 всего лишь попытка выполнить удаления (но как правило удачная), но пока на это и расчёт.
                            string query = string.Format("*[ System[EventID=4656 or EventID=4663 or EventID=4660] and System[TimeCreated[@SystemTime >= '{0}']] and EventData[Data[@Name='AccessMask']='0x10000'] ]",
                                min_time.AddSeconds(-1).ToUniversalTime().ToString("o")
                                );
                            */
                            //string query = string.Format("*[ System[EventID=4656] and System[TimeCreated[@SystemTime >= '{0}']] and EventData[Data[@Name='AccessMask']='0x10000'] ]",
                            string query = string.Format("*[ System[EventID=4656] and System[TimeCreated[@SystemTime >= '{0}']] ]",
                                min_time.AddSeconds(-1).ToUniversalTime().ToString("o")
                                );
                            EventLogQuery eventsQuery = new EventLogQuery("Security", PathType.LogName, query);
                            logReader = new EventLogReader(eventsQuery);
                            eventdetail = logReader.ReadEvent();
                            // Если записи из журнала прочитать не удалось, то читать их повторно, но не больше 10 раз (1 сек по 0.1)
                            if (i++ > 10 || eventdetail != null)
                            {
                                break;
                            }
                            Thread.Sleep(100);
                        } while (eventdetail == null);

                        for (; eventdetail != null; eventdetail = logReader.ReadEvent())
                        {
                            // https://techoctave.com/c7/posts/113-c-reading-xml-with-namespace
                            XmlDocument doc = new XmlDocument();
                            doc.LoadXml(eventdetail.ToXml());
                            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                            nsmgr.AddNamespace("def", "http://schemas.microsoft.com/win/2004/08/events/event");
                            XmlNode root = doc.DocumentElement;

                            Dictionary<string, string> dict_event_object = new Dictionary<string, string>();

                            XmlNode node_EventID = root["System"].SelectSingleNode("def:EventID", nsmgr);
                            string str_EventID = node_EventID.InnerText;
                            dict_event_object.Add("EventID", str_EventID);

                            switch (str_EventID)
                            {
                                // Не самый корректный способ определяющий удаление файла, но пока оставлю этот.
                                // TODO: 4660 - реальное событие удаление, но бывает не во всех ОС. Надо разобраться в чём дело.
                                case "4656":
                                    break;
                                default:
                                    // чтобы не тратить время на остальные проверки переходить к следующему событию.
                                    continue;
                            }

                            if (str_EventID == "4656")
                            {
                                XmlNode node_EventRecordID = root["System"].SelectSingleNode("def:EventRecordID", nsmgr);
                                string str_EventRecordID = node_EventRecordID.InnerText;
                                dict_event_object.Add("EventRecordID", str_EventRecordID);
                            }

                            if (str_EventID == "4656")
                            {
                                // Строки настраиваемых форматов даты и времени: https://msdn.microsoft.com/ru-ru/library/8kb3ddd4%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
                                // http://stackoverflow.com/questions/3075659/what-is-the-time-format-of-windows-event-log
                                XmlNode node_TimeCreated_SystemTime = root["System"].SelectSingleNode("def:TimeCreated[@SystemTime]", nsmgr);
                                string str_TimeCreated_SystemTime = node_TimeCreated_SystemTime.Attributes.GetNamedItem("SystemTime").InnerText;
                                DateTime dateValue = DateTime.Parse(str_TimeCreated_SystemTime, null, DateTimeStyles.None);
                                string str_TimeCreated = dateValue.ToString();
                                dict_event_object.Add("TimeCreated", str_TimeCreated);
                            }

                            string str_ObjectType = "";
                            if (str_EventID == "4656")
                            {
                                XmlNode node_ObjectType = root["EventData"].SelectSingleNode("def:Data[@Name='ObjectType']", nsmgr);
                                str_ObjectType = node_ObjectType.InnerText;
                                dict_event_object.Add("ObjectType", str_ObjectType);
                            }

                            if (str_EventID == "4656")
                            {
                                XmlNode node_SubjectUserName = root["EventData"].SelectSingleNode("def:Data[@Name='SubjectUserName']", nsmgr);
                                string str_SubjectUserName = node_SubjectUserName.InnerText;
                                dict_event_object.Add("SubjectUserName", str_SubjectUserName);
                            }

                            if (str_EventID == "4656")
                            {
                                XmlNode node_SubjectDomainName = root["EventData"].SelectSingleNode("def:Data[@Name='SubjectDomainName']", nsmgr);
                                string str_SubjectDomainName = node_SubjectDomainName.InnerText;
                                dict_event_object.Add("SubjectDomainName", str_SubjectDomainName);
                            }

                            if (str_EventID == "4656")
                            {
                                XmlNode node_ProcessName = root["EventData"].SelectSingleNode("def:Data[@Name='ProcessName']", nsmgr);
                                string str_ProcessName = node_ProcessName.InnerText;
                                dict_event_object.Add("ProcessName", str_ProcessName);
                            }

                            if (str_EventID == "4656")
                            {
                                XmlNode node_HandleId = root["EventData"].SelectSingleNode("def:Data[@Name='HandleId']", nsmgr);
                                string str_HandleId = node_HandleId.InnerText;
                                dict_event_object.Add("HandleId", str_HandleId);
                            }

                            string str_AccessMask = "";
                            if (str_EventID == "4656")
                            {
                                XmlNode node_HandleId = root["EventData"].SelectSingleNode("def:Data[@Name='AccessMask']", nsmgr);
                                str_AccessMask = node_HandleId.InnerText;
                                dict_event_object.Add("AccessMask", str_AccessMask);
                                int value = (int)new System.ComponentModel.Int32Converter().ConvertFromString(str_AccessMask);
                                if( (value & 0x10000) != 0x10000)
                                {
                                    continue;
                                }
                            }

                            if (str_EventID == "4656")
                            {
                                XmlNode node_ObjectName = root["EventData"].SelectSingleNode("def:Data[@Name='ObjectName']", nsmgr);
                                string str_ObjectName = node_ObjectName.InnerText;
                                dict_event_object.Add("ObjectName", str_ObjectName);

                                // Дружественное название имени объекта в списке зарегистрированных событий:
                                string user_friendly_path = str_ObjectName;
                                foreach (KeyValuePair<string, string> drive_phisical in dict_drive_phisical)
                                {
                                    string drive = drive_phisical.Key;
                                    string phisical = drive_phisical.Value;
                                    if (str_ObjectName.StartsWith(phisical) == true || str_ObjectName.StartsWith(drive) == true)
                                    {
                                        user_friendly_path = str_ObjectName.Replace(phisical, drive + "\\");
                                        bool bool_is_path_watchable = false;
                                        // https://www.ultimatewindowssecurity.com/securitylog/encyclopedia/event.aspx?eventID=4663
                                        // Object Type: "File" for file or folder but can be other types of objects such as Key, SAM, SERVICE OBJECT, etc.
                                        bool_is_path_watchable = check_path_is_in_watchable(user_friendly_path, "Folder"); // После удаления нет возможности отличить каталог от файла. Поэтому буду проверять путь только на соответствие каталогу. str_ObjectType);
                                        if (bool_is_path_watchable == true)
                                        {
                                            dict_event_object.Add("_user_friendly_path", user_friendly_path);
                                            dict_event_path_object.Add(dict_event_object);
                                        }
                                        break;
                                    }
                                }
                                // Т.к. событие обработано, то больше эта запись из журнала регистрации событий в программе не понадобиться. Удалить её совсем:
                                //DateTime temp = new DateTime();
                                Path_ObjectType temp;
                                if (dict_path_time.TryRemove(user_friendly_path, out temp) == false)
                                {
                                    Console.Write("Удаление ключа " + user_friendly_path + " не удалось");
                                }
                            }
                        }
                        // Очистить главный стек событий стёртых файлов от обработанных событий:
                        foreach (Dictionary<string, string> o in dict_event_path_object.ToArray() )
                        {
                            string _path = null;
                            if( o.TryGetValue("_user_friendly_path", out _path) == true)
                            {
                                //DateTime temp = new DateTime();
                                Path_ObjectType temp;
                                if (dict_path_time.TryRemove(_path, out temp) == false)
                                {
                                    Console.Write("Удаление ключа " + _path + " не удалось");
                                }
                            }
                        }
                        // Read Event details
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while reading the event logs");
                    StackTrace st = new StackTrace(ex, true);
                    StackFrame st_frame = st.GetFrame(st.FrameCount - 1);
                    appendLogToDictionary("in: " + st_frame.GetFileName() + ":(" + st_frame.GetFileLineNumber() + "," + st_frame.GetFileColumnNumber() + ")" + "\n" + ex.Message, BalloonIcon.Error);
                    //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "in: " + st_frame.GetFileName() + ":(" + st_frame.GetFileLineNumber() + "," + st_frame.GetFileColumnNumber() + ")" + "\n" + ex.Message, BalloonIcon.Error);
                }
                List<Dictionary<string, string>> events = dict_event_path_object;
                // Если на остальные объекты не нашлось событий в журнале безопасности windows, то сообщить о том, что информации на них нет.
                // Это хотя бы уведомит, что файл удалён:
                foreach(KeyValuePair<string, Path_ObjectType> o in dict_path_time.ToArray() )
                {
                    // TODO: проверить, что объект является наблюдаемым. Но пока это невозможно, т.к. неизвесен тип объекта (файл или каталог).
                    string str_ObjectType = o.Value.wType == WatchingObjectType.File ? "File" : "Folder";
                    bool bool_is_path_watchable = false;
                    bool_is_path_watchable = check_path_is_in_watchable(o.Value.path.ToString(), "Folder"); // После удаления нет возможности отличить каталог от файла. Поэтому буду проверять путь только на соответствие каталогу. str_ObjectType);
                    if (bool_is_path_watchable==true)
                    {
                        Dictionary<string, string> file_event = new Dictionary<string, string>();
                        file_event.Add("_user_friendly_path", o.Key);
                        file_event.Add("TimeCreated", o.Value.dateTime.ToString());
                        file_event.Add("ObjectType", "[unknown]");
                        file_event.Add("SubjectUserName", "[unknown]");
                        file_event.Add("SubjectDomainName", "[unknown]");
                        file_event.Add("ProcessName", "[unknown]");
                        file_event.Add("HandleId", "[unknown]");
                        file_event.Add("ObjectName", o.Key);
                        //file_data.Add("", "[unknown]");
                        events.Add(file_event);
                    }
                    //DateTime temp=DateTime.Now;
                    Path_ObjectType temp;
                    dict_path_time.TryRemove(o.Key, out temp);
                }
                // Если нашлись хоть какие-то события удаления файлов, удовлетворяющие условиям наблюдения, то вывести добавить их в ответку:
                if (events.Count > 0)
                {
                    list_events.AddRange(events);
                }
            }
            return list_events;
        }

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

        // Проверить, что путь удовлетворяет наблюдаемым правилам:
        public static bool check_path_is_in_watchable(string user_friendly_path, string str_ObjectType)
        {
            bool bool_is_path_watchable = false;
            foreach (string watch_folder in list_folders_for_watch)
            {
                if (user_friendly_path.Replace(watch_folder, "") == "" ||
                    user_friendly_path.Replace(watch_folder, "")[0] == Path.DirectorySeparatorChar)
                {
                    bool_is_path_watchable = true;
                }
                else
                {
                    continue;
                }
                // Проверить, а не начинается ли путь с исключения:
                foreach (string ex_path in arr_folders_for_exceptions)
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
                // Проверить, а не является ли объект файлом с наблюдаемым расширением:
                if (bool_is_path_watchable == true && str_ObjectType == "File")
                {
                    if (_re_extensions.IsMatch(user_friendly_path) == false)
                    {
                        bool_is_path_watchable = false;
                    }
                }
                // Проверить, а не начинается ли имя файла с исключения:
                foreach (string ex_start_with in arr_files_for_exceptions)
                {
                    if (Path.GetFileNameWithoutExtension(user_friendly_path).StartsWith(ex_start_with) == true)
                    {
                        bool_is_path_watchable = false;
                        break;
                    }
                }

            }
            return bool_is_path_watchable;
        }

        private static void ShowPopupDeletePath(object sender, DoWorkEventArgs e)
        {
            try
            {
                MenuItemData menuItemData = (MenuItemData)e.Argument;
                /*
                Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                {
                    mi = new MenuItem();
                });
                //*/

                    List<Dictionary<string, string>> list_events = getDeleteInfo();
                // Если нашлись хоть какие-то события удаления файлов, удовлетворяющие условиям наблюдения, то вывести баллон:
                if (list_events.Count > 0)
                {
                    //appendLogToDictionary("removed " + list_events.Count + " " + DateTime.Now, BalloonIcon.Warning);
                    //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "removed " + list_events.Count + " " + DateTime.Now /* + "\nlast: " + SubjectDomainName + "\\\\" + SubjectUserName + "\n" + str_path*/, BalloonIcon.Warning);
                }
                else
                {
                    return;
                }

                string str_path = null;
                list_events.First().TryGetValue("ObjectName", out str_path);
                string SubjectUserName = null;
                list_events.First().TryGetValue("SubjectUserName", out SubjectUserName);
                string SubjectDomainName = null;
                list_events.First().TryGetValue("SubjectDomainName", out SubjectDomainName);

                Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                {
                    MenuItem mi = new MenuItem();
                    string str_path_short = str_path.Length > (menuitem_header_length * 2 + 5) ? str_path.Substring(0, menuitem_header_length) + " ... " + str_path.Substring(str_path.Length - menuitem_header_length) : str_path;
                    string str_path_prefix = menuItemData.date_time.ToString("yyyy/MM/dd HH:mm:ss ")+"removed " +list_events.Count +" object(s). Last one:";
                    mi.Header = str_path_prefix + "\n" + str_path_short;
                    mi.ToolTip = "Open dialog for listing of deleted objects.\n"+str_path;
                    mi.Command = CustomRoutedCommand_DialogListingDeletedFiles;
                    mi.CommandParameter = list_events;
                    mi.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 139, 0));
                    //mi.MouseRightButtonUp

                    //MenuItemData id = new MenuItemData(str_path, mi, MenuItemData.Type.removed_items); // user_owner
                    menuItemData.path = str_path;
                    menuItemData.mi = mi;
                    menuItemData.type = MenuItemData.Type.removed_items;
                    stackPaths.Add(menuItemData);
                    currentMenuItem = menuItemData;

                    // Если текущий пункт меню является первым в стеке, то можно вывести баллон, иначе - нет, потому что возникло следующее событие
                    // и его нельзя перекрывать старым баллоном.
                    MenuItemData first_menuItemData = stackPaths.OrderBy(d => d.index).Last();
                    if (first_menuItemData == menuItemData)
                    {
                        // Взять первые три файла из списка и вывести их в диалоговое окно:
                        //List<string> text = new List<string>();

                        List<Dictionary<string,string>> temp_list =  list_events.GetRange(0, list_events.Count<=3 ? list_events.Count : 3 );
                        List<string> temp_file_names = new List<string>();
                        foreach(Dictionary<string, string> rec in temp_list)
                        {
                            string ObjectName = null;
                            rec.TryGetValue("ObjectName", out ObjectName);
                            temp_file_names.Add(ObjectName);
                        }
                        string str_for_message = String.Join("\n", temp_file_names);
                        TrayPopupMessage popup = new TrayPopupMessage("Open dialog for listing of deleted objects\n\n"+ str_for_message, "removed " + list_events.Count + " " + DateTime.Now, WatchingObjectType.File, _notifyIcon, TrayPopupMessage.ControlButtons.Clipboard);
                        popup.MouseDown+= (_sender, args) =>
                        {
                            _notifyIcon.CustomBalloon.IsOpen = false;
                            DialogListingDeletedFiles(list_events);
                        };
                        // TODO: Подумать, как уведомлять пользователя об удалении файлов другим методом, потому что возможно прямо сейчас висит баллон,
                        // который показывает пользователю, что файл поменялся, а это важнее, чем, например удаление файлов блокировки .git
                        //_notifyIcon.ShowCustomBalloon(popup, PopupAnimation.Fade, 4000);
                    }

                    reloadCustomMenuItems();
                });
            }
            catch( Exception ex )
            {
                Console.WriteLine("Error while reading the event logs");
                StackTrace st = new StackTrace(ex, true);
                StackFrame st_frame = st.GetFrame(st.FrameCount - 1);
                string logText = "in: " + st_frame.GetFileName() + ":(" + st_frame.GetFileLineNumber() + "," + st_frame.GetFileColumnNumber() + ")" + "\n" + ex.Message;
                appendLogToDictionary(logText, BalloonIcon.Error);
                //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "in: "+st_frame.GetFileName()+":(" + st_frame.GetFileLineNumber()+","+st_frame.GetFileColumnNumber()+")" + "\n" + ex.Message, BalloonIcon.Error);
            }
        }

        // http://stackoverflow.com/questions/12570324/c-sharp-run-a-thread-every-x-minutes-but-only-if-that-thread-is-not-running-alr
        private static BackgroundWorker worker = new BackgroundWorker(); // Класс BackgroundWorker позволяет выполнить операцию в отдельном, выделенном потоке. https://msdn.microsoft.com/ru-ru/library/system.componentmodel.backgroundworker%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396

        // Путь к файлу и время, когда вызвано событие (Искать в журнале будем с учётом этого времени -1с)
        // Валидны только те записи, которые не старше 60 сек от момента проверки и только те пути,
        // которые есть у наблюдателя (в логах пишутся много событий удаления)
        static ConcurrentDictionary<string, Path_ObjectType> dict_path_time = new ConcurrentDictionary<string, Path_ObjectType>();

        private static void OnChanged_file(object source, FileSystemEventArgs e)
        {
            // Застолбить место в меню:
            MenuItemData menuItemData = new MenuItemData();
            OnChanged(source, e, WatchingObjectType.File, menuItemData);
        }

        private static void OnChanged_folder(object source, FileSystemEventArgs e)
        {
            // Застолбить место в меню:
            MenuItemData menuItemData = new MenuItemData();
            OnChanged(source, e, WatchingObjectType.Folder, menuItemData);
        }

        private static void OnChanged(object source, FileSystemEventArgs e, WatchingObjectType wType, MenuItemData menuItemData)
        {
            if(e.ChangeType == WatcherChangeTypes.Deleted)
            {
                if (stackPaths.Exists(x=>x.path==e.FullPath) == true)
                {
                    reloadCustomMenuItems();
                }

                DateTime oldDateTime = DateTime.Now;
                //dict_path_time.AddOrUpdate(e.FullPath, DateTime.Now, (key, oldValue) => oldDateTime);
                Path_ObjectType path_object_type = new Path_ObjectType(e.FullPath, wType, DateTime.Now);
                dict_path_time.AddOrUpdate(e.FullPath, path_object_type, (key, oldValue) => path_object_type);

                // Обработку списка удалённых файлов отправить в фон, если фона ещё нет. Если фон есть,
                // то не надо ничего запускать. Фоновый процесс сам мониторит изменения в очереди.
                if ( !worker.IsBusy)
                {
                    worker = new BackgroundWorker();
                    worker.DoWork += new DoWorkEventHandler(ShowPopupDeletePath);
                    worker.RunWorkerAsync(menuItemData);
                }
                else
                {
                    Console.Write("skip");
                }
                return;
            }

            if(File.Exists(e.FullPath) == true)
            {
                wType = WatchingObjectType.File;
            }
            else if(Directory.Exists(e.FullPath)==true)
            {
                wType = WatchingObjectType.Folder;
            }
            else
            {
                // WTF???
                //_notifyIcon.ShowBalloonTip("FileChangesWatcher", "Object " + e.FullPath + " not Exist!", BalloonIcon.Error);
                return;
            }

            // Проверить, а не начинается ли путь с исключения:
            for (int i = 0; i <= arr_folders_for_exceptions.Count - 1; i++)
            {
                if (e.FullPath.StartsWith(arr_folders_for_exceptions.ElementAt(i)) == true &&
                        (
                            e.FullPath.Replace(arr_folders_for_exceptions.ElementAt(i), "") == "" ||
                            e.FullPath.Replace(arr_folders_for_exceptions.ElementAt(i), "")[0] == Path.DirectorySeparatorChar
                        )
                    )
                {
                    return;
                }
            }
            
            // Если изменяемым является только каталог, то не регистрировать это изменение.
            // Я заметил возникновение этого события, когда я меняю что-то непосредственно в подкаталоге 
            // (например, переименовываю его подфайл или подкаталога)
            // Не регистрировать изменения каталога (это не переименование)
            if ( wType==WatchingObjectType.Folder)
            {
                return;
            }

            if ( wType==WatchingObjectType.File)
            {
                String file_name = Path.GetFileName(e.FullPath);
                if (_re_extensions.IsMatch(file_name) == false)
                {
                    return;
                }
                // Проверить, а не начинается ли имя файла с исключения:
                for (int i = 0; i <= arr_files_for_exceptions.Count - 1; i++)
                {
                    if (Path.GetFileNameWithoutExtension(e.FullPath).StartsWith(arr_files_for_exceptions.ElementAt(i)) == true)
                    {
                        return;
                    }
                }
            }
            appendPathToDictionary(e.FullPath, e.ChangeType, wType, menuItemData);
        }

        private static void OnRenamed_file(object source, RenamedEventArgs e)
        {
            // Застолбить место в меню:
            MenuItemData menuItemData = new MenuItemData();
            OnRenamed(source, e, WatchingObjectType.File, menuItemData);
        }

        private static void OnRenamed_folder(object source, RenamedEventArgs e)
        {
            // Застолбить место в меню:
            MenuItemData menuItemData = new MenuItemData();
            OnRenamed(source, e, WatchingObjectType.Folder, menuItemData);
        }
        private static void OnRenamed(object source, RenamedEventArgs e, WatchingObjectType wType, MenuItemData menuItemData)
        {
            // Проверить, а не начинается ли путь с исключения:
            foreach (string ex_path in arr_folders_for_exceptions)
            {
                if (e.FullPath.StartsWith(ex_path) == true &&
                        (
                            e.FullPath.Replace(ex_path, "") == "" ||
                            e.FullPath.Replace(ex_path, "")[0] == Path.DirectorySeparatorChar
                        )
                    )
                {
                    return;
                }
            }

            // Проверить, а не является ли расширение наблюдаемым?
            if (wType==WatchingObjectType.File)
            {
                String new_file_name = Path.GetFileName(e.FullPath);
                bool bool_new_is_exception = false;
                String old_file_name = Path.GetFileName(e.OldFullPath);
                bool bool_old_is_exception = false;
                foreach (string ex_path in arr_files_for_exceptions )
                {
                    if (old_file_name.StartsWith(ex_path) == true)
                    {
                        bool_old_is_exception=true;
                        break;
                    }
                }

                // Проверить, а не начинается ли имя файла с исключения для имён файлов:
                foreach (string ex_path in arr_files_for_exceptions)
                {
                    if (new_file_name.StartsWith(ex_path) == true)
                    {
                        bool_new_is_exception = true;
                        break;
                    }
                }

                // Если старый путь не был исключением, то перестроить меню (т.к. возможно старый путь там был)
                if (bool_old_is_exception == false)
                {
                    reloadCustomMenuItems();
                }
                if (bool_new_is_exception == true)
                {
                    return;
                }

                if (_re_extensions.IsMatch(new_file_name) == false)
                {
                    return;
                }
            }

            appendPathToDictionary(e.FullPath, e.ChangeType, wType, menuItemData);
        }

        // Конец параметров для отслеживания изменений в файловой системе. =======================================

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon.Dispose();
            base.OnExit(e);
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
                if (!File.Exists(dllPath))
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
                if (!File.Exists(dllPath))
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
        static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);

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

        // Этот набор нужен, чтобы вывести какое-либо окно на передний план.
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, uint Msg);

        // http://stackoverflow.com/questions/10740346/setforegroundwindow-only-working-while-visual-studio-is-open?answertab=votes#tab-top
        private const int ALT = 0xA4;
        private const uint Restore = 9;
        private const int EXTENDEDKEY = 0x1;
        private const int KEYUP = 0x2;

        // Сделать окно по указанному handle главным и в фокусе. Это нетривиальная задача. 
        // Вот этот код работает в 99% случаев, но ооочень редко всё-таки иногда даёт сбой.
        // Как добиться 100% пока не знаю.
        public static void ActivateWindow(IntPtr mainWindowHandle)
        {
            //check if already has focus
            if (mainWindowHandle == GetForegroundWindow()) return;

            //check if window is minimized
            if (IsIconic(mainWindowHandle))
            {
                ShowWindow(mainWindowHandle, Restore);
            }

            // Simulate a key press
            keybd_event((byte)ALT, 0x45, EXTENDEDKEY | 0, 0);

            //SetForegroundWindow(mainWindowHandle);

            // Simulate a key release
            keybd_event((byte)ALT, 0x45, EXTENDEDKEY | KEYUP, 0);

            SetForegroundWindow(mainWindowHandle);
        }

        // Методы для открытия свойств каталога:
        // http://stackoverflow.com/questions/1936682/how-do-i-display-a-files-properties-dialog-from-c?answertab=votes#tab-top
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const int SW_SHOW = 5;
        private const uint SEE_MASK_INVOKEIDLIST = 12;
        // Описание функции: https://msdn.microsoft.com/ru-ru/library/windows/desktop/bb759784(v=vs.85).aspx
        public static bool ShowFileProperties(string Filename)
        {
            SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(info);
            info.lpVerb = "properties";  // edit, explore, find, open, print
            info.lpFile = Filename;
            info.nShow = SW_SHOW;
            info.fMask = SEE_MASK_INVOKEIDLIST;
            return ShellExecuteEx(ref info);
        }
    }

}

