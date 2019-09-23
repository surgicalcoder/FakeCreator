using System.Collections.Generic;

namespace FakeCreator
{
    public class PropertyMapping
    {
        public string Name { get; set; }
        public string TransformName { get; set; }
        public string Type { get; set; }

        public bool IsGeneric { get; set; }
        public bool IsEnum { get; set; }
        public bool IsNullable { get; set; }
        public bool IsList { get; set; }
        public bool IsSquashedType { get; set; }
        public string SquashedValue { get; set; }

        public bool IsDictionary { get; set; }

        public List<string> DictionaryTypes { get; set; }
    }
}