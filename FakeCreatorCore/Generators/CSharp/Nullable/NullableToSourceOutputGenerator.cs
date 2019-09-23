using System;
using System.Linq;
using System.Text;

namespace FakeCreatorCore.Generators.CSharp.Nullable
{
    public class NullableToSourceOutputGenerator : IOutputGenerator
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

                if (type.GetProperty(remotePropertyName) == null || type.GetProperty(remotePropertyName).SetMethod == null)
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
                else if (propertyMapping.IsDictionary)
                {
                    var dictLine = $"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{" ;

                    dictLine = dictLine + $"local.{localPropertyName} = remote.{remotePropertyName}.ToDictionary(";

                    foreach (var dictionaryType in propertyMapping.DictionaryTypes)
                    {
                        int index = propertyMapping.DictionaryTypes.IndexOf(dictionaryType);
                        
                        if (dictionaryType.IsASimpleType())
                        {

                            dictLine = dictLine + $"pair=>";
                            if (index == 0)
                            {
                                dictLine = dictLine + $"pair.Key";
                            }
                            else
                            {
                                dictLine = dictLine + $"pair.Value";
                            }
                        }
                        else
                        {
                            dictLine = dictLine + $"pair=>";
                            if (index == 0)
                            {
                                dictLine = dictLine + $"{GetRemotePopulatorDTOMethodName(dictionaryType)}(pair.Key)";
                            }
                            else
                            {
                                dictLine = dictLine + $"{GetRemotePopulatorDTOMethodName(dictionaryType)}(pair.Value)";
                            }
                        }
                        dictLine = dictLine + $",";
                    }
                    if (dictLine.EndsWith(","))
                    {
                        dictLine = dictLine.Substring(0, dictLine.Length - 1);
                    }

                    dictLine = dictLine + " }";

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
                        builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
                    }
                    else
                    {
                        var internalMapping = Singleton.Instance.MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

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
}
