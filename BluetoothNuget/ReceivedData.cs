using System;
namespace BluetoothNuget
{
	public class ReceivedData
	{
		public byte[] data { get; set; }
		public TransmissionState state { get; set; }

		public ReceivedData()
		{
			this.data = new byte[] { };
			this.state = TransmissionState.NONE;
		}

		public ReceivedData(byte[] data, TransmissionState state)
		{
			this.data = data;
			this.state = state;
		}

		public override string ToString()
		{
			var stringa = "Data received: ";
			int i = 0;
			foreach (byte b in data)
			{
				stringa += " byte" + i + ": " + b.ToString()+" ";
				i++;
			}

			return stringa;
		}
	}
}
