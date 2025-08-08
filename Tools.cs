using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;

file static class SchemaHelper
{
    public static string GenerateSchema(Type type)
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings
        {
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull
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
                ["arguments"] = JObject.FromObject(Arguments)
            }
        };
    }


    internal static ToolCall[] Parse(string toolCallsJson)
    {
        var toolCalls = JsonConvert.DeserializeObject<JArray>(toolCallsJson);
        if (toolCalls == null)
            return Array.Empty<ToolCall>();

        temporary.Clear();

        foreach (var token in toolCalls)
        {
            var type = token["type"]?.ToString();
            if (type != "function")
                continue;

            var id = token["id"]?.ToString();
            var function = token["function"] as JObject;
            if (function == null || id == null)
                continue;

            var name = function["name"]?.ToString();
            var argsJson = function["arguments"]?.ToString();
            if (name == null || argsJson == null)
                continue;

            var targetTool = Tool.Get(name); // assuming Tool.Get returns Type
            var args = SchemaHelper.ParseFromSchema(targetTool.Schema, argsJson, targetTool.Type);
            if (args == null)
                continue;

            temporary.Add(new ToolCall(args, name, id));
        }

        ToolCall[] result = temporary.ToArray();
        temporary.Clear();
        return result;
    }
}