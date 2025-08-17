//---------------------------------------------------------------------------
//                    ПРОЕКТ  "РУСЛОВЫЕ ПРОЦЕССЫ"
//                         проектировщик:
//                           Потапов И.И.
//---------------------------------------------------------------------------
//                 кодировка : 30.09.2024 Потапов И.И.
//---------------------------------------------------------------------------
//                  + 
//                 кодировка : 29.03.2025 Потапов И.И.
//---------------------------------------------------------------------------
namespace TestDelaunayGenerator.SimpleStructures
{
    using CommonLib;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// структура треугольника, содержащая 3 его вершины
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Troika
    {
        /// <summary>
        /// Флаг, обозначающий атрибуты тройки вершин (вхождение в область и т.п.)
        /// </summary>
        public TriangleState flag;
        public int i;
        public int j;
        public int k;

        /// <summary>
        /// Получить вершину треугольника из тройки
        /// </summary>
        /// <param name="index">индекс вершины треугольника (относительно самого треугольника, внутренний индекс) [0..2]. <br/>
        /// Если передать другое число, то результатом будет вершины по внутреннему индексу 2
        /// </param>
        /// <returns>Индекс вершины треугольника в общем массиве точек</returns>
        public int this[int index]
        {
            get => index == 0 ? i : index == 1 ? j : k;
            set { if (index == 0) i = value; else if (index == 1) j = value; else k = value; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int i, int j, int k) Get() => (i, j, k);

        /// <summary>
        /// Преобразовать в треугольник
        /// </summary>
        public TriElement GetTri => new TriElement((uint)i, (uint)j, (uint)k);

        public override string ToString()
        {
            return $"{i},{j},{k};flag:{flag}";
        }

        /// <summary>
        /// true - треугольник содержит вершину <paramref name="vid"/>
        /// </summary>
        /// <param name="vid"></param>
        /// <returns></returns>
        public bool Contains(int vid)
        {
            if (i == vid ||
                j == vid ||
                k == vid)
                return true;
            return false;
        }
    }
}