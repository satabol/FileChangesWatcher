using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using System.Management;

using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Interop;

using System.Reflection;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

using System.Web;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Collections;
using IniParser;

namespace Stroiproject
{

    // http://www.thomaslevesque.com/tag/clipboard/
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct BITMAPFILEHEADER
    {
        public static readonly short BM = 0x4d42; // BM

        public short bfType;
        public int bfSize;
        public short bfReserved1;
        public short bfReserved2;
        public int bfOffBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

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

    public static class BinaryStructConverter
    {
        public static T FromByteArray<T>(byte[] bytes) where T : struct
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                int size = Marshal.SizeOf(typeof(T));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(bytes, 0, ptr, size);
                object obj = Marshal.PtrToStructure(ptr, typeof(T));
                return (T)obj;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        public static byte[] ToByteArray<T>(T obj) where T : struct
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                int size = Marshal.SizeOf(typeof(T));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(obj, ptr, true);
                byte[] bytes = new byte[size];
                Marshal.Copy(ptr, bytes, 0, size);
                return bytes;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
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
        static SortedDictionary<String, MenuItemData> stackPaths = new SortedDictionary<string, MenuItemData>();

        // Пользовательская команда:
        public static RoutedCommand CustomRoutedCommand = new RoutedCommand();
        private void ExecutedCustomCommand(object sender, ExecutedRoutedEventArgs e)
        {
            //MessageBox.Show("Custom Command Executed: "+ e.Parameter);
            Process.Start("explorer.exe", "/select,\"" + e.Parameter + "\"");
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
            // Сначала очистить пункты меню с путями к файлам:
            foreach(var obj in stackPaths)
            {
                notifyIcon.ContextMenu.Items.Remove(obj.Value.mi);
            }

            // Заполнить новые пункты:
            foreach (var obj in stackPaths.OrderBy(d => d.Value.index))
            {
                MenuItem mi = obj.Value.mi;
                notifyIcon.ContextMenu.Items.Insert(0, mi);
            }
        }

        private static TaskbarIcon notifyIcon=null;
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

            notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

            //notifyIcon.ContextMenu.Items.Insert(0, new Separator() );  // http://stackoverflow.com/questions/4823760/how-to-add-horizontal-separator-in-a-dynamically-created-contextmenu
            CommandBinding customCommandBinding = new CommandBinding(CustomRoutedCommand, ExecutedCustomCommand, CanExecuteCustomCommand);
            notifyIcon.ContextMenu.CommandBindings.Add(customCommandBinding);

            ht_icons = GetFileTypeAndIcon();
            initApplication(e);
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
            String iniFilePath = null; // System.IO.Path.GetDirectoryName(Environment.CommandLine.Replace("\"", "")) + "\\Stroiproject.ini";
            iniFilePath = Process.GetCurrentProcess().MainModule.FileName;
            iniFilePath = System.IO.Path.GetDirectoryName(iniFilePath) + "\\" + System.IO.Path.GetFileNameWithoutExtension(iniFilePath) + ".ini";

            if (File.Exists(iniFilePath) == false)
            {
                data.Sections.AddSection("General");
                data.Sections.AddSection("Extensions");
                data.Sections.AddSection("FoldersForWatch");
                data.Sections.AddSection("FoldersForExceptions");
                data.Sections.AddSection("FileNamesExceptions");
                // Количество файлов, видимое в меню:
                data.Sections["General"].AddKey("log_contextmenu_size", "7");
                // Список расширений, которые надо вывести на экран:
                data.Sections["Extensions"].AddKey("extensions01", ".tar|.jar|.zip|.bzip2|.gz|.tgz|.doc|.docx|.xls|.xlsx|.ppt|.pptx|.rtf|.pdf|.html|.xhtml|.txt|.mp3|.aiff|.au|.midi|.wav|.pst|.xml|.xslt|.java");
                data.Sections["Extensions"].AddKey("extensions02", ".gif|.png|.jpeg|.jpg|.tiff|.tif|.bmp");
                data.Sections["Extensions"].AddKey("extensions03", ".cs|.xaml|.config|.ico");
                data.Sections["Extensions"].AddKey("extensions04", ".gitignore|.md");
                data.Sections["Extensions"].AddKey("extensions05", ".msg|.ini");
                // Список каталогов, за которыми надо следить:
                data.Sections["FoldersForWatch"].AddKey("folder01", "D:\\");
                data.Sections["FoldersForWatch"].AddKey("folder02", "E:\\Docs");
                // Список каталогов, которые надо исключить из "слежения":
                data.Sections["FoldersForExceptions"].AddKey("folder01", "D:\\temp");
                data.Sections["FileNamesExceptions"].AddKey("file01", "~$");

                fileIniDataParser.WriteFile(iniFilePath, data);
            }
            else
            {
                data = fileIniDataParser.ReadFile(iniFilePath);
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

            // Определить список расширений, за которыми будет следить программа:
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
            extensions = _extensions;

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
                notifyIcon.ShowBalloonTip("Info", "No watching for folders. Set folders correctly.", BalloonIcon.Info);
            }
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
                watcher.NotifyFilter = NotifyFilters.LastAccess
                    | NotifyFilters.LastWrite
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
            notifyIcon.ShowBalloonTip("Info", "Watching folders: \n" + String.Join("\n",  _paths.ToArray() ), BalloonIcon.Info);
        }
        
        // Параметры для отлеживания изменений в файлах: ========================================

        // https://lucidworks.com/blog/2009/09/02/content-extraction-with-tika/
        // static String extentions = @".*(\.tar|\.jar|\.zip|\.bzip2|\.gz|\.tgz|\.doc|\.xls|\.ppt|\.rtf|\.pdf|\.html|\.xhtml|\.txt|\.bmp|\.gif|\.png|\.jpeg|\.tiff|\.mp3|\.aiff|\.au|\.midi|\.wav|\.pst|\.xml|\.class|\.java)$";
        static String extensions = null;
        // Список каталогов, которые надо исключить из вывода:
        static List<String> arr_folders_for_exceptions = null;
        static List<String> arr_files_for_exceptions = null;
        // Количество файлов, которые видны в контекстном меню:
        static int log_contextmenu_size = 5;

        private static void appendPathToDictionary(String _path, WatcherChangeTypes changedType)
        {
            String str = _path;
            Application.Current.Dispatcher.Invoke((Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
            {
                notifyIcon.ShowBalloonTip("", _path, BalloonIcon.Info);
                if (stackPaths.ContainsKey(_path) == true)
                {
                    removePathFromDictionary(_path);
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

                    // Создать пункт меню и наполнить его смыслом:
                    MenuItem mi = new MenuItem();
                    int delta = 20;
                    mi.Header = _path.Length>(delta*2+5) ? _path.Substring(0, delta)+" ... "+_path.Substring( _path.Length-delta) : _path;
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
                    mi.ToolTip = _path;
                    mi.Command = CustomRoutedCommand;
                    mi.CommandParameter = _path;

                    // Получить иконку файла в меню:
                    // http://www.codeproject.com/Articles/29137/Get-Registered-File-Types-and-Their-Associated-Ico
                    // Загрузить иконку файла в меню: http://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap?answertab=votes#tab-top
                    // Как-то зараза не грузится простым присваиванием.
                    String file_ext = Path.GetExtension(_path);
                    Icon mi_icon = getIconByExt(file_ext);
                    if(mi_icon != null)
                    {
                        BitmapImage bitmapImage = null;
                        using (MemoryStream memory = new MemoryStream())
                        {
                            mi_icon.ToBitmap().Save(memory, ImageFormat.Png);
                            memory.Position = 0;
                            bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memory;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                        }
                        mi.Icon = new System.Windows.Controls.Image
                        {
                            Source = bitmapImage
                        };
                    }
                    MenuItemData id = new MenuItemData(mi, max_value + 1);
                    stackPaths.Add(_path, id);
                    notifyIcon.ContextMenu.Items.Insert(0, id.mi);

                    // Максимальное количество файлов в списке:
                    if (stackPaths.Count > log_contextmenu_size)
                    {
                        // Удалить самый старый элемент из списка путей и из меню
                        String first = stackPaths.OrderBy(d => d.Value.index).First().Key;

                        MenuItemData _id = null;
                        if (stackPaths.TryGetValue(first, out _id))
                        {
                            notifyIcon.ContextMenu.Items.Remove(_id.mi);
                        }
                        stackPaths.Remove(first);
                    }
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
                    notifyIcon.ContextMenu.Items.Remove(_id.mi);
                    stackPaths.Remove(_path);
                    reloadCustomMenuItems();
                }
            });
        }

        // Запомнить файлы, которые изменялись последними:
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            // Проверить, а не начинается ли каталог с исключения:
            for(int i=0; i<= arr_folders_for_exceptions.Count-1; i++)
            {
                if( e.FullPath.StartsWith(arr_folders_for_exceptions.ElementAt(i))==true)
                {
                    return;
                }
            }
            // Проверить, а не начинается ли имя файла с исключения:
            for (int i = 0; i <= arr_files_for_exceptions.Count - 1; i++)
            {
                if (Path.GetFileNameWithoutExtension(e.FullPath).StartsWith(arr_files_for_exceptions.ElementAt(i)) == true)
                {
                    return;
                }
            }

            if (Regex.IsMatch(e.FullPath, extensions, RegexOptions.IgnoreCase))
            {
                String lastFilePath=null;
                // Console.WriteLine("watched file type changed.");
                // Specify what is done when a file is changed, created, or deleted.
                // Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    removePathFromDictionary(e.FullPath);
                    if (stackPaths.Count > 0)
                    {
                        lastFilePath = stackPaths.Last().Key;
                    }
                    else
                    {
                        lastFilePath = null;
                    }
                }
                else
                {
                    lastFilePath = e.FullPath;
                    appendPathToDictionary(lastFilePath, e.ChangeType);
                }
            }
        }

