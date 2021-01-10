using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

enum Instructions : byte {
    push,
    pop,
    dmp,
    prt,
    add,
    add_r,
    sub,
    sub_r,
    mul,
    mul_r,
    set,
    dec,
    inc,
    swp,
    mov_r2s, // register to stack
    mov_s2r, // stack to register
    mov_r2r, // register to register
    jmp,
    jz,
    jnz,
    je,
    jne,
    shr,
    shl,
    not,
    stb,
    clr
}

enum Registers : byte {
    arg0,
    arg1,
    arg2,
    arg3,
    reg4,
    reg5,
    reg6,
    jmp,
    loop,
    reg9,
    regA,
    regB,
    regC,
    regD,
    regE,
    regF
}

// yeah this is *totally* safe
readonly struct Number {
    [FieldOffset(0)]
    public readonly int i;
    [FieldOffset(0)]
    public readonly float f;

    public Number(int i) : this() {
        this.i = i;
    }

    public Number(float f) : this() {
        this.f = f;
    }

    public override string ToString() {
        return $"{i} ({f})";
    }
}

class Machine {
    // const
    public const bool CODE_DUMP = false;
    private Stack<Number> stack;
    private string[] code;
    private Dictionary<string, Action> instructions = new();

    private Number[] reg = new Number[16]; // registers
    /*
     * 0-3 = instruction arguments
     * 4-6 - general purpose
     * 7 = jmp counter
     * 8 = loop counter
     * 9-15 = general purpose
     */

    public Machine(string[] code) {
        this.code = code;
        stack = new Stack<Number>();
        setupInstructions();
        processLabels();
    }

    private void processLabels() {
        var labels = new Dictionary<string, int>();
        for (var i = 0; i < code.Length; i++) {
            var line = code[i];
            if (line.StartsWith(":")) {
                try {
                    labels.Add(line.Substring(1), i);
                }
                catch (ArgumentException e) {
                    Console.WriteLine($"Label with name {line.Substring(1)} has been reused at line {i}, invalid program");
                    Environment.Exit(1);
                }

                code[i] = "";
            }
        }

        for (var i = 0; i < code.Length; i++) {
            foreach (var label in labels) {
                code[i] = code[i].Replace(label.Key, label.Value.ToString());
            }
        }

        if (CODE_DUMP) {
            for (var i = 0; i < code.Length; i++) {
                var o = code[i];
                Console.Out.WriteLine($"{i}: {o}");
            }
        }
    }

