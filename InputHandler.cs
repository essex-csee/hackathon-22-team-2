using Godot;
using System.Collections.Generic;

public class InputHandler : Node
{
    private struct FrameState
    {
        public Fobble.Inputs inputs;
        public Fobble.GameState gameState;
        public int frame;
    }

    const int INPUT_DELAY = 5;

    const int ROLLBACK = 7;

    //Number of packets to send when sending. In order to mitigate UDP loses.
    const int PACKET_AMOUNT = 5;
    const int DUPLICATE_AMOUNT = 5;

    int frameNum;
    Fobble.Inputs[] prevInputs;
    List<FrameState> stateQueue;

    PacketPeerUDP udpPeer;
    Thread networkThread;
    
    Fobble.Inputs[] inputArrivals;
    bool[] viableInputs;
    bool inputRecieved;
    Mutex inputArrayMutex;
    Mutex viableMutex;
    Mutex inputRecievedMutex;
    bool[] prevFrameArrival;
    
    string currIcon;
    Fobble.CardSlots? currSlot; 

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        prevInputs = new Fobble.Inputs[256];
        stateQueue = new List<FrameState>(ROLLBACK);

        for (int i = 0; i < ROLLBACK; i++)
        {
            stateQueue.Add(new FrameState());
        }

        udpPeer = new PacketPeerUDP();
        
        inputArrivals = new Fobble.Inputs[256];
        viableInputs = new bool[256];
        inputRecieved = false;
        inputArrayMutex = new Mutex();
        viableMutex = new Mutex();
        inputRecievedMutex = new Mutex();

        for (int i = 0; i < INPUT_DELAY; i++)
        {
            viableInputs[i] = true;
            inputArrivals[i] = new Fobble.Inputs();
            prevInputs[i] = new Fobble.Inputs();
        }

        GD.Print(udpPeer.Listen(42069, "*"));
        GD.Print(udpPeer.SetDestAddress("::1", 42069));

        networkThread = new Thread();
        networkThread.Start(this, "NetworkThreadInputs");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(float delta)
    {
        inputRecievedMutex.Lock();

        if (inputRecieved)
        {
            inputArrayMutex.Lock();

            if (viableInputs[(byte)frameNum])
            {
                inputArrayMutex.Unlock();
                inputRecievedMutex.Unlock();
                HandleInput();
            }
            else
            {
                //We don't have an input for this frame
                inputArrayMutex.Unlock();
                inputRecieved = false;
            }
        }

        inputRecievedMutex.Unlock();

        if (!inputRecieved && Fobble.Instance.gameStatus == Fobble.GameStatus.Playing)
        {
            //Make a request for this frame
            for (int i = 0; i < PACKET_AMOUNT; i++)
                udpPeer.PutPacket(new byte[] { 1, (byte)frameNum, (byte)(frameNum + INPUT_DELAY) });

            GD.Print("Waiting for net input " + frameNum);
        }
        else if (Fobble.Instance.gameStatus == Fobble.GameStatus.Waiting)
        {
            //Make a handshake
            for (int i = 0; i < PACKET_AMOUNT; i++)
                udpPeer.PutPacket(new byte[] { 2, 0 });
        }
    }

    public void InputMade(Fobble.CardSlots slot, string icon)
    {
        currSlot = slot;
        currIcon = icon;
    }

    public Fobble.Inputs GetInput(byte frame)
    {
        return prevInputs[frame];
    }

    private void HandleInput()
    {
        Fobble.GameState preState = Fobble.Instance.GetGameState();

        //Inputs made this frame
        Fobble.Inputs newInput =  new Fobble.Inputs
        {
            localIcon = currIcon,
            localSlot = currSlot
        };

        prevInputs[(byte)(frameNum + INPUT_DELAY)] = newInput;

        inputArrayMutex.Lock();

        Fobble.Inputs currInput = prevInputs[(byte)frameNum];
        currInput.netIcon = inputArrivals[(byte)frameNum]?.netIcon;
        currInput.netSlot = inputArrivals[(byte)frameNum]?.netSlot;

        //Craft input recieved packet to send to other player
        byte[] packet = new byte[1 + 3 * DUPLICATE_AMOUNT];
        packet[0] = 0;
        for (int i = 0; i < DUPLICATE_AMOUNT; i++)
        {
            Fobble.Inputs localInput = prevInputs[(byte)(frameNum + INPUT_DELAY - i)];
            packet[(i*3) + 1] = (byte)frameNum;
            packet[(i*3) + 2] = localInput.Encoded[0];
            packet[(i*3) + 3] = localInput.Encoded[1];
        }

        //Send inputs
        for (int i = 0; i < PACKET_AMOUNT; i++)
            udpPeer.PutPacket(packet);

        viableMutex.Lock();
        viableInputs[(byte)(frameNum + INPUT_DELAY)] = true;
        viableInputs[(byte)(frameNum - INPUT_DELAY)] = false;//Old input is no longer viable
        viableMutex.Unlock();

        //Dequeue the oldest state
        stateQueue.RemoveAt(0);

        //Queue the new state
        FrameState fs = new FrameState
        {
            frame = frameNum,
            inputs = currInput,
            gameState = Fobble.Instance.GetGameState()
        };
        stateQueue.Add(fs);

        frameNum++;
        currIcon = null;
        currSlot = null;
    }

