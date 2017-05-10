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
    }
}