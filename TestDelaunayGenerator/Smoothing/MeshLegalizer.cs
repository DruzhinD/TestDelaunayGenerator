using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestDelaunayGenerator.Boundary;
using TestDelaunayGenerator.SimpleStructures;
using TestDelaunayGenerator.DCELMesh;

namespace TestDelaunayGenerator.Smoothing
{
    public class MeshLegalizer
    {
        DcelTriMesh mesh;

        int[] edgeStack;
        
        public void Legalize(DcelTriMesh mesh)
        {
            this.mesh = mesh;

            this.edgeStack = new int[mesh.CountElements];
        }

        //protected int Legalizer(int edgeA_ID)
        //{
        //    int i = 0;
        //}
    }
}
