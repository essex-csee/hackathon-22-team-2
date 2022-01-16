using Godot;
using System;
using System.Collections.Generic;

public class Fobble : Node2D
{
    public static readonly string[][] BASE_DECK = new string[][] {
        new string[] {"Apple", "Beans", "Car", "Dog", "Earth", "Gold"},
        new string[] {"Apple", "Fish", "Helicopter", "IceCream", "Jellyfish", "King"},
        new string[] {"Apple", "Love", "Moon", "Nail", "Orange", "Panda"},
        new string[] {"Apple", "Question", "Rocket", "Snail", "Tree", "UFO"},
        new string[] {"Apple", "Volcano", "Windmill", "X", "Yellow", "Zeppelin"},
        new string[] {"Apple", "Arrow", "Bee", "Carrot", "Duck", "Eyes"},
        new string[] {"Beans", "Fish", "Love", "Question", "Volcano", "Arrow"},
        new string[] {"Beans", "Helicopter", "Moon", "Rocket", "Windmill", "Bee"},
        new string[] {"Beans", "IceCream", "Nail", "Snail", "X", "Carrot"},
        new string[] {"Beans", "Jellyfish", "Orange", "Tree", "Yellow", "Duck"},
        new string[] {"Beans", "King", "Panda", "UFO", "Zeppelin", "Eyes"},
        new string[] {"Car", "Fish", "Moon", "Snail", "Yellow", "Eyes"},
        new string[] {"Car", "Helicopter", "Nail", "Tree", "Zeppelin", "Arrow"},
        new string[] {"Car", "IceCream", "Orange", "UFO", "Volcano", "Bee"},
        new string[] {"Car", "Jellyfish", "Panda", "Question", "Windmill", "Carrot"},
        new string[] {"Car", "King", "Love", "Rocket", "X", "Duck"},
        new string[] {"Dog", "Fish", "Nail", "UFO", "Windmill", "Duck"},
        new string[] {"Dog", "Helicopter", "Orange", "Question", "X", "Eyes"},
        new string[] {"Dog", "IceCream", "Panda", "Rocket", "Yellow", "Arrow"},
        new string[] {"Dog", "Jellyfish", "Love", "Snail", "Zeppelin", "Bee"},
        new string[] {"Dog", "King", "Moon", "Tree", "Volcano", "Carrot"},
        new string[] {"Earth", "Fish", "Orange", "Rocket", "Zeppelin", "Carrot"},
        new string[] {"Earth", "Helicopter", "Panda", "Snail", "Volcano", "Duck"},
        new string[] {"Earth", "IceCream", "Love", "Tree", "Windmill", "Eyes"},
        new string[] {"Earth", "Jellyfish", "Moon", "UFO", "X", "Arrow"},
        new string[] {"Earth", "King", "Nail", "Question", "Yellow", "Bee"},
        new string[] {"Gold", "Fish", "Panda", "Tree", "X", "Bee"},
        new string[] {"Gold", "Helicopter", "Love", "UFO", "Yellow", "Carrot"},
        new string[] {"Gold", "IceCream", "Moon", "Question", "Zeppelin", "Duck"},
        new string[] {"Gold", "Jellyfish", "Nail", "Rocket", "Volcano", "Eyes"},
        new string[] {"Gold", "King", "Orange", "Snail", "Windmill", "Arrow"}
    };

    public static readonly string[] SYMBOLS = new string[]
    {
        "Apple", "Beans", "Car", "Dog", "Earth", "Gold",
        "Fish", "Helicopter", "IceCream", "Jellyfish", "King", "Love",
        "Moon", "Nail", "Orange", "Panda", "Question", "Rocket", 
        "Snail", "Tree", "UFO", "Volcano", "Windmill", "X",
        "Yellow", "Zeppelin", "Arrow", "Bee", "Carrot", "Duck", "Eyes"
    };

    private static Fobble instance;
    public static Fobble Instance
    {
        get { return instance; }
    }

    public byte[] deckOrder;
    int deckIndex = BASE_DECK.Length - 1;
    public GameStatus gameStatus;
    
    PackedScene cardScene;

