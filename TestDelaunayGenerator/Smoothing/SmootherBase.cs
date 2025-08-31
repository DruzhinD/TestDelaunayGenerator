using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator;
using TestDelaunayGenerator.DCELMesh;

namespace TestDelaunayGenerator.Smoothing
{
    /// <summary>
    /// Сглаживатель
    /// </summary>
    public abstract class SmootherBase
    {
        /// <summary>
        /// Конфигурация сглаживания
        /// </summary>
        public SmootherConfig Config { get; set; }

        /// <summary>
        /// Объект сетки
        /// </summary>
        public IRestrictedDCEL DcelMesh { get => mesh; }

        /// <summary>
        /// Объект сетки, доступен для записи
        /// </summary>
        protected IRestrictedDCEL mesh;

        /// <summary>
        /// Инициализация сглаживания
        /// </summary>
        /// <param name="config">конфигурация</param>
        /// <param name="mesh">сетка типа DCEL с ограничениями</param>
        public SmootherBase(SmootherConfig config, IRestrictedDCEL mesh)
        {
            this.Config = config;
            this.mesh = mesh;
        }

        /// <summary>
        /// Применить сглаживание
        /// </summary>
        public abstract void Smooth();
    }
}
