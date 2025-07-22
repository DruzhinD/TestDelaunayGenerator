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
    }
}
