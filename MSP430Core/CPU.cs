using System;
using System.IO;

namespace msp430sim
{
	class CPU //MSP430F149 processor
	{
		public Memory memory;
		public CPURegisters registers;
		public System.Timers.Timer mainTimer;
		static ushort startInstructionAddress = 0xFFFE; //Reset interrupt address
		ushort stackTopAddress = 0x0200 + 0x0800; //Stack size (0x800) plus stack end (0x200) - 2KB of RAM
		long cpuBusy;

		public CPUDebugMode debugMode;
		System.Collections.Generic.List<ushort> breakpoints; //debug - "breakpoints"
		System.Collections.Generic.List<Action<CPU>> actions; //debug - actions to perform at the end of every tick

		public CPU()
		{
			memory = new Memory();
			registers = new CPURegisters();
			breakpoints = new System.Collections.Generic.List<ushort>();
			actions = new System.Collections.Generic.List<Action<CPU>>();
			debugMode = CPUDebugMode.None;
			mainTimer = new System.Timers.Timer();
			mainTimer.Elapsed += tick;
			cpuBusy = 0;
		}

		public void setBreakpoint(ushort address)
		{
			breakpoints.Add(address);
		}

		public void setBlock(Action<CPU> block)
		{
			actions.Add(block);
		}

		public void executeInstruction(Instruction instruction)
		{
			registers.select[REG.PC].incrementBy(2);

			try
			{
				switch (instruction.type)
				{
					case InstructionType.SingleOperand:
						{
							runSingleOperandCmd(instruction);
							break;
						}
					case InstructionType.Jump:
						{
							runJMPCmd(instruction);
							break;
						}
					case InstructionType.TwoOperand:
						{
							runTwoOperandCmd(instruction);
							break;
						}
					default: break;
				}
			}
			catch (MSP430Exception exc)
			{
				Console.WriteLine("Fatal Error: {0}! CPU stopped", exc.Message);
				stop();
			}
		}

		public void runSingleOperandCmd(Instruction instruction)
		{
			ushort opcode = (ushort)((instruction.code >> 7) & 0x07); //Parse instruction
			ushort As = (ushort)((instruction.code >> 4) & 0x03);
			ushort dstRegister = (ushort)(instruction.code & 0x0F);
			ushort BW = (ushort)((instruction.code >> 6) & 0x01);

			ushort dstValue = 0xFFFF;
			ushort dstAddress = 0;
			bool registerMode = false;

			bool willWrite = false;
			bool willUpdateSR = true;

			if (opcode == Opcode.PUSH || opcode == Opcode.CALL) //If we have to deal with the stack
			{
				registers.select[REG.SP].incrementBy(-2);
			}

			switch (As) //Loading destination according to Addressing mode
			{
				case AddressingMode.Register:
					registerMode = true;
					break;

				case AddressingMode.Indexed:
					dstAddress = memory.readWord(registers.select[REG.PC].value).toShort();
					dstAddress = (ushort)((short)dstAddress + (short)registers.select[dstRegister].value);
					registers.select[REG.PC].incrementBy(2);
					break;

				case AddressingMode.IndirectRegister:
					dstAddress = registers.select[dstRegister].value;
					break;

				case AddressingMode.IndirectAutoincrement:
					dstAddress = registers.select[dstRegister].value;
					if (dstRegister == REG.PC)
					{
						registers.select[REG.PC].incrementBy(2);
					}
					else {
						registers.select[dstRegister].incrementBy((short)((BW == 0) ? 2 : 1));
					}

					break;

				default:
					break;
			}

			if (registerMode)
				dstValue = registers.select[dstRegister].value;
			else
			{
				dstValue = memory.readWord(dstAddress).toShort();
				//if (BW == 1) dstValue = (ushort)(dstValue >> 8); //DOUBLECHECK!
			}

			if (debugMode == CPUDebugMode.Verbose) Console.WriteLine("Single Operand Instruction: {0:X} with opcode: {1:X}", instruction.code, opcode);

			switch (opcode)
			{
				case Opcode.RRC: //Rotate right through carry
					{
						ushort nextC = (ushort)(dstValue & 0x1); //Carry is loaded from LSB
						dstValue = (ushort)(dstValue >> 1); //Rotate
						ushort MSB_position = (ushort)(1 << (15 - BW * 8));
						Register sr = registers.select[REG.SR];
						dstValue = (ushort)(dstValue | (sr.getC() > 0 ? MSB_position : 0)); //Load MSB from carry
						registers.select[REG.SR].setC(nextC); //Save new carry value
						registers.select[REG.SR].setV(0); //Reset overflow
						willWrite = true;
						break;
					}
				case Opcode.SWPB: //Swap bytes
					{
						dstValue = (ushort)((dstValue << 8) & 0xff00 + (dstValue >> 8) & 0x00ff); //Swap
						willWrite = true;
						willUpdateSR = false;
						break;
					}
				case Opcode.RRA: //Rotate right arithmetically
					{
						registers.select[REG.SR].setC((ushort)(dstValue & 0x1)); //Carry is loaded from LSB
						ushort MSB_position = (ushort)(1 << (15 - BW * 8));
						dstValue = (ushort)((dstValue & MSB_position) | dstValue >> 1); //Rotate & keep MSB
						registers.select[REG.SR].setV(0); //Reset overflow
						willWrite = true;
						break;
					}
				case Opcode.SXT: //Extend sign
					{
						if ((dstValue & 0x80) > 0) dstValue |= 0xFF00; //If sign bit is set
						else dstValue &= 0x00FF;
						registers.select[REG.SR].setV(0); //Reset overflow
						registers.select[REG.SR].setC(Convert.ToUInt16(dstValue != 0)); //Set carry if result's not zero
						willWrite = true;
						break;
					}
				case Opcode.PUSH: //Push word/byte onto stack
					{
						memory.write(dstValue, registers.select[REG.SP].value, BW); //src -> @SP
						willUpdateSR = false;
						break;
					}
				case Opcode.CALL: //Subroutine
					{
						memory.write(registers.select[REG.PC].value, registers.select[REG.SP].value, BW); //PC -> @SP
						registers.select[REG.PC].set(new WORD(dstValue)); //dst -> PC
						willUpdateSR = false;
						break;
					}
				case Opcode.RETI: //Return from interrupt
					{
						throw new MSP430Exception("NOT_IMPLEMENTED_EXC");
					}
				default:
					throw new MSP430Exception("UNKNOWN_OPCODE_EXC");
			}

			if (willWrite)
			{
				if (registerMode)
					registers.select[dstRegister].set(new WORD(dstValue));
				else
					memory.write(dstValue, dstAddress, BW);
			}

			if (willUpdateSR) registers.updateSR(dstValue, BW);
		}

