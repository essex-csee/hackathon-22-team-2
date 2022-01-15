using Godot;
using System;

public class Fobble : Node2D
{
    readonly string[][] BASE_DECK = new string[][] {
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

    string[][] deck;
    int deckIndex = 0;
    bool gameOver;
    
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

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        cardScene = GD.Load<PackedScene>("res://Card.tscn");

        leftSlot = GetNode<Node2D>("CardSlotLeft");
        rightSlot = GetNode<Node2D>("CardSlotRight");

        themScoreLabel = GetNode<RichTextLabel>("Panel/ThemScore");
        meScoreLabel = GetNode<RichTextLabel>("Panel/MeScore");
        deckCountLabel = GetNode<RichTextLabel>("Panel/DeckCount");

        winMessage = GetNode<RichTextLabel>("WinMessage");
        loseMessage = GetNode<RichTextLabel>("LoseMessage");
        resetMessage = GetNode<RichTextLabel>("ResetMessage");

        InitFobble();
    }

    public void InitFobble()
    {
        gameOver = false;

        winMessage.Visible = false;
        loseMessage.Visible = false;
        resetMessage.Visible = false;
        leftSlot.Visible = true;
        rightSlot.Visible = true;

        rng = new RandomNumberGenerator();
        rng.Randomize();

        themScore = 0;
        meScore = 0;

        deckIndex = BASE_DECK.Length - 1;

        //Create the deck
        deck = new string[BASE_DECK.Length][];
        Array.Copy(BASE_DECK, deck, BASE_DECK.Length);

        //Shuffle
        int n = BASE_DECK.Length;
        while (n > 1)
        {
            int k = rng.RandiRange(0, n);
            n--;

            var temp = deck[n];
            deck[n] = deck[k];
            deck[k] = temp;
        }

        DrawCard(Slots.Left);
        DrawCard(Slots.Right);

        UpdateScores();
    }

    private bool DrawCard(Slots slot)
    {
        if (deckIndex == -1)
            return false;

        string[] cardInfo = deck[deckIndex--];

        Card newCard = (Card)cardScene.Instance();
        
        if (slot == Slots.Left)
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
        newCard.InitCard(cardInfo);

        return true;
    }

    private void UpdateScores()
    {
        deckCountLabel.Text = (deckIndex + 1).ToString();
        meScoreLabel.Text = meScore.ToString();
        themScoreLabel.Text = themScore.ToString();
    }

    private void _on_Card_IconSelected(Card card, string icon)
    {
        if (gameOver)
            return;

        if (leftCard.HasIcon(icon) && rightCard.HasIcon(icon))
        {
            meScore++;
        }
        else
        {
            themScore++;
        }

        Slots slot = card == leftCard ? Slots.Left : Slots.Right;
        
        if (!DrawCard(slot))
        {
            //gg
            gameOver = true;
            winMessage.Visible = meScore > themScore;
            loseMessage.Visible = !(meScore > themScore);
            resetMessage.Visible = true;
            leftSlot.Visible = false;
            rightSlot.Visible = false;
        }

        UpdateScores();
    }

    public override void _Input(InputEvent ie)
    {
        //If mouse button click (but not held)
        if (ie is InputEventKey && ie.IsPressed() && !ie.IsEcho() && ie.AsText() == "Space" && gameOver)
        {
            InitFobble();
        }
    }

    private enum Slots
    {
        Left,
        Right
    }
}