    private void setupInstructions() {
        /*
         * inst format:
         * <inst> <arg1>,<arg2>...
         * <inst> [<stackarg1>,<stackarg2>...]
         */
        instructions.Add("push", () => { stack.push(new Number(reg[0].i)); });
        instructions.Add("pop", () => { stack.pop(); });
        instructions.Add("dmp", () => {
            Console.WriteLine("STACKPTR: " + stack.curr);
            Console.WriteLine("DUMP:");
            foreach (var el in stack.elements().GetRange(0, stack.curr + 1)) {
                Console.Write(el + " ");
            }

            Console.WriteLine();
            Console.WriteLine("REGISTERS:");
            for (var i = 0; i < reg.Length; i++) {
                var el = reg[i];
                Console.WriteLine($"{(Registers) i}: {el}");
            }
        });
        instructions.Add("prt", () => { Console.WriteLine(stack.get()); });
        instructions.Add("add", () => {
            // add [a, b]
            reg[0] = stack.pop();
            reg[1] = stack.pop();
            stack.push(new Number(reg[0].i + reg[1].i));
        });
        instructions.Add("sub", () => {
            // sub [a, b]
            reg[0] = stack.pop();
            reg[1] = stack.pop();
            stack.push(new Number(reg[0].i - reg[1].i));
        });
        instructions.Add("mul", () => {
            // mul [a, b]
            reg[0] = stack.pop();
            reg[1] = stack.pop();
            stack.push(new Number(reg[0].i * reg[1].i));
        });
        instructions.Add("set", () => {
            // set reg, num
            reg[reg[0].i] = reg[1];
        });
        instructions.Add("dec", () => {
            // dec reg
            reg[reg[0].i] = new Number(reg[reg[0].i].i - 1);
        });
        instructions.Add("inc", () => {
            // inc reg
            reg[reg[0].i] = new Number(reg[reg[0].i].i + 1);
        });
        instructions.Add("swp", () => {
            // swp
            reg[0] = stack.pop();
            reg[1] = stack.pop();
            stack.push(reg[0]);
            stack.push(reg[1]);
        });
        instructions.Add("mov", () => {
            // mov reg, [num]
            reg[1] = stack.get();
            reg[reg[0].i] = reg[1];
        });
        instructions.Add("rmov", () => {
            //rmov reg
            stack.push(reg[reg[0].i]);
        });
        instructions.Add("smov", () => {
            //smov reg, reg2
            reg[reg[1].i] = reg[reg[0].i];
        });
        instructions.Add("jmp", () => {
            // jmp dst
            reg[7] = reg[0];
        });
        instructions.Add("jz", () => {
            // jz dst, [val]
            reg[1] = stack.get();
            if (reg[1].i == 0) {
                reg[7] = reg[0];
            }
        });
        instructions.Add("jnz", () => {
            // jnz dst, [val]
            reg[1] = stack.get();
            if (reg[1].i != 0) {
                reg[7] = reg[0];
            }
        });
        instructions.Add("je", () => {
            // je dst, reg1, [val] 
            reg[2] = stack.get();
            if (reg[1].i == reg[2].i) {
                reg[7] = reg[0];
            }
        });
        instructions.Add("jne", () => {
            // jne dst, reg1, [val]
            reg[2] = stack.get();
            if (reg[1].i != reg[2].i) {
                reg[7] = reg[0];
            }
        });
        instructions.Add("shr", () => {
            // shr reg, cnt
            reg[reg[0].i] = new Number(reg[reg[0].i].i >> reg[1].i);
        });
        instructions.Add("shl", () => {
            // shl reg, cnt
            reg[reg[0].i] = new Number(reg[reg[0].i].i << reg[1].i);
        });
        instructions.Add("not", () => {
            // not reg, idx
            reg[reg[0].i] = new Number(reg[reg[0].i].i ^ 1 << reg[1].i);
        });
        instructions.Add("stb", () => {
            // stb reg, idx
            reg[reg[0].i] = new Number(reg[reg[0].i].i | 1 << reg[1].i);
        });
        instructions.Add("clr", () => {
            // clr reg, idx
            reg[reg[0].i] = new Number(reg[reg[0].i].i & ~(1 << reg[1].i));
        });
    }

    public void execute() {
        for (var i = 0; i < code.Length; i++) {
            var line = code[i];
            if (line.StartsWith("#") || line == string.Empty) {
                // rem
                continue;
            }

            //Console.WriteLine("lineno: " + i);
            var instSep = line.IndexOf(" "); // after the instruction, before any arguments
            if (instSep == -1) {
                // no args
                executeInstruction(line); // just push the whole thing in, there are no args
                continue;
            }

            var inst = line.Substring(0, instSep);
            var args = line.Substring(instSep + 1).Replace(" ", "").Split(",");
            executeInstruction(inst, args);
            if (reg[7].i != 0) {
                // test jmp counter after inst
                i = reg[7].i; // jump to line
                reg[7] = new Number(0); // clear jmp counter
            }
        }

        ;
    }

    private void executeInstruction(string inst, params string[] args) {
        for (int i = 0; i < args.Length; i++) {
            reg[i] = new Number(int.Parse(args[i]));
        }

        instructions[inst]();
    }
}

class Stack<T> {
    private const int MAX = 256;
    public int curr; // current element's index
    private T[] storage = new T[MAX];


    public Stack() {
        curr = -1;
    }

    public void push(T el) {
        if (curr == MAX - 1) {
            Console.WriteLine($"STACK OVERFLOW, tried to push {el}");
        }

        curr++;
        storage[curr] = el;
    }

    public T pop() {
        if (curr == -1) {
            Console.WriteLine($"STACK UNDERFLOW, tried to pop");
        }

        T val = storage[curr];
        storage[curr] = default;
        curr--;
        return val;
    }

    public T get() {
        return storage[0];
    }

    public List<T> elements() {
        return new(storage);
    }
}

class Program {
    static void Main(string[] args) {
        var machine = new Machine(File.ReadAllLines("code.txt"));
        machine.execute();
    }
}