using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using PowerArgs;
using RazorEngine.Configuration;
using RazorEngine.Templating;

namespace FakeCreatorCore
{

    class Program
    {
        static void Main(string[] args)
        {

            try
            {
                var parsed = Args.Parse<InputArgs>(args);

                string[] FileToLoad = parsed.Dll.Split(new char[] { ',', ';' });
                parsed.Types = parsed.RawTypes.Split(new char[] { ',', ';' }).ToList();
                Singleton.Instance.InputArgs = parsed;

                Singleton.Instance.Assemblies = new List<Assembly>();

                FileToLoad.ForEach(r =>
                {
                    
                    Singleton.Instance.Assemblies.Add(PluginLoader.CreateFromAssemblyFile(r, PluginLoaderOptions.None).LoadDefaultAssembly());

                    //Singleton.Instance.Assemblies.Add(Assembly.LoadFrom(r));
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
                Console.WriteLine($"Found Generator : {type.FullName}");
                if (Activator.CreateInstance(type) is IOutputGenerator outputGenerator)
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

            MappingList = JsonConvert.DeserializeObject<List<Mapping>>(File.ReadAllText(Singleton.Instance.InputArgs.MappingFile));

            foreach (var mapping in MappingList)
            {
                foreach (var instanceOutputGenerator in Singleton.Instance.OutputGenerators)
                {
                    string fileExtension = instanceOutputGenerator.GetFileExtension(mapping);
                    var outp = instanceOutputGenerator.Generate(mapping);

                    if (String.IsNullOrWhiteSpace(outp))
                    {
                        continue;
                    }

                    var path = Path.GetDirectoryName(Path.GetFullPath(Singleton.Instance.InputArgs.MappingFile)) + "\\" + mapping.Name + "\\";
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    File.WriteAllText(path + instanceOutputGenerator.GetType().FullName + fileExtension, outp);
                }

                foreach (var additionalTemplate in additionalTemplates)
                {
                    Console.WriteLine($"[{mapping.Name}] Processing Template {additionalTemplate.Key}");
                    var directory = Path.GetDirectoryName(Singleton.Instance.InputArgs.MappingFile);
                    if (!Directory.Exists(Path.Combine(directory, mapping.Name)))
                    {
                        Directory.CreateDirectory(Path.Combine(directory, mapping.Name));
                    }
                    File.WriteAllText(Path.Combine(directory, mapping.Name, string.Format(additionalTemplate.Key, mapping.Name)).Replace(".cshtml", ""), PerformRazor(additionalTemplate.Key, additionalTemplate.Value, mapping));
                }
            }

            File.WriteAllText(Singleton.Instance.InputArgs.MappingFile + ".run.bat", "\"" + Assembly.GetExecutingAssembly().Location.Replace(".dll",".exe") + "\" " + string.Join(" ", GetCommandargs()));
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
            config.CachingProvider = new DefaultCachingProvider(t => { });

            var razorEngineService = RazorEngineService.Create(config);

            var result = razorEngineService.RunCompile(template, file, null, mapping);
            return result;
        }

        private static void GenerateMapping()
        {
            List<Type> mainTypes = Singleton.Instance.Assemblies.SelectMany(r => r.GetTypes()).Where(r => Singleton.Instance.InputArgs.Types.Contains(r.Name)).ToList();

            List<Type> KnownTypes = new List<Type>();
            KnownTypes.AddRange(mainTypes);

            bool stillLookingUp = true;

            while (stillLookingUp)
            {
                int doINeedToContinue = 0;
                List<Type> intKnownTypes = new List<Type>();
                intKnownTypes.AddRange(KnownTypes);
                foreach (var type in intKnownTypes)
                {
                    foreach (var propertyInfo in type.GetProperties())
                    {
                        if ((!propertyInfo.PropertyType.IsEnum && !propertyInfo.PropertyType.IsNullableEnum()) && (propertyInfo.PropertyType.IsTypeASimpleType() || propertyInfo.PropertyType.IsTypeAGenericSimpleType()))
                        {
                            continue;
                        }
                        else
                        {
                            if (propertyInfo.PropertyType.IsGenericType)
                            {
                                foreach (var argument in propertyInfo.PropertyType.GenericTypeArguments)
                                {
                                    if (KnownTypes.Contains(argument))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        KnownTypes.Add(argument);
                                        doINeedToContinue++;
                                    }
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

            Dictionary<string, string> transformation = string.IsNullOrWhiteSpace(Singleton.Instance.InputArgs.Transformation) ? new Dictionary<string, string>() : Singleton.Instance.InputArgs.Transformation.Split(';').ToDictionary(r => r.Split('>')[0], r => r.Split('>')[1]);

            foreach (var knownType in KnownTypes)
            {
                Mapping mapping = new Mapping();
                mapping.Name = knownType.Name;
                mapping.FullName = knownType.FullName;
                mapping.IsMainType = mainTypes.Contains(knownType);
                mapping.IsEnum = knownType.IsEnum;
                mapping.Assembly = knownType.Assembly.FullName;
                mapping.Mappings = knownType.GetProperties().Select(delegate (PropertyInfo info)
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

                    pMap.IsDictionary = info.PropertyType.IsGenericType &&
                    (
                        info.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                        || info.PropertyType.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                    );

                    if (pMap.IsDictionary)
                    {
                        pMap.DictionaryTypes = info.PropertyType.GenericTypeArguments.Select(r => r.Name).ToList();
                    }

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
            File.WriteAllText(Singleton.Instance.InputArgs.MappingFile, JsonConvert.SerializeObject(mappings));
        }
    }


    public class MyRazorTemplate<T> : RazorEngine.Templating.TemplateBase<T>
    {
        protected void BeginContext(string virtualPath, int startPosition, int length, bool isLiteral) { }

        protected void EndContext(string virtualPath, int startPosition, int length, bool isLiteral) { }

        protected Fake Context { get; set; }

        public class Fake
        {
            public object ApplicationInstance { get; set; }
        }
    }

}
