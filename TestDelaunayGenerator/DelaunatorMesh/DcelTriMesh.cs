using CommonLib;
using CommonLib.Geometry;
using MemLogLib;
using MeshLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.DelaunatorModels;
using TestDelaunayGenerator.Smoothing;

namespace TestDelaunayGenerator.DelaunatorMesh
{

    /// <summary>
    /// Расширенная сетка <see cref="TriMesh"/>
    /// засчет HalfEdge и указателей на принадлежность точек
    /// </summary>
    public class DcelTriMesh : TriMesh, IRestrictedDCEL
    {
        public DcelTriMesh(int[] halfEdges, PointStatus[] pointStatuses, Triangle[] triangles, ContourPoint[] boundaryEdges, IHPoint[] points)
        {
            this.HalfEdges = halfEdges;
            this.PointStatuses = pointStatuses;
            this.Faces = triangles;
            this.BoundaryEdges = boundaryEdges;
            this.Points = points;
        }

        public DcelTriMesh(DcelTriMesh mesh)
            : base(mesh)
        {
            //нельзя передавать в ref свойство, поэтому используется переменная
            int[] halfEdges = null;
            MEM.MemCopy(ref halfEdges, mesh.HalfEdges);
            this.HalfEdges = halfEdges;

            PointStatus[] pointStatuses = null;
            MEM.MemCopy(ref pointStatuses, mesh.PointStatuses);
            this.PointStatuses = pointStatuses;

            Triangle[] triangles = null;
            MEM.MemCopy(ref triangles, mesh.Faces);
            this.Faces = triangles;

            ContourPoint[] boundaryEdges = null;
            MEM.MemCopy(ref boundaryEdges, mesh.BoundaryEdges);
            this.BoundaryEdges = boundaryEdges;

            IHPoint[] points = null;
            MEM.MemCopy(ref points, mesh.Points);
            this.Points = points;

        }

        public int[] HalfEdges { get; set; }

        public PointStatus[] PointStatuses { get; set; }

        public Triangle[] Faces { get; set; }

        public ContourPoint[] BoundaryEdges { get; set; }
        public IHPoint[] Points { get; set; }

        public override IMesh Clone()
        {
            return new DcelTriMesh(this);
        }
    }
}
