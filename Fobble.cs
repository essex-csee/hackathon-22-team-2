using Godot;
using System;

public class Fobble : Node2D
{
    readonly string[][] BASE_DECK = new string[][] {
        new string[] {"Apple", "Beans", "Car", "Dog", "Earth"},
        new string[] {"Apple", "Gold", "Fish", "Helicopter", "IceCream"},
        new string[] {"Apple", "Jellyfish", "King", "Love", "Moon"},
        new string[] {"Apple", "Nail", "Orange", "Panda", "Question"},
        new string[] {"Apple", "Rocket", "Snail", "Tree", "UFO"},
        new string[] {"Beans", "Gold", "Jellyfish", "Nail", "Rocket"},
        new string[] {"Beans", "Fish", "King", "Orange", "Snail"},
        new string[] {"Beans", "Helicopter", "Love", "Panda", "Tree"},
        new string[] {"Beans", "IceCream", "Moon", "Question", "UFO"},
        new string[] {"Car", "Gold", "King", "Panda", "UFO"},
        new string[] {"Car", "Fish", "Love", "Question", "Rocket"},
        new string[] {"Car", "Helicopter", "Moon", "Nail", "Snail"},
        new string[] {"Car", "IceCream", "Jellyfish", "Orange", "Tree"},
        new string[] {"Dog", "Gold", "Love", "Nail", "Tree"},
        new string[] {"Dog", "Fish", "Moon", "Orange", "UFO"},
        new string[] {"Dog", "Helicopter", "Jellyfish", "Panda", "Rocket"},
        new string[] {"Dog", "IceCream", "King", "Question", "Snail"},
        new string[] {"Earth", "Gold", "Moon", "Panda", "Snail"},
        new string[] {"Earth", "Fish", "Jellyfish", "Question", "Tree"},
        new string[] {"Earth", "Helicopter", "King", "Nail", "UFO"},
        new string[] {"Earth", "IceCream", "Love", "Orange", "Rocket"}
    };

    string[][] deck;
    int deckIndex = 0;
    
    RandomNumberGenerator rng;
    PackedScene cardScene;

    Node2D left;
    Node2D right;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        cardScene = GD.Load<PackedScene>("res://Card.tscn");

        left = GetNode<Node2D>("CardSlotLeft");
        right = GetNode<Node2D>("CardSlotRight");

        InitFobble();
    }

    public void InitFobble()
    {
        rng = new RandomNumberGenerator();
        rng.Randomize();

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

        DrawCard();
        DrawCard();
    }

    public void DrawCard()
    {
        string[] cardInfo = deck[deckIndex--];

        Card newCard = (Card)cardScene.Instance();
        
        if (deckIndex % 2 == 1)
        {
            left.AddChild(newCard);
            newCard.Owner = left;
        }
        else
        {
            right.AddChild(newCard);
            newCard.Owner = right;
        }

        newCard.InitCard(cardInfo);
    }
}
