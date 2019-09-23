using System.Collections.Generic;
using PowerArgs;

namespace FakeCreator
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

        [ArgDescription("Types that are references")]
        public string TypesThatAreRefereces { get; set; }

        [ArgRequired]
        public string MappingFile { get; set; }

        public string TemplateDirectory { get; set; }
        
    }
}