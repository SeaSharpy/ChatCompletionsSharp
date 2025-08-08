using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;

namespace ChatCompletionsSharp;

file static class SchemaHelper
{
    public static string GenerateSchema(Type type)
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings
        {
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
        };

        var schema = JsonSchemaGenerator.FromType(type, settings);
        return schema.ToJson();
    }

    public static object? ParseFromSchema(string schemaJson, string json, Type type)
    {
        var schema = JsonSchema.FromJsonAsync(schemaJson).GetAwaiter().GetResult();

        var errors = schema.Validate(json);
        if (errors.Count > 0)
        {
            return null;
        }

        return JsonConvert.DeserializeObject(json, type);
    }
}
public class Tool {
    public string Name { get; }
    public string Description { get; }
    public bool Strict { get; }
    internal string Schema { get; private set; }
    internal JObject SchemaJson { get; private set; }
    public Type Type { get; private set; }

    internal JObject Definition { get; private set; }

    private static Dictionary<string, Tool> ToolTypes = new();

    internal static JArray AllAsJson()
    {
        // return a , separated string of all tool definitions
        return new JArray(ToolTypes.Values.Select(t => t.ToJson()));
    }

    public Tool(string name, string description, Type type, bool strict = false)
    {
        if (ToolTypes.ContainsKey(name))
            throw new ArgumentException($"Tool with name '{name}' already exists.");
        Name = name;
        Description = description;
        Strict = strict;
        Type = type;
        Schema = SchemaHelper.GenerateSchema(type);
        SchemaJson = JObject.Parse(Schema);
        Definition = new JObject
        {
            ["type"] = "function",
            ["function"] = new JObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = JObject.Parse(Schema),
                ["strict"] = strict
            }
        };
        ToolTypes.Add(name, this);
    }

    internal JObject ToJson()
    {
        return new JObject
        {
            ["type"] = "function",
            ["function"] = new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["parameters"] = SchemaJson,
                ["strict"] = Strict
            }
        };
    }

    internal static Tool Get(string name)
    {
        return ToolTypes.TryGetValue(name, out var type) ? type : throw new KeyNotFoundException($"Tool with name '{name}' not found.");
    }
}

public class ToolCall
{
    public object Arguments;
    public string Name;
    public string Id;

    public ToolCall(object arguments, string name, string id)
    {
        Name = name;
        Arguments = arguments;
        Id = id;
    }

    internal static List<ToolCall> temporary = new();

    internal JObject ToJson()
    {
        return new JObject
        {
            ["type"] = "function",
            ["id"] = Id,
            ["function"] = new JObject
            {
                ["name"] = Name,
                ["arguments"] = JObject.FromObject(Arguments).ToString()
            }
        };
    }
    public static ToolCall FromJson(JToken token)
    {
        var type = token["type"]?.ToString();
        if (type != "function")
            throw new ArgumentException($"Tool call must be of type 'function', but was '{type}'");

        var id = token["id"]?.ToString();
        var function = token["function"] as JObject;
        if (function == null || id == null)
            throw new ArgumentException("Tool call must have a function and an id");

        var name = function["name"]?.ToString();
        var argsJson = function["arguments"]?.ToString();
        if (name == null || argsJson == null)
            throw new ArgumentException("Tool call must have a name and arguments");

        var targetTool = Tool.Get(name); // assuming Tool.Get returns Type
        var args = SchemaHelper.ParseFromSchema(targetTool.Schema, argsJson, targetTool.Type);
        if (args == null)
            throw new ArgumentException("Tool call arguments could not be parsed");

        return new ToolCall(args, name, id);
    }
}