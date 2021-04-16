using System;
using System.Collections.Generic;
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
    }
}
