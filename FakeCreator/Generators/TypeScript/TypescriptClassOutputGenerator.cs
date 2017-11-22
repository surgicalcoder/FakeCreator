using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FakeCreator.Generators.TypeScript
{
    public class TypescriptClassOutputGenerator : IOutputGenerator
    {
        public string GetFileExtension(Mapping mapping)
        {
            return ".ts";
        }

        Dictionary<string, string> TSPropertyTypeMapping = new Dictionary<string, string>
        {
            {"Int32","number" },
            {"DateTime","Date" },
        };

        public string Generate(Mapping mapping)
        {
            if (mapping.IsEnum)
            {
                return null;
            }
            StringBuilder builder = new StringBuilder();
            Type type = mapping.Fetch();





            builder.AppendLine($"export interface {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");

            foreach (var propertyMapping in mapping.Mappings)
            {
                string localPropertyType = propertyMapping.Type.IsASimpleType() ? propertyMapping.Type : Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
                string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string propertyType = Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;

                if (propertyMapping.IsDictionary)
                {

                    builder.AppendLine($"\t[Key: {GetTSSafeType(propertyMapping.DictionaryTypes.FirstOrDefault())}]: {propertyMapping.DictionaryTypes.Skip(1).FirstOrDefault()};");

                    if (TSPropertyTypeMapping.ContainsKey(localPropertyType))
                    {
                        
                    }
                    else
                    {
                        builder.AppendLine($"\t{propertyName}: {localPropertyType.ToLower()};");
                    }
                    // [Key: string]: T;
                }
                else
                {
                    if (TSPropertyTypeMapping.ContainsKey(localPropertyType))
                    {
                        builder.AppendLine($"\t{propertyName}: {TSPropertyTypeMapping[localPropertyType]};");
                    }
                    else
                    {
                        builder.AppendLine($"\t{propertyName}: {localPropertyType.ToLower()};");
                    }
                }
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        public string GetTSSafeType(string name)
        {
            if (TSPropertyTypeMapping.ContainsKey(name))
            {
                return TSPropertyTypeMapping[name];
            }
            return name;
        }
    }
}