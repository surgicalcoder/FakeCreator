using System;
using System.Linq;
using System.Text;

namespace FakeCreator.Generators.CSharp.Nullable
{
    public class CsharpNullableClassOutputGenerator : IOutputGenerator
    {

        public string GetFileExtension(Mapping mapping)
        {
            return ".cs";
        }

        public string Generate(Mapping mapping)
        {
            if (mapping.IsEnum)
            {
                return null;
            }

            var builder = new StringBuilder();

            var type = mapping.Fetch();

            if (mapping.IsAReference)
            {
                builder.AppendLine("// I am a reference");
            }
            builder.AppendLine($"public class {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");

            foreach (var propertyMapping in mapping.Mappings)
            {
                string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string propertyType;
                if (propertyMapping.Type.IsASimpleType())
                {
                    propertyType = propertyMapping.Type;
                }
                else
                {
                    var internalMapping = Singleton.Instance.MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                    if (internalMapping != null && internalMapping.IsAReference)
                    {
                        propertyType = String.Format(Singleton.Instance.InputArgs.IsAReferenceTypeKey, Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix);
                    }
                    else
                    {
                        propertyType = Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
                    }

                }
                
                
                if (propertyMapping.IsNullable)
                {
                    builder.AppendLine($"\tpublic Nullable<{propertyType}> {propertyName} {{get; set;}}");
                }
                else if (propertyMapping.IsEnum)
                {
                    builder.AppendLine($"\tpublic {propertyType} {propertyName} {{get; set;}}");
                }
                else if (propertyMapping.IsList)
                {
                    builder.AppendLine($"\tpublic List<{propertyType}> {propertyName} {{get; set;}}");
                }
                else
                {
                    var propertyInfo = type.GetProperty(propertyName);
                    if (propertyInfo == null)
                    {
                        continue;
                    }

                    if (propertyInfo.PropertyType.IsValueType)
                    {
                        builder.AppendLine($"\tpublic Nullable<{propertyType}> {propertyName} {{get; set;}}");
                    }
                    else
                    {
                        builder.AppendLine($"\tpublic {propertyType} {propertyName} {{get; set;}}");
                    }
                }
            }


            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