    Node2D leftSlot;
    Node2D rightSlot;

    Card leftCard;
    Card rightCard;
    int leftCardInd;
    int rightCardInd;

    int themScore = 0;
    int meScore = 0;

    RichTextLabel themScoreLabel;
    RichTextLabel meScoreLabel;
    RichTextLabel deckCountLabel;
    RichTextLabel winMessage;
    RichTextLabel loseMessage;
    RichTextLabel resetMessage;

    AudioStreamPlayer winSound;
    AudioStreamPlayer drawSound;
    AudioStreamPlayer loseSound;
    AudioStreamPlayer winGameSound;
    AudioStreamPlayer drawGameSound;
    AudioStreamPlayer loseGameSound;

    InputHandler inputHandler;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        instance = this;

        cardScene = GD.Load<PackedScene>("res://Card.tscn");

        winSound = GetNode<AudioStreamPlayer>("WinSound");
        drawSound = GetNode<AudioStreamPlayer>("DrawSound");
        loseSound = GetNode<AudioStreamPlayer>("LoseSound");
        winGameSound = GetNode<AudioStreamPlayer>("WinGameSound");
        drawGameSound = GetNode<AudioStreamPlayer>("DrawGameSound");
        loseGameSound = GetNode<AudioStreamPlayer>("LoseGameSound");

        leftSlot = GetNode<Node2D>("CardSlotLeft");
        rightSlot = GetNode<Node2D>("CardSlotRight");

        themScoreLabel = GetNode<RichTextLabel>("Panel/ThemScore");
        meScoreLabel = GetNode<RichTextLabel>("Panel/MeScore");
        deckCountLabel = GetNode<RichTextLabel>("Panel/DeckCount");

        winMessage = GetNode<RichTextLabel>("WinMessage");
        loseMessage = GetNode<RichTextLabel>("LoseMessage");
        resetMessage = GetNode<RichTextLabel>("ResetMessage");
        