        private static void OnRenamed(object source, RenamedEventArgs e)
        {
            // Проверить, а не начинается ли каталог с исключения:
            bool exceptionFullPath = false;
            for(int i=0; i<= arr_folders_for_exceptions.Count-1; i++)
            {
                if( e.FullPath.StartsWith(arr_folders_for_exceptions.ElementAt(i))==true)
                {
                    exceptionFullPath=true;
                    break;
                }
            }
            bool exceptionFileNameFullPath = false;
            for (int i = 0; i <= arr_files_for_exceptions.Count - 1; i++)
            {
                if (System.IO.Path.GetFileNameWithoutExtension(e.FullPath).StartsWith(arr_files_for_exceptions.ElementAt(i)) == true)
                {
                    exceptionFileNameFullPath = true;
                    break;
                }
            }

            if (Regex.IsMatch(e.FullPath, extensions, RegexOptions.IgnoreCase)
                || Regex.IsMatch(e.OldFullPath, extensions, RegexOptions.IgnoreCase)
                )
            {
                String lastFilePath = null;
                // Specify what is done when a file is renamed.
                // Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);

                removePathFromDictionary(e.OldFullPath);
                if (exceptionFullPath==false && exceptionFileNameFullPath==false && Regex.IsMatch(e.FullPath, extensions, RegexOptions.IgnoreCase))
                {
                    lastFilePath = e.FullPath;
                    appendPathToDictionary(lastFilePath, e.ChangeType);
                }
            }
        }

