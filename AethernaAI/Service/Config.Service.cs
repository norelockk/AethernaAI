using Newtonsoft.Json;
using System.Linq.Expressions;
using AethernaAI.Enum;
using AethernaAI.Util;
using AethernaAI.Model;

namespace AethernaAI.Service;

public class ConfigService
{
  private readonly string configFilePath = "config.json";
  public ConfigModel? Config { get; private set; }

  public ConfigService()
  {
    if (!File.Exists(configFilePath))
    {
      CreateDefaultConfig();
      Logger.Log(LogLevel.Info, "Config file created");
    }
    LoadConfig();
  }

  private void CreateDefaultConfig()
  {
    Config = new ConfigModel
    {
    };
    SaveConfig();
  }

  private void LoadConfig()
  {
    var configJson = File.ReadAllText(configFilePath);
    Config = JsonConvert.DeserializeObject<ConfigModel>(configJson);

    UpdateConfig();
  }

  private void SaveConfig()
  {
    var configJson = JsonConvert.SerializeObject(Config, Formatting.Indented);
    File.WriteAllText(configFilePath, configJson);
  }

  private void UpdateConfig()
  {
    var configJson = File.ReadAllText(configFilePath);
    var config = JsonConvert.DeserializeObject<ConfigModel>(configJson);
    var properties = typeof(ConfigModel).GetProperties();
    foreach (var property in properties)
    {
      if (property.GetValue(config) == null)
      {
        property.SetValue(config, property.GetValue(Config));
      }
    }
    Config = config;
    SaveConfig();
  }

  public T GetConfig<T>(Expression<Func<ConfigModel, T>> keySelector)
  {
    if (keySelector == null)
    {
      throw new ArgumentNullException(nameof(keySelector));
    }

    var memberExpression = keySelector.Body as MemberExpression;
    if (memberExpression == null || Config == null)
    {
      throw new ArgumentException("Invalid key selector or Config is null.");
    }

    var property = memberExpression.Member as System.Reflection.PropertyInfo;
    if (property == null)
    {
      throw new ArgumentException("Key selector does not refer to a property.");
    }

    var value = property.GetValue(Config);

    return (T)value!;
  }

  public T SetConfig<T>(Expression<Func<ConfigModel, T>> keySelector, T value)
  {
    if (keySelector == null)
    {
      throw new ArgumentNullException(nameof(keySelector));
    }

    var memberExpression = keySelector.Body as MemberExpression;
    if (memberExpression == null || Config == null)
    {
      throw new ArgumentException("Invalid key selector or Config is null.");
    }

    var property = memberExpression.Member as System.Reflection.PropertyInfo;
    if (property == null)
    {
      throw new ArgumentException("Key selector does not refer to a property.");
    }

    property.SetValue(Config, value);
    SaveConfig();

    return value;
  }
}