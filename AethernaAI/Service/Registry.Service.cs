using Newtonsoft.Json;
using AethernaAI.Enum;
using AethernaAI.Util;

namespace AethernaAI.Service;

public class Registry<T> where T : class
{
  private readonly string _folderPath;
  private readonly Dictionary<string, T> _cache = new();

  public Registry(string name)
  {
    _folderPath = Path.Combine("registry", name);
    Directory.CreateDirectory(_folderPath);
    LoadAllFiles();
  }

  private void LoadAllFiles()
  {
    foreach (var file in Directory.GetFiles(_folderPath, "*.json"))
    {
      var id = Path.GetFileNameWithoutExtension(file);
      _cache[id] = JsonConvert.DeserializeObject<T>(File.ReadAllText(file))!;
    }
  }

  public void Save(string id, T item)
  {
    var path = Path.Combine(_folderPath, $"{id}.json");
    File.WriteAllText(path, JsonConvert.SerializeObject(item, Formatting.Indented));
    _cache[id] = item;

    Logger.Log(LogLevel.Info, $"Registered entity {id}");
  }

  public void Update(string id, Action<T> updateAction)
  {
    if (_cache.TryGetValue(id, out var item))
    {
      updateAction(item);
      Save(id, item);

      Logger.Log(LogLevel.Info, $"Updated entity {id}");
    }
  }

  public void Delete(string id)
  {
    var path = Path.Combine(_folderPath, $"{id}.json");
    if (File.Exists(path))
    {
      File.Delete(path);
      _cache.Remove(id);

      Logger.Log(LogLevel.Info, $"Deleted entity {id}");
    }
  }

  public bool Has(string id) => _cache.ContainsKey(id);

  public T? Get(string id) => _cache.TryGetValue(id, out var item) ? item : null;

  public List<T> GetAll() => _cache.Values.ToList();
}

public class RegistryService
{
  public Registry<Model.User> Users { get; } = new("users");
  // public Registry<Avatar> Avatars { get; } = new("Avatars");
}