    private void UpdateQueueInputs(byte frame, Fobble.Inputs inputs)
    {
        for (int i = 0; i < ROLLBACK; i++)
        {
            if ((byte)stateQueue[i].frame == frame)
            {
                FrameState state = stateQueue[i];
                state.inputs.netIcon = inputs.netIcon;
                state.inputs.netSlot = inputs.netSlot;
                stateQueue[i] = state;
            }
        }
    }

    private void NetworkThreadInputs(object data)
    {
        int packetIndex = 3;
        bool newInput = false;
        byte[] result = null;

        while (true)
        {
            inputRecievedMutex.Lock();

            if (Fobble.Instance.gameStatus == Fobble.GameStatus.End)
            {
                inputRecievedMutex.Unlock();
                return;
            }

            inputRecievedMutex.Unlock();

            result = udpPeer.GetPacket();

            if (result != null && result.Length >= 1)
            {
                // string strStatus = "";
                // foreach (byte b in result)
                // {
                //     strStatus += b + " ";
                // }
                // GD.Print(strStatus);

                byte status = result[0];
                switch (status)
                {
                    //Input recieved
                    case 0:
                    {
                        int numPackets = result[1];
                        if (numPackets > 0)
                        {
                            inputArrayMutex.Lock();
                            
                            while (packetIndex < result.Length)
                            {
                                byte frame = result[packetIndex - 2];
                                if (!viableInputs[frame])//If it's not a duplicate input
                                {
                                    //Decode byte inputs
                                    byte icon = result[packetIndex];
                                    Fobble.CardSlots? slot = null;

                                    if (result[packetIndex] != 255)
                                        slot = (Fobble.CardSlots)result[packetIndex - 1];

                                    Fobble.Inputs netInput = new Fobble.Inputs
                                    {
                                        netIcon = Fobble.SYMBOLS[icon],
                                        netSlot = slot
                                    };
                                    inputArrivals[frame] = netInput;
                                    newInput = true;

                                    UpdateQueueInputs(frame, netInput);
                                }
                                packetIndex += 2;
                            }
                        }
                        inputArrayMutex.Unlock();

                        if (newInput)
                        {
                            inputRecievedMutex.Lock();
                            inputRecieved = true;

                            if (Fobble.Instance.gameStatus == Fobble.GameStatus.Waiting)
                                Fobble.Instance.gameStatus = Fobble.GameStatus.Playing;
                            
                            inputRecievedMutex.Unlock();
                        }
                        newInput = false;
                        break;
                    }
                    //Input requested
                    case 1:
                    {
                        byte frameNum = result[1];
                        byte numPackets = 0;
                        List<byte> packetArr = new List<byte>();
                        packetArr.Add(0);

                        inputArrayMutex.Lock();
                        viableMutex.Lock();

                        //Send only the requested frame and recent frames 
                        while (frameNum != result[2])
                        {
                            //No viable input for this frame
                            if (!viableInputs[frameNum])
                                break;
                            
                            packetArr.Add(frameNum);
                            packetArr.AddRange(prevInputs[frameNum].Encoded);
                            frameNum++;
                            numPackets++;
                        }

                        inputArrayMutex.Unlock();
                        viableMutex.Unlock();

                        if (numPackets > 0)
                        {
                            //Send inputs
                            for (int i = 0; i < PACKET_AMOUNT; i++)
                                udpPeer.PutPacket(packetArr.ToArray());
                        }

                        break;
                    }
                    //Game start / handshake
                    case 2:
                    {
                        inputRecievedMutex.Lock();

                        if (Fobble.Instance.gameStatus == Fobble.GameStatus.Waiting)
                        {
                            Fobble.Instance.gameStatus = Fobble.GameStatus.Playing;
                            inputRecieved = true;
                            inputRecievedMutex.Unlock();
                        }
                        else
                        {
                            inputRecievedMutex.Unlock();
                            if (result[1] == 0)
                            {
                                //Send reply handshake
                                for (int i = 0; i < PACKET_AMOUNT; i++)
                                    udpPeer.PutPacket(new byte[]{ 2, 1 });
                            }
                        }
                        
                        break;
                    }
                    //Game end
                    case 3:
                    {
                        inputRecievedMutex.Lock();
                        Fobble.Instance.gameStatus = Fobble.GameStatus.End;
                        inputRecievedMutex.Unlock();
                        break;
                    }
                }
            }
        }
    }
}
