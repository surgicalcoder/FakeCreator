using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Jil;
using PowerArgs;
using Utilities.DataTypes.ExtensionMethods;
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;

namespace FakeCreator2
{
    public class InputArgs
    {
        [ArgRequired]
        [ArgPosition(0)]
        public string Dll { get; set; }
        [ArgRequired]
        [ArgPosition(1)]

        public string RawTypes { get; set; }
        public bool ExtrapolateTypes { get; set; }

        [ArgRequired]
        public bool GenerateMappingFile { get; set; }

        public List<string> Types { get; set; }

        [ArgDescription("Transformation, usage: \"Id>UniqueId;AnotherParameter>Transform;Third>Fourth\" ")]
        public string Transformation { get; set; }

        public string ClassPrefix { get; set; }

        public string ClassPostfix { get; set; }

        [ArgDescription("Usage: LookupItem<{0}>(remote.{1}) will turn into MongoRef<string>(remote.Id) for a string")]
        public string IsAReferenceTypeFormat { get; set; }

        [ArgDescription("Reference Lookup")]
        public string IsAReferenceTypeLookupKey { get; set; }
        [ArgDescription("The actual type for a reference")]
        public string IsAReferenceTypeKey { get; set; }

        [ArgRequired]
        public string MappingFile { get; set; }

        public string TemplateDirectory { get; set; }
        
    }
   
    class Program
    {
        private static InputArgs inputArgs;
        private static List<Assembly> assemblies;
        static void Main(string[] args)
        {
            
            try
            {
                var parsed = Args.Parse<InputArgs>(args);

                string[] FileToLoad = parsed.Dll.Split(';');
                parsed.Types = parsed.RawTypes.Split(',').ToList();
                inputArgs = parsed;

                assemblies = new List<Assembly>();

                FileToLoad.ForEach(r =>
                {
                    assemblies.Add(Assembly.LoadFrom(r));
                });

                if (parsed.GenerateMappingFile)
                {
                    GenerateMapping();
                }
                else
                {
                    GenerateClasses();
                }
            }
            catch (ArgException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ArgUsage.GenerateUsageFromTemplate<InputArgs>());
            }
        }
        private static List<Mapping> MappingList;
        private static void GenerateClasses()
        {
            Dictionary<string, string> additionalTemplates = new Dictionary<string, string>();
            if (!String.IsNullOrWhiteSpace(inputArgs.TemplateDirectory) && Directory.Exists(inputArgs.TemplateDirectory))
            {
                foreach (var file in Directory.GetFiles(inputArgs.TemplateDirectory))
                {

                    additionalTemplates.Add(Path.GetFileName(file), File.ReadAllText(file));
                }
            }

            MappingList = JSON.Deserialize<List<Mapping>>(File.ReadAllText(inputArgs.MappingFile));
            
            StringBuilder classFile = new StringBuilder();
            StringBuilder fromRemote = new StringBuilder();
            StringBuilder forRemote = new StringBuilder();
            StringBuilder typescriptFile = new StringBuilder();
            StringBuilder typescriptFromRemote = new StringBuilder();

            foreach (var mapping in MappingList)
            {
                if (mapping.IsEnum)
                {
                    classFile.AppendLine(OutputObjectForEnum(mapping));
                }
                else
                {
                    classFile.AppendLine(OutputObjectForClass(mapping));
                }

                foreach (var additionalTemplate in additionalTemplates)
                {
                    var directory = Path.GetDirectoryName(inputArgs.MappingFile);

                    File.WriteAllText(Path.Combine(directory,string.Format(additionalTemplate.Key, mapping.Name )).Replace(".cshtml","") , PerformRazor(additionalTemplate.Key,additionalTemplate.Value, mapping) );
                }
                        

                fromRemote.AppendLine(OutputFromSource(mapping));
                forRemote.AppendLine(OutputForToSource(mapping));
                typescriptFile.AppendLine(GenerateTypeScriptFile(mapping));
                typescriptFromRemote.AppendLine(GenerateTypeScriptFromRemoteFile(mapping));
            }

            File.WriteAllText(inputArgs.MappingFile + ".classses.cs", classFile.ToString());
            File.WriteAllText(inputArgs.MappingFile + ".FromSource.cs", fromRemote.ToString());
            File.WriteAllText(inputArgs.MappingFile + ".ToSource.cs", forRemote.ToString());
            File.WriteAllText(inputArgs.MappingFile + ".classes.ts", typescriptFile.ToString());
            File.WriteAllText(inputArgs.MappingFile + ".FromSource.ts", typescriptFromRemote.ToString());
        }

