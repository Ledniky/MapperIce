using MapperIce.Models;
using System.Text.Json;

namespace MapperIce.Services;

public class RepositoryManager
{
    private List<Repository> _repositories = new();
    private string? _selectedRepositoryId;

    private string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MapperIce",
        "repositories.json"
    );

    public IReadOnlyList<Repository> Repositories => _repositories;
    public string? SelectedRepositoryId => _selectedRepositoryId;
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
            if (_selectedRepositoryId == id)
                _selectedRepositoryId = null;
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

    public void SetSelectedRepository(string? id)
    {
        _selectedRepositoryId = id;
        Save();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var config = new RepoConfig
            {
                Repositories = _repositories,
                SelectedRepositoryId = _selectedRepositoryId
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
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

            try
            {
                var config = JsonSerializer.Deserialize<RepoConfig>(json);
                if (config != null && config.Repositories != null)
                {
                    _repositories = config.Repositories;
                    _selectedRepositoryId = config.SelectedRepositoryId;
                    return;
                }
            }
            catch { }

            // Совместимость со старым форматом файла (просто список репозиториев)
            _repositories = JsonSerializer.Deserialize<List<Repository>>(json) ?? new List<Repository>();
        }
        catch
        {
            _repositories = new List<Repository>();
        }
    }

    private class RepoConfig
    {
        public List<Repository> Repositories { get; set; } = new();
        public string? SelectedRepositoryId { get; set; }
    }
}