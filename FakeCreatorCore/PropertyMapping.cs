using System.Collections.Generic;
using Newtonsoft.Json;

namespace FakeCreatorCore
{
    public class PropertyMapping
    {
        public string Name { get; set; }
        public string HumanizedName { get; set; }
        public string TransformName { get; set; }
        public string Type { get; set; }

        public string GenericTypeName { get; set; }

        public string GenericTypeFullName { get; set; }

        public bool IsGeneric { get; set; }
        public bool IsEnum { get; set; }
        public bool IsNullable { get; set; }
        public bool IsList { get; set; }

        public List<string> Attributes { get; set; }

        public List<string> EnumValues { get; set; }

        public bool IsDictionary { get; set; }

        public List<string> DictionaryTypes { get; set; }

        public string DictionaryKeyType { get; set; }

        public string DictionaryValueType { get; set; }

        [JsonIgnore]
        public string ResolvedDictionaryKeyType => DictionaryKeyType ?? (DictionaryTypes != null && DictionaryTypes.Count > 0 ? DictionaryTypes[0] : null);

        [JsonIgnore]
        public string ResolvedDictionaryValueType => DictionaryValueType ?? (DictionaryTypes != null && DictionaryTypes.Count > 1 ? DictionaryTypes[1] : null);
    }
}