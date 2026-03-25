namespace PokeCrystal.Editor;

/// <summary>
/// Command pattern for undo/redo. Every mutation in the editor is an IEditorAction.
/// </summary>
public interface IEditorAction
{
    string Description { get; }
    void Do();
    void Undo();
}
