using Verse;

namespace FireControl;

public class Dialog_RenameSomething : Dialog_Rename
{
    private readonly INamed named;

    public Dialog_RenameSomething(INamed arg)
    {
        named = arg;
        curName = named.Name;
    }

    public override void SetName(string name)
    {
        named.Name = name;
    }
}

public interface INamed
{
    public string Name { get; set; }
}
