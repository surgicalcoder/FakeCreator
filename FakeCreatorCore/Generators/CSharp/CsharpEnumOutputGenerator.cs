using System.Text;

namespace FakeCreatorCore.Generators.CSharp
{
    public class CsharpEnumOutputGenerator : IOutputGenerator
    {
        public string GetFileExtension(Mapping mapping)
        {
            return ".cs";
        }

        public string Generate(Mapping mapping)
        {
            if (!mapping.IsEnum)
            {
                return null;
            }
            var builder = new StringBuilder();
            builder.AppendLine($"public enum {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{mapping.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");
            foreach (var enumName in mapping.Fetch().GetEnumNames())
            {
                builder.AppendLine(enumName + ",");
            }
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