        private static string PerformRazor(string file, string template, Mapping mapping)
        {
            TemplateServiceConfiguration config = new TemplateServiceConfiguration();
            config.DisableTempFileLocking = true;
            config.CachingProvider = new DefaultCachingProvider(t=> {});

            var razorEngineService = RazorEngineService.Create(config);
            
            var result = razorEngineService.RunCompile(template, file, null,mapping);
            return result;
        }

        private static string GenerateTypeScriptFromRemoteFile(Mapping mapping)
        {
            if (mapping.IsEnum)
            {
                return null;
            }
            StringBuilder builder = new StringBuilder();
            Type type = FetchType(mapping);


            builder.AppendLine($"function to{inputArgs.ClassPrefix ?? ""}{type.Name}{inputArgs.ClassPostfix ?? ""} (r:any): {inputArgs.ClassPrefix ?? ""}{type.Name}{inputArgs.ClassPostfix ?? ""} {{");
            builder.AppendLine($"let item = <{inputArgs.ClassPrefix ?? ""}{type.Name}{inputArgs.ClassPostfix ?? ""}>({{");

            foreach (var propertyMapping in mapping.Mappings)
            {
                string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string propertyType = inputArgs.ClassPrefix + propertyMapping.Type + inputArgs.ClassPostfix;
                builder.AppendLine($"\t{propertyName}: r.{propertyType},");
            }
            builder.AppendLine("});");
            builder.AppendLine("return item;");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string GenerateTypeScriptFile(Mapping mapping)
        {
            if (mapping.IsEnum)
            {
                return null;
            }
            StringBuilder builder = new StringBuilder();
            Type type = FetchType(mapping);



            Dictionary<string, string> TSPropertyTypeMapping = new Dictionary<string, string>
            {
                {"Int32","number" },
                {"DateTime","Date" },
            };

            builder.AppendLine($"export interface {inputArgs.ClassPrefix ?? ""}{type.Name}{inputArgs.ClassPostfix ?? ""} {{");
            
            foreach (var propertyMapping in mapping.Mappings)
            {
                string localPropertyType = propertyMapping.Type.IsASimpleType() ? propertyMapping.Type : inputArgs.ClassPrefix + propertyMapping.Type + inputArgs.ClassPostfix;
                string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string propertyType = inputArgs.ClassPrefix + propertyMapping.Type + inputArgs.ClassPostfix;

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

        private static string OutputForToSource(Mapping mapping)
        {
            if (mapping.IsEnum)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            Type type = FetchType(mapping);
            string outputTypeName = mapping.Name;
            string inputTypeName = $"{inputArgs.ClassPrefix ?? ""}{type.Name}{inputArgs.ClassPostfix ?? ""}";
            string methodName = GetRemotePopulatorDTOMethodName(outputTypeName);

            builder.AppendLine($"public static {outputTypeName} {methodName} ({inputTypeName} remote, {outputTypeName} local = null) {{");
            builder.AppendLine($"\tif (local == null) {{local = new {outputTypeName}();}}");

            foreach (var propertyMapping in mapping.Mappings)
            {
                string localPropertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
                string remotePropertyName = propertyMapping.Name;

                string localPropertyType = propertyMapping.Type.IsASimpleType() ? propertyMapping.Type : inputArgs.ClassPrefix + propertyMapping.Type + inputArgs.ClassPostfix;
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
                        builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {GetRemotePopulatorDTOMethodName(localPropertyType)} (remote.{remotePropertyName}); }}");
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
                        var internalMapping = MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                        if (internalMapping != null && internalMapping.IsAReference)
                        {
                            builder.AppendLine($"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {string.Format(inputArgs.IsAReferenceTypeFormat, localPropertyType, inputArgs.IsAReferenceTypeLookupKey)} ).ToList();  }} ");
                        }
                        else
                        {
                            builder.AppendLine($"\t if (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {GetRemotePopulatorDTOMethodName(localPropertyType)}(r) ).ToList();  }} ");
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
                            builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {string.Format(inputArgs.IsAReferenceTypeFormat, localPropertyType, inputArgs.IsAReferenceTypeLookupKey)};  }}");
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

        private static string OutputFromSource(Mapping mapping)
        {
            if (mapping.IsEnum)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            Type type = FetchType(mapping);
            string inputTypeName = mapping.Name;
            string outputTypeName = $"{inputArgs.ClassPrefix ?? ""}{type.Name}{inputArgs.ClassPostfix ?? ""}";
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
                        : inputArgs.ClassPrefix + propertyMapping.Type + inputArgs.ClassPostfix;
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
                            var internalMapping = MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                            if (internalMapping != null && internalMapping.IsAReference)
                            {
                                builder.AppendLine(
                                    $"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {string.Format(inputArgs.IsAReferenceTypeFormat, localPropertyType, inputArgs.IsAReferenceTypeLookupKey)} ).ToList();  }} ");
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
                            var internalMapping = MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                            if (internalMapping != null && internalMapping.IsAReference)
                            {
                                builder.AppendLine(
                                    $"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {string.Format(inputArgs.IsAReferenceTypeFormat, localPropertyType, inputArgs.IsAReferenceTypeLookupKey)};  }}");
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

        private static string GetRemotePopulatorDTOMethodName(string outputTypeName)
        {
            return "Populate" + outputTypeName + "ToSource";
        }private static string GetRemotePopulatorMethodName(string outputTypeName)
        {
            return "Populate" + outputTypeName + "FromSource";
        }

        private static string OutputObjectForClass(Mapping mapping)
        {
            StringBuilder builder = new StringBuilder();

            Type type = FetchType(mapping);

            if (mapping.IsAReference)
            {
                builder.AppendLine("// I am a reference");
            }
            builder.AppendLine($"public class {inputArgs.ClassPrefix??""}{type.Name}{inputArgs.ClassPostfix??""} {{");

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
                    var internalMapping = MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

                    if (internalMapping != null && internalMapping.IsAReference)
                    {
                        propertyType = String.Format(inputArgs.IsAReferenceTypeKey, inputArgs.ClassPrefix + propertyMapping.Type + inputArgs.ClassPostfix);
                    }
                    else
                    {
                        propertyType = inputArgs.ClassPrefix + propertyMapping.Type + inputArgs.ClassPostfix;
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
                    builder.AppendLine($"\tpublic {propertyType} {propertyName} {{get; set;}}");
                }
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static Type FetchType(Mapping mapping)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(r => r.FullName == mapping.Assembly);
            var type = assembly?.GetTypes().FirstOrDefault(r=>r.Name == mapping.Name);

            return type;
        }

        private static string OutputObjectForEnum(Mapping type)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"public enum {inputArgs.ClassPrefix ?? ""}{type.Name}{inputArgs.ClassPostfix ?? ""} {{");
            foreach (var enumName in FetchType(type).GetEnumNames())
            {
                builder.AppendLine(enumName + ",");
            }
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void GenerateMapping()
        {
            List<Type> mainTypes = assemblies.SelectMany(r=>r.GetTypes()).Where(r => inputArgs.Types.Contains(r.Name)).ToList();


            List<Type> KnownTypes = new List<Type>() {mainTypes};

            bool stillLookingUp = true;

            while (stillLookingUp)
            {
                int doINeedToContinue = 0;
                List<Type> intKnownTypes = new List<Type> {KnownTypes};
                foreach (var type in intKnownTypes)
                {
                    foreach (var propertyInfo in type.GetProperties())
                    {
                        if ( (!propertyInfo.PropertyType.IsEnum && !propertyInfo.PropertyType.IsNullableEnum()) && ( propertyInfo.PropertyType.IsTypeASimpleType() || propertyInfo.PropertyType.IsTypeAGenericSimpleType()))
                        {
                            continue;
                        }
                        else
                        {
                            if (propertyInfo.PropertyType.IsGenericType)
                            {
                                var genParam = propertyInfo.PropertyType.GenericTypeArguments.FirstOrDefault();
                                if (KnownTypes.Contains(genParam))
                                {
                                    continue;
                                }
                                else
                                {
                                    KnownTypes.Add(genParam);
                                    doINeedToContinue++;
                                }
                            }
                            else
                            {
                                if (KnownTypes.Contains(propertyInfo.PropertyType))
                                {
                                    continue;
                                }
                                else
                                {
                                    KnownTypes.Add(propertyInfo.PropertyType);
                                    doINeedToContinue++;
                                }
                            }
                        }
                    }
                }

                stillLookingUp = doINeedToContinue > 0;
            }

            KnownTypes.RemoveAll(r => r.Assembly == typeof(DateTime).Assembly);

            List<Mapping> mappings = new List<Mapping>();

            Dictionary<string, string> transformation = string.IsNullOrWhiteSpace(inputArgs.Transformation) ? new Dictionary<string,string>() : inputArgs.Transformation.Split(';').ToDictionary(r => r.Split('>')[0], r => r.Split('>')[1]);

            foreach (var knownType in KnownTypes)
            {
                Mapping mapping = new Mapping();
                mapping.Name = knownType.Name;
                mapping.FullName = knownType.FullName;
                mapping.IsMainType = mainTypes.Contains(knownType);
                mapping.IsEnum = knownType.IsEnum;
                mapping.Assembly = knownType.Assembly.FullName;
                mapping.Mappings = knownType.GetProperties().Select(delegate(PropertyInfo info)
                {
                    PropertyMapping pMap = new PropertyMapping();

                    pMap.Name = info.Name;
                    if (transformation.ContainsKey(info.Name))
                    {
                        pMap.TransformName = transformation[info.Name];
                    }
                    pMap.IsGeneric = info.PropertyType.IsGenericType;
                    pMap.IsEnum = (info.PropertyType.IsEnum || info.PropertyType.IsNullableEnum());
                    pMap.IsNullable = info.PropertyType.IsGenericType && info.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
                    pMap.IsList = info.PropertyType.IsGenericType && 
                         (
                            
                            info.PropertyType.GetGenericTypeDefinition() == typeof(List<>) 
                            || info.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) 
                            || info.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) 
                            || info.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>) 

                        );

                    if (pMap.IsGeneric)
                    {
                        pMap.Type = info.PropertyType.GetGenericArguments().FirstOrDefault().Name;
                    }
                    else
                    {
                        pMap.Type = info.PropertyType.Name;
                    }

                    return pMap;
                }).ToList();

                mappings.Add(mapping);
            }

            File.WriteAllText(inputArgs.MappingFile, JSON.Serialize(mappings, Options.PrettyPrintExcludeNullsIncludeInherited));
        }
    }

