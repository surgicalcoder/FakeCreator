using System;
using System.Text;

namespace FakeCreatorCore.Generators.CSharp
{
    public class FromSourceOutputGenerator : IOutputGenerator
    {
        public FromSourceOutputGenerator()
        {
        }

        private static string GetRemotePopulatorMethodName(string outputTypeName)
        {
            return "Populate" + outputTypeName + "FromSource";
        }

        private static string BuildDictEntry(string typeName, string sourceAccessor)
        {
            if (string.IsNullOrWhiteSpace(typeName) || typeName.IsASimpleType())
                return $"pair=> pair.{sourceAccessor}";
            return $"pair=> {GetRemotePopulatorMethodName(typeName)}(pair.{sourceAccessor})";
        }

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

            StringBuilder builder = new StringBuilder();
            Type type = mapping.Fetch();
            string inputTypeName = mapping.Name;
            string outputTypeName = $"{Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""}";
            string methodName = GetRemotePopulatorMethodName(outputTypeName);

            builder.AppendLine($"public static {outputTypeName} {methodName} ({inputTypeName} remote, {outputTypeName} local = null) {{");

            if (!mapping.IsEnum)
            {
                builder.AppendLine($"if (local == null) {{local = new {outputTypeName}();}}");
                foreach (var propertyMapping in mapping.Mappings)
                {
                    string localPropertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName)
                        ? propertyMapping.Name
                        : propertyMapping.TransformName;
                    string remotePropertyName = propertyMapping.Name;

                    string localPropertyType = propertyMapping.Type.IsASimpleType()
                        ? propertyMapping.Type
                        : Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
                    string remotePropertyType = propertyMapping.Type;

                    if (propertyMapping.IsEnum || type.IsNullableEnum())
                    {
                        Type enumType = Helpers.GetUnderlyingType(type);

                        if (propertyMapping.Type.IsASimpleType())
                        {
                            builder.AppendLine(
                                $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
                        }
                        else if (propertyMapping.IsNullable)
                        {
                            builder.AppendLine(
                                $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = ({localPropertyType}) Enum.Parse(typeof({localPropertyType}), remote.{remotePropertyName}.ToString() ); }}");
                        }
                        else
                        {
                            builder.AppendLine(
                                $"\tlocal.{localPropertyName} = ({localPropertyType}) Enum.Parse(typeof({localPropertyType}), remote.{remotePropertyName}.ToString() );");
                        }
                    }
                    else if (propertyMapping.IsNullable)
                    {
                        if (propertyMapping.Type.IsASimpleType())
                        {
                            builder.AppendLine(
                                $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
                        }
                        else
                        {
                            builder.AppendLine(
                                $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {GetRemotePopulatorMethodName(localPropertyType)} (remote.{remotePropertyName}); }}");
                        }
                    }
                    else if (propertyMapping.IsDictionary)
                    {
                        string dictLine = $"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{";
                        dictLine += $"local.{localPropertyName} = remote.{remotePropertyName}.ToDictionary(";
                        dictLine += BuildDictEntry(propertyMapping.DictionaryKeyType, "Key");
                        dictLine += ", ";
                        dictLine += BuildDictEntry(propertyMapping.DictionaryValueType, "Value");
                        dictLine += "); }}";

                        builder.AppendLine(dictLine);
                    }
                    else if (propertyMapping.IsList)
                    {
                        if (propertyMapping.Type.IsASimpleType())
                        {
                            builder.AppendLine($"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> r ).ToList();  }} ");
                        }
                        else
                        {
                            if (Singleton.Instance.MappingIndex != null && Singleton.Instance.MappingIndex.TryGetValue(propertyMapping.Type, out var internalMapping) && internalMapping.IsAReference)
                            {
                                builder.AppendLine(
                                    $"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {string.Format(Singleton.Instance.InputArgs.IsAReferenceTypeFormat, localPropertyType, Singleton.Instance.InputArgs.IsAReferenceTypeLookupKey)} ).ToList();  }} ");
                            }
                            else
                            {
                                builder.AppendLine(
                                    $"\t if (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {GetRemotePopulatorMethodName(localPropertyType)}(r) ).ToList();  }} ");
                            }
                        }
                    }
                    else
                    {
                        if (propertyMapping.Type.IsASimpleType())
                        {
                            if (propertyMapping.IsNullable)
                            {
                                builder.AppendLine(
                                    $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
                            }
                            else
                            {
                                builder.AppendLine($"\tlocal.{localPropertyName} = remote.{remotePropertyName};");
                            }
                        }
                        else
                        {
                            if (Singleton.Instance.MappingIndex != null && Singleton.Instance.MappingIndex.TryGetValue(propertyMapping.Type, out var internalMapping) && internalMapping.IsAReference)
                            {
                                builder.AppendLine(
                                    $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {string.Format(Singleton.Instance.InputArgs.IsAReferenceTypeFormat, localPropertyType, Singleton.Instance.InputArgs.IsAReferenceTypeLookupKey)};  }}");
                            }
                            else
                            {
                                builder.AppendLine(
                                    $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {GetRemotePopulatorMethodName(localPropertyType)} (remote.{remotePropertyName}); }}");
                            }
                        }
                    }
                }

                builder.AppendLine("\treturn local;");
            }


            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}