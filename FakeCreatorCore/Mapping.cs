using System;
using System.Collections.Generic;
using System.Linq;

namespace FakeCreatorCore
{
    public class Mapping
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Assembly { get; set; }
        public bool IsMainType { get; set; }
        public bool IsEnum { get; set; }
        public bool IsAReference { get; set; }
        public List<PropertyMapping> Mappings { get; set; }

        public override string ToString()
        {
            return $"{nameof(Name)}: {Name}";
        }

        public Type Fetch()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(r => r.FullName == this.Assembly);
            var type = assembly?.GetTypes().FirstOrDefault(r => r.Name == this.Name);

            return type;
        }
    }
}