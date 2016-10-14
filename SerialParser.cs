using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.RegularExpressions;

public class SerialParser
{
    private const long k_ticks_per_us = 10;     // Tick is 100ns
    private const int k_max_buff_size = 1024;
    private const int k_max_queue_size = 256;
    
    private SerialPort _sp;
    private List<byte> _match;
    private List<byte> _buff;
    private DateTime _last_activity_dt;
    private long _timeout_us;
    private Queue<Buffer> _fifo;
    private int _port;


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="match"></param>
    /// <param name="timeout_us"></param>
    /// <param name="fifo"></param>
    public SerialParser (List<byte> match, long timeout_us, Queue<Buffer> fifo)
    {
        // Assign private vars
        if (match == null)
        {
            _match = new List<byte>();
        }
        else
        {
            _match = match;
        }
        _timeout_us = timeout_us;
        _fifo = fifo;
        _buff = new List<byte>();
    }


    /// <summary>
    /// Open serial port
    /// </summary>
    /// <param name="port"></param>
    /// <param name="baudrate"></param>
    /// <returns></returns>
    public bool Open(String port, int baudrate)
    {
        // Open serial port
        Console.Write("Opening Serial Port " + port + " ..");
        try
        {
            _sp = new SerialPort(port, baudrate, Parity.None, 8, StopBits.One);
            _sp.Parity = Parity.None;
            _sp.StopBits = StopBits.One;
            _sp.DataBits = 8;
            _sp.Handshake = Handshake.None;
            _sp.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            // Reset timeout
            _last_activity_dt = HightResTime.NowUTC().ToLocalTime();

            // Open port
            _sp.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Exception opening serial port " + port + " : " + ex.Message);
            return false;
        }
        Console.WriteLine(". Ok");

        // DEBUG
        //byte[] b = { 0x05, 0x64, 0x08, 0xc4, 0x37, 0x00, 0x0a, 0x00, 0x3f, 0xd2, 0xc0, 0xca, 0x17, 0xb3, 0xd3};
        //PostToFIFO(new List<byte>(b));
        //_buff.Clear();

        // Figure out com port number        
        Match m = Regex.Match(_sp.PortName, @"\d+");
        if (m.Length <= 0 || !int.TryParse(m.Value, out _port))
        {
            // Can't parse it, assign defult port
            _port = 999;
        }

        return true;
    }


    /// <summary>
    /// Close serial port
    /// </summary>
    public void Close()
    {
        _sp.Close();
    }


    /// <summary>
    /// Call this function periodically to flush packet on timeout
    /// </summary>
    public void Run()
    {
        // TODO: Add locking?
        // Check for timeout
        if (((DateTime.Now - _last_activity_dt).Ticks / k_ticks_per_us) > _timeout_us)
        {
            // Timeout!
            _last_activity_dt = HightResTime.NowUTC().ToLocalTime();
            PostToFIFO(_buff);
        }
    }


    // ============================================================
    // Private functions
    // ============================================================

    /// <summary>
    /// Serial port receive data interrupt
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void DataReceivedHandler(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
    {
        // Check for timeout
        if ( ((DateTime.Now - _last_activity_dt).Ticks / k_ticks_per_us) > _timeout_us)
        {
            _last_activity_dt = HightResTime.NowUTC().ToLocalTime();
            // Timeout!
            PostToFIFO(_buff);
        }            

        while (_sp.BytesToRead > 0)
        {
            // Got something
            // Add to buffer
            _last_activity_dt = HightResTime.NowUTC().ToLocalTime();
            byte b = (byte)_sp.ReadByte();
            _buff.Add(b);

            // Does it match the pattern?
            if (_match.Count > 0 && _buff.Count > _match.Count)
            {
                bool match_found = true;
                int start = _buff.IndexOf(_match[0]);
                if (start >= 0 && _buff.Count >= (start + _match.Count))
                {
                    for (int i = 0; i < _match.Count; i++)
                    {
                        if (_buff[start + i] != _match[i])
                        {
                            match_found = false;
                            break;
                        }
                    }
                }
                else
                {
                    match_found = false;
                }

                // Found match?
                if (match_found)
                {                    
                    List<byte> lst = _buff.GetRange(0, start);
                    PostToFIFO(lst);
                    _buff.RemoveRange(0, start);
                }
            }

            // Overfilling buffer?
            if (_buff.Count > k_max_buff_size)
            {
                PostToFIFO(_buff);
            }
        }
    }


    /// <summary>
    /// Post buffer to FIFO
    /// </summary>
    /// <param name="lst"></param>
    /// <param name="timestamp_us"></param>
    private void PostToFIFO (List<byte> lst)
    {
        if(lst.Count <= 0)
        {
            return;
        }

        // Create new buffer
        Buffer bf = new Buffer(lst);
        bf.timestamp_dt = HightResTime.NowUTC().ToLocalTime();                
        bf.PortNumber = _port;
        // Be sure we are not overfilling queue
        if (_fifo.Count <= k_max_queue_size)
        {
            PrintArray(bf.byte_buff);
            _fifo.Enqueue(bf);
            lst.Clear();
        }
    }


    /// <summary>
    /// Print array to the console
    /// </summary>
    /// <param name="lst"></param>
    private void PrintArray(List<byte> lst)
    {
        Console.Write(String.Format("Port {0}; Len {1}; Message: ", _sp.PortName, lst.Count));
        for (int i = 0; i < lst.Count; i++)
        {
            Console.Write(String.Format("{0,2:X} ", lst[i]));
        }
        Console.WriteLine();
    }

}
