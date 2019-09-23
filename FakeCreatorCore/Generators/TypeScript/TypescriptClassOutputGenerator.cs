using System;
using System.Collections.Generic;
using System.Text;

namespace FakeCreatorCore.Generators.TypeScript
{
    public class TypescriptClassOutputGenerator : IOutputGenerator
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



            Dictionary<string, string> TSPropertyTypeMapping = new Dictionary<string, string>
            {
                {"Int32","number" },
                {"DateTime","Date" },
            };

            builder.AppendLine($"export interface {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");

            foreach (var propertyMapping in mapping.Mappings)
            {
                string localPropertyType = propertyMapping.Type.IsASimpleType() ? propertyMapping.Type : Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
                string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string propertyType = Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;

                if (TSPropertyTypeMapping.ContainsKey(localPropertyType))
                {
                    builder.AppendLine($"\t{propertyName}: {TSPropertyTypeMapping[localPropertyType]};");
                }
                else
                {
                    builder.AppendLine($"\t{propertyName}: {localPropertyType.ToLower()};");
                }

            }

            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}