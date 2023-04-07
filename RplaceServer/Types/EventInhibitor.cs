namespace RplaceServer.Types;

public class EventInhibitor
{
    public bool Raised { get; private set; }
    
    public void Raise()
    {
        Raised = true;
    }
}