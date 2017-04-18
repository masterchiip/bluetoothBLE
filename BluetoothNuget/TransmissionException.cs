using System;
namespace BluetoothNuget
{
	public class TransmissionException : Exception
	{
		public TransmissionState State;

		public TransmissionException(TransmissionState state)
		{
			this.State = state;
		}
	}
}
