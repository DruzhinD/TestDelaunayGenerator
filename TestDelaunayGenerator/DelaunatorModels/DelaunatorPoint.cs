using CommonLib.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.DelaunatorModels
{
    //TODO добавить в делонатор
    /// <summary>
    /// Структура точки, используемая в генераторе (делонаторе)
    /// </summary>
    public struct DelaunatorPoint
    {
        public IHPoint Point;
        public PointStatus PointStatus;
        public ContourPoint ContourPoint;

        public DelaunatorPoint(IHPoint point, ref PointStatus pointStatus, ref ContourPoint contourPoint)
        {
            this.Point = point;
            this.PointStatus = pointStatus;
            this.ContourPoint = contourPoint;
        }

        /// <summary>
        /// Устанавливает стандартные значения для точки
        /// </summary>
        public void SetDefault()
        {
            this.PointStatus = PointStatus.None;
        }
    }
}
