using System;
using System.Collections.Generic;
using System.Text;

namespace BluetoothNuget
{
    public enum ModbusFrameType
    {
        RequestRead,
        RequestWrite,
        ResponseRequest,
		ResponseException
    }
}
