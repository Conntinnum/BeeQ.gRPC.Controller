using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BeeQ;

public partial class DynamicGrpcService
{
    /// <summary>
    /// Genera un documento OpenAPI (Swagger) basado en los métodos gRPC dinámicos registrados.
    /// </summary>
    /// <returns></returns>
    public static string GenerateOpenApi()
    {
        var root = new JsonObject
        {
            ["openapi"] = "3.0.3",

            ["info"] = new JsonObject
            {
                ["title"] = "Dynamic gRPC API",
                ["version"] = "1.0.0"
            },

            ["paths"] = new JsonObject(),

            ["components"] = new JsonObject
            {
                ["schemas"] = new JsonObject()
            }
        };

        var paths = (JsonObject)root["paths"]!;
        var schemas = (JsonObject)root["components"]!["schemas"]!;

        foreach (var item in ServiceMethods)
        {
            var key = item.Key;

            var serviceType = item.Value.ServiceType;
            var methodInfo = item.Value.MethodInfo;
            var parameterInfos = item.Value.ParameterInfos;

            var serviceAttribute = serviceType.GetCustomAttribute<GrpcServiceAttribute>();

            var methodAttribute = methodInfo.GetCustomAttribute<GrpcMethodAttribute>();

            if (serviceAttribute == null || methodAttribute == null)
                continue;

            var serviceName = serviceAttribute.Name;
            var methodName = methodAttribute.Name;
            var version = methodAttribute.Version;

            var path =
                $"/grpc/{serviceName}/{methodName}/{version}";

            var operation = new JsonObject
            {
                ["summary"] = $"{serviceName}.{methodName}.{version}",

                ["operationId"] = key,

                ["responses"] = new JsonObject
                {
                    ["200"] = new JsonObject
                    {
                        ["description"] = "Successful response"
                    }
                }
            };

            // -----------------------------------------
            // Request
            // -----------------------------------------

            var requestParameters = parameterInfos
                .Where(p => p.ParameterType != typeof(CancellationToken))
                .ToArray();

            if (requestParameters.Length == 1)
            {
                var requestType = requestParameters[0].ParameterType;

                var schemaName = GetSchemaName(requestType);

                AddSchema(schemas, schemaName, requestType);

                operation["requestBody"] = new JsonObject
                {
                    ["required"] = true,

                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = new JsonObject
                            {
                                ["$ref"] =
                                    $"#/components/schemas/{schemaName}"
                            }
                        }
                    }
                };
            }

            // -----------------------------------------
            // Response
            // -----------------------------------------

            var returnType = GetReturnType(methodInfo);

            if (returnType != null)
            {
                var responseSchema = CreateSchema(
                    schemas,
                    returnType);

                ((JsonObject)operation["responses"]!)["200"] =
                    new JsonObject
                    {
                        ["description"] = "Successful response",
                        ["content"] = new JsonObject
                        {
                            ["application/json"] = new JsonObject
                            {
                                ["schema"] = responseSchema
                            }
                        }
                    };
            }

            paths[path] = new JsonObject
            {
                ["post"] = operation
            };
        }

        // -----------------------------------------
        // Bearer authentication
        // -----------------------------------------

        ((JsonObject)root["components"]!)["securitySchemes"] =
            new JsonObject
            {
                ["bearerAuth"] = new JsonObject
                {
                    ["type"] = "http",
                    ["scheme"] = "bearer",
                    ["bearerFormat"] = "JWT"
                }
            };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static Type? GetReturnType(MethodInfo method)
    {
        var returnType = method.ReturnType;

        if (returnType == typeof(Task))
            return null;

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            return returnType.GetGenericArguments()[0];

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            return returnType.GetGenericArguments()[0];

        if (returnType == typeof(ValueTask))
            return null;

        return returnType;
    }

    private static string GetSchemaName(Type type)
    {
        if (type.IsGenericType)
        {
            var name = type.Name[..type.Name.IndexOf('`')];

            var arguments = string.Join("And", type.GetGenericArguments().Select(GetSchemaName));

            return $"{name}Of{arguments}";
        }

        return type.Name;
    }

    private static JsonObject CreateSchema(JsonObject schemas, Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        var primitiveSchema = CreatePrimitiveSchema(type);
        if (primitiveSchema != null)
            return primitiveSchema;

        var enumSchema = CreateEnumSchema(type);
        if (enumSchema != null)
            return enumSchema;

        var arraySchema = CreateArraySchema(schemas, type);
        if (arraySchema != null)
            return arraySchema;

        // class / complex type
        var schemaName = GetSchemaName(type);
        AddSchema(schemas, schemaName, type);

        return new JsonObject
        {
            ["$ref"] = $"#/components/schemas/{schemaName}"
        };
    }

    private static void AddSchema(JsonObject schemas, string schemaName, Type type)
    {
        if (schemas.ContainsKey(schemaName))
            return;

        var schema = new JsonObject
        {
            ["type"] = "object"
        };

        var properties = new JsonObject();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
                continue;

            properties[property.Name] = CreateSchema(schemas, property.PropertyType);
        }

        schema["properties"] = properties;

        schemas[schemaName] = schema;
    }

    private static JsonObject? CreatePrimitiveSchema(Type type)
    {
        if (type == typeof(string) || type == typeof(Guid))
            return new JsonObject { ["type"] = "string" };

        if (type == typeof(bool))
            return new JsonObject { ["type"] = "boolean" };

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return new JsonObject
            {
                ["type"] = "integer",
                ["format"] = type == typeof(long) ? "int64" : "int32"
            };

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            return new JsonObject { ["type"] = "number" };

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time"
            };

        return null;
    }

    private static JsonObject? CreateEnumSchema(Type type)
    {
        if (!type.IsEnum)
            return null;

        var values = new JsonArray();
        foreach (var value in Enum.GetNames(type))
            values.Add(value);

        return new JsonObject
        {
            ["type"] = "string",
            ["enum"] = values
        };
    }

    private static JsonObject? CreateArraySchema(JsonObject schemas, Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = CreateSchema(schemas, elementType)
            };
        }

        if (type != typeof(string) &&
            type.IsGenericType &&
            typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            var elementType = type.GetGenericArguments()[0];
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = CreateSchema(schemas, elementType)
            };
        }

        return null;
    }
}