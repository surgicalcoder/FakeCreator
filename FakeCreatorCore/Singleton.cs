using System;
using System.Collections.Generic;
using System.Reflection;

namespace FakeCreatorCore
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
}