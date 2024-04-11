using BrRun.Interpreters;
using System;
using System.IO;

namespace BrRun;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("fatal: no file provided.");
            return;
        }

        string path = args[0];
        bool stepFlag = false, usefulFlag = false;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--step":
                    if (stepFlag) Console.WriteLine("warn: duplicate --step flag.");
                    stepFlag = true;
                    break;

                case "--useful":
                    if (usefulFlag) Console.WriteLine("warn: duplicate --useful flag.");
                    usefulFlag = true;
                    break;

                default:
                    Console.WriteLine($"warn: unknown {args[i]} argument.");
                    break;
            }
        }

        if (!File.Exists(path))
        {
            Console.WriteLine($"fatal: file does not exist at {path}");
            return;
        }

        InterpretMode mode;
        if (path.EndsWith(".bf") || path.EndsWith(".br") || path.EndsWith(".b"))
        {
            mode = InterpretMode.StandardBr;
            if (usefulFlag) Console.WriteLine("warn: --useful flag is not applicable to standard brainfuck.");
        }
        else if (path.EndsWith(".bpp") || path.EndsWith(".b++") || path.EndsWith(".bfpp") ||
                 path.EndsWith(".bf++") || path.EndsWith(".brpp") || path.EndsWith(".br++"))
        {
            mode = InterpretMode.BrPlusPlus;
            if (usefulFlag) mode = InterpretMode.UsefulBr;
        }
        else
        {
            Console.WriteLine($"fatal: unsupported file type {path[path.LastIndexOf('.')..]}.");
            return;
        }

        BrInterpretContext context = new()
        {
            filePath = path,
            stepFlag = stepFlag,
            usefulFlag = usefulFlag,
            mode = mode
        };

        BrInterpreterBase interpreter;
        switch (mode)
        {
            case InterpretMode.StandardBr:
                interpreter = new StandardBrInterpreter(path, context);
                break;

            default:
                Console.WriteLine("fatal: unknown interpreter mode. how did this happen?");
                return;
        }

        interpreter.Interpret();
    }
}
