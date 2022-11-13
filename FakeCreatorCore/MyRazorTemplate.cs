namespace FakeCreatorCore;

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