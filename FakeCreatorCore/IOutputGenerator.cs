namespace FakeCreatorCore
{
    public interface IOutputGenerator
    {
        string GetFileExtension(Mapping mapping);
        string Generate(Mapping mapping);
    }
}