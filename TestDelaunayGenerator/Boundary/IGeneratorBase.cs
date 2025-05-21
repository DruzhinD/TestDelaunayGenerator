namespace TestDelaunayGenerator.Boundary
{
    using CommonLib.Geometry;

    public interface IGeneratorBase
    {
        //TODO убрать метод генератора
        IHPoint[] Generate(BoundaryHill boundary);
        IHPoint[] Generate(BoundaryNew boundary);
    }
}
