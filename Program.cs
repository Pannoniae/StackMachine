using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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
    mov, // this doesn't exist at runtime, just for preprocess
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
[StructLayout(LayoutKind.Explicit)]
readonly struct Number {
    [FieldOffset(0)] public readonly int i;
    [FieldOffset(0)] public readonly float f;

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
    public const bool CODE_DUMP = true;
    private Stack<Number> stack;
    private string[] code;
    private Dictionary<string, Action> instructions = new();

    private static Dictionary<string, Func<Instruction, Instruction>> mnemonics = new() {
        {"mov", Instruction.movProcessor}
    };

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
        processMnemonics();
        if (CODE_DUMP) {
            for (var i = 0; i < code.Length; i++) {
                var o = code[i];
                Console.Out.WriteLine($"{i}: {o}");
            }
        }
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
                    Console.WriteLine(
                        $"Label with name {line.Substring(1)} has been reused at line {i}, invalid program");
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
    }

    class Instruction {
        public Instructions inst;
        public string[] args;

        public Instruction(Instructions inst, string[] args) {
            this.inst = inst;
            this.args = args;
        }

        public Instruction(Instructions inst) {
            this.inst = inst;
            this.args = Array.Empty<string>();
        }

        public Instruction(Instructions inst, string arg) {
            this.inst = inst;
            this.args = new[] {arg};
        }

        public string print() {
            var sb = new StringBuilder();
            sb.Append(inst.ToString());
            sb.Append(' ');
            sb.AppendJoin(", ", args);
            return sb.ToString();
        }

        // returns whether the arg at index i is an argument which will get pushed onto the stack
        public bool isStackArg(int i) {
            return args[i].StartsWith("[");
        }

        // strip the brackets around a stack arg
        public static string stripStackArg(string arg) {
            return arg[1..^1];
        }

        public static Instruction parse(string line) {
            if (line.StartsWith("#") || line == string.Empty) {
                // rem
                return null;
            }

            var instSep = line.IndexOf(" "); // after the instruction, before any arguments
            if (instSep == -1) {
                Instructions i = Enum.Parse<Instructions>(line);
                return new Instruction(i);
            }

            var _inst = line.Substring(0, instSep);
            var args = line.Substring(instSep + 1).Replace(" ", "").Split(",");
            Instructions _i = Enum.Parse<Instructions>(_inst);
            return new Instruction(_i, args);
        }

        public static Instruction movProcessor(Instruction mov) {
            Instruction newInst = null; // won't be null but the c# compiler doesn't shut up
            if (mov.args.Length == 2) {
                newInst = new Instruction(Instructions.mov_r2r, mov.args);
            }

            if (mov.args.Length == 1) {
                if (mov.isStackArg(0)) {
                    newInst = new Instruction(Instructions.mov_s2r, stripStackArg(mov.args[0]));
                }
                else {
                    newInst = new Instruction(Instructions.mov_r2s, mov.args[0]);
                }
            }

            return newInst;
        }
    }

    private void processMnemonics() {
        for (var i = 0; i < code.Length; i++) {
            var line = code[i];
            var inst = Instruction.parse(line);
            if (inst == null) continue;
            var instName = inst.inst.ToString();
            if (mnemonics.ContainsKey(instName)) {
                code[i] = mnemonics[instName](inst).print();
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
        //instructions.Add("mov", () => {
        //    // mov reg, [num]
        //    reg[1] = stack.get();
        //    reg[reg[0].i] = reg[1];
        //});
        instructions.Add("mov_s2r", () => {
            // mov_s2r reg, [num], stack to register 
            reg[1] = stack.get();
            reg[reg[0].i] = reg[1];
        });
        instructions.Add("mov_r2s", () => {
            //mov_r2s [reg], register to stack
            stack.push(reg[reg[0].i]);
        });
        instructions.Add("mov_r2r", () => {
            //mov_r2r reg, reg2, register to register
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

            //Console.WriteLine("lineno: " + i);
            var inst = Instruction.parse(line);
            if (inst == null) continue;
            executeInstruction(inst.inst.ToString(), inst.args);
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
        string path;
        if (args.Length == 1) {
            path = args[0];
        }
        else if (args.Length == 0) {
            path = "code.txt";
        }
        else {
            throw new ArgumentException("The user is a bloody idiot.");
        }

        var machine = new Machine(File.ReadAllLines(path));
        machine.execute();
    }
}