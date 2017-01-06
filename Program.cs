using System;

namespace msp430sim
{
	class MSP430
	{
		public static void Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("error: no input files");
				return;
			}

			string binPath = args[0];

			var cpu = new CPU();
			if (cpu.loadFlashFromPath(binPath)) // load firmware
			{
				cpu.init();
				cpu.debugMode = CPUDebugMode.Verbose;

				cpu.mainTimer.Interval = 50;

				cpu.setBreakpoint(0x1242); // main() address
				cpu.setBlock((currentCpu) => { Console.WriteLine("PORT B state: {0}", Convert.ToString(currentCpu.memory.readByte(0x0029), 2).PadLeft(8, '0')); }); // monitoring PORTB state
				cpu.memory.writeWord(new WORD(0x0010), 0x1268); // "patch" blink delay

				cpu.start();

				while (true)
				{
					string cmd = Console.ReadLine();
					if (cmd == "x") break;
					else if (cmd == "b") cpu.stop();
					cpu.tick(null, null);
				}
				return;
			}
			Console.WriteLine("error: file doesn't exist");
		}
	}
}
