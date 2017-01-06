using System;
namespace msp430sim
{
	public static class REG
	{
		public const ushort PC = 0x0;
		public const ushort SP = 0x1;
		public const ushort SR = 0x2;
		public const ushort CG1 = 0x2;
		public const ushort R3 = 0x3;
		public const ushort CG2 = 0x3;
		public const ushort R4 = 0x4;
		public const ushort R5 = 0x5;
		public const ushort R6 = 0x6;
		public const ushort R7 = 0x7;
		public const ushort R8 = 0x8;
		public const ushort R9 = 0x9;
		public const ushort R10 = 0xA;
		public const ushort R11 = 0xB;
		public const ushort R12 = 0xC;
		public const ushort R13 = 0xD;
		public const ushort R14 = 0xE;
		public const ushort R15 = 0xF;
	}

	class Register
	{
		public ushort value;

		public Register()
		{
			value = 0;
		}

		public void set(WORD value)
		{
			this.value = value.toShort();
		}

		public WORD get()
		{
			var word = new WORD(value);
			return word;
		}

		public void incrementBy(short offset)
		{
			value = (ushort)(value + offset);
		}

		//Status register stuff:
		public ushort getV() //overflow flag
		{
			return (ushort)((value & 0x0100) >> 8);
		}

		public void setV(ushort value)
		{
			this.value = (ushort)((value == 0) ? this.value & 0xFEFF : this.value | 0x0100);
		}

		public ushort getN() //negative flag
		{
			return (ushort)((value & 0x4) >> 2);
		}

		public void setN(ushort value)
		{
			this.value = (ushort)((value == 0) ? this.value & 0xFFFB : this.value | 0x4);
		}

		public ushort getZ() //zero flag
		{
			return (ushort)((value & 0x2) >> 1);
		}

		public void setZ(ushort value)
		{
			this.value = (ushort)((value == 0) ? this.value & 0xFFFD : this.value | 0x2);
		}

		public ushort getC() //carry flag
		{
			return (ushort)(value & 0x1);
		}

		public void setC(ushort value)
		{
			this.value = (ushort)((value == 0) ? this.value & 0xFFFE : this.value | 0x1);
		}
	}

	class CPURegisters
	{
		public Register[] select; //Registers array
		public ushort[,] CG_constants; //A table for the constant generator

		public CPURegisters()
		{
			CG_constants = new ushort[,] { { 0, 0, 4, 8 }, { 0, 1, 2, 0xffff } };
			select = new Register[16];

			for (int i = 0; i < 16; i++)
			{
				select[i] = new Register();
			}
		}

		public void updateSR(ushort dstValue, ushort BW)
		{
			select[REG.SR].setZ(Convert.ToUInt16(dstValue == 0)); //if zero, set zero bit
			select[REG.SR].setN(Convert.ToUInt16((dstValue & (1 << (15 - BW * 8))) > 0)); //if overflow, set negative bit
		}

		public void setRegister(ushort register, WORD value)
		{
			select[register].value = value.toShort();
		}

		public void setRegister(ushort register, ushort value)
		{
			select[register].value = value;
		}

		public ushort readRegister(ushort register, ushort mode)
		{
			if ((register == REG.CG1 && mode != 0) || register == REG.CG2)
			{
				return CG_constants[register - 2, mode];
			}

			else return select[register].value;
		}

		public void printState()
		{
			string state = "";
			state += String.Format("PC: {0:X} | SP: {1:X} | SR: {2} | ", select[REG.PC].get().toShort(), select[REG.SP].get().toShort(), Convert.ToString(select[REG.SR].get().toShort(), 2).PadLeft(16, '0'));

			for (int i = 0x4; i <= 0xF; i++)
			{
				state += String.Format("R{0}: 0x{1:X} | ", i, select[i].value);
			}

			Console.WriteLine(state);
		}
	}
}
