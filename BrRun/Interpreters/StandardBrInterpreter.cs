using System;
using System.Collections.Generic;
using System.IO;

namespace BrRun.Interpreters;

public class StandardBrInterpreter : BrInterpreterBase
{
    // Can technically be changed, but it should remain the same to match convention.
    public static readonly short TapeMinValue = 0, TapeMaxValue = 255;
    public static readonly int TapeLength = 30_000;

    protected FileStream? reader;
    protected readonly short[] tape;

    protected int dataPointer;
    protected Stack<long> stack;
    protected Stack<(int, int)> debugOpeningStack;

    private int lineNumber;
    private int charNumber;
    private char curChar;
    private int awakeTop = 0;

    public StandardBrInterpreter(string filePath, BrInterpretContext context) : base(filePath, context)
    {
        reader = null;
        tape = new short[TapeLength];
        dataPointer = 0;
        stack = [];
        debugOpeningStack = [];

        lineNumber = 1;
        charNumber = 0;
    }

    public override void Interpret()
    {
        awakeTop = Console.CursorTop;
        if (Context.stepFlag)
        {
            for (int i = 0; i < 10; i++) Console.WriteLine();
            awakeTop = Console.CursorTop - 10;
        }
        reader = new(FilePath, FileMode.Open);

        IntentionKind intent;
        while ((intent = StepProgram()) != IntentionKind.EndOfFile)
        {
            if (Context.stepFlag) ShowDebugScreen(intent);
            HandleIntent(intent);
        }

        reader.Close();
        Console.CursorVisible = true;
    }

    protected void HandleIntent(IntentionKind intent)
    {
        switch (intent)
        {
            case IntentionKind.IncrementPointer:
                dataPointer++;
                if (dataPointer >= TapeLength)
                {
                    Console.WriteLine($"warn L{lineNumber} C{charNumber}: data pointer has overflowed! (length {TapeLength})");
                    dataPointer = 0;
                }
                break;

            case IntentionKind.DecrementPointer:
                dataPointer--;
                if (dataPointer < 0)
                {
                    Console.WriteLine($"warn L{lineNumber} C{charNumber}: data pointer has underflowed! (length {TapeLength})");
                    dataPointer = TapeLength - 1;
                }
                break;

            case IntentionKind.IncrementValue:
                if (tape[dataPointer] == TapeMaxValue) tape[dataPointer] = TapeMinValue;
                else tape[dataPointer]++;
                break;

            case IntentionKind.DecrementValue:
                if (tape[dataPointer] == TapeMinValue) tape[dataPointer] = TapeMaxValue;
                else tape[dataPointer]--;
                break;

            case IntentionKind.OutputValue:
                Console.Write((char)tape[dataPointer]);
                break;

            case IntentionKind.InputValue:
                Console.CursorVisible = true;
                tape[dataPointer] = (byte)Console.ReadKey().KeyChar;
                break;

            case IntentionKind.BeginGroup:
                if (reader is null) Console.WriteLine($"error L{lineNumber} C{charNumber}: file hasn't been opened yet! how did this happen?");
                else
                {
                    stack.Push(reader.Position);
                    debugOpeningStack.Push((lineNumber, charNumber));
                    if (tape[dataPointer] == 0)
                    {
                        // Look for closing brace.
                        IntentionKind newIntent;
                        while ((newIntent = StepProgram()) != IntentionKind.EndOfFile)
                        {
                            if (newIntent == IntentionKind.EndGroup) break; // Found closing bracket.
                        }
                        if (newIntent == IntentionKind.EndOfFile)
                        {
                            Console.WriteLine($"error L{lineNumber} C{charNumber}: no closing bracket to match opening bracket.");
                            return;
                        }
                    }
                }
                break;

            case IntentionKind.EndGroup:
                if (stack.Count == 0)
                {
                    Console.WriteLine($"error L{lineNumber} C{charNumber}: no opening bracket to match closing bracket.");
                    return;
                }

                if (tape[dataPointer] == 0)
                {
                    // Exit loop.
                    stack.Pop();
                    debugOpeningStack.Pop();
                }
                else
                {
                    // Restart.
                    if (reader is null) Console.WriteLine($"error L{lineNumber} C{charNumber}: file hasn't been opened yet! how did this happen?");
                    else reader.Seek(stack.Peek(), SeekOrigin.Begin);

                    (int newL, int newC) = debugOpeningStack.Peek();
                    lineNumber = newL;
                    charNumber = newC;
                }
                break;

            default:
                Console.WriteLine($"warn L{lineNumber} C{charNumber}: unknown intent! how did this happen?");
                break;
        }
    }

