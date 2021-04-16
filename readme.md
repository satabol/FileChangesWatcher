## Download

https://sourceforge.net/projects/filechangeswatcher/files/

No drivers, no spyware, no admin rights.

## Description

![File.05](markdown.images/file.05.png)

This application for **Windows** XP/8/10 users only and it is just a UI of WinAPI functions
System.IO.FileSystemEventHandler. See documentation of Win-Api main functions:

https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemeventhandler?view=netframework-4.0

and

https://weblogs.asp.net/ashben/31773

Application has tray icon. When user or program create files, change files, remove files then this application
show popup of event and you can click it to open windows explorer and set current file (if file exists).

## When to use it?

I use it when I need to know some disk activity:

- Save file activity:

![Blender Save File And Open Its Place](markdown.images/BlenderSaveFileAndOpenItsPlace.gif)

- Save Scripts and images:

![Blender Save Image And Scripts](markdown.images/BlenderSaveImageAndScripts.gif)

- mysql activity to modify database:

![File.02](markdown.images/file.02.png)

- Save MS Word file

![Save Word File](markdown.images/SaveWordFile.gif)

## Settings

The program has several settings in file FileChangesWatcher.js. This file has json format and has several sections:

![File.03](markdown.images/file.03.png)

If you do not have this file then application create it with default values.

#### General

```JSON
  "General": {
    "log_contextmenu_size": 10,
    "display_notifications": true,
    "log": true,
    "log_path": ".",
    "log_file_prefix": ""
  },
```

- **log_contextmenu_size**: 10 - How many items you will see in context menu of this application when you press right mouse button:

![File.04](markdown.images/file.04.png)

- **display_notifications**: true/false - Show popup notifications when this application catch dist events

![File.05](markdown.images/file.05.png)

- **log**: true/false - write all events to log file.
- **log_path**: "." | "D:/log" - Where to write log files. "." - into folder where application runs.
- **log_file_prefix**: "" - prefix for log files. This program always append current date at end of file name with format YYYY.MM.dd. 

#### Extensions

```JSON
"Extensions": [
    [{"archivers": ".tar|.jar|.zip|.bzip2|.gz|.tgz|.7z|.rar"}],
    {
      "office": ".xls|.xlt|.xlm|.xlsx|.xlsm|.xltx|.xltm|.xlsb|.xla|.xlam|.xll|.xlw|.ppt|.pot|.pptx|.pptm|.potx|.potm|.ppam|.ppsx|.ppsm|.sldx|.sldm|.vsd|.vsdx|.vdx|.vsx|.vtx|.vsl|.vsdm",
      "autodesk": ".dwg|.dxf|.dwf|.dwt|.dxb|.lsp|.dcl",
      "images": ".gif|.png|.jpeg|.jpg|.tiff|.tif|.bmp",
      "visual_studio": ".csproj|.sln|.vsix",
    },
    {
      "extensions03": ".cs|.xaml|.config|.ico",
    },
    {
      "extensions04": ".gitignore|.md",
    },
    {
      "extensions05": ".msg|.ini"
    },
    ".pdf|.html|.xhtml|.txt|.mp3|.aiff|.au|.midi|.wav|.pst|.xml|.java|.js|.php|.json|.exe|.html|.htm|.css|.csv|.dbd|.sql|.svg|.conf|.msi|.cptx|.mp4|.mp3|.flv|.dbf|.jsp|.rpm|.det|.dll",
	".pro",
	"trail.txt.[0-9]{2}",
	".dvb|.txt",
	".dbd", // diagrams dbForge
	".mp4|.ctb",
  ],
```

This is list of extensions for that application catch disk file events. WinAPI generate file events always for any files and disk.
This is very huge count of events! So this is not convinent. So this is list of extension with devider '|'. I create single regular
expression of this list. You can experiment with this format. See:

https://docs.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex?view=net-5.0

http://regexstorm.net/tester

JSON struct in this setting has no means. Program read only values. So you can create any hierarchy you want to classify extensions.


#### UserExtensions

```JSON
  "UserExtensions": [
    {
      "extensions01": ".json"
    },
    {
      "officeword": ".doc|.docx|.docm|.dotx|.dotm|.rtf|.xls|.xlsx|.xlsm|.vsd|.vsdx",
      "visual_studio": ".csproj|.sln",
    },
    ".bmp|.gif|.jpg|.jpeg|.tiff|.tif|.js|.cs|.java|.exe|.dwg|.dxf|.rar",
    ".tar|.jar|.zip|.bzip2|.gz|.tgz|.7z|.rar|.pro|.dvb|.txt|.pdf|.png|.dll",
    ".msg",
    ".dbd", // Diagramm dbForge
    ".mp4",
    ".ico|.md|.ctb",
    ".mht|.html",
    ".vba|.bas",
    ".reg",
    ".pc3",
    ".mct",  // Midas
    ".svg|.xaml",
    ".[0-9]+", // ProE versions files
    ".g4",
    ".ifc|.obj",
    ".py",
    ".exe|.dll|.h|.c|.cpp",
    ".blend[0-9]*", // blender
    ".py",
  ],
```
Only these extensions any user see in the context menu.

![File.06](markdown.images/file.06.png)

#### FileNamesExceptions

```JSON
  "FileNamesExceptions": [
    "~$"
  ]
```
Files with whese extensions will exclude to watch. Events of these files will exclude from the context menu and logging.

#### FoldersForWatch

```JSON
"FoldersForWatch": [
    {
      "folder01": "D:\\", // may be string or array of strings. You can finish path with double backslash or without double backslash
    },
    {"locals":["E:\\Docs", "F:\\", "T:\\", "C:\\Users", ]},
  ],
```

Folders or disks for watch. JSON struct in this setting has no means. Program read only values. So you can create any hierarchy you want to classify extensions.


#### FoldersForExceptions

```JSON
"FoldersForExceptions": [
    {
      "folder01": "D:\\temp"
    },
    "F:\\Enternet\\projects\\tender\\target"
  ],
```

If you want exclude some folders from watch so append them here. JSON struct in this setting has no means. Program read only values. So you can create any hierarchy you want to classify extensions.

## Other

![File.07](markdown.images/file.07.png)

1. Open setting file
2. Reload settings

## Author

email: **satabol@yandex.ru**