		public void runTwoOperandCmd(Instruction instruction)
		{
			ushort opcode = (ushort)((instruction.code >> 12) & 0x0F); //Parse
			ushort As = (ushort)((instruction.code >> 4) & 0x03);
			ushort Ad = (ushort)((instruction.code >> 7) & 0x01);
			ushort dstRegister = (ushort)(instruction.code & 0x0F);
			ushort srcRegister = (ushort)((instruction.code >> 8) & 0x0F);
			ushort BW = (ushort)((instruction.code >> 6) & 0x01);

			if (debugMode == CPUDebugMode.Verbose) Console.WriteLine("Two Operand Instruction: {0:X} with opcode: {1:X}", instruction.code, opcode);

			ushort dstValue = 0;
			ushort dstAddress = 0xFFFF;
			ushort srcValue = 0;
			ushort srcAddress = 0xFFFF;

			bool registerMode = (Ad == 0);

			bool willWrite = false;
			bool willUpdateSR = true;

			//read
			if ((srcRegister == REG.CG1 && As > 1) || srcRegister == REG.CG2)
			{
				srcValue = registers.readRegister(srcRegister, As);
			}
			else
			{
				switch (As)
				{
					case AddressingMode.Register:
						srcValue = registers.readRegister(srcRegister, As);
						break;

					case AddressingMode.Indexed:
						srcAddress = memory.readWord(registers.select[REG.PC].value).toShort();
						srcAddress = (ushort)(srcAddress + (short)(registers.readRegister(srcRegister, BW)));
						registers.select[REG.PC].incrementBy(2);
						break;

					case AddressingMode.IndirectRegister:
						srcAddress = registers.readRegister(srcRegister, As);
						break;

					case AddressingMode.IndirectAutoincrement:
						srcAddress = registers.readRegister(srcRegister, As);

						if (srcRegister == REG.PC)
						{
							srcValue = memory.readWord(registers.select[REG.PC].value).toShort();
							registers.select[REG.PC].incrementBy(2);
						}
						else
						{
							registers.select[srcRegister].incrementBy((short)((BW == 0) ? 2 : 1));
						}

						break;

					default:
						break;
				}
			}

			if (registerMode)
			{
				if (opcode != Opcode.MOV)
					dstValue = registers.readRegister(dstRegister, As);
			}
			else {

				if (dstRegister == 2)
				{
					dstAddress = memory.readWord(registers.select[REG.PC].value).toShort();
				}
				else
				{
					dstAddress = memory.readWord(registers.select[REG.PC].value).toShort();
					dstAddress = (ushort)(dstAddress + (short)registers.readRegister(dstRegister, As));
				}

				if (opcode != Opcode.MOV)
				{
					dstValue = memory.readWord(dstAddress).toShort();
				}

				registers.select[REG.PC].incrementBy(2);
			}

			if (srcAddress != 0xFFFF)
			{
				srcValue = memory.readWord(srcAddress).toShort();
			}

			switch (opcode)
			{
				case Opcode.MOV: //src -> dst
					{
						dstValue = srcValue;
						willWrite = true;
						willUpdateSR = false;
						break;
					}
				case Opcode.ADD: //Add src to dst
					{
						ushort MSB_position = (ushort)(1 << (15 - BW * 8));
						ushort tmpValue = (ushort)((srcValue ^ dstValue) & MSB_position);
						dstValue = (ushort)(dstValue + srcValue);
						ushort b2 = (ushort)(BW == 0 ? 0xffff : 0xff);
						registers.select[REG.SR].setC(Convert.ToUInt16(dstValue > b2));
						registers.select[REG.SR].setV(Convert.ToUInt16(tmpValue == 0 && ((srcValue ^ dstValue) & MSB_position) > 0));
						willWrite = true;
						break;
					}
				case Opcode.ADDC: //Not implemented
					{
						throw new MSP430Exception("NOT_IMPLEMENTED_EXC");
					}
				case Opcode.SUBC: //Not implemented
					{
						throw new MSP430Exception("NOT_IMPLEMENTED_EXC");
					}
				case Opcode.SUB: //Subtract source from destination
					{
						srcValue = (ushort)((srcValue ^ 0xffff) & 0xffff);
						ushort MSB_position = (ushort)(1 << (15 - BW * 8));
						ushort tmpValue = (ushort)((srcValue ^ dstValue) & MSB_position);
						dstValue = (ushort)(dstValue + srcValue + 1);
						ushort b2 = (ushort)(BW == 0 ? 0xffff : 0xff);
						registers.select[REG.SR].setC(Convert.ToUInt16(dstValue > b2));
						registers.select[REG.SR].setV(Convert.ToUInt16(tmpValue == 0 && ((srcValue ^ dstValue) & MSB_position) > 0));
						willWrite = true;
						break;
					}
				case Opcode.CMP: //Compare src and dst
					{
						ushort MSB_position = (ushort)(1 << (15 - BW * 8));
						registers.select[REG.SR].setC(Convert.ToUInt16(dstValue >= srcValue));
						ushort tmpValue = (ushort)(dstValue - srcValue);
						bool arithmeticOverflow = ((srcValue ^ tmpValue) & MSB_position) == 0 && (((srcValue ^ dstValue) & MSB_position) != 0);
						registers.select[REG.SR].setV(Convert.ToUInt16(arithmeticOverflow));
						dstValue = tmpValue;
						break;
					}
				case Opcode.DADD: //Not implemented
					{
						throw new MSP430Exception("NOT_IMPLEMENTED_EXC");

						//willUpdateSR = true;
						//break;
					}
				case Opcode.BIT: //Not implemented
					{
						throw new MSP430Exception("NOT_IMPLEMENTED_EXC");
					}
				case Opcode.BIC: //Clear bits of dst
					{
						dstValue = (ushort)(~srcValue & dstValue);
						willWrite = true;
						willUpdateSR = false;
						break;
					}
				case Opcode.BIS: //src OR dst -> dst
					{
						dstValue = (ushort)(srcValue | dstValue);
						willWrite = true;
						willUpdateSR = false;
						break;
					}
				case Opcode.XOR: //src XOR dst -> dst
					{
						ushort MSB_position = (ushort)(1 << (15 - BW * 8));
						if ((srcValue & MSB_position) > 0 && (dstValue & MSB_position) > 0) registers.select[REG.SR].setV(1);
						dstValue = (ushort)(dstValue ^ srcValue);
						registers.select[REG.SR].setC(Convert.ToUInt16(dstValue != 0));
						willWrite = true;
						break;
					}
				case Opcode.AND: //src & dst -> dst
					{
						dstValue = (ushort)(srcValue & dstValue);
						if (dstValue != 0) registers.select[REG.SR].setC(1);
						registers.select[REG.SR].setV(0);
						willWrite = true;
						break;
					}
				default:
					throw new MSP430Exception("UNKNOWN_OPCODE_EXC");
			}

			if (willWrite)
			{
				if (registerMode)
					registers.select[dstRegister].set(new WORD(dstValue));
				else
					memory.write(dstValue, dstAddress, BW);
			}

			if (willUpdateSR) registers.updateSR(dstValue, BW);
		}

