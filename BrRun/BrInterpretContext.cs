namespace BrRun;

public class BrInterpretContext
{
    public required string filePath;
    public required bool stepFlag;
    public required bool usefulFlag;
    public required InterpretMode mode;

    internal BrInterpretContext() { }
}
