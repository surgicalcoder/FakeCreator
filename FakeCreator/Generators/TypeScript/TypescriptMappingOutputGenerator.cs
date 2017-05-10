using System;
using System.Text;

namespace FakeCreator.Generators.TypeScript
{
    public class TypescriptMappingOutputGenerator : IOutputGenerator
    {
        public string GetFileExtension(Mapping mapping)
        {
            return ".ts";
        }

        public string Generate(Mapping mapping)
        {
            if (mapping.IsEnum)
            {
                return null;
            }
            StringBuilder builder = new StringBuilder();
            Type type = mapping.Fetch();


            builder.AppendLine($"function to{Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} (r:any): {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");
            builder.AppendLine($"let item = <{Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""}>({{");

            foreach (var propertyMapping in mapping.Mappings)
            {
                string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string propertyType = Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
                builder.AppendLine($"\t{propertyName}: r.{propertyType},");
            }
            builder.AppendLine("});");
            builder.AppendLine("return item;");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}