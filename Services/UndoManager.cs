using MapperIce.Models;

namespace MapperIce.Services;

public class UndoManager
{
    private Stack<List<Room>> _undoStack = new();
    private Stack<List<Room>> _redoStack = new();
    private List<Room> _currentState = new();

    public void SaveState(List<Room> rooms)
    {
        var copy = rooms.Select(r => r.Clone()).ToList();
        
        // Сохраняем в стек
        _undoStack.Push(copy);
        _redoStack.Clear();
        _currentState = copy;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public List<Room> Undo()
    {
        if (!CanUndo) return _currentState;
        
        _redoStack.Push(_currentState);
        _currentState = _undoStack.Pop();
        return _currentState.Select(r => r.Clone()).ToList();
    }

    public List<Room> Redo()
    {
        if (!CanRedo) return _currentState;
        
        _undoStack.Push(_currentState);
        _currentState = _redoStack.Pop();
        return _currentState.Select(r => r.Clone()).ToList();
    }
}