        inputHandler = GetNode<InputHandler>("InputHandler");
        gameStatus = GameStatus.Waiting;
    }

    public void StartUDPConnection(string address, int port)
    {
        inputHandler.StartUDPPeer(address, port);
    }

    public void InitFobble()
    {
        winMessage.Visible = false;
        loseMessage.Visible = false;
        resetMessage.Visible = false;
        leftSlot.Visible = true;
        rightSlot.Visible = true;

        themScore = 0;
        meScore = 0;

        deckIndex = BASE_DECK.Length - 1;

        DrawCard(CardSlots.Left);
        DrawCard(CardSlots.Right);

        UpdateScores();
    }

    public static byte[] CreateDeck()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Randomize();

        //Create the deck
        byte[] deck = new byte[BASE_DECK.Length];
        for (byte i = 0; i < BASE_DECK.Length; i++)
            deck[i] = i;

        //Shuffle
        int n = BASE_DECK.Length;
        while (n > 1)
        {
            n--;
            int k = rng.RandiRange(0, n);

            byte temp = deck[n];
            deck[n] = deck[k];
            deck[k] = temp;
        }

        return deck;
    }

    private bool initialised = false;
    public override void _Process(float delta)
    {
        leftSlot.Visible = gameStatus == GameStatus.Playing;
        rightSlot.Visible = gameStatus == GameStatus.Playing;

        if (gameStatus == GameStatus.Playing && !initialised)
        {
            initialised = true;
            Fobble.Instance.InitFobble();
        }
    }

    public bool DrawCard(CardSlots slot)
    {
        if (deckIndex == -1)
            return false;

        if (slot == CardSlots.Left)
        {
            leftCardInd = deckOrder[deckIndex--];
        }
        else
        {
            rightCardInd = deckOrder[deckIndex--];
        }

        return true;
    }

    private void _on_Card_IconSelected(Card card, string icon)
    {
        CardSlots slot = card == leftCard ? CardSlots.Left : CardSlots.Right;
        inputHandler.InputMade(slot, icon);
    }

    public void UpdateScores()
    {
        deckCountLabel.Text = (deckIndex + 1).ToString();
        meScoreLabel.Text = meScore.ToString();
        themScoreLabel.Text = themScore.ToString();
    }

    public void UpdateAll(Inputs input)
    {
        if (!input.LocalActive && !input.NetActive)
            return;

        bool localWon = input.LocalActive && Card.HasIcon(leftCardInd, input.localIcon) && Card.HasIcon(rightCardInd, input.localIcon);
        bool localLost = input.LocalActive && (!Card.HasIcon(leftCardInd, input.localIcon) || !Card.HasIcon(rightCardInd, input.localIcon));
        bool netWon = input.NetActive && Card.HasIcon(leftCardInd, input.netIcon) && Card.HasIcon(rightCardInd, input.netIcon);
        bool netLost = input.NetActive && (!Card.HasIcon(leftCardInd, input.netIcon) || !Card.HasIcon(rightCardInd, input.netIcon));

        if (localWon && netWon)
        {
            GD.Print("draw");
            meScore++;
            themScore++;
            drawSound.Play(0);
        }
        else if (localWon || netLost)
        {
            GD.Print(localWon ? "we won" : "they lost");
            meScore++;
            winSound.Play(0);
        }
        else if (netWon || localLost)
        {
            GD.Print(netWon ? "they won" : "we lost");
            themScore++;
            loseSound.Play(0);
        }

        bool leftSlotUsed = (input.LocalActive && input.localSlot == CardSlots.Left) || (input.NetActive && input.netSlot == CardSlots.Left);
        bool rightSlotUsed = (input.LocalActive && input.localSlot == CardSlots.Right) || (input.NetActive && input.netSlot == CardSlots.Right);

        bool deckOut = false;
        if (leftSlotUsed)
            deckOut = !DrawCard(CardSlots.Left);
        if (rightSlotUsed && ! deckOut)
            deckOut = !DrawCard(CardSlots.Right);

        if (deckOut)
        {
            GG();
        }
    }

    public void UpdateUI()
    {
        if (deckOrder[leftCardInd] != leftCard?.cardIndex)
        {
            Card newCard = (Card)cardScene.Instance();
            if (leftCard != null)
                leftCard.CallDeferred("free");

            leftCard = newCard;
            leftSlot.AddChild(newCard);
            newCard.Owner = leftSlot;
            newCard.InitCard(deckOrder[leftCardInd]);
            newCard.Connect("IconSelected", this, "_on_Card_IconSelected");
        }

        if (deckOrder[rightCardInd] != rightCard?.cardIndex)
        {
            Card newCard = (Card)cardScene.Instance();
            if (rightCard != null)
                rightCard.CallDeferred("free");

            rightCard = newCard;
            rightSlot.AddChild(newCard);
            newCard.Owner = rightSlot;
            newCard.InitCard(deckOrder[rightCardInd]);
            newCard.Connect("IconSelected", this, "_on_Card_IconSelected");
        }

        UpdateScores();
    }

    public void GG()
    {
        gameStatus = GameStatus.End;
        winMessage.Visible = meScore > themScore;
        loseMessage.Visible = !(meScore > themScore);
        resetMessage.Visible = true;
        leftSlot.Visible = false;
        rightSlot.Visible = false;

        if (meScore == themScore)
        {
            drawGameSound.Play(0);
        }
        else if (meScore > themScore)
        {
            winGameSound.Play(0);
        }
        else
        {
            loseGameSound.Play(0);
        }
    }

    public enum CardSlots
    {
        Left = 0,
        Right = 1
    }

    public class Inputs
    {
        public string localIcon;
        public Fobble.CardSlots? localSlot;
        public string netIcon;
        public Fobble.CardSlots? netSlot;

        public bool LocalActive
        {
            get { return localIcon != null && localSlot != null; }
        }

        public bool NetActive
        {
            get { return netIcon != null && netSlot != null; }
        }

        public byte[] Encoded
        {
            get
            {
                return new byte[]
                {
                    localSlot == null ? (byte)255 : (byte)localSlot,
                    (byte)Array.IndexOf(SYMBOLS, localIcon)
                };
            }
        }
    }

    public enum GameStatus
    {
        Waiting = 0,
        Playing = 1,
        End = 2
    }
}
