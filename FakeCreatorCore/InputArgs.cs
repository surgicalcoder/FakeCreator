using System.Collections.Generic;
using PowerArgs;

namespace FakeCreatorCore
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
        [ArgDescription("Generates a new mapping file, then runs mapping. If false, then uses existing mapping file.")]
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

        [ArgDefaultValue(false)]
        public bool SkipBuiltinGenerators { get; set; }

        [ArgDefaultValue(false)]
        [ArgDescription("Disables RazorLight template compilation caching (dev-time stale-template fix).")]
        public bool NoCache { get; set; }

        public List<string> IgnoreTypesFromNamespace { get; set; } = new();

    }
}