    private int remainingSkips = 0;
    protected void ShowDebugScreen(IntentionKind currentIntent)
    {
        const int numSpace = 3;

        Console.CursorVisible = false;
        int initialTop = Console.CursorTop, initialLeft = Console.CursorLeft;
        int totalCanFit = (Console.WindowWidth - 1) / (numSpace + 3);

        int startIndex = int.Max(0, (int)(dataPointer - totalCanFit * 0.5)),
            endIndex = int.Min(startIndex + totalCanFit - 1, TapeLength - 1);

        int consolePos = 0;
        for (int i = startIndex; i <= endIndex; i++)
        {
            Console.SetCursorPosition(consolePos, awakeTop + 1);
            Console.Write($" {i,numSpace + 2}");

            Console.SetCursorPosition(consolePos, awakeTop + 2);
            if (i == startIndex) Console.Write("╔═════");
            else Console.Write("╦═════");

            Console.SetCursorPosition(consolePos, awakeTop + 3);
            Console.Write($"║ {tape[i],numSpace} ");

            Console.SetCursorPosition(consolePos, awakeTop + 4);
            if (i == startIndex) Console.Write("╚═════");
            else Console.Write("╩═════");

            Console.SetCursorPosition(consolePos, awakeTop + 5);
            if (i == dataPointer) Console.Write("  ^   ");
            else Console.Write("      ");

            Console.SetCursorPosition(consolePos, awakeTop + 9);
            Console.Write($"──────");

            consolePos += numSpace + 3;
        }
        Console.SetCursorPosition(consolePos, awakeTop + 2);
        Console.Write('╗');
        Console.SetCursorPosition(consolePos, awakeTop + 3);
        Console.Write('║');
        Console.SetCursorPosition(consolePos, awakeTop + 4);
        Console.Write('╝');
        Console.SetCursorPosition(consolePos, awakeTop + 9);
        Console.Write('─');

        Console.SetCursorPosition(0, awakeTop + numSpace + 3);
        string message = $"L{lineNumber} C{charNumber}: {curChar}    {GetDebugDescriptionOfOperator(currentIntent)}    ";
        Console.Write(message + new string(' ', int.Max(Console.WindowWidth - message.Length - 1, 0)));

        if (remainingSkips < int.MaxValue - 1) remainingSkips--;
        if (remainingSkips == int.MaxValue - 1 && currentIntent == IntentionKind.EndGroup && tape[dataPointer] == 0) remainingSkips = 0;

        Console.SetCursorPosition(0, awakeTop + 7);
        if (currentIntent == IntentionKind.InputValue)
        {
            Console.Write("Enter one character to step the program." + new string(' ', 70));
        }
        else
        {
            if (remainingSkips > 0)
            {
                string message2;
                if (remainingSkips == int.MaxValue) message2 = $"Continuing program to completion...";
                else if (remainingSkips == int.MaxValue - 1) message2 = $"Waiting for loop exit...";
                else message2 = $"Skipping {remainingSkips} steps...";
                Console.Write(message2 + new string(' ', 110 - message2.Length));
            }
            else
            {
                Console.Write("Press space to step the system.    D = +5 steps, F = +25 steps, G = +100 steps, J = until loop ends, K = continuous");

            _readKey:
                ConsoleKeyInfo stepKey = Console.ReadKey(true);
                switch (stepKey.Key)
                {
                    case ConsoleKey.Spacebar:
                        remainingSkips++;
                        break;
                    case ConsoleKey.D:
                        remainingSkips += 5;
                        break;
                    case ConsoleKey.F:
                        remainingSkips += 25;
                        break;
                    case ConsoleKey.G:
                        remainingSkips += 100;
                        break;
                    case ConsoleKey.J:
                        remainingSkips = int.MaxValue - 1;
                        break;
                    case ConsoleKey.K:
                        remainingSkips = int.MaxValue;
                        break;
                    default: goto _readKey;
                }
            }
        }

        Console.SetCursorPosition(initialLeft, initialTop);
    }
    private string GetDebugDescriptionOfOperator(IntentionKind intent) => intent switch
    {
        IntentionKind.IncrementPointer => $"Move pointer to right ({dataPointer} -> {dataPointer + 1})",
        IntentionKind.DecrementPointer => $"Move pointer to left ({dataPointer} -> {dataPointer - 1})",
        IntentionKind.IncrementValue   => $"Increase value at position {dataPointer} ({tape[dataPointer]} -> {tape[dataPointer] + 1})",
        IntentionKind.DecrementValue   => $"Decrease value at position {dataPointer} ({tape[dataPointer]} -> {tape[dataPointer] - 1})",
        IntentionKind.OutputValue      => $"Print out current value as character (value {tape[dataPointer]})",
        IntentionKind.InputValue       => $"Input next character input into position {dataPointer}",
        IntentionKind.BeginGroup       => tape[dataPointer] == 0
                                        ? "Skipping loop. Moving execution forward to closing bracket."
                                        : "Beginning a loop",
        IntentionKind.EndGroup         => tape[dataPointer] == 0
                                        ? $"Breaking out of a loop"
                                        : $"Moving execution back to L{debugOpeningStack.Peek().Item1} C{debugOpeningStack.Peek().Item2} until value at position {dataPointer} is zero (currently {tape[dataPointer]})",
        _                              => "?? unknown intent ??"
    };

    protected IntentionKind StepProgram()
    {
        if (reader is null)
        {
            if (reader is null) Console.WriteLine("error: file hasn't been opened yet! how did this happen?");
            return IntentionKind.EndOfFile;
        }

        int cI = reader.ReadByte();
        if (cI == -1) return IntentionKind.EndOfFile;

        char c = (char)cI;
        if (c == '\n')
        {
            lineNumber++;
            charNumber = 0;
        }
        else if (c != '\r') charNumber++;


        curChar = c;
        switch (c)
        {
            case '\r' or '\n' or ' ' or '\t': return StepProgram(); // Skip newlines.
            case '#':
                Console.WriteLine("error: comments are not supported in standard brainfuck.");
                return StepProgram();
            case '>': return IntentionKind.IncrementPointer;
            case '<': return IntentionKind.DecrementPointer;
            case '+': return IntentionKind.IncrementValue;
            case '-': return IntentionKind.DecrementValue;
            case '.': return IntentionKind.OutputValue;
            case ',': return IntentionKind.InputValue;
            case '[': return IntentionKind.BeginGroup;
            case ']': return IntentionKind.EndGroup;
            default:
                Console.WriteLine($"error: unsupported operator {c}");
                return StepProgram();
        }
    }

    protected enum IntentionKind
    {
        EndOfFile,
        IncrementPointer,
        DecrementPointer,
        IncrementValue,
        DecrementValue,
        OutputValue,
        InputValue,
        BeginGroup,
        EndGroup
    }
}
