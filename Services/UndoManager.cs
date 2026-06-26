using MapperIce.Models;

namespace MapperIce.Services;

public class UndoManager
{
    private List<List<Room>> _history = new();
    private int _currentIndex = -1;

    public void AddState(List<Room> rooms)
    {
        // Если мы не в конце списка - обрезаем будущее
        if (_currentIndex < _history.Count - 1)
        {
            _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
        }
        
        var copy = rooms.Select(r => r.Clone()).ToList();
        _history.Add(copy);
        _currentIndex = _history.Count - 1;
    }

    public bool CanUndo => _currentIndex > 0;
    public bool CanRedo => _currentIndex < _history.Count - 1;

    public List<Room> Undo()
    {
        if (!CanUndo) return _history[_currentIndex];
        _currentIndex--;
        return _history[_currentIndex].Select(r => r.Clone()).ToList();
    }

    public List<Room> Redo()
    {
        if (!CanRedo) return _history[_currentIndex];
        _currentIndex++;
        return _history[_currentIndex].Select(r => r.Clone()).ToList();
    }
}