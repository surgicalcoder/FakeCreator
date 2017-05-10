using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Jil;
using Microsoft.AspNetCore.Razor.CodeGenerators;
using PowerArgs;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using Utilities.DataTypes.ExtensionMethods;

namespace FakeCreator
{
    public sealed class Singleton
    {
        private static readonly Lazy<Singleton> lazy = new Lazy<Singleton>(() => new Singleton());
        public static Singleton Instance => lazy.Value;

        private Singleton()
        {
            OutputGenerators = new List<IOutputGenerator>();
        }

        public InputArgs InputArgs { get; set; }
        public List<Mapping> MappingList { get; set; }
        public List<Assembly> Assemblies { get; set; }
        public List<IOutputGenerator> OutputGenerators { get; set; }
    }


    public interface IOutputGenerator
    {
        string GetFileExtension(Mapping mapping);
        string Generate(Mapping mapping);
    }

    class Program
    {
        static void Main(string[] args)
        {
            
            try
            {
                var parsed = Args.Parse<InputArgs>(args);
                
                string[] FileToLoad = parsed.Dll.Split(new char[] { ',', ';' });
                parsed.Types = parsed.RawTypes.Split(new char[] {',',';'}).ToList();
                Singleton.Instance.InputArgs = parsed;

                Singleton.Instance.Assemblies = new List<Assembly>();

                FileToLoad.ForEach(r =>
                {
                    Singleton.Instance.Assemblies.Add(Assembly.LoadFrom(r));
                });

                SetupOutputGenerators();

                if (parsed.GenerateMappingFile)
                {
                    GenerateMapping();
                    GenerateClasses();
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

        private static void SetupOutputGenerators()
        {
            
            var enumerable = typeof(Program).Assembly.GetTypes().Where(r => !r.IsInterface && typeof(IOutputGenerator).IsAssignableFrom(r));
            foreach (var type in enumerable)
            {
                Console.WriteLine(type.FullName);
                var outputGenerator = Activator.CreateInstance(type) as IOutputGenerator;
                if (outputGenerator != null)
                {
                    Singleton.Instance.OutputGenerators.Add(outputGenerator);
                }
            }
        }

        private static List<Mapping> MappingList;
        private static void GenerateClasses()
        {
            Dictionary<string, string> additionalTemplates = new Dictionary<string, string>();
            if (!String.IsNullOrWhiteSpace(Singleton.Instance.InputArgs.TemplateDirectory) && Directory.Exists(Singleton.Instance.InputArgs.TemplateDirectory))
            {
                foreach (var file in Directory.GetFiles(Singleton.Instance.InputArgs.TemplateDirectory))
                {

                    additionalTemplates.Add(Path.GetFileName(file), File.ReadAllText(file));
                }
            }

            MappingList = JSON.Deserialize<List<Mapping>>(File.ReadAllText(Singleton.Instance.InputArgs.MappingFile));
            
            //StringBuilder classFile = new StringBuilder();
            //StringBuilder fromRemote = new StringBuilder();
            //StringBuilder forRemote = new StringBuilder();
            //StringBuilder typescriptFile = new StringBuilder();
            //StringBuilder typescriptFromRemote = new StringBuilder();

            foreach (var mapping in MappingList)
            {
                foreach (var instanceOutputGenerator in Singleton.Instance.OutputGenerators)
                {
                    string fileExtension = instanceOutputGenerator.GetFileExtension(mapping);
                    var outp = instanceOutputGenerator.Generate(mapping);
                    var path = Path.GetDirectoryName(Singleton.Instance.InputArgs.MappingFile) + "\\" + instanceOutputGenerator.GetType().Name + "\\";
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    File.WriteAllText(path + mapping.Name + fileExtension, outp );
                }
                //if (mapping.IsEnum)
                //{
                //    classFile.AppendLine(OutputObjectForEnum(mapping));
                //}
                //else
                //{
                //    classFile.AppendLine(OutputObjectForClass(mapping));
                //}

                foreach (var additionalTemplate in additionalTemplates)
                {
                    Console.WriteLine($"[{mapping.Name}] Processing Template {additionalTemplate.Key}");
                    var directory = Path.GetDirectoryName(Singleton.Instance.InputArgs.MappingFile);
                    if (!Directory.Exists(Path.Combine(directory, mapping.Name)))
                    {
                        Directory.CreateDirectory(Path.Combine(directory, mapping.Name));
                    }
                    File.WriteAllText(Path.Combine(directory, mapping.Name, string.Format(additionalTemplate.Key, mapping.Name )).Replace(".cshtml","") , PerformRazor(additionalTemplate.Key,additionalTemplate.Value, mapping) );
                }


                //fromRemote.AppendLine(OutputFromSource(mapping));
                //forRemote.AppendLine(OutputForToSource(mapping));
                //typescriptFile.AppendLine(GenerateTypeScriptFile(mapping));
                //typescriptFromRemote.AppendLine(GenerateTypeScriptFromRemoteFile(mapping));
            }

            //File.WriteAllText(Singleton.Instance.InputArgs.MappingFile + ".classses.cs", classFile.ToString());
            //File.WriteAllText(Singleton.Instance.InputArgs.MappingFile + ".FromSource.cs", fromRemote.ToString());
            //File.WriteAllText(Singleton.Instance.InputArgs.MappingFile + ".ToSource.cs", forRemote.ToString());
            //File.WriteAllText(Singleton.Instance.InputArgs.MappingFile + ".classes.ts", typescriptFile.ToString());
            //File.WriteAllText(Singleton.Instance.InputArgs.MappingFile + ".FromSource.ts", typescriptFromRemote.ToString());
            File.WriteAllText(Singleton.Instance.InputArgs.MappingFile + ".run.bat", "\"" + Assembly.GetExecutingAssembly().Location + "\" " + string.Join(" ",  GetCommandargs() ));
        }

        private static string[] GetCommandargs()
        {
            return Environment.GetCommandLineArgs().Skip(1).Select((s, i) =>
            {
                if (s.Contains(" "))
                {
                    return "\"" + s + "\"";
                }
                return s;
            }).ToArray();
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

        //private static string GenerateTypeScriptFromRemoteFile(Mapping mapping)
        //{
        //    if (mapping.IsEnum)
        //    {
        //        return null;
        //    }
        //    StringBuilder builder = new StringBuilder();
        //    Type type = FetchType(mapping);


        //    builder.AppendLine($"function to{Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} (r:any): {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");
        //    builder.AppendLine($"let item = <{Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""}>({{");

        //    foreach (var propertyMapping in mapping.Mappings)
        //    {
        //        string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
        //        string propertyType = Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
        //        builder.AppendLine($"\t{propertyName}: r.{propertyType},");
        //    }
        //    builder.AppendLine("});");
        //    builder.AppendLine("return item;");
        //    builder.AppendLine("}");
        //    return builder.ToString();
        //}

        //private static string GenerateTypeScriptFile(Mapping mapping)
        //{
        //    if (mapping.IsEnum)
        //    {
        //        return null;
        //    }
        //    StringBuilder builder = new StringBuilder();
        //    Type type = FetchType(mapping);



        //    Dictionary<string, string> TSPropertyTypeMapping = new Dictionary<string, string>
        //    {
        //        {"Int32","number" },
        //        {"DateTime","Date" },
        //    };

        //    builder.AppendLine($"export interface {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");
            
        //    foreach (var propertyMapping in mapping.Mappings)
        //    {
        //        string localPropertyType = propertyMapping.Type.IsASimpleType() ? propertyMapping.Type : Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
        //        string propertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
        //        string propertyType = Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;

        //        if (TSPropertyTypeMapping.ContainsKey(localPropertyType))
        //        {
        //            builder.AppendLine($"\t{propertyName}: {TSPropertyTypeMapping[localPropertyType]};");
        //        }
        //        else
        //        {
        //            builder.AppendLine($"\t{propertyName}: {localPropertyType.ToLower()};");
        //        }
                
        //    }

        //    builder.AppendLine("}");
        //    return builder.ToString();

        //}

        //private static string OutputForToSource(Mapping mapping)
        //{
        //    if (mapping.IsEnum)
        //    {
        //        return null;
        //    }

        //    StringBuilder builder = new StringBuilder();
        //    Type type = FetchType(mapping);
        //    string outputTypeName = mapping.Name;
        //    string inputTypeName = $"{Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""}";
        //    string methodName = GetRemotePopulatorDTOMethodName(outputTypeName);

        //    builder.AppendLine($"public static {outputTypeName} {methodName} ({inputTypeName} remote, {outputTypeName} local = null) {{");
        //    builder.AppendLine($"\tif (local == null) {{local = new {outputTypeName}();}}");

        //    foreach (var propertyMapping in mapping.Mappings)
        //    {
        //        string localPropertyName = String.IsNullOrWhiteSpace(propertyMapping.TransformName) ? propertyMapping.Name : propertyMapping.TransformName;
        //        string remotePropertyName = propertyMapping.Name;

        //        string localPropertyType = propertyMapping.Type.IsASimpleType() ? propertyMapping.Type : Singleton.Instance.InputArgs.ClassPrefix + propertyMapping.Type + Singleton.Instance.InputArgs.ClassPostfix;
        //        string remotePropertyType = propertyMapping.Type;

        //        if (type.GetProperty(remotePropertyName).SetMethod == null)
        //        {
        //            continue;
        //        }
                
        //        if (propertyMapping.IsEnum || type.IsNullableEnum())
        //        {
        //            Type enumType = Helpers.GetUnderlyingType(type);

        //            if (propertyMapping.Type.IsASimpleType())
        //            {
        //                builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
        //            }
        //            else if (propertyMapping.IsNullable)
        //            {
        //                builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = ({remotePropertyType}) Enum.Parse(typeof({remotePropertyType}), remote.{remotePropertyName}.ToString() ); }}");
        //            }
        //            else
        //            {
        //                builder.AppendLine($"\tlocal.{localPropertyName} = ({remotePropertyType}) Enum.Parse(typeof({remotePropertyType}), remote.{remotePropertyName}.ToString() );");
        //            }
        //        }
        //        else if (propertyMapping.IsNullable)
        //        {
        //            if (propertyMapping.Type.IsASimpleType())
        //            {
        //                builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
        //            }
        //            else
        //            {
        //                builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = { GetRemotePopulatorDTOMethodName(remotePropertyType)} (remote.{remotePropertyName}); }}");
        //            }

        //        }
        //        else if (propertyMapping.IsList)
        //        {

        //            if (propertyMapping.Type.IsASimpleType())
        //            {
        //                builder.AppendLine($"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> r ).ToList();  }} ");
        //            }
        //            else
        //            {
        //                var internalMapping = MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

        //                if (internalMapping != null && internalMapping.IsAReference)
        //                {
        //                    builder.AppendLine($"\tif (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {string.Format(Singleton.Instance.InputArgs.IsAReferenceTypeFormat, localPropertyType, Singleton.Instance.InputArgs.IsAReferenceTypeLookupKey)} ).ToList();  }} ");
        //                }
        //                else
        //                {
        //                    builder.AppendLine($"\t if (remote.{remotePropertyName} != null && remote.{remotePropertyName}.Any()) {{ local.{localPropertyName} = remote.{remotePropertyName}.Select(r=> {GetRemotePopulatorDTOMethodName(remotePropertyType)}(r) ).ToList();  }} ");
        //                }
        //            }
        //        }
        //        else
        //        {
        //            if (propertyMapping.Type.IsASimpleType())
        //            {
        //                if (propertyMapping.IsNullable)
        //                {
        //                    builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = remote.{remotePropertyName}; }}");
        //                }
        //                else
        //                {
        //                    builder.AppendLine($"\tlocal.{localPropertyName} = remote.{remotePropertyName};");
        //                }
                        
        //            }
        //            else
        //            {
        //                var internalMapping = MappingList.FirstOrDefault(r => r.Name == propertyMapping.Type);

        //                if (internalMapping != null && internalMapping.IsAReference)
        //                {
        //                    builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {string.Format(Singleton.Instance.InputArgs.IsAReferenceTypeFormat, localPropertyType, Singleton.Instance.InputArgs.IsAReferenceTypeLookupKey)};  }}");
        //                }
        //                else
        //                {
        //                    builder.AppendLine($"\tif (remote.{remotePropertyName} != null) {{ local.{localPropertyName} = {GetRemotePopulatorDTOMethodName(localPropertyType)} (remote.{remotePropertyName}); }}");
        //                }
        //            }
        //        }

        //    }

        //    builder.AppendLine("\treturn local;");

        //    builder.AppendLine("}");
        //    return builder.ToString();
        //}

        //private static string OutputFromSource(Mapping mapping)
        //{
            
        //}

        private static string GetRemotePopulatorDTOMethodName(string outputTypeName)
        {
            return "Populate" + outputTypeName + "ToSource";
        }
        //private static string GetRemotePopulatorMethodName(string outputTypeName)
        //{
        //    return "Populate" + outputTypeName + "FromSource";
        //}

        private static string OutputObjectForClass(Mapping mapping)
        {
            StringBuilder builder = new StringBuilder();

            Type type = FetchType(mapping);

            if (mapping.IsAReference)
            {
                builder.AppendLine("// I am a reference");
            }
            builder.AppendLine($"public class {Singleton.Instance.InputArgs.ClassPrefix??""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix??""} {{");

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
            builder.AppendLine($"public enum {Singleton.Instance.InputArgs.ClassPrefix ?? ""}{type.Name}{Singleton.Instance.InputArgs.ClassPostfix ?? ""} {{");
            foreach (var enumName in FetchType(type).GetEnumNames())
            {
                builder.AppendLine(enumName + ",");
            }
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void GenerateMapping()
        {
            List<Type> mainTypes = Singleton.Instance.Assemblies.SelectMany(r=>r.GetTypes()).Where(r => Singleton.Instance.InputArgs.Types.Contains(r.Name)).ToList();


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

            Dictionary<string, string> transformation = string.IsNullOrWhiteSpace(Singleton.Instance.InputArgs.Transformation) ? new Dictionary<string,string>() : Singleton.Instance.InputArgs.Transformation.Split(';').ToDictionary(r => r.Split('>')[0], r => r.Split('>')[1]);

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
            Singleton.Instance.MappingList = mappings;
            File.WriteAllText(Singleton.Instance.InputArgs.MappingFile, JSON.Serialize(mappings, Options.PrettyPrintExcludeNullsIncludeInherited));
        }
    }
}
