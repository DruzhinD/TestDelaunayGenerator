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

        public SmootherBase(SmootherConfig config)
        {
            this.Config = config;
        }

        /// <summary>
        /// Применить сглаживание
        /// </summary>
        /// <param name="dcelMesh"></param>
        public abstract void Smooth(IRestrictedDCEL dcelMesh);
    }
}
