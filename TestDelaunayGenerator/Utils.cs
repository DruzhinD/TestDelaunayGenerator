using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator
{
    /// <summary>
    /// Содержит вспомогательный функционал,
    /// не относящийся к конкретной задаче
    /// </summary>
    public static class Utils
    {

        /// <summary>
        /// Подсвеченный вывод на консоль
        /// </summary>
        /// <param name="consoleColor">цвет</param>
        /// <param name="log">лог/сообщение</param>
        public static void ConsoleWriteLineColored(ConsoleColor consoleColor, string log)
        {
            var defaultColor = Console.BackgroundColor;
            Console.BackgroundColor = consoleColor;
            Console.WriteLine(log);
            Console.BackgroundColor = defaultColor;
        }
    }
}
