using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;

namespace ChatCompletionsSharp;

/// <summary>
/// Utility helpers for generating and consuming JSON Schema representations for tool argument types.
/// </summary>
file static class SchemaHelper
{
    /// <summary>
    /// Generates a JSON schema for the supplied CLR type using the configured schema generator settings.
    /// </summary>
    /// <param name="type">CLR type to generate a schema for.</param>
    /// <returns>JSON schema as a string.</returns>
    public static string GenerateSchema(Type type)
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings
        {
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
        };

        var schema = JsonSchemaGenerator.FromType(type, settings);
        return schema.ToJson();
    }

    /// <summary>
    /// Validates JSON against the supplied schema and deserializes it into the given CLR type.
    /// </summary>
    /// <param name="schemaJson">JSON schema to validate against.</param>
    /// <param name="json">JSON text representing the arguments.</param>
    /// <param name="type">CLR type to deserialize into.</param>
    /// <returns>The deserialized object when validation succeeds; otherwise null.</returns>
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

/// <summary>
/// Represents a tool definition that can be invoked by the assistant, including schema, metadata, and optional callback.
/// </summary>
public class Tool {
    /// <summary>
    /// Name exposed in the tool catalog and referenced by the assistant.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// Description used to help the model select this tool appropriately.
    /// </summary>
    public string Description { get; internal set; }

    /// <summary>
    /// Indicates whether strict argument adherence is requested.
    /// </summary>
    public bool Strict { get; internal set; }

    /// <summary>
    /// Raw JSON schema string describing the tool arguments.
    /// </summary>
    internal string Schema { get; private set; }

    /// <summary>
    /// Parsed JObject representation of <see cref="Schema"/>.
    /// </summary>
    internal JObject SchemaJson { get; private set; }

    /// <summary>
    /// CLR type used to generate the schema and deserialize arguments.
    /// </summary>
    public Type Type { get; private set; }

    /// <summary>
    /// Callback signature used to process tool calls.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    /// <param name="toolCall">Tool call emitted by the assistant.</param>
    /// <returns>A response instructing the runtime how to proceed.</returns>
    public delegate CompletionToolCallbackResponse CompletionToolCallback(CompletionRequest r, ToolCall toolCall);

    /// <summary>
    /// Optional callback that executes when the assistant requests this tool.
    /// </summary>
    public CompletionToolCallback? Callback;

    /// <summary>
    /// Function definition payload sent to the OpenAI API.
    /// </summary>
    internal JObject Definition { get; private set; }

    /// <summary>
    /// Converts all registered tools into the JSON array representation expected by the API.
    /// </summary>
    /// <param name="toolTypes">Registered tools keyed by name.</param>
    /// <returns>A JArray describing every tool.</returns>
    internal static JArray AllAsJson(Dictionary<string, Tool> toolTypes)
    {
        return new JArray(toolTypes.Values.Select(t => t.ToJson()));
    }

    /// <summary>
    /// Creates a new tool definition and generates the associated JSON schema and function payload.
    /// </summary>
    /// <param name="name">Unique tool identifier.</param>
    /// <param name="description">Human-readable description surfaced to the model.</param>
    /// <param name="type">CLR type describing the tool arguments.</param>
    /// <param name="strict">Optional strict flag forwarded to the API.</param>
    /// <param name="callback">Optional callback invoked when the tool is executed.</param>
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

    /// <summary>
    /// Serializes the tool into the OpenAI function definition format.
    /// </summary>
    /// <returns>A JObject describing the tool.
    /// </returns>
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

/// <summary>
/// Represents a single tool invocation emitted by the assistant, including the deserialized arguments.
/// </summary>
public class ToolCall
{
    /// <summary>
    /// Arguments produced by the model and deserialized according to the tool schema.
    /// </summary>
    public object? Arguments;

    /// <summary>
    /// Name of the tool the assistant attempted to call.
    /// </summary>
    public string Name;

    /// <summary>
    /// Unique identifier assigned by the OpenAI API for correlating tool responses.
    /// </summary>
    public string Id;

    /// <summary>
    /// Tool metadata associated with this call.
    /// </summary>
    public Tool Tool;

    /// <summary>
    /// Creates a new tool call with the provided arguments and metadata.
    /// </summary>
    /// <param name="arguments">Deserialized argument payload, or null if validation failed.</param>
    /// <param name="name">Tool name emitted by the assistant.</param>
    /// <param name="id">Unique tool call identifier.</param>
    /// <param name="tool">Tool metadata associated with the call.</param>
    public ToolCall(object? arguments, string name, string id, Tool tool)
    {
        Name = name;
        Arguments = arguments;
        Id = id;
        Tool = tool;
    }

    /// <summary>
    /// Serializes the tool call into the format expected by the OpenAI API.
    /// </summary>
    /// <returns>A JObject describing the tool call.</returns>
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

    /// <summary>
    /// Parses a tool call payload from the OpenAI response, validating arguments against the registered schema.
    /// </summary>
    /// <param name="toolTypes">Tool registry used to resolve metadata and schema.</param>
    /// <param name="token">JSON token representing the tool call.</param>
    /// <returns>A <see cref="ToolCall"/> representing the parsed payload.</returns>
    /// <exception cref="ArgumentException">Thrown when the payload is malformed or references an unknown tool.</exception>
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
