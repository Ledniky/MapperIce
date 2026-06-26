using MapperIce.Models;
using System.Text.Json;

namespace MapperIce.Services;

public class RepositoryManager
{
    private List<Repository> _repositories = new();
    private string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MapperIce",
        "repositories.json"
    );

    public IReadOnlyList<Repository> Repositories => _repositories;
    public event Action? OnRepositoriesChanged;

    public RepositoryManager()
    {
        Load();
    }

    public void AddRepository(string path)
    {
        if (!Directory.Exists(path))
        {
            MessageBox.Show($"Папка не найдена: {path}");
            return;
        }

        if (_repositories.Any(r => r.Path == path))
        {
            MessageBox.Show("Этот репозиторий уже добавлен");
            return;
        }

        var name = Path.GetFileName(path);
        var repo = new Repository
        {
            Name = name,
            Path = path,
            IsIndexed = false
        };

        if (_repositories.Any(r => r.Name == name))
        {
            int count = _repositories.Count(r => r.Name.StartsWith(name)) + 1;
            repo.Name = $"{name} ({count})";
        }

        _repositories.Add(repo);
        Save();
        OnRepositoriesChanged?.Invoke();
    }

    public void RemoveRepository(string id)
    {
        var repo = _repositories.FirstOrDefault(r => r.Id == id);
        if (repo != null)
        {
            _repositories.Remove(repo);
            Save();
            OnRepositoriesChanged?.Invoke();
        }
    }

    public void MarkAsIndexed(string id, int count)
    {
        var repo = _repositories.FirstOrDefault(r => r.Id == id);
        if (repo != null)
        {
            repo.IsIndexed = true;
            repo.LastIndexed = DateTime.Now;
            repo.PrototypeCount = count;
            Save();
        }
    }

    public Repository? GetRepository(string id)
    {
        return _repositories.FirstOrDefault(r => r.Id == id);
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(_repositories, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_configPath)) return;
            var json = File.ReadAllText(_configPath);
            _repositories = JsonSerializer.Deserialize<List<Repository>>(json) ?? new List<Repository>();
        }
        catch
        {
            _repositories = new List<Repository>();
        }
    }
}