using Flax.Build;

public class WsCodeEditorTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for game
        Modules.Add("WsCodeEditor");
    }
}
