using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FakeCreator
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

    public class ToSourceOutputGenerator : IOutputGenerator
    {
        public string GetFileExtension(Mapping mapping)
        {
            return ".cs";
        }

        private static string GetRemotePopulatorDTOMethodName(string outputTypeName)
        {
            return "Populate" + outputTypeName + "ToSource";
        }

        public string Generate(Mapping mapping)
        {

            if (mapping.IsEnum)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            Type type = mapping.Fetch();
            string outputTypeName = mapping.Name;
            string inputTypeName = $"{Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""}";
            string methodName = GetRemotePopulatorDTOMethodName(outputTypeName);

            builder.AppendLine($"public static {outputTypeName} {methodName} ({inputTypeName} remote, {outputTypeName} local = null) {{");
            builder.AppendLine($"\tif (local == null) {{local = new {outputTypeName}();}}");

            foreach (var propertyMapping in mapping.Mappings)
            {
                string localPropertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string remotePropertyName = propertyMapping.Name;

                string localPropertyType = propertyMapping.Type.IsASimpleType() ? propertyMapping.Type : Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
                string remotePropertyType = propertyMapping.Type;

                if (type.GetProperty(remotePropertyName).SetMethod == null)
                {
                    continue;
                }

                if (propertyMapping.IsEnum || type.IsNullableEnum())
                {
                    Type enumType = Helpers.GetUnderlyingType(type);

                    if (propertyMapping.Type.IsASimpleType())
                    {
                        builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
                    }
                    else if (propertyMapping.IsNullable)
                    {
                        builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = ({remotePropertyType}) Enum.Parse(typeof({remotePropertyType}), remote.{remotePropertyName}.ToString() ); }}");
                    }
                    else
                    {
                        builder.AppendLine($"\tlocal.{localPropertyName} = ({remotePropertyType}) Enum.Parse(typeof({remotePropertyType}), remote.{remotePropertyName}.ToString() );");
                    }
                }
                else if (propertyMapping.IsNullable)
                {
                    if (propertyMapping.Type.IsASimpleType())
                    {
                        builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
                    }
                    else
                    {
                        builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = { GetRemotePopulatorDTOMethodName(remotePropertyType)} (remote.{remotePropertyName}); }}");
                    }

                }
                else if (propertyMapping.IsList)
                {

                    if (propertyMapping.Type.IsASimpleType())
                    {
                        builder.AppendLine($"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> r ).ToList();  }} ");
                    }
                    else
                    {
                        var internalMapping = Singleton.Instance.MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                        if (internalMapping != null && internalMapping.IsAReference)
                        {
                            builder.AppendLine($"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {string.Format(Singleton.Instance.InputArgs.IsAReferenceTypeFormat, localPropertyType, Singleton.Instance.InputArgs.IsAReferenceTypeLookupKey)} ).ToList();  }} ");
                        }
                        else
                        {
                            builder.AppendLine($"\t if (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {GetRemotePopulatorDTOMethodName(remotePropertyType)}(r) ).ToList();  }} ");
                        }
                    }
                }
                else
                {
                    if (propertyMapping.Type.IsASimpleType())
                    {
                        if (propertyMapping.IsNullable)
                        {
                            builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
                        }
                        else
                        {
                            builder.AppendLine($"\tlocal.{localPropertyName} = remote.{remotePropertyName};");
                        }

                    }
                    else
                    {
                        var internalMapping = MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                        if (internalMapping != null && internalMapping.IsAReference)
                        {
                            builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {string.Format(Singleton.Instance.InputArgs.IsAReferenceTypeFormat, localPropertyType, Singleton.Instance.InputArgs.IsAReferenceTypeLookupKey)};  }}");
                        }
                        else
                        {
                            builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {GetRemotePopulatorDTOMethodName(localPropertyType)} (remote.{remotePropertyName}); }}");
                        }
                    }
                }

            }

            builder.AppendLine("\treturn local;");

            builder.AppendLine("}");
            return builder.ToString();
        }
    }







    public class FromSourceOutputGenerator : IOutputGenerator
    {
        public FromSourceOutputGenerator()
        {
        }

        private static string GetRemotePopulatorMethodName(string outputTypeName)
        {
            return "Populate" + outputTypeName + "FromSource";
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
                    else if (propertyMapping.IsList)
                    {
                        if (propertyMapping.Type.IsASimpleType())
                        {
                            builder.AppendLine(
                                $"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> r ).ToList();  }} ");
                        }
                        else
                        {
                            var internalMapping = Singleton.Instance.MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                            if (internalMapping != null && internalMapping.IsAReference)
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
                            var internalMapping = Singleton.Instance.MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                            if (internalMapping != null && internalMapping.IsAReference)
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
