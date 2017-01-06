
namespace msp430sim
{
	public class MSP430Exception : System.Exception
	{
		public MSP430Exception(string message) : base(message) { }
	}

	public static class Opcode
	{
		//Jump instructions
		public const ushort JNZ = 0x0;
		public const ushort JZ = 0x1;
		public const ushort JNC = 0x2;
		public const ushort JC = 0x3;
		public const ushort JN = 0x4;
		public const ushort JGE = 0x5;
		public const ushort JL = 0x6;
		public const ushort JMP = 0x7;

		//1-Op instructions
		public const ushort RRC = 0x0;
		public const ushort SWPB = 0x1;
		public const ushort RRA = 0x2;
		public const ushort SXT = 0x3;
		public const ushort PUSH = 0x4;
		public const ushort CALL = 0x5;
		public const ushort RETI = 0x6;

		//2-Op instructions
		public const ushort MOV = 0x4;
		public const ushort ADD = 0x5;
		public const ushort ADDC = 0x6;
		public const ushort SUBC = 0x7;
		public const ushort SUB = 0x8;
		public const ushort CMP = 0x9;
		public const ushort DADD = 0xA;
		public const ushort BIT = 0xB;
		public const ushort BIC = 0xC;
		public const ushort BIS = 0xD;
		public const ushort XOR = 0xE;
		public const ushort AND = 0xF;
	}

	public static class AddressingMode
	{
		public const ushort Register = 0x0;
		public const ushort Indexed = 0x1;
		public const ushort IndirectRegister = 0x2;
		public const ushort IndirectAutoincrement = 0x3;
	}

	enum CPUDebugMode
	{
		Verbose,
		None
	}

	class WORD // [--HIGH--][--LOW---]
	{
		public byte high;
		public byte low;

		public WORD()
		{
			high = 0x00;
			low = 0x00;
		}

		public WORD(ushort value)
		{
			low = (byte)(value & 0x00FF);
			high = (byte)((value >> 8) & 0x00FF);
		}

		public ushort toShort()
		{
			return (ushort)((high << 8) | low);
		}

		public string stringValue()
		{
			return high.ToString("X") + low.ToString("X");
		}
	}

	enum InstructionType
	{
		SingleOperand, Jump, TwoOperand
	}

	class Instruction
	{
		public ushort code;
		public InstructionType type;

		public Instruction(WORD word)
		{
			code = word.toShort();

			if ((code >> 10) == 0x04)
				type = InstructionType.SingleOperand;
			else if ((code >> 13) == 0x01)
				type = InstructionType.Jump;
			else
				type = InstructionType.TwoOperand;
		}

	}
}
