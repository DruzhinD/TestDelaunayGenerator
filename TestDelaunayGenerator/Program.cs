using CommonLib.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.Areas;
using TestDelaunayGenerator.Boundary;

namespace TestDelaunayGenerator
{
    internal class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            while (true)
            {

                Test test = new Test();
                Console.WriteLine("Выбор тестовой области:");
                Console.WriteLine("1. Прямоугольник простой");
                Console.WriteLine("2. Прямоугольник большой");
                Console.WriteLine("3. Трапеция");
                Console.WriteLine("4. Круглое множество");
                Console.WriteLine("5. Круглое множество с границей");
                Console.WriteLine("6. Круглое множество с вогнутой границей");
                //Console.WriteLine("7. Равномерное распределение");
                //Console.WriteLine("8. Звезда (сетка) (с границей)");
                Console.WriteLine("Esc: выход");
                try
                {
                    ConsoleKeyInfo consoleKeyInfo = Console.ReadKey(true);
                    switch (consoleKeyInfo.Key)
                    {
                        case ConsoleKey.Escape:
                            return;
                        case ConsoleKey.D1:
                            test.CreateRestArea(0);
                            test.Run();
                            break;
                        case ConsoleKey.D2:
                            test.CreateRestArea(1);
                            test.Run();
                            break;
                        case ConsoleKey.D3:
                            test.CreateRestArea(2);
                            test.Run();
                            break;
                        case ConsoleKey.D4:
                            test.CreateRestArea(3);
                            test.Run();
                            break;
                        case ConsoleKey.D5:
                            test.CreateRestArea(4);
                            test.Run();
                            break;
                        case ConsoleKey.D6:
                            test.CreateRestArea(5);
                            test.Run();
                            break;
                    }
                    Console.Clear();
                }
                catch (Exception ee)
                {
                    Console.WriteLine(ee.Message);
                }
            }
        }
    }
}
