using Godot;
using System;

public class Lobby : Node2D
{
    PackedScene gameScene;
    TextEdit address;
    string text;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        gameScene = GD.Load<PackedScene>("res://Fobble.tscn");

        address = GetNode<TextEdit>("Panel/TextEdit");
    }

    Fobble game = null;
    public void _on_Button_pressed()
    {
        game = (Fobble)gameScene.Instance();
        GD.Print(game);
        game.Connect("ready", this, "_on_Game_ready");

        text = address.Text;

        GetTree().Root.AddChild(game);
    }

    private void _on_Game_ready()
    {
        GD.Print(game);
        game.StartUDPConnection(text, 42069);

        GetTree().Root.RemoveChild(this);
        CallDeferred("free");
    }
}
