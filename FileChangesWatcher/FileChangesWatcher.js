{
  "General": {
    "log_contextmenu_size": 10,
    "display_notifications": true,
    "log": true,
    "log_path": ".",
    "log_file_prefix": ""
  },
  // What save to log files:
  "Extensions": [
    [{"archivers": ".tar|.jar|.zip|.bzip2|.gz|.tgz|.7z|.rar"}],
    {
      "officeexcel": ".xls|.xlt|.xlm|.xlsx|.xlsm|.xltx|.xltm|.xlsb|.xla|.xlam|.xll|.xlw",
      "officepowerpoint": ".ppt|.pot|.pptx|.pptm|.potx|.potm|.ppam|.ppsx|.ppsm|.sldx|.sldm",
      "officevisio": ".vsd|.vsdx|.vdx|.vsx|.vtx|.vsl|.vsdm",
      "autodesk": ".dwg|.dxf|.dwf|.dwt|.dxb|.lsp|.dcl",
      "extensions02": ".gif|.png|.jpeg|.jpg|.tiff|.tif|.bmp",
      "visual_studio": ".csproj|.sln|.vsix",
    },
    {
      "extensions03": ".cs|.xaml|.config|.ico|.c|.h|.hh",
    },
    {
      "extensions04": ".gitignore|.md",
    },
    {
      "extensions05": ".msg|.ini"
    },
    ".pdf|.html|.xhtml|.txt|.mp3|.aiff|.au|.midi|.wav|.pst|.xml|.java|.js|.php|.json|.exe|.html|.htm|.css|.csv|.dbd|.sql|.svg|.conf|.msi|.msu|.cptx|.mp4|.mp3|.flv|.dbf|.jsp|.rpm|.det|.dll",
	".pro",
	"trail.txt.[0-9]{2}",
	".dvb",
	".txt",
	".dbd", // diagrams dbForge
	".mp4",
    ".ctb",
    ".editorconfig",
  ],
  // What show in UI
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
    ".dbd", // diagram dbForge
    ".mp4",
    ".ico|.md|.ctb",
    ".mht|.html",
    ".vba|.bas",
    ".reg",
    ".pc3",
    ".mct",  // Midas
    ".svg|.xaml",
    ".[0-9]+", // ProE. Version files + mysql database files
    ".g4",
    ".ifc|.obj|.fbx",
    ".saz", // fiddler2 request archive
    ".dwt", // nanocad
    ".blend[0-9]*", // blender
    ".py",
	".fspy", // https://fspy.io/
	".pur", // https://www.pureref.com/
	".gltf|.fbx|.stl|.obj",
	".mov|.mp3|.avi",
    ".fsc", // Fast Stone
	".cif",".pdb", // chemical, openbabel as converter
	".pyd", // Python
	".mkv", // Export Blender
	".c|.h|.hh", // C
  ],
  "FoldersForWatch": [
    {
      "folder01": "D:\\",
    },
    {"Local disks":[
            "E:\\Docs", "F:\\", "G:\\", "T:\\",
			"C:\\Users\\Anna\\Downloads\\Telegram Desktop",
        ]},
    {
        "Network Disks":[
            //"\\\\server1.domain.com\\PROJECTS",
            //"\\\\server2.domain.com\\Files\Folder",
        ],
    }
  ],
  "FoldersForExceptions": [
    {
      "folder01": "D:\\temp"
    },
    "F:\\Enternet\\projects\\tender\\target",
  ],
  "FileNamesExceptions": [
    "~$",
  ]
}