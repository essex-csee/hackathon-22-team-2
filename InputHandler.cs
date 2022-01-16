using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public class InputHandler : Node
{
    private struct FrameState
    {
        public bool actualInput;
        public Fobble.Inputs inputs;
        public Fobble.GameState gameState;
        public byte frame;
    }

    Label statusLabel;
    string status;

    const int INPUT_DELAY = 10;

    const int ROLLBACK = 10;

    //Number of packets to send when sending. In order to mitigate UDP loses.
    const int PACKET_AMOUNT = 1;
    const int DUPLICATE_AMOUNT = 5;

    byte frameNum;
    Fobble.Inputs[] inputArray;
    List<FrameState> stateQueue;

    PacketPeerUDP udpPeer;
    Thread networkThread;
    
    bool[] inputArrivals;
    bool[] prevFrameInputArrivals;
    bool[] viableInputs;
    bool inputRecieved;
    Mutex inputArrayMutex;
    Mutex viableMutex;
    Mutex inputRecievedMutex;
    
    string currIcon;
    Fobble.CardSlots? currSlot; 

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        statusLabel = GetParent().GetNode<Label>("NetStatusLabel");

        inputArray = new Fobble.Inputs[256];
        stateQueue = new List<FrameState>(ROLLBACK);

        udpPeer = new PacketPeerUDP();
        
        inputArrivals = new bool[256];
        prevFrameInputArrivals = new bool[256];
        viableInputs = new bool[256];
        inputRecieved = false;
        inputArrayMutex = new Mutex();
        viableMutex = new Mutex();
        inputRecievedMutex = new Mutex();

        for (int i = 0; i < 256; i++)
        {
            inputArray[i] = new Fobble.Inputs();
            viableInputs[i] = i < INPUT_DELAY;
            inputArrivals[i] = i < INPUT_DELAY;
        }

        for (int i = 0; i < ROLLBACK; i++)
        {
            stateQueue.Add(new FrameState()
            {
                inputs = new Fobble.Inputs(),
                frame = (byte)i,
                gameState = null,
                actualInput = true
            });
            prevFrameInputArrivals[i] = true;
        }
    }

    public void StartUDPPeer(string address, int port = 42069)
    {
        GD.Print(udpPeer.Listen(port, "*"));
        GD.Print(udpPeer.SetDestAddress(address, port));

        networkThread = new Thread();
        networkThread.Start(this, "NetworkThreadInputs");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(float delta)
    {
        inputRecievedMutex.Lock();

        if (inputRecieved)
        {
            if (stateQueue[0].actualInput)
            {
                inputRecievedMutex.Unlock();
                HandleInput();
                status = "";
            }
            else
            {
                inputArrayMutex.Lock();
                if (inputArrivals[stateQueue[0].frame])
                {
                    inputArrayMutex.Unlock();
                    inputRecievedMutex.Unlock();
                    HandleInput();
                    status = "";
                }
                else
                {
                    inputArrayMutex.Unlock();
                    inputRecieved = false;
                    inputRecievedMutex.Unlock();

                    byte[] packet = { 1, (byte)stateQueue[0].frame, (byte)(frameNum + INPUT_DELAY) };
                    for (int i = 0; i < PACKET_AMOUNT; i++)
                        udpPeer.PutPacket(packet);

                    status = "Lag: Waiting for net input. Frame: " + frameNum;
                }
            }
        }
        
        if (!inputRecieved && Fobble.Instance.gameStatus == Fobble.GameStatus.Playing)
        {
            inputRecievedMutex.Unlock();
            //Make a request for this frame
            for (int i = 0; i < PACKET_AMOUNT; i++)
                udpPeer.PutPacket(new byte[] { 1, stateQueue[0].frame, (byte)(frameNum + INPUT_DELAY) });
        }
        else if (Fobble.Instance.gameStatus == Fobble.GameStatus.Waiting)
        {
            byte[] deckOrder = Fobble.Instance.deckOrder;
            if (deckOrder == null)
            {
                deckOrder = Fobble.CreateDeck();
                Fobble.Instance.deckOrder = deckOrder;
            }

            inputRecievedMutex.Unlock();
            byte[] packet = new byte[2 + Fobble.BASE_DECK.Length];
            Array.Copy(deckOrder, 0, packet, 2, Fobble.BASE_DECK.Length);
            packet[0] = 2;
            packet[1] = 0;
            
            //Make a handshake
            for (int i = 0; i < PACKET_AMOUNT; i++)
                udpPeer.PutPacket(packet);
            
            status = "Waiting to connect";
        }

        statusLabel.Text = status;
    }

    public void InputMade(Fobble.CardSlots slot, string icon)
    {
        currSlot = slot;
        currIcon = icon;
    }

    public Fobble.Inputs GetInput(byte frame)
    {
        return inputArray[frame];
    }

    private void HandleInput()
    {
        if (Fobble.Instance.gameStatus != Fobble.GameStatus.Playing)
            return;

        Fobble.GameState preState = null;

        //Inputs made this frame
        Fobble.Inputs newInput =  new Fobble.Inputs
        {
            localIcon = currIcon,
            localSlot = currSlot
        };

        inputArray[(byte)(frameNum + INPUT_DELAY)] = newInput;

        inputArrayMutex.Lock();

        inputArray[(byte)(frameNum + INPUT_DELAY)].localIcon = currIcon;
        inputArray[(byte)(frameNum + INPUT_DELAY)].localSlot = currSlot;

        //Fobble.Inputs currInput = inputArray[(byte)frameNum];
        //currInput.netIcon = inputArrivals[(byte)frameNum]?.netIcon;
        //currInput.netSlot = inputArrivals[(byte)frameNum]?.netSlot;

        //Craft input recieved packet to send to other player
        byte[] packet = new byte[1 + 3 * DUPLICATE_AMOUNT];
        packet[0] = 0;
        int inputCount = 0;
        for (int i = 0; i < DUPLICATE_AMOUNT; i++)
        {
            Fobble.Inputs localInput = inputArray[(byte)(frameNum + INPUT_DELAY - i)];
            if (!localInput.LocalActive)
                continue;

            packet[(inputCount*3) + 1] = (byte)(frameNum + INPUT_DELAY - i);
            packet[(inputCount*3) + 2] = localInput.Encoded[0];//Slot
            packet[(inputCount*3) + 3] = localInput.Encoded[1];//Icon
            inputCount++;
        }

        //Send inputs
        if (inputCount > 0)
        {
            byte[] finalPacket = new byte[1 + 3 * inputCount];
            Array.Copy(packet, finalPacket, 1 + 3 * inputCount);
            for (int i = 0; i < PACKET_AMOUNT; i++)
                udpPeer.PutPacket(finalPacket);
        }

        Fobble.Inputs currInput = inputArray[(byte)frameNum];

        //Make a guess input if there is none available
        bool actualInput = true;
        if (!inputArrivals[(byte)frameNum])
        {
            //Make no actual guess lol
            currInput.netIcon = null;
            currInput.netSlot = null;
            actualInput = false;
        }

        //Reset arrival bool for old frame
        inputArrivals[(byte)(frameNum + INPUT_DELAY*2 + ROLLBACK + 1)] = false;

        List<bool> currFrameArrivalsLst = new List<bool>();
        List<Fobble.Inputs> pastActualInputs = new List<Fobble.Inputs>();
        bool currFrameArrival = inputArrivals[(byte)frameNum];
        for (int i = 1; i < ROLLBACK + 1; i++)
        {
            currFrameArrivalsLst.Insert(0, inputArrivals[(byte)(frameNum - i)]);
            if (inputArrivals[(byte)(frameNum - i)] != prevFrameInputArrivals[ROLLBACK - i])
            {
                pastActualInputs.Insert(0, inputArray[(byte)(frameNum - i)]);
            }
        }

        inputArrayMutex.Unlock();

        viableMutex.Lock();
        viableInputs[(byte)(frameNum + INPUT_DELAY)] = true;
        viableInputs[(byte)(frameNum - INPUT_DELAY - ROLLBACK * 2)] = false;//Old input is no longer viable
        viableMutex.Unlock();

        bool startRollback = false;
        if (pastActualInputs.Count != 0)
        {
            Fobble.Inputs newPastActualInput = null;

            for (int i = 0; i < stateQueue.Count; i++)
            {
                FrameState state = stateQueue[i];

                //If we can replace the guess input
                if (!prevFrameInputArrivals[i] && currFrameArrivalsLst[i])
                {
                    newPastActualInput = pastActualInputs[0];
                    pastActualInputs.RemoveAt(0);

                    state.inputs = newPastActualInput;
                    if (state.frame == inputFrame)
                    {
                        GD.Print("Input frame ", inputFrame, " net active ", state.inputs.NetActive);
                    }

                    if (!startRollback && (state.inputs.LocalActive || state.inputs.NetActive))
                    {
                        Fobble.Instance.ResetGameState(state.gameState);
                        startRollback = true;
                        GD.Print("Rolling back");
                    }

                    state.actualInput = true;
                }

                if (startRollback)
                {
                    preState = Fobble.Instance.GetGameState();
                    
                    Fobble.Instance.UpdateAll(preState, state.inputs);
                    state.gameState = Fobble.Instance.GetGameState();
                }
            }
        }

        preState = Fobble.Instance.GetGameState();
        Fobble.Instance.UpdateAll(preState, currInput);

        Fobble.Instance.UpdateUI();

        //Dequeue the oldest state
        stateQueue.RemoveAt(0);

        //Queue the new state
        FrameState fs = new FrameState
        {
            frame = frameNum,
            inputs = currInput,
            gameState = preState,
            actualInput = actualInput
        };
        stateQueue.Add(fs);

        currFrameArrivalsLst.RemoveAt(0);
        currFrameArrivalsLst.Insert(currFrameArrivalsLst.Count-1, currFrameArrival);
        prevFrameInputArrivals = currFrameArrivalsLst.ToArray();

        frameNum++;
        currIcon = null;
        currSlot = null;
    }

    private void NetworkThreadInputs(object data)
    {
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
                        //0FSIFSIFSI...

                        inputArrayMutex.Lock();

                        for (int packetIndex = 3; packetIndex < result.Length; packetIndex += 3)
                        {
                            //Decode byte inputs
                            byte frame = result[packetIndex - 2];
                            byte icon = result[packetIndex];
                            if (!inputArrivals[frame])//If it's not a valid input
                            {
                                Fobble.CardSlots? slot = null;

                                if (result[packetIndex - 1] != 255)
                                    slot = (Fobble.CardSlots)result[((byte)packetIndex) - 1];

                                inputArray[frame].netIcon = icon == 255 ? null : Fobble.SYMBOLS[icon];
                                inputArray[frame].netSlot = slot;

                                inputArrivals[frame] = true;
                                newInput = true;
                                inputFrame = frame;
                                if (icon != 255)
                                    GD.Print("new input  - frame: ", frame, " , icon: ",inputArray[frame].netIcon);
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
                            packetArr.AddRange(inputArray[frameNum].Encoded);
                            frameNum++;
                        }

                        inputArrayMutex.Unlock();
                        viableMutex.Unlock();

                        //Send inputs
                        for (int i = 0; i < PACKET_AMOUNT; i++)
                            udpPeer.PutPacket(packetArr.ToArray());

                        break;
                    }
                    //Game start / handshake
                    case 2:
                    {
                        inputRecievedMutex.Lock();

                        if (result[1] == 0)
                        {
                            inputRecievedMutex.Unlock();
                            byte[] deckOrder = new byte[Fobble.BASE_DECK.Length];
                            byte[] packet = new byte[2 + Fobble.BASE_DECK.Length];
                            Array.Copy(result, 2, deckOrder, 0, Fobble.BASE_DECK.Length);
                            Array.Copy(result, 2, packet, 2, Fobble.BASE_DECK.Length);

                            packet[0] = 2;
                            packet[1] = 1;
 
                            Fobble.Instance.deckOrder = deckOrder;

                            //Send reply handshake
                            for (int i = 0; i < PACKET_AMOUNT; i++)
                                udpPeer.PutPacket(packet);
                        }
                        else if (result[1] == 1)
                        {
                            inputRecievedMutex.Unlock();
                            byte[] deckOrder = new byte[Fobble.BASE_DECK.Length];
                            Array.Copy(result, 2, deckOrder, 0, Fobble.BASE_DECK.Length);

                            IStructuralEquatable se = deckOrder;
                            if (se.Equals(Fobble.Instance.deckOrder, StructuralComparisons.StructuralEqualityComparer))
                            {
                                Fobble.Instance.deckOrder = deckOrder;
                                byte[] packet = new byte[2];
                                packet[0] = 2;
                                packet[1] = 2;
    
                                Fobble.Instance.deckOrder = deckOrder;

                                //Send reply handshake
                                for (int i = 0; i < PACKET_AMOUNT; i++)
                                    udpPeer.PutPacket(packet);
                            }
                        }
                        else if (result[1] == 2)
                        {
                            if (Fobble.Instance.gameStatus == Fobble.GameStatus.Waiting)
                            {
                                frameNum = 0;
                                Fobble.Instance.gameStatus = Fobble.GameStatus.Playing;
                                inputRecieved = true;

                                byte[] packet = new byte[2];
                                packet[0] = 2;
                                packet[1] = 2;

                                //Send reply handshake
                                for (int i = 0; i < PACKET_AMOUNT; i++)
                                    udpPeer.PutPacket(packet);
                            }
                            inputRecievedMutex.Unlock();
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
