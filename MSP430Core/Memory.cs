
namespace msp430sim
{
	class Memory
	{
		const int memSize = 65536;
		public byte[] data;

		public Memory()
		{
			data = new byte[memSize];
		}

		public WORD readWord(ushort address)
		{
			var word = new WORD();
			word.low = data[address];
			word.high = data[address + 1];
			return word;
		}

		public byte readByte(ushort address)
		{
			return data[address];
		}

		public void writeWord(WORD value, ushort address)
		{
			data[address] = value.low;
			data[address + 1] = value.high;
		}

		public void writeByte(byte value, ushort address)
		{
			data[address] = value;
		}

		public void write(ushort value, ushort address, ushort BW)
		{
			if (BW == 0) //Word mode
				writeWord(new WORD(value), address);
			else
				writeByte(new WORD(value).low, address);
		}

		public void loadROM(byte[] ROMdata)
		{
			int dataLength = ROMdata.Length;
			System.Buffer.BlockCopy(ROMdata, 0, data, memSize - dataLength, dataLength);
		}
	}
}