		public void runJMPCmd(Instruction instruction) //Conditional(or not) jumps
		{
			ushort opcode = (ushort)((instruction.code & 0x1C00) >> 10);
			short offset = (short)(instruction.code & 0x03FF);

			if ((offset & 0x0200) > 0) //Convert to signed if 'sign' bit is set (10-bit to 16-bit)
				offset = (short)(-1 ^ 0x03FF | offset);

			if (debugMode == CPUDebugMode.Verbose) Console.WriteLine("JMP Instruction: {0:X} with opcode: {1} with offset: {2:X} (PC+({3}))", instruction.code, Convert.ToString(opcode, 2).PadLeft(3, '0'), offset, (offset * 2 + 2));

			bool willJump = false;

			switch (opcode)
			{
				case Opcode.JNZ: //Jump if zero flag not set
					willJump = registers.select[REG.SR].getZ() == 0;
					break;

				case Opcode.JZ: //Jump if zero flag set
					willJump = registers.select[REG.SR].getZ() == 1;
					break;

				case Opcode.JNC: //Jump if carry flag not set
					willJump = registers.select[REG.SR].getC() == 0;
					break;

				case Opcode.JC: //Jump if carry flag set
					willJump = registers.select[REG.SR].getC() == 1;
					break;

				case Opcode.JN: //Jump if negative flag set
					willJump = registers.select[REG.SR].getN() == 1;
					break;

				case Opcode.JGE: //Jump if greater or equals
					willJump = (registers.select[REG.SR].getN() ^ registers.select[REG.SR].getV()) == 0;
					break;

				case Opcode.JL: //Jump if less
					willJump = (registers.select[REG.SR].getN() ^ registers.select[REG.SR].getV()) == 1;
					break;

				case Opcode.JMP: //Just do a jump
					willJump = true;
					break;

				default:
					throw new MSP430Exception("UNKNOWN_OPCODE_EXC");
			}

			if (willJump) registers.select[REG.PC].incrementBy((short)(offset * 2)); //jump!
		}

