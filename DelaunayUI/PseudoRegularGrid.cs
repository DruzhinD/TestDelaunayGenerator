using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using CommonLib.Geometry;

namespace PseudoRegularGrid
{
    /// <summary>
    /// Генератор псевдорегулярных сеток для решения проблемы условия Делоне
    /// 
    /// Основная проблема: в регулярной квадратной сетке образуются квадраты,
    /// у которых все 4 вершины лежат на одной окружности, что нарушает
    /// условие Делоне (не более 3 точек на одной окружности).
    /// 
    /// Решение: добавление контролируемых случайных смещений к узлам сетки.
    /// </summary>
    public class PseudoRegularGridGenerator
    {
        private Random _random;

        /// <summary>
        /// Типы смещений для генерации псевдорегулярной сетки
        /// </summary>
        public enum PerturbationType
        {
            Uniform,    // Равномерное распределение в прямоугольнике
            Gaussian,   // Нормальное (гауссово) распределение
            Circular    // Равномерное распределение в круге
        }

        /// <summary>
        /// Конструктор генератора
        /// </summary>
        /// <param name="seed">Семя для генератора случайных чисел (null для случайного)</param>
        public PseudoRegularGridGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Генерирует псевдорегулярную сетку
        /// </summary>
        /// <param name="rows">Количество строк</param>
        /// <param name="cols">Количество столбцов</param>
        /// <param name="cellSize">Размер базовой ячейки</param>
        /// <param name="perturbation">Максимальное смещение (0.0-0.49)</param>
        /// <param name="perturbationType">Тип смещения</param>
        /// <param name="avoidSquares">Избегать образования квадратов</param>
        /// <returns>Список точек сетки</returns>
        public List<IHPoint> GenerateGrid(int rows, int cols, double cellSize = 1.0,
                                         double perturbation = 0.3,
                                         PerturbationType perturbationType = PerturbationType.Uniform,
                                         bool avoidSquares = true)
        {
            // Ограничиваем смещение для избежания пересечений
            perturbation = Math.Min(perturbation, 0.49);
            var points = new List<IHPoint>();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    // Базовые координаты узла сетки
                    double xBase = j * cellSize;
                    double yBase = i * cellSize;

                    // Генерируем смещение в зависимости от типа
                    var displacement = GenerateDisplacement(perturbationType, perturbation * cellSize);
                    double dx = displacement.X;
                    double dy = displacement.Y;

                    // Дополнительная коррекция для избежания квадратов
                    if (avoidSquares && perturbation > 0)
                    {
                        // Добавляем небольшое детерминированное смещение для разрушения симметрии
                        double phaseX = (i + j) * 0.1;
                        double phaseY = (i - j) * 0.1;
                        dx += 0.05 * cellSize * Math.Sin(phaseX);
                        dy += 0.05 * cellSize * Math.Cos(phaseY);
                    }

                    // Итоговые координаты
                    double x = xBase + dx;
                    double y = yBase + dy;

                    points.Add(new HPoint(x, y));
                }
            }

            return points;
        }

        /// <summary>
        /// Генерирует смещение в зависимости от типа распределения
        /// </summary>
        private IHPoint GenerateDisplacement(PerturbationType type, double maxDisplacement)
        {
            switch (type)
            {
                case PerturbationType.Uniform:
                    // Равномерное распределение в прямоугольнике
                    double dx = (_random.NextDouble() - 0.5) * 2 * maxDisplacement;
                    double dy = (_random.NextDouble() - 0.5) * 2 * maxDisplacement;
                    return new HPoint(dx, dy);

                case PerturbationType.Gaussian:
                    // Нормальное (гауссово) распределение
                    double gaussX = GenerateGaussian(0, maxDisplacement / 3.0);
                    double gaussY = GenerateGaussian(0, maxDisplacement / 3.0);
                    // Ограничиваем значения
                    gaussX = Math.Max(-maxDisplacement, Math.Min(maxDisplacement, gaussX));
                    gaussY = Math.Max(-maxDisplacement, Math.Min(maxDisplacement, gaussY));
                    return new HPoint(gaussX, gaussY);

                case PerturbationType.Circular:
                    // Равномерное распределение в круге
                    double angle = _random.NextDouble() * 2 * Math.PI;
                    double distance = _random.NextDouble() * maxDisplacement;
                    double circX = distance * Math.Cos(angle);
                    double circY = distance * Math.Sin(angle);
                    return new HPoint(circX, circY);

                default:
                    throw new ArgumentException($"Неизвестный тип смещения: {type}");
            }
        }

        /// <summary>
        /// Генерирует число с нормальным распределением (метод Бокса-Мюллера)
        /// </summary>
        private double GenerateGaussian(double mean, double stdDev)
        {
            double u1 = 1.0 - _random.NextDouble(); // Uniform(0,1] random doubles
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }
    }
}