        // Конец параметров для отслеживания изменений в файловой системе. =======================================

        protected override void OnExit(ExitEventArgs e)
        {
            notifyIcon.Dispose();
            base.OnExit(e);
        }
        
        // Для извлечения иконок файлов: http://www.codeproject.com/Articles/29137/Get-Registered-File-Types-and-Their-Associated-Ico
        [DllImport("shell32.dll", EntryPoint = "ExtractIconA",
            CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr ExtractIcon
            (int hInst, string lpszExeFileName, int nIconIndex);

        private static Hashtable ht_icons = null;
        /// <summary>
        /// Gets registered file types and their associated icon in the system.
        /// </summary>
        /// <returns>Returns a hash table which contains the file extension as keys, 
        /// the icon file and param as values.</returns>
        public static Hashtable GetFileTypeAndIcon()
        {
            try
            {
                // Create a registry key object to represent the 
                // HKEY_CLASSES_ROOT registry section
                RegistryKey rkRoot = Registry.ClassesRoot;
                //Gets all sub keys' names.
                string[] keyNames = rkRoot.GetSubKeyNames();
                Hashtable iconsInfo = new Hashtable();
                //Find the file icon.
                foreach (string keyName in keyNames)
                {
                    if (String.IsNullOrEmpty(keyName))
                        continue;
                    int indexOfPoint = keyName.IndexOf(".");

                    //If this key is not a file extension, .zip), skip it.
                    if (indexOfPoint != 0)
                        continue;
                    RegistryKey rkFileType = rkRoot.OpenSubKey(keyName);
                    if (rkFileType == null)
                        continue;
                    //Gets the default value of this key that 
                    //contains the information of file type.
                    object defaultValue = rkFileType.GetValue("");
                    if (defaultValue == null)
                        continue;
                    //Go to the key that specifies the default icon 
                    //associates with this file type.
                    string defaultIcon = defaultValue.ToString() + "\\DefaultIcon";
                    RegistryKey rkFileIcon = rkRoot.OpenSubKey(defaultIcon);
                    if (rkFileIcon != null)
                    {
                        //Get the file contains the icon and the index of the icon in that file.
                        object value = rkFileIcon.GetValue("");
                        if (value != null)
                        {
                            //Clear all unnecessary " sign in the string to avoid error.
                            string fileParam = value.ToString().Replace("\"", "");
                            iconsInfo.Add(keyName, fileParam);
                        }
                        rkFileIcon.Close();
                    }
                    rkFileType.Close();
                }
                rkRoot.Close();
                return iconsInfo;
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }

        /// <summary>
        /// Shows the icon associates with a specific file type.
        /// </summary>
        /// <param name="fileType">The type of file (or file extension).</param>
        private static Icon getIconByExt(string fileType)
        {
            Icon icon = null;
            try
            {
                string fileAndParam = (ht_icons[fileType.ToLower()]).ToString();

                if (String.IsNullOrEmpty(fileAndParam))
                    return null;


                icon = ExtractIconFromFile(fileAndParam, false);
            }
            catch (Exception exc)
            {
                //throw exc;
            }
            return icon;
        }

        /// <summary>
        /// Structure that encapsulates basic information of icon embedded in a file.
        /// </summary>
        public struct EmbeddedIconInfo
        {
            public string FileName;
            public int IconIndex;
        }

        /// <summary>
        /// Extract the icon from file.
        /// <param name="fileAndParam">The params string, such as ex: 
        ///    "C:\\Program Files\\NetMeeting\\conf.exe,1".</param>
        /// <returns>This method always returns the large size of the icon 
        ///    (may be 32x32 px).</returns>
        public static Icon ExtractIconFromFile(string fileAndParam)
        {
            try
            {
                EmbeddedIconInfo embeddedIcon = getEmbeddedIconInfo(fileAndParam);

                //Gets the handle of the icon.
                IntPtr lIcon = ExtractIcon(0, embeddedIcon.FileName,
                            embeddedIcon.IconIndex);

                //Gets the real icon.
                return Icon.FromHandle(lIcon);
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx
        (string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
        private static unsafe extern int DestroyIcon(IntPtr hIcon);
        /// <summary>
        /// Extract the icon from file.
        /// </summary>
        /// <param name="fileAndParam">The params string, such as ex: 
        ///    "C:\\Program Files\\NetMeeting\\conf.exe,1".</param>
        /// <param name="isLarge">Determines the returned icon is a large 
        ///    (may be 32x32 px) or small icon (16x16 px).</param>
        public static Icon ExtractIconFromFile(string fileAndParam, bool isLarge)
        {
            unsafe
            {
                uint readIconCount = 0;
                IntPtr[] hDummy = new IntPtr[1] { IntPtr.Zero };
                IntPtr[] hIconEx = new IntPtr[1] { IntPtr.Zero };

                try
                {
                    EmbeddedIconInfo embeddedIcon =
                        getEmbeddedIconInfo(fileAndParam);

                    if (isLarge)
                        readIconCount = ExtractIconEx
                        (embeddedIcon.FileName, 0, hIconEx, hDummy, 1);
                    else
                        readIconCount = ExtractIconEx
                        (embeddedIcon.FileName, 0, hDummy, hIconEx, 1);

                    if (readIconCount > 0 && hIconEx[0] != IntPtr.Zero)
                    {
                        //Get first icon.
                        Icon extractedIcon =
                        (Icon)Icon.FromHandle(hIconEx[0]).Clone();

                        return extractedIcon;
                    }
                    else //No icon read.
                        return null;
                }
                catch (Exception exc)
                {
                    //Extract icon error.
                    throw new ApplicationException
                        ("Could not extract icon", exc);
                }
                finally
                {
                    //Release resources.
                    foreach (IntPtr ptr in hIconEx)
                        if (ptr != IntPtr.Zero)
                            DestroyIcon(ptr);

                    foreach (IntPtr ptr in hDummy)
                        if (ptr != IntPtr.Zero)
                            DestroyIcon(ptr);
                }
            }
        }

        /// <summary>
        /// Parses the parameters string to the structure of EmbeddedIconInfo.
        /// </summary>
        /// <param name="fileAndParam">The params string, such as ex: 
        ///    "C:\\Program Files\\NetMeeting\\conf.exe,1".</param>
        protected static EmbeddedIconInfo getEmbeddedIconInfo(string fileAndParam)
        {
            EmbeddedIconInfo embeddedIcon = new EmbeddedIconInfo();

            if (String.IsNullOrEmpty(fileAndParam))
                return embeddedIcon;

            //Use to store the file contains icon.
            string fileName = String.Empty;

            //The index of the icon in the file.
            int iconIndex = 0;
            string iconIndexString = String.Empty;

            int commaIndex = fileAndParam.IndexOf(",");
            //if fileAndParam is some thing likes this: 
            //"C:\\Program Files\\NetMeeting\\conf.exe,1".
            if (commaIndex > 0)
            {
                fileName = fileAndParam.Substring(0, commaIndex);
                iconIndexString = fileAndParam.Substring(commaIndex + 1);
            }
            else
                fileName = fileAndParam;

            if (!String.IsNullOrEmpty(iconIndexString))
            {
                //Get the index of icon.
                iconIndex = int.Parse(iconIndexString);
                if (iconIndex < 0)
                    iconIndex = 0;  //To avoid the invalid index.
            }

            embeddedIcon.FileName = fileName;
            embeddedIcon.IconIndex = iconIndex;

            return embeddedIcon;
        }
    }
}

