using Godot;
using System.Collections.Generic;

public class InputHandler : Node
{
    private struct FrameState
    {
        public Fobble.Inputs inputs;
        public Fobble.GameState gameState;
        public int frame;
    }

    const int INPUT_DELAY = 5;

    const int ROLLBACK = 7;

    int frameNum;
    Fobble.Inputs[] prevInputs;
    Queue<FrameState> stateQueue;
    
    string currIcon;
    Fobble.CardSlots? currSlot; 

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        prevInputs = new Fobble.Inputs[256];
        stateQueue = new Queue<FrameState>(ROLLBACK);

        for (int i = 0; i < ROLLBACK; i++)
        {
            stateQueue.Enqueue(new FrameState());
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(float delta)
    {
        Fobble.GameState preState = Fobble.Instance.GetGameState();

        //Inputs made this frame
        Fobble.Inputs newInput =  new Fobble.Inputs
        {
            icon = currIcon,
            slot = currSlot
        };

        prevInputs[(frameNum + INPUT_DELAY) % 255] = newInput;

        Fobble.Inputs currInput = prevInputs[frameNum % 255];

        if (Input.IsKeyPressed((int)KeyList.Enter))
        {
            if (currInput.Active)
                Fobble.Instance.ResetGameState(stateQueue.Peek().gameState);
            GD.Print("Resetting");
        }
        
        if (currInput.Active)
            Fobble.Instance.UpdateInput(currInput);

        //Dequeue the oldest state
        stateQueue.Dequeue();

        //Queue the new state
        FrameState fs = new FrameState
        {
            frame = frameNum,
            inputs = currInput,
            gameState = preState
        };
        stateQueue.Enqueue(fs);

        frameNum++;
        currIcon = null;
        currSlot = null;
    }

    public void InputMade(Fobble.CardSlots slot, string icon)
    {
        currSlot = slot;
        currIcon = icon;
    }
}
