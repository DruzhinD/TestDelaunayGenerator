using CommonLib;
using MemLogLib;
using MeshLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;
using TestDelaunayGenerator.Smoothing;

namespace TestDelaunayGenerator.Smoothing
{

    /// <summary>
    /// Расширенная сетка <see cref="TriMesh"/>
    /// засчет HalfEdge и указателей на принадлежность точек
    /// </summary>
    public class ExtendedTriMesh : TriMesh, IHalfEdge
    {
        public ExtendedTriMesh(int[] halfEdges, PointStatus[] pointStatuses, Troika[] triangles)
        {
            this.HalfEdges = halfEdges;
            this.PointStatuses = pointStatuses;
            this.Triangles = triangles;
        }

        public ExtendedTriMesh(ExtendedTriMesh mesh)
            : base(mesh)
        {
            //нельзя передавать в ref свойство, поэтому используется переменная
            int[] halfEdges = null;
            MEM.MemCopy(ref halfEdges, mesh.HalfEdges);
            this.HalfEdges = halfEdges;

            PointStatus[] pointStatuses = null;
            MEM.MemCopy(ref pointStatuses, mesh.PointStatuses);
            this.PointStatuses = pointStatuses;

            Troika[] triangles = null;
            MEM.MemCopy(ref triangles, mesh.Triangles);
            this.Triangles = triangles;

        }

        public int[] HalfEdges { get; set; }

        public PointStatus[] PointStatuses { get; set; }

        public Troika[] Triangles { get; set; }

        public int GetVertex(int index)
        {
            int triangleId = index / 3;
            var triangle = Triangles[triangleId];
            return (int)triangle[index % 3];
        }

        public override IMesh Clone()
        {
            return new ExtendedTriMesh(this);
        }
    }
}
