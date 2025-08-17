using MemLogLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.SimpleStructures;

namespace TestDelaunayGenerator.DCELMesh
{
    /// <summary>
    /// Класс, содержащий методы расширения для <see cref="DcelTriMesh"/> и
    /// <see cref="IRestrictedDCEL"/>
    /// </summary>
    public static class MeshExtensions
    {
        public static DcelTriMesh ToDcelTriMesh(this IRestrictedDCEL dcelMesh)
        {
            DcelTriMesh mesh = new DcelTriMesh(
                dcelMesh.HalfEdges,
                dcelMesh.PointStatuses,
                dcelMesh.Faces,
                dcelMesh.BoundaryEdges,
                dcelMesh.Points);

            mesh.CoordsX = dcelMesh.Points.Select(p => p.X).ToArray();
            mesh.CoordsY = dcelMesh.Points.Select(p => p.Y).ToArray();

            mesh.AreaElems = dcelMesh.Faces
                .Where(troika => troika.flag == TriangleState.Internal)
                .Select(troika => troika.GetTri)
                .ToArray();

            int boundPointCnt = dcelMesh.PointStatuses.Count(p => p == PointStatus.Boundary);
            //ребра
            MEM.Alloc(boundPointCnt, ref mesh.BoundElems);
            MEM.Alloc(boundPointCnt, ref mesh.BoundElementsMark);

            //узлы
            MEM.Alloc(boundPointCnt, ref mesh.BoundKnots);
            MEM.Alloc(boundPointCnt, ref mesh.BoundKnotsMark);

            int meshPointId = 0;
            for (int i = 0; i < dcelMesh.Points.Length; i++)
            {
                //неграничные пропускаем
                if (dcelMesh.PointStatuses[i] != PointStatus.Boundary)
                    continue;
                //ребра
                mesh.BoundElems[meshPointId].Vertex1 = (uint)i;
                mesh.BoundElems[meshPointId].Vertex2 = (uint)dcelMesh.BoundaryEdges[i].adjacent1;
                mesh.BoundElementsMark[meshPointId] = dcelMesh.BoundaryEdges[i].BoundaryID;

                //узлы
                mesh.BoundKnots[meshPointId] = i;
                mesh.BoundKnotsMark[meshPointId] = i;
                meshPointId++;
            }

            return mesh;
        }
    }
}
