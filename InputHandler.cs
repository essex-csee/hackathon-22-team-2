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

    byte frameNum;
    Fobble.Inputs[] inputArray;
    List<FrameState> stateQueue;

    PacketPeerUDP udpPeer;
    Thread networkThread;
    
    bool inputRecieved;
    Fobble.Inputs currInput;
    Mutex inputRecievedMutex;
    
    string currIcon;
    Fobble.CardSlots? currSlot; 

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        statusLabel = GetParent().GetNode<Label>("NetStatusLabel");

        currInput = new Fobble.Inputs();
        inputArray = new Fobble.Inputs[256];
        stateQueue = new List<FrameState>(ROLLBACK);

        udpPeer = new PacketPeerUDP();
        
        inputRecieved = false;
        inputRecievedMutex = new Mutex();

        for (int i = 0; i < 256; i++)
        {
            inputArray[i] = new Fobble.Inputs();
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
        
        if (Fobble.Instance.gameStatus == Fobble.GameStatus.Playing)
        {
            inputRecievedMutex.Unlock();
            HandleInput();
            //Make a request for this frame
            //for (int i = 0; i < PACKET_AMOUNT; i++)
            //    udpPeer.PutPacket(new byte[] { 1, stateQueue[0].frame, (byte)(frameNum + INPUT_DELAY) });
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

        
        inputRecievedMutex.Lock();
        //Inputs made this frame
        currInput.localIcon = currIcon;
        currInput.localSlot = currSlot;

        if (currInput.LocalActive)
        {
            GD.Print("LOCAL");
            GD.Print(currInput.localIcon);
            GD.Print(currInput.localSlot);
            GD.Print(frameNum);
            GD.Print("-----");
        }
        if (currInput.NetActive)
        {
            GD.Print("NET");
            GD.Print(currInput.netIcon);
            GD.Print(currInput.netSlot);
            GD.Print(frameNum);
            GD.Print("-----");
        }
        inputRecievedMutex.Unlock();

        //Craft input recieved packet to send to other player
        byte[] packet = new byte[4];
        packet[0] = 0;

        packet[1] = (byte)(frameNum);
        packet[2] = currInput.Encoded[0];//Slot
        packet[3] = currInput.Encoded[1];//Icon

        //Send inputs
        for (int i = 0; i < PACKET_AMOUNT; i++)
            udpPeer.PutPacket(packet);

        preState = Fobble.Instance.GetGameState();
        Fobble.Instance.UpdateAll(preState, currInput);

        Fobble.Instance.UpdateUI();

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

                        inputRecievedMutex.Lock();

                        //Decode byte inputs
                        byte frame = result[1];
                        byte icon = result[3];
                        Fobble.CardSlots? slot = null;

                        if (result[2] != 255)
                            slot = (Fobble.CardSlots)result[2];

                        currInput.netIcon = icon == 255 ? null : Fobble.SYMBOLS[icon];
                        currInput.netSlot = slot;

                        newInput = true;
                        if (icon != 255)
                            GD.Print("new input  - frame: ", frame, " , icon: ",inputArray[frame].netIcon);
                        

                        inputRecievedMutex.Unlock();

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
