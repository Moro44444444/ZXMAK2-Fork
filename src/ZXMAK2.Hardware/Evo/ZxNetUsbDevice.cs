using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// ZXNetUSB revision C network half (EPM3128 firmware + WIZnet W5300).
    ///
    /// The physical board also contains an SL811HS USB controller.  USB is
    /// deliberately not exposed here: this device implements the documented
    /// W5300 I/O path used by the NedoOS driver and maps W5300 hardware
    /// sockets to ordinary host TCP/UDP sockets.  No raw adapter, TAP driver
    /// or administrator rights are required.
    /// </summary>
    public sealed class ZxNetUsbDevice : BusDeviceBase
    {
        private const ushort PortAddress = 0x81AB;
        private const ushort PortControl = 0x82AB;
        private const ushort PortInterrupt = 0x83AB;

        private byte m_addressHigh;
        private byte m_control;
        private byte m_interruptControl;
        private readonly W5300Core m_w5300 = new W5300Core();

        public ZxNetUsbDevice()
        {
            Name = "ZXNetUSB Rev.C";
            Description =
                "ZXNetUSB revision C Ethernet: WIZnet W5300 hardware " +
                "sockets through the host network. USB is not emulated.";
            Category = BusDeviceCategory.Other;
        }

        public override void BusInit(IBusManager bmgr)
        {
            bmgr.Events.SubscribeWrIo(0xFFFF, PortAddress, WriteAddress);
            bmgr.Events.SubscribeRdIo(0xFFFF, PortAddress, ReadAddress);
            bmgr.Events.SubscribeWrIo(0xFFFF, PortControl, WriteControl);
            bmgr.Events.SubscribeRdIo(0xFFFF, PortControl, ReadControl);
            bmgr.Events.SubscribeWrIo(0xFFFF, PortInterrupt, WriteInterrupt);
            bmgr.Events.SubscribeRdIo(0xFFFF, PortInterrupt, ReadInterrupt);

            // Revision C exposes W5300 in #00AB..#7FAB.  The two halves are
            // mirrors because A13..A8 supply W5300 A5..A0 while #81AB
            // supplies A9..A6.  NedoOS deliberately uses the lower mirror.
            bmgr.Events.SubscribeWrIo(0x80FF, 0x00AB, WriteW5300);
            bmgr.Events.SubscribeRdIo(0x80FF, 0x00AB, ReadW5300);
            bmgr.Events.SubscribeReset(ResetBoard);
        }

        public override void BusConnect()
        {
            ResetBoard();
        }

        public override void BusDisconnect()
        {
            m_w5300.CloseAll();
        }

        public override void ResetState()
        {
            ResetBoard();
        }

        private void ResetBoard()
        {
            m_addressHigh = 0;
            // W5300 reset released, I/O and memory windows disabled.
            m_control = 0;
            m_interruptControl = 0x10;
            m_w5300.Reset();
        }

        private void WriteAddress(ushort address, byte value,
            ref bool handled)
        {
            m_addressHigh = (byte)(value & 0x0F);
            handled = true;
        }

        private void ReadAddress(ushort address, ref byte value,
            ref bool handled)
        {
            value = m_addressHigh;
            handled = true;
        }

        private void WriteControl(ushort address, byte value,
            ref bool handled)
        {
            // Bits 2 and 4 select mutually exclusive memory/I/O windows.
            // Keep the last legal hardware state if software requests both.
            if ((value & 0x14) != 0x14)
                m_control = (byte)(value & 0x5F);
            handled = true;
        }

        private void ReadControl(ushort address, ref byte value,
            ref bool handled)
        {
            // USB power/role inputs are inactive; writable CPLD bits read
            // back because the NedoOS driver modifies the latch with RMW.
            value = m_control;
            handled = true;
        }

        private void WriteInterrupt(ushort address, byte value,
            ref bool handled)
        {
            bool wasReleased = (m_interruptControl & 0x10) != 0;
            m_interruptControl = (byte)(value & 0x7C);
            bool isReleased = (m_interruptControl & 0x10) != 0;
            if (wasReleased && !isReleased)
                m_w5300.Reset();
            handled = true;
        }

        private void ReadInterrupt(ushort address, ref byte value,
            ref bool handled)
        {
            // Bits 1/0 are the active interrupt inputs from USB/W5300.
            value = (byte)(m_interruptControl |
                (m_w5300.InterruptActive ? 0x01 : 0x00));
            handled = true;
        }

        private void WriteW5300(ushort address, byte value,
            ref bool handled)
        {
            if (!W5300IoEnabled)
                return;
            m_w5300.WriteByte(TranslateW5300Address(address), value);
            handled = true;
        }

        private void ReadW5300(ushort address, ref byte value,
            ref bool handled)
        {
            if (!W5300IoEnabled)
                return;
            value = m_w5300.ReadByte(TranslateW5300Address(address));
            handled = true;
        }

        private bool W5300IoEnabled
        {
            get
            {
                return (m_control & 0x10) != 0 &&
                    (m_control & 0x04) == 0 &&
                    (m_interruptControl & 0x10) != 0;
            }
        }

        private int TranslateW5300Address(ushort port)
        {
            int chipAddress = ((m_addressHigh & 0x0F) << 6) |
                ((port >> 8) & 0x3F);
            if ((m_control & 0x08) != 0)
                chipAddress ^= 1;
            return chipAddress;
        }

        private sealed class W5300Core
        {
            private const int RegisterCount = 0x400;
            private const int SocketBase = 0x200;
            private const int SocketSize = 0x40;
            private const int SocketCount = 8;
            private const int SocketBufferSize = 8192;

            private readonly byte[] m_registers = new byte[RegisterCount];
            private readonly W5300Socket[] m_sockets =
                new W5300Socket[SocketCount];

            public W5300Core()
            {
                for (int index = 0; index < m_sockets.Length; index++)
                    m_sockets[index] = new W5300Socket();
                Reset();
            }

            public bool InterruptActive
            {
                get
                {
                    PumpAll();
                    for (int index = 0; index < m_sockets.Length; index++)
                    {
                        if ((m_sockets[index].Interrupt &
                            m_registers[SocketBase + index * SocketSize + 5])
                            != 0)
                            return true;
                    }
                    return false;
                }
            }

            public void Reset()
            {
                CloseAll();
                Array.Clear(m_registers, 0, m_registers.Length);
                // Direct 8-bit bus, ordinary FIFO byte order.
                m_registers[0] = 0x00;
                m_registers[1] = 0x00;
                // W5300 defaults: 8 KiB TX and 8 KiB RX per socket.
                for (int index = 0; index < SocketCount; index++)
                {
                    m_registers[0x20 + index] = 8;
                    m_registers[0x28 + index] = 8;
                    m_sockets[index].ResetState();
                }
                m_registers[0x1C] = 0x07;
                m_registers[0x1D] = 0xD0;
                m_registers[0x1F] = 8;
                m_registers[0xFE] = 0x53;
                m_registers[0xFF] = 0x00;
            }

            public void CloseAll()
            {
                for (int index = 0; index < m_sockets.Length; index++)
                    m_sockets[index].Close();
            }

            public byte ReadByte(int address)
            {
                address &= RegisterCount - 1;
                if (address == 0xFE)
                    return 0x53;
                if (address == 0xFF)
                    return 0x00;

                int socketIndex;
                int socketOffset;
                if (!DecodeSocket(address, out socketIndex,
                    out socketOffset))
                    return m_registers[address];

                W5300Socket socket = m_sockets[socketIndex];
                socket.Pump();
                switch (socketOffset)
                {
                    case 0x02:
                    case 0x03:
                        return 0;
                    case 0x08:
                        return 0;
                    case 0x09:
                        return socket.Status;
                    case 0x06:
                        return 0;
                    case 0x07:
                        return socket.Interrupt;
                    case 0x24:
                    case 0x25:
                        return 0;
                    case 0x26:
                        return (byte)(socket.TxFree >> 8);
                    case 0x27:
                        return (byte)socket.TxFree;
                    case 0x28:
                    case 0x29:
                        return 0;
                    case 0x2A:
                        return (byte)(socket.RxAvailable >> 8);
                    case 0x2B:
                        return (byte)socket.RxAvailable;
                    case 0x30:
                    case 0x31:
                        return socket.ReadRxByte();
                    default:
                        return m_registers[address];
                }
            }

            public void WriteByte(int address, byte value)
            {
                address &= RegisterCount - 1;
                int socketIndex;
                int socketOffset;
                if (!DecodeSocket(address, out socketIndex,
                    out socketOffset))
                {
                    if (address == 0x02 || address == 0x03)
                        m_registers[address] &= (byte)~value;
                    else if (address != 0xFE && address != 0xFF)
                        m_registers[address] = value;
                    if (address == 1 && (value & 0x80) != 0)
                        Reset();
                    return;
                }

                W5300Socket socket = m_sockets[socketIndex];
                switch (socketOffset)
                {
                    case 0x03:
                        ExecuteCommand(socketIndex, value);
                        break;
                    case 0x07:
                        socket.Interrupt &= (byte)~value;
                        break;
                    case 0x2E:
                    case 0x2F:
                        socket.WriteTxByte(value);
                        break;
                    default:
                        m_registers[address] = value;
                        break;
                }
            }

            private void ExecuteCommand(int index, byte command)
            {
                W5300Socket socket = m_sockets[index];
                int offset = SocketBase + index * SocketSize;
                switch (command)
                {
                    case 0x01: // OPEN
                        socket.Open(m_registers[offset + 1],
                            ReadWord(offset + 0x0A));
                        break;
                    case 0x02: // LISTEN
                        socket.Listen(ReadWord(offset + 0x0A));
                        break;
                    case 0x04: // CONNECT
                        socket.Connect(ReadAddress(offset + 0x14),
                            ReadWord(offset + 0x12));
                        break;
                    case 0x08: // DISCON
                    case 0x10: // CLOSE
                        socket.Close();
                        break;
                    case 0x20: // SEND
                    case 0x21: // SEND_MAC
                        socket.Send(ReadAddress(offset + 0x14),
                            ReadWord(offset + 0x12),
                            ReadWord(offset + 0x22));
                        break;
                    case 0x40: // RECV
                        socket.ReleaseRxPacket();
                        break;
                }
            }

            private ushort ReadWord(int offset)
            {
                return (ushort)((m_registers[offset] << 8) |
                    m_registers[offset + 1]);
            }

            private IPAddress ReadAddress(int offset)
            {
                return new IPAddress(new byte[]
                {
                    m_registers[offset], m_registers[offset + 1],
                    m_registers[offset + 2], m_registers[offset + 3],
                });
            }

            private void PumpAll()
            {
                for (int index = 0; index < m_sockets.Length; index++)
                    m_sockets[index].Pump();
            }

            private static bool DecodeSocket(int address,
                out int socketIndex, out int socketOffset)
            {
                int relative = address - SocketBase;
                if (relative < 0)
                {
                    socketIndex = -1;
                    socketOffset = -1;
                    return false;
                }
                socketIndex = relative / SocketSize;
                socketOffset = relative % SocketSize;
                return socketIndex >= 0 && socketIndex < SocketCount;
            }

            private sealed class W5300Socket
            {
                private readonly List<byte> m_tx = new List<byte>();
                private readonly Queue<RxPacket> m_rx =
                    new Queue<RxPacket>();
                private Socket m_socket;
                private Socket m_listener;
                private bool m_connectPending;
                private bool m_dnsProxyPending;
                private byte m_mode;

                public W5300Socket()
                {
                    ResetState();
                }

                public byte Status { get; private set; }
                public byte Interrupt { get; set; }
                public int TxFree
                {
                    get { return Math.Max(0, SocketBufferSize - m_tx.Count); }
                }
                public int RxAvailable
                {
                    get
                    {
                        int count = 0;
                        foreach (RxPacket packet in m_rx)
                            count += packet.Remaining;
                        return Math.Min(0xFFFF, count);
                    }
                }

                public void ResetState()
                {
                    CloseNativeSockets();
                    m_tx.Clear();
                    m_rx.Clear();
                    m_connectPending = false;
                    m_dnsProxyPending = false;
                    m_mode = 0;
                    Status = 0x00;
                    Interrupt = 0;
                }

                public void Open(byte mode, int localPort)
                {
                    CloseNativeSockets();
                    m_tx.Clear();
                    m_rx.Clear();
                    Interrupt = 0;
                    m_mode = (byte)(mode & 0x0F);
                    try
                    {
                        if (m_mode == 1)
                        {
                            Status = 0x13;
                        }
                        else if (m_mode == 2)
                        {
                            m_socket = new Socket(AddressFamily.InterNetwork,
                                SocketType.Dgram, ProtocolType.Udp);
                            m_socket.Blocking = false;
                            m_socket.EnableBroadcast = true;
                            BindBestEffort(m_socket, localPort);
                            Status = 0x22;
                        }
                        else if (m_mode == 3)
                        {
                            Status = 0x32;
                        }
                        else
                        {
                            Status = 0x00;
                        }
                    }
                    catch (SocketException)
                    {
                        Close();
                        Interrupt |= 0x08;
                    }
                }

                public void Connect(IPAddress address, int port)
                {
                    if (m_mode != 1 || port == 0)
                    {
                        Interrupt |= 0x08;
                        return;
                    }
                    try
                    {
                        CloseNativeSockets();
                        m_socket = new Socket(AddressFamily.InterNetwork,
                            SocketType.Stream, ProtocolType.Tcp);
                        m_socket.Blocking = false;
                        Status = 0x15;
                        try
                        {
                            m_socket.Connect(new IPEndPoint(address, port));
                            Status = 0x17;
                            Interrupt |= 0x01;
                        }
                        catch (SocketException ex)
                        {
                            if (IsWouldBlock(ex.SocketErrorCode))
                                m_connectPending = true;
                            else
                                throw;
                        }
                    }
                    catch (SocketException)
                    {
                        CloseNativeSockets();
                        Status = 0x00;
                        Interrupt |= 0x08;
                    }
                }

                public void Listen(int localPort)
                {
                    if (m_mode != 1)
                        return;
                    try
                    {
                        CloseNativeSockets();
                        m_listener = new Socket(AddressFamily.InterNetwork,
                            SocketType.Stream, ProtocolType.Tcp);
                        m_listener.SetSocketOption(SocketOptionLevel.Socket,
                            SocketOptionName.ReuseAddress, true);
                        m_listener.Bind(new IPEndPoint(IPAddress.Loopback,
                            localPort == 0 ? 0 : localPort));
                        m_listener.Listen(1);
                        m_listener.Blocking = false;
                        Status = 0x14;
                    }
                    catch (SocketException)
                    {
                        CloseNativeSockets();
                        Status = 0x00;
                        Interrupt |= 0x08;
                    }
                }

                public void Close()
                {
                    CloseNativeSockets();
                    m_tx.Clear();
                    m_rx.Clear();
                    m_connectPending = false;
                    Status = 0x00;
                }

                public void WriteTxByte(byte value)
                {
                    if (m_tx.Count < SocketBufferSize)
                        m_tx.Add(value);
                }

                public byte ReadRxByte()
                {
                    if (m_rx.Count == 0)
                        return 0;
                    return m_rx.Peek().ReadByte();
                }

                public void ReleaseRxPacket()
                {
                    if (m_rx.Count != 0)
                        m_rx.Dequeue();
                    if (m_rx.Count == 0)
                        Interrupt &= 0xFB;
                }

                public void Send(IPAddress address, int port, int length)
                {
                    int count = Math.Min(Math.Min(length, m_tx.Count),
                        SocketBufferSize);
                    byte[] data = m_tx.GetRange(0, count).ToArray();
                    int consumed = Math.Min(m_tx.Count,
                        count + (count & 1));
                    m_tx.RemoveRange(0, consumed);
                    try
                    {
                        if (m_mode == 1 && m_socket != null &&
                            Status == 0x17)
                        {
                            int sent = 0;
                            while (sent < data.Length)
                            {
                                int part = m_socket.Send(data, sent,
                                    data.Length - sent, SocketFlags.None);
                                if (part <= 0)
                                    break;
                                sent += part;
                            }
                        }
                        else if (m_mode == 2)
                        {
                            if (port == 67 && IsDhcp(data))
                            {
                                EnqueueUdp(IPAddress.Parse("10.0.2.2"), 67,
                                    CreateDhcpReply(data));
                            }
                            else
                            {
                                EnsureUdpSocket();
                                IPAddress target = IsGuestDns(address, port)
                                    ? FindHostDns() : address;
                                m_dnsProxyPending =
                                    IsGuestDns(address, port);
                                m_socket.SendTo(data,
                                    new IPEndPoint(target, port));
                            }
                        }
                        else if (m_mode == 3)
                        {
                            EnqueueIpRaw(address, data);
                        }
                        Interrupt |= 0x10;
                    }
                    catch (SocketException)
                    {
                        Interrupt |= 0x08;
                    }
                }

                public void Pump()
                {
                    try
                    {
                        if (m_listener != null &&
                            m_listener.Poll(0, SelectMode.SelectRead))
                        {
                            m_socket = m_listener.Accept();
                            m_socket.Blocking = false;
                            m_listener.Close();
                            m_listener = null;
                            Status = 0x17;
                            Interrupt |= 0x01;
                        }

                        if (m_connectPending && m_socket != null)
                        {
                            if (m_socket.Poll(0, SelectMode.SelectError))
                            {
                                FailPendingConnection();
                            }
                            else if (m_socket.Poll(0, SelectMode.SelectWrite))
                            {
                                int error = (int)m_socket.GetSocketOption(
                                    SocketOptionLevel.Socket,
                                    SocketOptionName.Error);
                                if (error == 0)
                                {
                                    m_connectPending = false;
                                    Status = 0x17;
                                    Interrupt |= 0x01;
                                }
                                else
                                {
                                    FailPendingConnection();
                                }
                            }
                        }

                        if (m_socket == null || m_connectPending)
                            return;
                        if (m_mode == 1)
                            PumpTcp();
                        else if (m_mode == 2)
                            PumpUdp();
                    }
                    catch (SocketException ex)
                    {
                        if (!IsWouldBlock(ex.SocketErrorCode))
                        {
                            if (m_mode == 1)
                                Status = 0x1C;
                            Interrupt |= 0x02;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }

                private void PumpTcp()
                {
                    if (Status != 0x17 ||
                        !m_socket.Poll(0, SelectMode.SelectRead))
                        return;
                    byte[] buffer = new byte[4096];
                    int count = m_socket.Receive(buffer);
                    if (count == 0)
                    {
                        Status = 0x1C;
                        Interrupt |= 0x02;
                        return;
                    }
                    byte[] packet = new byte[2 + count + (count & 1)];
                    packet[0] = (byte)(count >> 8);
                    packet[1] = (byte)count;
                    Array.Copy(buffer, 0, packet, 2, count);
                    Enqueue(packet);
                }

                private void PumpUdp()
                {
                    int guard = 8;
                    while (guard-- > 0 && m_socket.Available > 0)
                    {
                        byte[] buffer = new byte[8192];
                        EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                        int count = m_socket.ReceiveFrom(buffer, ref source);
                        IPEndPoint endpoint = source as IPEndPoint;
                        if (endpoint != null)
                        {
                            byte[] payload = new byte[count];
                            Array.Copy(buffer, payload, count);
                            IPAddress sourceAddress = m_dnsProxyPending
                                ? IPAddress.Parse("10.0.2.3")
                                : endpoint.Address;
                            m_dnsProxyPending = false;
                            EnqueueUdp(sourceAddress, endpoint.Port, payload);
                        }
                    }
                }

                private void EnqueueUdp(IPAddress address, int port,
                    byte[] payload)
                {
                    byte[] ip = address.GetAddressBytes();
                    int count = payload.Length;
                    byte[] packet = new byte[8 + count + (count & 1)];
                    Array.Copy(ip, 0, packet, 0, 4);
                    packet[4] = (byte)(port >> 8);
                    packet[5] = (byte)port;
                    packet[6] = (byte)(count >> 8);
                    packet[7] = (byte)count;
                    Array.Copy(payload, 0, packet, 8, count);
                    Enqueue(packet);
                }

                private void EnqueueIpRaw(IPAddress address, byte[] payload)
                {
                    byte[] ip = address.GetAddressBytes();
                    int count = payload.Length;
                    byte[] packet = new byte[6 + count + (count & 1)];
                    Array.Copy(ip, 0, packet, 0, 4);
                    packet[4] = (byte)(count >> 8);
                    packet[5] = (byte)count;
                    Array.Copy(payload, 0, packet, 6, count);
                    // An echo request is enough for the NedoOS diagnostic.
                    if (count > 0 && packet[6] == 8)
                    {
                        packet[6] = 0;
                        if (count >= 4)
                        {
                            packet[8] = 0;
                            packet[9] = 0;
                            ushort checksum = InternetChecksum(packet, 6,
                                count);
                            packet[8] = (byte)(checksum >> 8);
                            packet[9] = (byte)checksum;
                        }
                    }
                    Enqueue(packet);
                }

                private void Enqueue(byte[] data)
                {
                    m_rx.Enqueue(new RxPacket(data));
                    Interrupt |= 0x04;
                }

                private void EnsureUdpSocket()
                {
                    if (m_socket != null)
                        return;
                    m_socket = new Socket(AddressFamily.InterNetwork,
                        SocketType.Dgram, ProtocolType.Udp);
                    m_socket.Blocking = false;
                    m_socket.EnableBroadcast = true;
                    BindBestEffort(m_socket, 0);
                }

                private static void BindBestEffort(Socket socket,
                    int localPort)
                {
                    try
                    {
                        socket.Bind(new IPEndPoint(IPAddress.Any,
                            localPort == 0 ? 0 : localPort));
                    }
                    catch (SocketException)
                    {
                        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                    }
                }

                private void FailPendingConnection()
                {
                    CloseNativeSockets();
                    m_connectPending = false;
                    Status = 0x00;
                    Interrupt |= 0x08;
                }

                private void CloseNativeSockets()
                {
                    CloseSocket(ref m_socket);
                    CloseSocket(ref m_listener);
                }

                private static void CloseSocket(ref Socket socket)
                {
                    if (socket == null)
                        return;
                    try { socket.Close(); }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) { }
                    socket = null;
                }

                private static bool IsWouldBlock(SocketError error)
                {
                    return error == SocketError.WouldBlock ||
                        error == SocketError.InProgress ||
                        error == SocketError.AlreadyInProgress;
                }

                private static bool IsGuestDns(IPAddress address, int port)
                {
                    return port == 53 &&
                        address.Equals(IPAddress.Parse("10.0.2.3"));
                }

                private static IPAddress FindHostDns()
                {
                    foreach (NetworkInterface network in
                        NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (network.OperationalStatus !=
                            OperationalStatus.Up)
                            continue;
                        foreach (IPAddress dns in network.GetIPProperties()
                            .DnsAddresses)
                        {
                            if (dns.AddressFamily ==
                                AddressFamily.InterNetwork)
                                return dns;
                        }
                    }
                    return IPAddress.Parse("8.8.8.8");
                }

                private static bool IsDhcp(byte[] data)
                {
                    return data.Length >= 240 && data[236] == 0x63 &&
                        data[237] == 0x82 && data[238] == 0x53 &&
                        data[239] == 0x63;
                }

                private static byte[] CreateDhcpReply(byte[] request)
                {
                    byte requestType = FindDhcpMessageType(request);
                    byte replyType = requestType == 1 ? (byte)2 : (byte)5;
                    byte[] reply = new byte[300];
                    reply[0] = 2;
                    reply[1] = request[1];
                    reply[2] = request[2];
                    reply[3] = 0;
                    Array.Copy(request, 4, reply, 4, 4);
                    reply[16] = 10; reply[17] = 0;
                    reply[18] = 2; reply[19] = 15;
                    reply[20] = 10; reply[21] = 0;
                    reply[22] = 2; reply[23] = 2;
                    Array.Copy(request, 28, reply, 28,
                        Math.Min(16, request.Length - 28));
                    reply[236] = 0x63; reply[237] = 0x82;
                    reply[238] = 0x53; reply[239] = 0x63;
                    int pos = 240;
                    AddDhcpOption(reply, ref pos, 53,
                        new byte[] { replyType });
                    AddDhcpOption(reply, ref pos, 54,
                        new byte[] { 10, 0, 2, 2 });
                    AddDhcpOption(reply, ref pos, 1,
                        new byte[] { 255, 255, 255, 0 });
                    AddDhcpOption(reply, ref pos, 3,
                        new byte[] { 10, 0, 2, 2 });
                    AddDhcpOption(reply, ref pos, 6,
                        new byte[] { 10, 0, 2, 3 });
                    AddDhcpOption(reply, ref pos, 51,
                        new byte[] { 0, 1, 0x51, 0x80 });
                    reply[pos++] = 255;
                    Array.Resize(ref reply, pos);
                    return reply;
                }

                private static byte FindDhcpMessageType(byte[] packet)
                {
                    int pos = 240;
                    while (pos + 1 < packet.Length)
                    {
                        int option = packet[pos++];
                        if (option == 255)
                            break;
                        if (option == 0)
                            continue;
                        int length = packet[pos++];
                        if (pos + length > packet.Length)
                            break;
                        if (option == 53 && length != 0)
                            return packet[pos];
                        pos += length;
                    }
                    return 0;
                }

                private static void AddDhcpOption(byte[] packet,
                    ref int position, byte option, byte[] value)
                {
                    packet[position++] = option;
                    packet[position++] = (byte)value.Length;
                    Array.Copy(value, 0, packet, position, value.Length);
                    position += value.Length;
                }

                private static ushort InternetChecksum(byte[] data,
                    int offset, int count)
                {
                    uint sum = 0;
                    int end = offset + count;
                    while (offset + 1 < end)
                    {
                        sum += (uint)((data[offset] << 8) |
                            data[offset + 1]);
                        offset += 2;
                    }
                    if (offset < end)
                        sum += (uint)(data[offset] << 8);
                    while ((sum >> 16) != 0)
                        sum = (sum & 0xFFFF) + (sum >> 16);
                    return (ushort)~sum;
                }

                private sealed class RxPacket
                {
                    private readonly byte[] m_data;
                    private int m_position;

                    public RxPacket(byte[] data)
                    {
                        m_data = data;
                    }

                    public int Remaining
                    {
                        get { return m_data.Length - m_position; }
                    }

                    public byte ReadByte()
                    {
                        if (m_position >= m_data.Length)
                            return 0;
                        return m_data[m_position++];
                    }
                }
            }
        }
    }
}
