using Godot;
using System;

public class Icon : Area2D
{
    [Signal] public delegate void OnClick();

    public Card Card
    {
        get
        {
            return GetParent() is Card ? GetParent() as Card : null;
        }
    }

    public void SetTexture(Texture img)
    {
        GetNode<Sprite>("Sprite").Texture = img;
    }

    public override void _Input(InputEvent ie)
    {
        //If mouse button click (but not held)
        if (ie is InputEventMouseButton && ie.IsPressed() && !ie.IsEcho())
        {
            GD.Print("Clicked icon " + Name);
        }
    }
}
