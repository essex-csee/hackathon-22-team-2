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

    int[] deckOrder;
    int deckIndex = 0;
    public GameStatus gameStatus;
    
    RandomNumberGenerator rng;
    PackedScene cardScene;

    Node2D leftSlot;
    Node2D rightSlot;

    Card leftCard;
    Card rightCard;

    int themScore = 0;
    int meScore = 0;

    RichTextLabel themScoreLabel;
    RichTextLabel meScoreLabel;
    RichTextLabel deckCountLabel;
    RichTextLabel winMessage;
    RichTextLabel loseMessage;
    RichTextLabel resetMessage;

    InputHandler inputHandler;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        instance = this;

        cardScene = GD.Load<PackedScene>("res://Card.tscn");

        leftSlot = GetNode<Node2D>("CardSlotLeft");
        rightSlot = GetNode<Node2D>("CardSlotRight");

        themScoreLabel = GetNode<RichTextLabel>("Panel/ThemScore");
        meScoreLabel = GetNode<RichTextLabel>("Panel/MeScore");
        deckCountLabel = GetNode<RichTextLabel>("Panel/DeckCount");

        winMessage = GetNode<RichTextLabel>("WinMessage");
        loseMessage = GetNode<RichTextLabel>("LoseMessage");
        resetMessage = GetNode<RichTextLabel>("ResetMessage");
        
        inputHandler = GetNode<InputHandler>("InputHandler");

        InitFobble();
    }

    public void InitFobble()
    {
        gameStatus = GameStatus.Waiting;

        winMessage.Visible = false;
        loseMessage.Visible = false;
        resetMessage.Visible = false;
        leftSlot.Visible = false;
        rightSlot.Visible = false;

        rng = new RandomNumberGenerator();
        rng.Randomize();

        themScore = 0;
        meScore = 0;

        deckIndex = BASE_DECK.Length - 1;

        //Create the deck
        deckOrder = new int[BASE_DECK.Length];
        for (int i = 0; i < deckOrder.Length; i++)
            deckOrder[i] = i;

        //Shuffle
        int n = BASE_DECK.Length-1;
        while (n > 1)
        {
            int k = rng.RandiRange(0, n);
            n--;

            var temp = deckOrder[n];
            deckOrder[n] = deckOrder[k];
            deckOrder[k] = temp;
        }

        DrawCard(CardSlots.Left);
        DrawCard(CardSlots.Right);

        UpdateScores();
    }

    public override void _Process(float delta)
    {
        leftSlot.Visible = gameStatus == GameStatus.Playing;
        rightSlot.Visible = gameStatus == GameStatus.Playing;
    }

    public bool DrawCard(CardSlots slot)
    {
        if (deckIndex == -1)
            return false;

        Card newCard = (Card)cardScene.Instance();
        
        if (slot == CardSlots.Left)
        {
            if (leftCard != null)
                leftCard.CallDeferred("free");

            leftCard = newCard;
            leftSlot.AddChild(newCard);
            newCard.Owner = leftSlot;
        }
        else
        {
            if (rightCard != null)
                rightCard.CallDeferred("free");

            rightCard = newCard;
            rightSlot.AddChild(newCard);
            newCard.Owner = rightSlot;
        }

        newCard.Connect("IconSelected", this, "_on_Card_IconSelected");
        newCard.InitCard(deckOrder[deckIndex--]);

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

    public void UpdateInput(Inputs input)
    {
        bool localWon = input.LocalActive && leftCard.HasIcon(input.localIcon) && rightCard.HasIcon(input.localIcon);
        bool netWon = input.NetActive && leftCard.HasIcon(input.netIcon) && rightCard.HasIcon(input.netIcon);

        if (localWon && !netWon)
        {
            meScore++;
            
            if (!DrawCard(input.localSlot.Value))
                GG();
        }
        else if (netWon && !localWon)
        {
            themScore++;
            
            if (!DrawCard(input.netSlot.Value))
                GG();
        }
        else if (localWon && netWon)
        {
            themScore++;
            meScore++;

            if (input.localSlot == input.netSlot)
            {
                if (!DrawCard(input.localSlot.Value))
                    GG();
            }
            else
            {
                if (!DrawCard(input.localSlot.Value))
                    GG();
                if (!DrawCard(input.netSlot.Value))
                    GG();
            }
        }

        UpdateScores();
    }

    public void ResetGameState(GameState oldState)
    {
        gameStatus = oldState.status;
        deckOrder = oldState.deckOrder;
        deckIndex = oldState.deckIndex;
        meScore = oldState.playerOnePoints;
        themScore = oldState.playerTwoPoints;
    }

    public GameState GetGameState()
    {
        GameState state = new GameState
        {
            deckIndex = deckIndex,
            deckOrder = deckOrder,
            status = gameStatus,
            playerOnePoints = meScore,
            playerTwoPoints = themScore
        };

        return state;
    }

    public void GG()
    {
        gameStatus = GameStatus.End;
        winMessage.Visible = meScore > themScore;
        loseMessage.Visible = !(meScore > themScore);
        resetMessage.Visible = true;
        leftSlot.Visible = false;
        rightSlot.Visible = false;
    }

    public override void _Input(InputEvent ie)
    {
        //If mouse button click (but not held)
        if (ie is InputEventKey && ie.IsPressed() && !ie.IsEcho() && ie.AsText() == "Space" && gameStatus == GameStatus.End)
        {
            InitFobble();
        }
    }

    public enum CardSlots
    {
        Left = 0,
        Right = 1
    }
    public enum Players
    {
        One = 0,
        Two = 1
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

    public struct GameState
    {
        public Inputs input;
        public int[] deckOrder;
        public GameStatus status;
        public int playerOnePoints;
        public int playerTwoPoints;
        public int deckIndex;
        public int leftCardIndex;
        public int rightCardIndex;
        public int frameNum;
    }
}
