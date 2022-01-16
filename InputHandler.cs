using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public class InputHandler : Node
{
    Label statusLabel;
    string status;

    const int INPUT_DELAY = 10;

    const int ROLLBACK = 10;

    //Number of packets to send when sending. In order to mitigate UDP loses.
    const int PACKET_AMOUNT = 1;

    byte frameNum;

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

        udpPeer = new PacketPeerUDP();
        
        inputRecieved = false;
        inputRecievedMutex = new Mutex();
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
            
            status = "";
        }
        else if (Fobble.Instance.gameStatus == Fobble.GameStatus.Waiting)
        {
            SendHandshake();

            inputRecievedMutex.Unlock();
            
            status = "Waiting to connect";
        }
        else if (Fobble.Instance.gameStatus == Fobble.GameStatus.End)
        {
            inputRecievedMutex.Unlock();
            HandleInput();
            
            status = "GG";
        }

        statusLabel.Text = status;
    }

    private void SendHandshake()
    {
        byte[] deckOrder = Fobble.Instance.deckOrder;
        if (deckOrder == null)
        {
            deckOrder = Fobble.CreateDeck();
            Fobble.Instance.deckOrder = deckOrder;
        }
        byte[] packet = new byte[2 + Fobble.BASE_DECK.Length];
        Array.Copy(deckOrder, 0, packet, 2, Fobble.BASE_DECK.Length);
        packet[0] = 2;
        packet[1] = 0;
        
        GD.Print("Putting handshake");
        //Make a handshake
        for (int i = 0; i < PACKET_AMOUNT; i++)
            udpPeer.PutPacket(packet);
    }

    public void InputMade(Fobble.CardSlots slot, string icon)
    {
        currSlot = slot;
        currIcon = icon;
    }

    private void HandleInput()
    {
        if (Fobble.Instance.gameStatus == Fobble.GameStatus.End)
        {
            GD.Print("End");
            if (Input.IsKeyPressed((int)KeyList.Space))
            {
                GD.Print("Reset");
                Fobble.Instance.deckOrder = Fobble.CreateDeck();
                
                Fobble.Instance.gameStatus = Fobble.GameStatus.Waiting;
            }
            return;
        }

        if (Fobble.Instance.gameStatus != Fobble.GameStatus.Playing)
            return;
        
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

        Fobble.Instance.UpdateAll(currInput);

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
                continue;
            }

            inputRecievedMutex.Unlock();

            result = udpPeer.GetPacket();

            if (result != null && result.Length >= 1)
            {
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
                        GD.Print("Handshake recieved");
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
