using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FileChangesWatcher {
    class AppUtility {

        /// <summary>
        /// Получение OldFullPath падает на длинных именах, т.к. там идёт одна проверка, которая и приводит к исключению. https://referencesource.microsoft.com/#system/services/io/system/io/RenamedEventArgs.cs,51
        /// Меня эта проверка не сильно волнует, вот и пришлось использовать эту загогулину: https://stackoverflow.com/questions/3303126/how-to-get-the-value-of-private-field-in-c
        /// <br/>Field OldFullPath throw exception on long file names (>255). So use this hack.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="instance"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static object GetInstanceField(Type type, object instance, string fieldName) {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            object value = field.GetValue(instance);
            return value;
        }

        static public byte[] GetZipFromByteArray(string file_name, byte[] content)
        {
            byte[] res = null;
            using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream())
            {
                // create a zip
                using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    string file_name_with_ext = Path.GetFileName(file_name);
                    System.IO.Compression.ZipArchiveEntry zipItem = zip.CreateEntry(file_name_with_ext, CompressionLevel.Optimal);
                    // add the item bytes to the zip entry by opening the original file and copying the bytes 
                    using (System.IO.MemoryStream originalFileMemoryStream = new System.IO.MemoryStream(content))
                    {
                        using (System.IO.Stream entryStream = zipItem.Open())
                        {
                            originalFileMemoryStream.CopyTo(entryStream);
                        }
                    }
                }
                res = memoryStream.ToArray();
            }
            return res;
        }
    }
}
