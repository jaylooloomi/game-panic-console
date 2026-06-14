using PanicConsole.Core;

public class FakeMinigame : MinigameBase
{
    public int SimTicks;
    public int ResetCount;
    public bool LastTickFocused;

    public FakeMinigame(string id = "fake") { GameId = id; }
    public override void Reset() { ResetCount++; }
    protected override void Simulate(float dt, bool isFocused)
    {
        SimTicks++;
        LastTickFocused = isFocused;
    }
    public void FailNow() => TriggerFail();
    public void ScoreNow(int points) => RaiseScore(points);
}