		public void tick(object sender, EventArgs e)
		{
			if (System.Threading.Interlocked.Read(ref cpuBusy) == 0) //Sync timer and CPU
			{
				System.Threading.Interlocked.Increment(ref cpuBusy);
				if (breakpoints.Contains(registers.select[REG.PC].value))
				{
					Console.WriteLine("[*] Breakpoint at 0x{0:X} reached.\nGoing to Step-by-step mode\n", registers.select[REG.PC].value);
					debugMode = CPUDebugMode.Verbose;
					mainTimer.Stop();
				}
				if (debugMode == CPUDebugMode.Verbose)
				{
					registers.printState();
				}

				var instruction = new Instruction(memory.readWord(registers.select[REG.PC].value));
				executeInstruction(instruction);

				foreach (Action<CPU> action in actions)
				{
					action(this);
				}
				System.Threading.Interlocked.Decrement(ref cpuBusy);
			}
		}

		public void init()
		{
			registers.select[REG.PC].set(memory.readWord(startInstructionAddress));
			registers.select[REG.SP].set(new WORD(stackTopAddress));
		}

		public void start()
		{
			mainTimer.Start();
		}

		public void stop()
		{
			mainTimer.Stop();
		}

		public bool loadFlashFromPath(string path) //Load ROM from file
		{
			if (!File.Exists(path)) return false;
			byte[] ROMdata = File.ReadAllBytes(path);
			memory.loadROM(ROMdata);
			return true;
		}
	}
}
