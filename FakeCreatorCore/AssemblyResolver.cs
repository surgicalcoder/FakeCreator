using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace FakeCreatorCore;

public sealed class AssemblyResolver : IDisposable
{
    private readonly ICompilationAssemblyResolver _assemblyResolver;
    private readonly DependencyContext _dependencyContext;
    private readonly AssemblyLoadContext _loadContext;
    public Assembly InitialAssembly { get; }

    public AssemblyResolver(string assemblyPath)
    {
        //this assemblyPath has to have a deps.json-file in the same directory.
        InitialAssembly = Assembly.LoadFile(assemblyPath);
                        
        _dependencyContext = DependencyContext.Load(InitialAssembly);

        var resolver = new ICompilationAssemblyResolver[]
        {
            new AppBaseCompilationAssemblyResolver(Path.GetDirectoryName(assemblyPath)),
            new ReferenceAssemblyPathResolver(),
            new PackageCompilationAssemblyResolver()
        };

        _assemblyResolver = new CompositeCompilationAssemblyResolver(resolver);
            
        _loadContext = AssemblyLoadContext.GetLoadContext(InitialAssembly);
        _loadContext.Resolving += OnResolving;
    }

    /// <summary>
    /// returns all assemblies of the current context.
    /// </summary>
    /// <returns></returns>
    public List<Assembly> GetAssemblies()
    {
        return _loadContext.Assemblies.ToList();
    }

    /// <summary>
    /// Resolving dependencies of the assembly
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    private Assembly OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        bool NamesMatch(RuntimeLibrary runtime)
        {
            var res = string.Equals(runtime.Name, name.Name, StringComparison.OrdinalIgnoreCase);
            if(!res)
            {
                //iterate through every assemblygroup. This will recognize also assemblies in nuget-packages.
                foreach(var group in runtime.RuntimeAssemblyGroups)
                {
                    foreach(var l in group.RuntimeFiles)
                    {
                        if (Path.GetFileNameWithoutExtension(l.Path) == name.Name) //optional version-check:  && l.AssemblyVersion == name.Version.ToString())  
                            return true;
                    }
                }
            }

            return res;
        }

        RuntimeLibrary library = _dependencyContext.RuntimeLibraries.FirstOrDefault(NamesMatch);
        if (library == null)
            return null;

        var wrapper = new CompilationLibrary(
            library.Type,
            library.Name,
            library.Version,
            library.Hash,
            library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
            library.Dependencies,
            library.Serviceable
        );

        var assemblies = new List<string>();
        _assemblyResolver.TryResolveAssemblyPaths(wrapper, assemblies);
        var assembly = assemblies.FirstOrDefault(a => Path.GetFileNameWithoutExtension(a) == name.Name);

        return assembly==null
            ? null
            : _loadContext.LoadFromAssemblyPath(assembly);

    }

    public void Dispose()
    {
        _loadContext.Resolving -= this.OnResolving;
    }
}