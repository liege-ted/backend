namespace TED.API.Attributes
{

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RequiredQueryParamAttribute(string paramName) : Attribute
    {
        public string ParamName { get; } = paramName;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RequiredBodyParamAttribute(string paramName) : Attribute
    {
        public string ParamName { get; } = paramName;
    }
}