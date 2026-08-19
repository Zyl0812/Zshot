namespace Starshot.Core.Editor;

public sealed class EditorDocument
{
    public List<EditorElement> Elements { get; } = new();

    public EditorElement? Find(Guid id) => Elements.FirstOrDefault(e => e.Id == id);
}

public interface IEditorCommand
{
    void Execute(EditorDocument document);
    void Undo(EditorDocument document);
}

public sealed class AddElementCommand : IEditorCommand
{
    private readonly EditorElement _element;

    public AddElementCommand(EditorElement element) => _element = element;

    public void Execute(EditorDocument document)
    {
        if (!document.Elements.Contains(_element))
        {
            document.Elements.Add(_element);
        }
    }

    public void Undo(EditorDocument document) => document.Elements.Remove(_element);
}

public sealed class RemoveElementCommand : IEditorCommand
{
    private readonly EditorElement _element;
    private int _index;

    public RemoveElementCommand(EditorElement element) => _element = element;

    public void Execute(EditorDocument document)
    {
        _index = document.Elements.IndexOf(_element);
        if (_index >= 0)
        {
            document.Elements.RemoveAt(_index);
        }
    }

    public void Undo(EditorDocument document)
    {
        if (_index < 0)
        {
            return;
        }

        _index = Math.Min(_index, document.Elements.Count);
        document.Elements.Insert(_index, _element);
    }
}

public sealed class MoveElementCommand : IEditorCommand
{
    private readonly EditorElement _element;
    private readonly double _dx;
    private readonly double _dy;

    public MoveElementCommand(EditorElement element, double dx, double dy)
    {
        _element = element;
        _dx = dx;
        _dy = dy;
    }

    public void Execute(EditorDocument document) => _element.MoveBy(_dx, _dy);

    public void Undo(EditorDocument document) => _element.MoveBy(-_dx, -_dy);
}

public sealed class ClearElementsCommand : IEditorCommand
{
    private List<EditorElement> _snapshot = new();

    public void Execute(EditorDocument document)
    {
        _snapshot = document.Elements.ToList();
        document.Elements.Clear();
    }

    public void Undo(EditorDocument document)
    {
        document.Elements.Clear();
        document.Elements.AddRange(_snapshot);
    }
}

public sealed class EditorHistory
{
    private readonly List<IEditorCommand> _undo = new();
    private readonly List<IEditorCommand> _redo = new();

    public EditorHistory(int maxDepth = 80)
    {
        MaxDepth = Math.Max(1, maxDepth);
    }

    public int MaxDepth { get; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Execute(EditorDocument document, IEditorCommand command)
    {
        command.Execute(document);
        _undo.Add(command);
        if (_undo.Count > MaxDepth)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    public bool Undo(EditorDocument document)
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Undo(document);
        _redo.Add(command);
        return true;
    }

    public bool Redo(EditorDocument document)
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Execute(document);
        _undo.Add(command);
        return true;
    }
}