    public class Mapping
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Assembly { get; set; }
        public bool IsMainType { get; set; }
        public bool IsEnum { get; set; }
        public bool IsAReference { get; set; }
        public List<PropertyMapping> Mappings { get; set; }
    }

    public class PropertyMapping
    {
        public string Name { get; set; }
        public string TransformName { get; set; }
        public string Type { get; set; }

        public bool IsGeneric { get; set; }
        public bool IsEnum { get; set; }
        public bool IsNullable { get; set; }
        public bool IsList { get; set; }
    }

    public static class Helpers
    {
        public static bool IsTypeAGenericSimpleType(this Type typeToCheck)
        {
            return typeToCheck.IsGenericType && typeToCheck.GetGenericArguments().All(r=>r.IsTypeASimpleType());
        }

        public static bool IsASimpleType(this string value)
        {
            List<string> baseTypes = new List<string> {"Boolean",
"Byte",
"Char",
"DateTime",
"DateTimeOffset",
"Decimal",
"Double",
"Int16",
"Int32",
"Int64",
"SByte",
"Single",
"String",
"UInt16",
"UInt32",
"UInt64"};

            return baseTypes.Contains(value);
        }


        public static bool IsNullableEnum(this Type typeToCheck)
        {
            return typeToCheck.IsGenericType && typeToCheck.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static bool IsTypeASimpleType(this Type typeToCheck)
        {
            var typeCode = Type.GetTypeCode(GetUnderlyingType(typeToCheck));

            switch (typeCode)
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.String:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        public static Type GetUnderlyingType(Type typeToCheck)
        {
            if (typeToCheck.IsGenericType &&
                typeToCheck.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return Nullable.GetUnderlyingType(typeToCheck);
            }
            else
            {
                return typeToCheck;
            }
        }
    }
}
