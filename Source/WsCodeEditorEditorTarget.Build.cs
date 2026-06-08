using Flax.Build;

public class WsCodeEditorEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for editor
        Modules.Add("WsCodeEditor");
        Modules.Add("WsCodeEditorEditor");
    }
}
