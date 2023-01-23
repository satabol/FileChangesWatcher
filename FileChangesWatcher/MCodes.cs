using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileChangesWatcher {
    public enum MCodes {
        _000,
        _0001,
        _0002,
        _0003,
        _0004,
        _0005,
        _0006,
        _0007,
        _0008,
        _0009,
        _0010,
        _0011,
        _0012,
        _0013,
        _0014,
        _0015,
        _0016,
        _0017,
        _0018,
        _0019,
        _0020,
        _0021,
        _0022,
        _0023,
        _0024,
        _0025,
        _0026,
        _0027,
        _0028,
        _0029,
        _0030,
        _0031,
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
