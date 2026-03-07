using System.Text.Json;
using System.Text.Json.Nodes;
using BTSS.IAR.Models;

namespace BTSS.IAR.Services;

public sealed class ServiceRuntimeFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<ServiceRuntimeDocument> LoadAsync(string path)
    {
        try
        {
            if (!File.Exists(path)) return new ServiceRuntimeDocument();
            var node = JsonNode.Parse(await File.ReadAllTextAsync(path))?.AsObject();
            var serviceNode = node?["Service"]?.AsObject();
            return serviceNode?.Deserialize<ServiceRuntimeDocument>(JsonOptions) ?? new ServiceRuntimeDocument();
        }
        catch
        {
            return new ServiceRuntimeDocument();
        }
    }

    public async Task SaveAsync(string path, ServiceRuntimeDocument runtime)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonObject root;
        if (File.Exists(path))
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(path))?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject
            {
                ["Logging"] = new JsonObject
                {
                    ["LogLevel"] = new JsonObject
                    {
                        ["Default"] = "Information",
                        ["Microsoft.Hosting.Lifetime"] = "Information"
                    }
                }
            };
        }

        root["Service"] = JsonSerializer.SerializeToNode(runtime, JsonOptions);
        await File.WriteAllTextAsync(path, root.ToJsonString(JsonOptions));
    }
}
