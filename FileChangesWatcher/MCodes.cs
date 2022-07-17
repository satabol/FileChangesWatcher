using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileChangesWatcher {
    public enum MCodes {
        _000,
        _0001,
    }

    /// <summary>
    /// Преобразователь кода ошибки в число.
    /// https://stackoverflow.com/questions/15388072/how-to-add-extension-methods-to-enums?answertab=votes#tab-top
    /// </summary>
    static class _MCodeExtensions
    {
        public static int toInt(this MCodes mcode) {
            int val = Int32.Parse(mcode.ToString().Substring(1));
            return val;
        }

        // <FIX11>Добавил функцию преобразования MCodes ошибки в число.</FIX11>
        /// <summary>
        /// mini formar error message - краткий формат представления ошибки.
        /// </summary>
        /// <param name="mcode"></param>
        /// <returns></returns>
        public static string mfem(this MCodes mcode) {
            //string str = $"{nameof(rcode)} = {rcode}, {nameof(mcode)} = {mcode}";
            int val = Int32.Parse(mcode.ToString().Substring(1));
            string str = $"{nameof(mcode)} = {val}";
            return str;
        }


        // https://stackoverflow.com/questions/4104910/convert-system-drawing-color-to-system-windows-media-color

        /// <summary>
        /// Преобразование Цвета
        /// </summary>
        public static System.Windows.Media.Color ToMediaColor(this System.Drawing.Color color)
        {
           return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}
