using CommonLib.Geometry;
using MemLogLib;
using MeshLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.DCELMesh
{
    /// <summary>
    /// <inheritdoc cref="IRestrictedDCEL"/>
    /// </summary>
    [DataContract]
    [KnownType(typeof(HPoint))]
    public class RestrictedDCEL : IRestrictedDCEL
    {
        public RestrictedDCEL() { }
        public RestrictedDCEL(IHPoint[] points, int[] halfEdges, PointStatus[] pointStatuses, Troika[] faces, EdgeIndex[] boundaryEdges)
        {
            Points = points;
            HalfEdges = halfEdges;
            PointStatuses = pointStatuses;
            Faces = faces;
            BoundaryEdges = boundaryEdges;
        }

        [DataMember]
        public IHPoint[] Points { get; set; }
        [DataMember]
        public int[] HalfEdges { get; set; }
        [DataMember]
        public PointStatus[] PointStatuses { get; set; }
        [DataMember]
        public Troika[] Faces { get; set; }
        [DataMember]
        public EdgeIndex[] BoundaryEdges { get; set; }

        public int GetVertex(int index)
        {
            throw new NotImplementedException();
        }

        public DcelTriMesh ToTriMesh()
        {
            DcelTriMesh mesh = new DcelTriMesh(
                this.HalfEdges,
                this.PointStatuses,
                this.Faces,
                this.BoundaryEdges,
                this.Points);

            mesh.CoordsX = Points.Select(p => p.X).ToArray();
            mesh.CoordsY = Points.Select(p => p.Y).ToArray();

            mesh.AreaElems = this.Faces
                .Where(troika => troika.flag == (int)TriangleInfect.Internal)
                .Select(troika => troika.GetTri)
                .ToArray();

            int boundPointCnt = this.PointStatuses.Count(p => p == PointStatus.Boundary);
            //ребра
            MEM.Alloc(boundPointCnt, ref mesh.BoundElems);
            MEM.Alloc(boundPointCnt, ref mesh.BoundElementsMark);

            //узлы
            MEM.Alloc(boundPointCnt, ref mesh.BoundKnots);
            MEM.Alloc(boundPointCnt, ref mesh.BoundKnotsMark);

            int meshPointId = 0;
            for (int i = 0; i < this.Points.Length; i++)
            {
                //неграничные пропускаем
                if (this.PointStatuses[i] != PointStatus.Boundary)
                    continue;
                //ребра
                mesh.BoundElems[meshPointId].Vertex1 = (uint)i;
                mesh.BoundElems[meshPointId].Vertex2 = (uint)BoundaryEdges[i].adjacent1;
                mesh.BoundElementsMark[meshPointId] = BoundaryEdges[i].BoundaryID;

                //узлы
                mesh.BoundKnots[meshPointId] = i;
                mesh.BoundKnotsMark[meshPointId] = i;
                meshPointId++;
            }

            return mesh;
        }
    }
}
