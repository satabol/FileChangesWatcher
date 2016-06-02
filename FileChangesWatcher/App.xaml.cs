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
    
    // Данные для путей файлов, которые будут показываться у меню:
    class MenuItemData
    {
        public Int32 index;
        public MenuItem mi;

        public MenuItemData(MenuItem menuItem, int index)
        {
            this.mi = menuItem;
            this.index = index;
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : Application
    {
        // Добавление пользовательских меню выполнено на основе: https://msdn.microsoft.com/ru-ru/library/ms752070%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396

        // Список пользовательских пунктов меню, в которые записываются пути изменяемых файлов:
        static SortedDictionary<string, MenuItemData> stackPaths = new SortedDictionary<string, MenuItemData>();
        public static String GetStackPathsAsString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var obj in stackPaths.OrderBy(d => d.Value.index).Reverse().ToArray())
            {
                if(sb.Length > 0)
                {
                    sb.Append("\n");
                }
                sb.Append(obj.Key);
            }
            return sb.ToString();
        }

        // Пользовательская команда:
        public static RoutedCommand CustomRoutedCommand = new RoutedCommand();
        private void ExecutedCustomCommand(object sender, ExecutedRoutedEventArgs e)
        {
            //MessageBox.Show("Custom Command Executed: "+ e.Parameter);
            String str_path = e.Parameter.ToString();
            gotoPathByWindowsExplorer(str_path);
        }

        private static void gotoPathByWindowsExplorer(string _path)
        {
            String str_path = _path;
            bool bool_path_is_file = true;
            try
            {
                // Проверить о чём идёт речь - о каталоге или о файле:
                bool_path_is_file = !File.GetAttributes(str_path).HasFlag(FileAttributes.Directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is FileNotFoundException)
            {
                // TODO: В дальнейшем надо подумать как на них реагировать.
                //reloadCustomMenuItems(); // Может так? Иногда события не успевают обработаться и опаздывают. Например при удалении каталога можно ещё получить его изменения об удалении файлов. Но когда обработчик приступит к обработке изменений, то каталог может быть уже удалён.
                _notifyIcon.ShowBalloonTip("cannot open explorer", ex.Message, BalloonIcon.Error);
                return;
            }

            if (bool_path_is_file)
            {
                Process.Start("explorer.exe", "/select,\"" + str_path + "\"");
            }
            else
            {
                Process.Start("explorer.exe", "\"" + str_path + "\"");
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
                    _notifyIcon.ContextMenu.Items.Remove(obj.Value.mi);
                }

                // Заполнить новые пункты:
                //Grid mi = null;
                MenuItem mi = null;
                foreach (var obj in stackPaths.OrderBy(d => d.Value.index).ToArray())
                {
                    if (File.Exists(obj.Key) == true || Directory.Exists(obj.Key) == true)
                    {
                        mi = obj.Value.mi;
                        //mi.Children.Cast<MenuItem>().First(e=>Grid.GetRow(e)==0 && Grid.GetColumn(e)==0).FontWeight = FontWeights.Normal;
                        mi.FontWeight = FontWeights.Normal;
                        _notifyIcon.ContextMenu.Items.Insert(0, mi);
                    }
                    else
                    {
                        stackPaths.Remove(obj.Key);
                    }
                }
                if (mi != null)
                {
                    //mi.FontWeight = FontWeights.Bold;
                    //mi.Children.Cast<MenuItem>().First(e => Grid.GetRow(e) == 0 && Grid.GetColumn(e) == 0).FontWeight = FontWeights.Bold;
                    mi.FontWeight = FontWeights.Bold;
                }

                // Максимальное количество файлов в списке должно быть не больше указанного максимального значения:
                if (stackPaths.Count > log_contextmenu_size)
                {
                    // Удалить самый старый элемент из списка путей и из меню
                    String first = stackPaths.OrderBy(d => d.Value.index).First().Key;

                    MenuItemData _id = null;
                    if (stackPaths.TryGetValue(first, out _id))
                    {
                        _notifyIcon.ContextMenu.Items.Remove(_id.mi);
                    }
                    stackPaths.Remove(first);
                }
            });
        }

        private static TaskbarIcon _notifyIcon=null;
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
            _notifyIcon.TrayBalloonTipClicked += (sender, args) =>
            {
                if (stackPaths.Count > 0 && bool_is_path_tooltip==true)
                {
                    // http://stackoverflow.com/questions/11549580/find-key-with-max-value-from-sorteddictionary
                    string path = stackPaths.OrderBy(d => d.Value.index).Last().Key;
                    gotoPathByWindowsExplorer(path);
                    bool_is_path_tooltip = false;
                }
            };

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
            _notifyIcon.TrayBalloonTipClosed += (sender, args) =>
            {
                if (bool_is_ballow_was_shown == true)
                {
                    bool_is_path_tooltip = false;
                    bool_is_ballow_was_shown = false;
                }
            };

            _notifyIcon.TrayBalloonTipShown += (sender, args) =>
            {
                bool_is_ballow_was_shown = true;
            };

            //notifyIcon.ContextMenu.Items.Insert(0, new Separator() );  // http://stackoverflow.com/questions/4823760/how-to-add-horizontal-separator-in-a-dynamically-created-contextmenu
            CommandBinding customCommandBinding = new CommandBinding(CustomRoutedCommand, ExecutedCustomCommand, CanExecuteCustomCommand);
            _notifyIcon.ContextMenu.CommandBindings.Add(customCommandBinding);

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
                data = fileIniDataParser.ReadFile(iniFilePath);
                data.Sections["General"].RemoveKey("autostart_on_windows");
                data.Sections["General"].AddKey("autostart_on_windows", "true");
                UTF8Encoding a = new UTF8Encoding();
                fileIniDataParser.WriteFile(iniFilePath, data);
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
                data = fileIniDataParser.ReadFile(iniFilePath);
                data.Sections["General"].RemoveKey("autostart_on_windows");
                data.Sections["General"].AddKey("autostart_on_windows", "false");
                fileIniDataParser.WriteFile(iniFilePath, data);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }


        public static void initApplication(StartupEventArgs e)
        {
            // Сбросить всех наблюдателей, установленных ранее:
            foreach(FileSystemWatcher watcher in watchers.ToArray() )
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= new FileSystemEventHandler(OnChanged);
                watcher.Created -= new FileSystemEventHandler(OnChanged);
                watcher.Deleted -= new FileSystemEventHandler(OnChanged);
                watcher.Renamed -= new RenamedEventHandler(OnRenamed);
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
                data.Sections["Extensions"].AddKey("others", ".pdf|.html|.xhtml|.txt|.mp3|.aiff|.au|.midi|.wav|.pst|.xml|.java");
                //data.Sections["Extensions"].AddKey("", "");
                // Список каталогов, за которыми надо следить:
                data.Sections["FoldersForWatch"].AddKey("folder01", @"D:\");
                data.Sections["FoldersForWatch"].AddKey("folder02", @"E:\Docs");
                data.Sections["FoldersForWatch"].AddKey("folder03", @"F:\");
                data.Sections["FoldersForWatch"].AddKey("folder04", @"\\test.server.network\path");
                // Список каталогов, которые надо исключить из "слежения" (просто будут сравниваться начала имён файлов):
                data.Sections["FoldersForExceptions"].AddKey("folder01", "D:\\temp");
                data.Sections["FileNamesExceptions"].AddKey("file01", "~$");

                fileIniDataParser.WriteFile(iniFilePath, data);
            }
            else
            {
                try
                {
                    data = fileIniDataParser.ReadFile(iniFilePath);
                    _notifyIcon.ToolTipText = "FileChangesWatcher. Right-click for menu";
                }
                catch (IniParser.Exceptions.ParsingException ex)
                {
                    _notifyIcon.ToolTipText = "FileChangesWatcher not working. Error in ini-file. Open settings in menu, please.";
                    _notifyIcon.ShowBalloonTip("Error in ini-file. Open settings in menu, please", ""+ex.Message + "", BalloonIcon.Error);
                    /*
                    if(System.Windows.MessageBox.Show("Error in ini-file:\n" + ex.Message+"\n\n Please - correct file or delete it for recreation.\n Application exit!\n\nOpen ini file before exit?", "Alert!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(iniFilePath);
                    }
                    //*/
                    //App.Current.Shutdown();
                    return;
                }
            }


            // При первом запуске проверить, если в настройках нет флага, отменяющего автозагрузку,
            // то прописать автозапуск приложения в реестр:
            if (data.Sections["General"].GetKeyData("autostart_on_windows") == null) {
                setAutostart();
            }else 
            if("true".Equals(data.Sections["General"].GetKeyData("autostart_on_windows").Value))
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

            // Определить список каталогов, за которыми надо наблюдать:
            ///*
            List<string> arr_folders_for_watch = new List<String>();
            for (int i = 0; i <= data.Sections["FoldersForWatch"].Count - 1; i++)
            {
                String folder = data.Sections["FoldersForWatch"].ElementAt(i).Value;
                if (folder.Length > 0 && arr_folders_for_watch.Contains(folder) == false && Directory.Exists(folder))
                {
                    arr_folders_for_watch.Add(folder);
                }
            }
            if (e != null)
            {
                for (int i = 0; i <= e.Args.Length - 1; i++)
                {
                    String folder = e.Args[i];
                    if (folder.Length > 0 && arr_folders_for_watch.Contains(folder) == false && Directory.Exists(folder))
                    {
                        arr_folders_for_watch.Add(folder);
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

            if (arr_folders_for_watch.Count >= 1)
            {
                setWatcherForFolderAndSubFolders(arr_folders_for_watch.ToArray());
            }
            else
            {
                _notifyIcon.ShowBalloonTip("Info", "No watching for folders. Set folders correctly.", BalloonIcon.Info);
            }
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

        private static void setWatcherForFolderAndSubFolders(String[] _paths)
        {
            foreach (String _path in _paths)
            {
                // Отслеживание изменения в файловой системе:
                FileSystemWatcher watcher = new FileSystemWatcher();
                watchers.Add(watcher);
                watcher.IncludeSubdirectories = true;

                watcher.Path = _path;
                /* Watch for changes in LastAccess and LastWrite times, and
                    the renaming of files or directories. */
                watcher.NotifyFilter = //NotifyFilters.LastAccess
                    NotifyFilters.LastWrite
                    | NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    ;
                // Only watch text files.
                // watcher.Filter = "*.*";
                watcher.Filter = "";

                // Add event handlers.
                watcher.Changed += new FileSystemEventHandler(OnChanged);
                watcher.Created += new FileSystemEventHandler(OnChanged);
                watcher.Deleted += new FileSystemEventHandler(OnChanged);
                watcher.Renamed += new RenamedEventHandler(OnRenamed);

                // Begin watching:
                watcher.EnableRaisingEvents = true;
            }
            //notifyIcon.ContextMenu.Opacity = 0.5;
            _notifyIcon.ShowBalloonTip("Info", "Watching folders: \n" + String.Join("\n",  _paths.ToArray() ), BalloonIcon.Info);
        }

        // Параметры для отлеживания изменений в файлах: ========================================

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
        private static int menuitem_header_length = 20;
        private static void appendPathToDictionary(String _path, WatcherChangeTypes changedType)
        {
            String str = _path;
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                _notifyIcon.CloseBalloon();
                _notifyIcon.ShowBalloonTip("go to path:",_path, BalloonIcon.Info);
                bool_is_path_tooltip = true; // После клика или после исчезновения баллона этот флаг будет сброшен.
                bool_is_ballow_was_shown = false;
                //_notifyIcon.TrayBalloonTipClicked
                // Если такой путь уже есть в логе, то нужно его удалить. Это позволит переместить элемент на верх списка.
                if (stackPaths.ContainsKey(_path) == true)
                {
                    //removePathFromDictionary(_path);
                    MenuItemData _id = null;
                    if (stackPaths.TryGetValue(_path, out _id))
                    {
                        _notifyIcon.ContextMenu.Items.Remove(_id.mi);
                        stackPaths.Remove(_path);
                    }
                }

                if (stackPaths.ContainsKey(_path) == false)
                {
                    int max_value = 0;
                    if (stackPaths.Count > 0)
                    {
                        // i = stackPaths.Last().Value;
                        // http://stackoverflow.com/questions/11549580/find-key-with-max-value-from-sorteddictionary
                        max_value = stackPaths.OrderBy(d => d.Value.index).Last().Value.index;
                    }

                    /*
                     * // Игрался с Grid, чтобы рядом с именем файла сделать кнопку Clipboard. Что-то эта кнопка
                     * прижималась к имени файла в конце, а никак не хотела выравниваться по правому краю контекстного меню.
                     * Пока ничего хорошего. Сделал общее копирование всех текущих путей.
                    // http://www.wpftutorial.net/gridlayout.html
                    Grid mi_grid = new Grid();
                    mi_grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                    ColumnDefinition col0 = new ColumnDefinition();
                    col0.Width = GridLength.Auto;
                    ColumnDefinition col1 = new ColumnDefinition();
                    col1.Width = new GridLength(18);
                    RowDefinition row0 = new RowDefinition();
                    mi_grid.ColumnDefinitions.Add(col0);
                    mi_grid.ColumnDefinitions.Add(col1);
                    mi_grid.RowDefinitions.Add(row0);

                    MenuItem mi_clipboard = new MenuItem();
                    mi_clipboard.Icon = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(
                        new Uri("pack://application:,,,/Icons/Clipboard_16x16.ico"))
                    };
                    //FileChangesWatcher.Resources.Clipboard_16x16;
                    mi_clipboard.ToolTip = "Copy path to clipboard";
                    MenuItem mi_file = new MenuItem();
                    Grid.SetColumn(mi_file, 0);
                    Grid.SetRow(mi_file, 0);
                    Grid.SetColumn(mi_clipboard, 1);
                    Grid.SetRow(mi_clipboard, 0);
                    mi_file.HorizontalAlignment = HorizontalAlignment.Stretch;
                    mi_clipboard.HorizontalAlignment = HorizontalAlignment.Right;
                    mi_grid.Children.Add(mi_file);
                    mi_grid.Children.Add(mi_clipboard);
                    */

                    // Создать пункт меню и наполнить его смыслом:
                    MenuItem mi = new MenuItem();
                    mi.Header = _path.Length>(menuitem_header_length*2+5) ? _path.Substring(0, menuitem_header_length)+" ... "+_path.Substring( _path.Length-menuitem_header_length) : _path;
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
                    mi.CommandParameter = _path;

                    // Получить иконку файла в меню:
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

                    MenuItemData id = new MenuItemData(mi, max_value + 1);
                    stackPaths.Add(_path, id);
                    _notifyIcon.ContextMenu.Items.Insert(0, id.mi);

                    reloadCustomMenuItems();
                }
            });
        }

        private static void removePathFromDictionary(String _path)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                MenuItemData _id = null;
                if (stackPaths.TryGetValue(_path, out _id))
                {
                    _notifyIcon.ContextMenu.Items.Remove(_id.mi);
                    stackPaths.Remove(_path);
                    reloadCustomMenuItems();
                }
            });
        }
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

        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            if(e.ChangeType == WatcherChangeTypes.Deleted)
            {
                if (stackPaths.ContainsKey(e.FullPath) == true)
                {
                    reloadCustomMenuItems();
                }
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

            bool bool_path_is_file = true;
            try
            {
                // Проверить о чём идёт речь - о каталоге или о файле:
                bool_path_is_file = !File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory);
            }
            catch(Exception ex) // when (ex is UnauthorizedAccessException || ex is FileNotFoundException) // Было ещё IOException
            {
                // TODO: В дальнейшем надо подумать как на них реагировать.
                reloadCustomMenuItems(); // Может так? Иногда события не успевают обработаться и опаздывают. Например при удалении каталога можно ещё получить его изменения об удалении файлов. Но когда обработчик приступит к обработке изменений, то каталог может быть уже удалён.
                return;
            }

            // Если изменяемым является только каталог, то не регистрировать это изменение.
            // Я заметил возникновение этого события, когда я меняю что-то непосредственно в подкаталоге 
            // (например, переименовываю его подфайл или подкаталог)
            // Не регистрировать изменения каталога (это не переименование)
            if ( bool_path_is_file==false)
            {
                return;
            }

            if (bool_path_is_file == true)
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
            appendPathToDictionary(e.FullPath, e.ChangeType);
        }

        private static void OnRenamed(object source, RenamedEventArgs e)
        {
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

            bool bool_path_is_file = true;
            try
            {
                // Проверить о чём идёт речь - о каталоге или о файле:
                bool_path_is_file = !File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory);
            }
            catch (Exception ex) //when (ex is UnauthorizedAccessException || ex is FileNotFoundException)  // Было ещё IOException
            {
                // TODO: В дальнейшем надо подумать как на них реагировать.
                reloadCustomMenuItems(); // Может так? Иногда события не успевают обработаться и опаздывают. Например при удалении каталога можно ещё получить его изменения об удалении файлов. Но когда обработчик приступит к обработке изменений, то каталог может быть уже удалён.
                return;
            }

            // Проверить, а не является ли расширение наблюдаемым?
            if (bool_path_is_file == true)
            {
                String file_name = Path.GetFileName(e.FullPath);

                // Проверить, а не начинается ли имя файла с исключения для имён файлов:
                for (int i = 0; i <= arr_files_for_exceptions.Count - 1; i++)
                {
                    if (file_name.StartsWith(arr_files_for_exceptions.ElementAt(i)) == true)
                    {
                        return;
                    }
                }

                if (_re_extensions.IsMatch(file_name) == false)
                {
                    return;
                }
            }

            appendPathToDictionary(e.FullPath, e.ChangeType);
        }

        // Конец параметров для отслеживания изменений в файловой системе. =======================================

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon.Dispose();
            base.OnExit(e);
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
                    App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Successfully registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                }
                else
                {
                    //MessageBox.Show("Not Registered!");
                    App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed registered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed\n"+ex.Message, BalloonIcon.Error);
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
                    App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Successfully unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Info);
                }
                else
                {
                    //MessageBox.Show("Not UnRegistered!");
                    App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed unregistered FileChangesWatcher in Windows Context Menu.", BalloonIcon.Error);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                App.NotifyIcon.ShowBalloonTip("FileChangesWatcher", "Failed\n" + ex.Message, BalloonIcon.Error);
            }
        }
        //*/

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
    }

}

