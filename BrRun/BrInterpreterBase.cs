namespace BrRun;

public abstract class BrInterpreterBase
{
    public BrInterpretContext Context { get; set; }
    public string FilePath { get; set; }

    public BrInterpreterBase(string filePath, BrInterpretContext context)
    {
        FilePath = filePath;
        Context = context;
    }

    public abstract void Interpret();
}
