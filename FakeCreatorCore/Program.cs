using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Humanizer;
using McMaster.NETCore.Plugins;
using Newtonsoft.Json;
using PowerArgs;
using RazorLight;

namespace FakeCreatorCore
{
    class Program
    {
        static async Task Main(string[] args)
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
                    var resolver = new AssemblyResolver(r);
                    Singleton.Instance.Assemblies.Add(resolver.InitialAssembly);
                });

                SetupOutputGenerators();

                if (parsed.GenerateMappingFile)
                {
                    await GenerateMapping();
                    await GenerateClasses();
                }
                else
                {
                    await GenerateClasses();
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
        private static async Task GenerateClasses()
        {
            var additionalTemplates = new Dictionary<string, string>();
            if (!String.IsNullOrWhiteSpace(Singleton.Instance.InputArgs.TemplateDirectory) && Directory.Exists(Singleton.Instance.InputArgs.TemplateDirectory))
            {
                foreach (var file in Directory.GetFiles(Singleton.Instance.InputArgs.TemplateDirectory))
                {
                    additionalTemplates.Add(Path.GetFileName(file), await File.ReadAllTextAsync(file));
                }
            }

            MappingList = JsonConvert.DeserializeObject<List<Mapping>>(await File.ReadAllTextAsync(Singleton.Instance.InputArgs.MappingFile));

            foreach (var mapping in MappingList)
            {
                foreach (var instanceOutputGenerator in Singleton.Instance.OutputGenerators)
                {
                    try
                    {
                        string fileExtension = instanceOutputGenerator.GetFileExtension(mapping);
                        var outp = instanceOutputGenerator.Generate(mapping);

                        if (string.IsNullOrWhiteSpace(outp))
                        {
                            continue;
                        }

                        var path = $"{Path.GetDirectoryName(Path.GetFullPath(Singleton.Instance.InputArgs.MappingFile))}\\{mapping.FullName}\\";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        await File.WriteAllTextAsync($"{path}{instanceOutputGenerator.GetType().FullName}{fileExtension}", outp);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                foreach (var additionalTemplate in additionalTemplates)
                {
                    Console.WriteLine($"[{mapping.Name}] Processing Template {additionalTemplate.Key}");
                    var directory = Path.GetDirectoryName(Singleton.Instance.InputArgs.MappingFile);
                    if (!Directory.Exists(Path.Combine(directory, mapping.FullName)))
                    {
                        Directory.CreateDirectory(Path.Combine(directory, mapping.FullName));
                    }

                    var contents = await PerformRazor(additionalTemplate.Key, additionalTemplate.Value, mapping);
                    await File.WriteAllTextAsync(Path.Combine(directory, mapping.FullName, string.Format(additionalTemplate.Key, mapping.Name)).Replace(".cshtml", ""), contents);
                }
            }

            await File.WriteAllTextAsync($"{Singleton.Instance.InputArgs.MappingFile}.run.bat", $"\"{Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe")}\" {string.Join(" ", GetCommandargs())}");
        }

        private static string[] GetCommandargs()
        {
            return Environment.GetCommandLineArgs().Skip(1).Select((s, i) =>
            {
                if (s.Contains(' '))
                {
                    return $"\"{s}\"";
                }
                return s;
            }).ToArray();
        }

        private static async Task<string> PerformRazor(string fileName, string templateContents, Mapping mapping)
        {
            var engine = new RazorLightEngineBuilder()
                .UseFileSystemProject(Singleton.Instance.InputArgs.TemplateDirectory)
                .SetOperatingAssembly(typeof(Program).Assembly)
                .UseMemoryCachingProvider()
                .Build();

            string result = await engine.CompileRenderStringAsync(fileName, templateContents, mapping);

            return result;
        }

        private static async Task GenerateMapping()
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

                stillLookingUp = doINeedToContinue > 0;
            }

            KnownTypes.RemoveAll(r => r.Assembly == typeof(DateTime).Assembly);

            List<Mapping> mappings = new List<Mapping>();

            Dictionary<string, string> transformation = string.IsNullOrWhiteSpace(Singleton.Instance.InputArgs.Transformation) ? new Dictionary<string, string>() : Singleton.Instance.InputArgs.Transformation.Split(';').ToDictionary(r => r.Split('>')[0], r => r.Split('>')[1]);

            foreach (var knownType in KnownTypes)
            {
                Mapping mapping = new Mapping();
                mapping.Name = knownType.Name;
                mapping.HumanizedName = mapping.Name.Humanize();
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

                    pMap.HumanizedName = info.Name.Humanize();
                    pMap.IsGeneric = info.PropertyType.IsGenericType;
                    pMap.IsEnum = (info.PropertyType.IsEnum || info.PropertyType.IsNullableEnum());
                    pMap.IsNullable = info.PropertyType.IsGenericType && info.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
                    
                    pMap.Attributes = info.CustomAttributes.Select(f => f.AttributeType.Name).Distinct().ToList();
                    
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
            await File.WriteAllTextAsync(Singleton.Instance.InputArgs.MappingFile, JsonConvert.SerializeObject(mappings));
        }
    }
}
