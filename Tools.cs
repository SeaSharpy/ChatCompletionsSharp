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
    public string Name { get; internal set; }
    public string Description { get; internal set; }
    public bool Strict { get; internal set; }
    internal string Schema { get; private set; }
    internal JObject SchemaJson { get; private set; }
    public Type Type { get; private set; }
    public delegate CompletionToolCallbackResponse CompletionToolCallback(CompletionRequest r, ToolCall toolCall);

    public CompletionToolCallback? Callback;
    internal JObject Definition { get; private set; }

    internal static JArray AllAsJson(Dictionary<string, Tool> toolTypes)
    {
        return new JArray(toolTypes.Values.Select(t => t.ToJson()));
    }
    public Tool(string name, string description, Type type)
    {
        Name = name;
        Description = description;
        Strict = false;
        Callback = null;
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
                ["strict"] = false
            }
        };
    }

    public Tool(string name, string description, Type type, bool strict = false)
    {
        Name = name;
        Description = description;
        Strict = strict;
        Callback = null;
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
    }
    public Tool(string name, string description, Type type, CompletionToolCallback? callback = null)
    {
        Name = name;
        Description = description;
        Strict = false;
        Callback = callback;
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
                ["strict"] = false
            }
        };
    }
    public Tool(string name, string description, Type type, CompletionToolCallback? callback = null, bool strict = false)
    {
        Name = name;
        Description = description;
        Strict = strict;
        Callback = callback;
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
    }
    public Tool(string name, string description, Type type, bool strict = false, CompletionToolCallback? callback = null)
    {
        Name = name;
        Description = description;
        Strict = strict;
        Callback = callback;
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
}

public class ToolCall
{
    public object? Arguments;
    public string Name;
    public string Id;
    public Tool Tool;

    public ToolCall(object? arguments, string name, string id, Tool tool)
    {
        Name = name;
        Arguments = arguments;
        Id = id;
        Tool = tool;
    }

    internal JObject ToJson()
    {
        return new JObject
        {
            ["type"] = "function",
            ["id"] = Id,
            ["function"] = new JObject
            {
                ["name"] = Name,
                ["arguments"] = Arguments != null ? JObject.FromObject(Arguments).ToString() : new JObject()
            }
        };
    }
    public static ToolCall FromJson(Dictionary<string, Tool> toolTypes, JToken token)
    {
        string type = token["type"]?.ToString() ?? throw new ArgumentException("Tool call must have a type");
        if (type != "function")
            throw new ArgumentException($"Tool call must be of type 'function', but was '{type}'");

        string id = token["id"]?.ToString() ?? throw new ArgumentException("Tool call must have an id");
 
        JObject function = token["function"] as JObject ?? throw new ArgumentException("Tool call must have a function");

        string name = function["name"]?.ToString() ?? throw new ArgumentException("Tool call must have a name");
        string argsJson = function["arguments"]?.ToString() ?? throw new ArgumentException("Tool call must have arguments");
        
        Tool targetTool = toolTypes.TryGetValue(name, out var tool) ? tool : throw new ArgumentException($"Tool with name '{name}' not found");
        object? args = SchemaHelper.ParseFromSchema(targetTool.Schema, argsJson, targetTool.Type);

        return new ToolCall(args, name, id, targetTool);
    }
}