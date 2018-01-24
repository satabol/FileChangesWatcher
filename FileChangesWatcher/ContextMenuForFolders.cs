using IniParser;
using SharepointSync;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileChangesWatcher
{
    // Тестовый класс для регистрации приложения в качестве расширения. Пока ничего не делает.
    // После регистрации выдаёт контекстное меню 
    // Exclude folder from FileChangesWatcher
    // 
    // NET Shell Extensions - Shell Context Menus: http://www.codeproject.com/Articles/512956/NET-Shell-Extensions-Shell-Context-Menus
    //[Guid("62B2D7F7-A21A-4029-81F4-78AE60D2742E")]
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.Directory)]
    //[COMServerAssociation(AssociationType.Class, @"Directory\Background")]  // Список ключей реестра: http://stackoverflow.com/questions/20449316/how-add-context-menu-item-to-windows-explorer-for-folders
    [COMServerAssociation(AssociationType.Class, "*")] // Зарегистрировать для всех расширений. https://github.com/dwmkerr/sharpshell/issues/28
    public class ContextMenuForFolders : SharpContextMenu
    {
        protected override void OnInitialiseMenu(int parentItemIndex)
        {
            base.OnInitialiseMenu(parentItemIndex);
        }
        protected override bool CanShowMenu()
        {
            return true;
        }

        private ToolStripMenuItem itemCountLines = null;
        protected override System.Windows.Forms.ContextMenuStrip CreateMenu()
        {
            //throw new NotImplementedException();
            //  Create the menu strip.
            var menu = new ContextMenuStrip();

            //  Create a 'count lines' item.
            itemCountLines = new ToolStripMenuItem
            {
                Text = "FileChangesWatcher",
            };
            itemCountLines.Image = Resources.FileChangesWatcher_16x16.ToBitmap();

            CreateSubMenuFileChangesWatcher(itemCountLines);

            //  When we click, we'll call the 'CountLines' function.
            //itemCountLines.Click += (sender, args) => ShowRemoteRepoList();
            //itemCountLines.MouseHover += (sender, args) => CreateSubMenuRemoteRepoList(itemCountLines);

            //  Add the item to the context menu.
            menu.Items.Add(itemCountLines);

            //  Return the menu.
            return menu;
        }

        private void CreateSubMenuFileChangesWatcher(ToolStripMenuItem menu)
        {
            //List<ToolStripMenuItem>
            StringBuilder sb = new StringBuilder();
            String path = null;
            String strError = " error:";
            foreach (var folderPath in SelectedItemPaths)
            {
                path = folderPath;
                break;
            }

            try
            {
                if (FolderPath == null)
                {
                    //MessageBox.Show("FolderPath: null");
                    strError += " FolderPath=null";
                }
                else
                {
                    //MessageBox.Show("FolderPath: " + FolderPath);
                    if (path == null)
                    {
                        path = FolderPath;
                    }
                }

                if (path != null)
                {
                    bool bool_path_is_file = true;
                    try
                    {
                        // Проверить о чём идёт речь - о каталоге или о файле:
                        //bool_path_is_file = !File.GetAttributes(path).HasFlag(FileAttributes.Directory);
                        bool_path_is_file = LongFile.Exists(path);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is FileNotFoundException)
                    {
                        MessageBox.Show("Error on access to\n \"" + path + "\"\n\n"+ex.Message, "FileChangesWatcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    String iniFilePath = FileChangesWatcher.App.getSettingsFilePath(".ini");

                    FileIniDataParser fileIniDataParser = new FileIniDataParser();
                    IniParser.Model.IniData data; // = new IniParser.Model.IniData();
                    data = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                    if (bool_path_is_file == false)
                    {
                        foreach (string section_name in new string[] {"FoldersForWatch", "FoldersForExceptions"})
                        {
                            ToolStripMenuItem item = new ToolStripMenuItem();
                            bool bool_folder_is_in_Section = false;
                            // Проверить, что каталога ещё нет в исключениях:
                            for (int i = 0; i <= data.Sections[section_name].Count - 1; i++)
                            {
                                String folder = data.Sections[section_name].ElementAt(i).Value;
                                if (folder.Equals(path) == true)
                                {
                                    bool_folder_is_in_Section = true;
                                    break;
                                }
                            }

                            if (bool_folder_is_in_Section == true)
                            {
                                item.Text = "path \"" + path + "\" is in "+ section_name + " Section already";
                                item.Enabled = false;
                            }
                            else
                            {
                                item.Text = "Add path \"" + path + "\" to "+ section_name + " Section";
                                item.Click += (sender, args) =>
                                {
                                // Добавить этот каталог в исключения:
                                data = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                                    for (int i = 0; i <= 100000; i++)
                                    {
                                        string new_key = "folder" + i;
                                        if (data.Sections[section_name].ContainsKey(new_key) == false)
                                        {
                                            data.Sections[section_name].AddKey(new_key, path);
                                            fileIniDataParser.WriteFile(iniFilePath, data, Encoding.UTF8);
                                            MessageBox.Show("Folder\n \"" + path + "\"\n is append to "+ section_name+" Section FileChangesWatcher. Do not forget RELOAD settings!!!", "FileChangesWatcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            break;
                                        }
                                    }
                                };
                            }
                            menu.DropDownItems.Add(item);
                        }
                    }
                    else
                    {
                        string ext = Path.GetExtension(path).ToLower();
                        ToolStripMenuItem item = new ToolStripMenuItem();

                        bool bool_folder_is_in_Section = false;
                        // Проверить, что расширение не наблюдается приложением:
                        if (FileChangesWatcher.App.getExtensionsRegEx(data, new string[] { "Extensions", "UserExtensions" }).IsMatch(ext) == true)
                        {
                            bool_folder_is_in_Section = true;
                        }

                        if (bool_folder_is_in_Section == true)
                        {
                            item.Text = "extension \"" + ext + "\" is in Extensions Section already";
                            item.Enabled = false;
                        }
                        else
                        {
                            item.Text = "Add \"" + ext + "\" to Extensions Section";
                            item.Click += (sender, args) =>
                            {
                                // Добавить расширение в исключения:
                                data = fileIniDataParser.ReadFile(iniFilePath, Encoding.UTF8);
                                for (int i = 0; i <= 100000; i++)
                                {
                                    string new_key = "extension" + i;
                                    if (data.Sections["Extensions"].ContainsKey(new_key) == false)
                                    {
                                        data.Sections["Extensions"].AddKey(new_key, ext);
                                        fileIniDataParser.WriteFile(iniFilePath, data, Encoding.UTF8);
                                        MessageBox.Show("Extension \n \"" + ext + "\"\n is append to Extensions Section FileChangesWatcher. Do not forget RELOAD settings!!!", "FileChangesWatcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        break;
                                    }
                                }
                            };
                        }
                        menu.DropDownItems.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                strError += " Exception:" + e.Message;
                Console.WriteLine("Something wrong with path: " + path + "\n" + e.ToString());
            }
        }
    }

}
