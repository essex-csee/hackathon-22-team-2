using Godot;
using System;

public class Icon : Area2D
{
    [Signal] public delegate void OnClick(string name);

    private bool mouseIn = false;

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

    //Below this comment is weird hacky click handling. Why there isn't a core OnClick handler is beyond me.
    public override void _Input(InputEvent ie)
    {
        //If mouse button click (but not held)
        if (ie is InputEventMouseButton && ie.IsPressed() && !ie.IsEcho() && mouseIn)
        {
            GD.Print("Clicked icon " + Name);
            EmitSignal("OnClick", Name);
        }
    }

    public void _on_Icon_mouse_entered()
    {
        mouseIn = true;
    }

    public void _on_Icon_mouse_exited()
    {
        mouseIn = false;
    }
}
