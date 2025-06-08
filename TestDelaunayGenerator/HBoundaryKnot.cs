using GeometryLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDelaunayGenerator
{
    public class HBoundaryKnot : HKnot
    {
        /// <summary>
        /// Идентификатор границы, в которую входит точка
        /// </summary>
        public readonly int boundaryId;
        /// <summary>
        /// Идентификатор ребра, которому принадлежит точка
        /// </summary>
        public readonly int edgeId;

        public HBoundaryKnot(
            double x, double y,
            int borderId, int edgeId,
            int marker = 0, int typeEx = 0)
            : base(x, y, marker, typeEx)
        {
            this.boundaryId = borderId;
            this.edgeId = edgeId;
        }
    }
}
