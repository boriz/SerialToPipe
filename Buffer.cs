using System;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// Buffer class to hold a packet
/// </summary>
public class Buffer
{
    public int PortNumber;
    public DateTime timestamp_dt;  // Current timestamp (datetime format)
    public List<byte> byte_buff; // Packt byte array

    public Buffer (List<byte> lst)
    {
        byte_buff = new List<byte>(lst);
    }
}
