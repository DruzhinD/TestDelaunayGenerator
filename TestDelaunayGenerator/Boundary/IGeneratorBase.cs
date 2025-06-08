namespace TestDelaunayGenerator.Boundary
{
    using CommonLib.Geometry;

    public interface IGeneratorBase
    {
        /// <summary>
        /// Множество точек
        /// </summary>
        IHPoint[] Points { get; }

        IHPoint[] Generate(BoundaryNew boundary);

        /// <summary>
        /// Индексы опорных вершин внутри <see cref="Points"/>
        /// </summary>
        int[] BaseVertexIds { get; }
    }
}
