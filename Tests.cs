using System;
using msp430sim;

namespace msp430sim_tests
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			Console.WriteLine("MSP430sim Unit Tests");
			int totalErrors = 0;
			int seed = 1234;

			//Memory tests
			{
				Console.WriteLine("\nTesting Memory..");
				Random rand = new Random(seed);
				Memory mem = new Memory();
				ushort randomAddress = (ushort)rand.Next(0, 0xFFF0);

				//Read/Write WORD
				ushort randomVal = (ushort)rand.Next();
				mem.write(randomVal, randomAddress, 0);
				ushort readValue = mem.readWord(randomAddress).toShort();
				Console.WriteLine("LOG: input:0x{0:X} expected:0x{1:X} output:0x{2:X}", randomVal, randomVal, readValue);
				if (readValue == randomVal) Console.WriteLine("[+] Mem WORD read/write Test Passed");
				else { Console.WriteLine("[-] Mem WORD read/write Test Failed"); totalErrors++; }

				//Read/Write byte
				var randomBytes = new byte[1];
				rand.NextBytes(randomBytes);
				mem.writeByte(randomBytes[0], randomAddress);
				byte readByte = mem.readByte(randomAddress);
				Console.WriteLine("LOG: input:0x{0:X} expected:0x{1:X} output:0x{2:X}", randomBytes[0], randomBytes[0], readByte);
				if (readByte == randomBytes[0] && readByte == mem.data[randomAddress]) Console.WriteLine("[+] Mem BYTE read/write Test Passed");
				else { Console.WriteLine("[-] Mem BYTE read/write Test Failed"); totalErrors++; }
			}

			//Register tests
			{
				Console.WriteLine("\nTesting Register..");
				Random rand = new Random(seed);
				Register reg = new Register();
				var randomBytes = new byte[2];
				rand.NextBytes(randomBytes);
				WORD randomWord = new WORD();
				randomWord.low = randomBytes[0];
				randomWord.high = randomBytes[1];
				reg.set(randomWord);

				//Read/Write register test
				WORD readRegister = reg.get();
				Console.WriteLine("LOG: input:0x{0:X} expected:0x{1:X} output:0x{2:X}", randomWord.toShort(), randomWord.toShort(), readRegister.toShort());
				if (readRegister.toShort() == randomWord.toShort()) Console.WriteLine("[+] Register read/write Test Passed");
				else { Console.WriteLine("[-] Register read/write Test Failed"); totalErrors++; }

				//Status Register Flags test
				int errorCount = 0;
				reg.setC(0);
				if (reg.getC() != 0) errorCount++;
				reg.setC(1);
				if (reg.getC() != 1) errorCount++;
				reg.setV(0);
				if (reg.getV() != 0) errorCount++;
				reg.setV(1);
				if (reg.getV() != 1) errorCount++;
				reg.setN(0);
				if (reg.getN() != 0) errorCount++;
				reg.setN(1);
				if (reg.getN() != 1) errorCount++;
				reg.setZ(0);
				if (reg.getZ() != 0) errorCount++;
				reg.setZ(1);
				if (reg.getZ() != 1) errorCount++;

				if (errorCount == 0) Console.WriteLine("[+] Status Register Flags read/write Test Passed");
				else { Console.WriteLine("[-] Status Register Flags read/write Test Failed"); totalErrors += errorCount; }

				//Register value increment test
				reg.set(randomWord);
				reg.incrementBy(4);
				Console.WriteLine("LOG: input:0x{0:X} expected:0x{1:X} output:0x{2:X}", randomWord.toShort(), randomWord.toShort() + 4, reg.value);
				if (reg.value - randomWord.toShort() == 4) Console.WriteLine("[+] Register value increment Test Passed");
				else { Console.WriteLine("[-] Register value increment Test Failed"); totalErrors++; }
			}

			//CPURegisters Tests
			{
				Console.WriteLine("\nTesting CPURegisters..");
				Random rand = new Random(seed);
				CPURegisters regs = new CPURegisters();
				ushort regNumber = (ushort)rand.Next(4, 15);
				var randomBytes = new byte[2];
				rand.NextBytes(randomBytes);
				WORD randomWord = new WORD();
				randomWord.low = randomBytes[0];
				randomWord.high = randomBytes[1];

				//Set-get register test
				regs.setRegister(regNumber, randomWord);
				Console.WriteLine("LOG: input:0x{0:X} expected:0x{1:X} output:0x{2:X}", randomWord.toShort(), randomWord.toShort(), regs.readRegister(regNumber, 0));
				if(regs.readRegister(regNumber, 0) == randomWord.toShort()) Console.WriteLine("[+] Read/write register Test Passed");
				else { Console.WriteLine("[-] Read/write register Test Failed"); totalErrors++; }

				//updateSR test (status register from dest value)
				int errorCount = 0;
				regs.updateSR(0, 1); //zero, non-negative, byte mode
				if (regs.select[REG.SR].getZ() != 1) errorCount++;
				if (regs.select[REG.SR].getN() != 0) errorCount++;
				regs.updateSR(0x00FF, 1); //non-zero, negative, byte mode
				if (regs.select[REG.SR].getZ() != 0) errorCount++;
				if (regs.select[REG.SR].getN() != 1) errorCount++;
				regs.updateSR(0, 0); //zero, non-negative, word mode
				if (regs.select[REG.SR].getZ() != 1) errorCount++;
				if (regs.select[REG.SR].getN() != 0) errorCount++;
				regs.updateSR(0xFFFF, 0); //non-zero, negative, word mode
				if (regs.select[REG.SR].getZ() != 0) errorCount++;
				if (regs.select[REG.SR].getN() != 1) errorCount++;

				if (errorCount == 0) Console.WriteLine("[+] UpdateSR Test Passed");
				else { Console.WriteLine("[-] UpdateSR Test Failed"); totalErrors += errorCount; }
			}

			//CPU Tests
			{
				Console.WriteLine("\nTesting CPU..");
				CPU cpu = new CPU();
				cpu.init();

				//Jumps tests
				int errorCount = 0;
				cpu.memory.write(0x2404, 0x1128, 0); //jz	$+10     	;abs 0x1132
				cpu.registers.select[REG.SR].setZ(1); //has to jump
				cpu.registers.select[REG.PC].set(new WORD(0x1128));
				cpu.tick(null, null);
				if (cpu.registers.select[REG.PC].value != 0x1132) errorCount++;

				cpu.registers.select[REG.SR].setZ(0); //no jump
				cpu.registers.select[REG.PC].set(new WORD(0x1128));
				cpu.tick(null, null);
				if (cpu.registers.select[REG.PC].value != 0x112A) errorCount++;

				cpu.memory.write(0x2803, 0x1340, 0); //jnc	$+8      	;abs 0x1348
				cpu.registers.select[REG.SR].setC(0); //has to jump
				cpu.registers.select[REG.PC].set(new WORD(0x1340));
				cpu.tick(null, null);
				if (cpu.registers.select[REG.PC].value != 0x1348) errorCount++;

				cpu.registers.select[REG.SR].setC(1); //no jump
				cpu.registers.select[REG.PC].set(new WORD(0x1340));
				cpu.tick(null, null);
				if (cpu.registers.select[REG.PC].value != 0x1342) errorCount++;

				if (errorCount == 0) Console.WriteLine("[+] Jumps Test Passed");
				else { Console.WriteLine("[-] Jumps Test Failed"); totalErrors += errorCount; }

				//Single Operand tests
				errorCount = 0;
				cpu.memory.write(0x12b0, 0x11a8, 0); //call	#4742		;#0x1286
				cpu.memory.write(0x1286, 0x11aa, 0);
				cpu.registers.select[REG.PC].set(new WORD(0x11a8));
				cpu.tick(null, null);
				if (cpu.registers.select[REG.PC].value != 0x1286) errorCount++;

				cpu.memory.write(0x120d, 0x1264, 0); //push r13;
				cpu.registers.select[REG.PC].set(new WORD(0x1264));
				cpu.registers.select[REG.R13].set(new WORD(0x1234));
				cpu.tick(null, null);
				if (cpu.memory.readWord(cpu.registers.select[REG.SP].value).toShort() != 0x1234) errorCount++;

				if (errorCount == 0) Console.WriteLine("[+] Single Operand Test Passed");
				else { Console.WriteLine("[-] Single Operand Test Failed"); totalErrors += errorCount; }

				//Two operand tests
				errorCount = 0;
				Random rand = new Random(seed);

				cpu.memory.write(0x4a0c, 0x11a6, 0); //mov	r10,	r12
				ushort randomVal = (ushort)rand.Next();
				cpu.registers.setRegister(REG.R10, randomVal);
				cpu.registers.setRegister(REG.PC, 0x11a6);
				cpu.tick(null, null);
				Console.WriteLine("LOG: input:0x{0:X} expected:0x{1:X} output:0x{2:X}", randomVal, randomVal, cpu.registers.select[REG.R12].value);
				if (cpu.registers.select[REG.R12].value != randomVal) errorCount++;

				cpu.memory.write(0x5e0f, 0x134a, 0); //add	r14,	r15
				ushort randomValA = (ushort)rand.Next(0, 0x1000);
				ushort randomValB = (ushort)rand.Next(0, 0x1000);
				cpu.registers.setRegister(REG.R15, randomValA);
				cpu.registers.setRegister(REG.R14, randomValB);
				cpu.registers.setRegister(REG.PC, 0x134a);
				cpu.tick(null, null);
				Console.WriteLine("LOG: input:0x{0:X}, 0x{1:X} expected:0x{2:X} output:0x{3:X}", randomValA, randomValB, (randomValA + randomValB), cpu.registers.readRegister(REG.R15, 0));
				if (cpu.registers.select[REG.R15].value != (randomValA + randomValB)) errorCount++;

				if (errorCount == 0) Console.WriteLine("[+] Two Operand Test Passed");
				else { Console.WriteLine("[-] Two Operand Test Failed"); totalErrors += errorCount; }
			}

			Console.WriteLine("\nTotal errors count = {0}\nTests {1}", totalErrors, totalErrors == 0 ? "passed" : "failed");
		}
	}
}
