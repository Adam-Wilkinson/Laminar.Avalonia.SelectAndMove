using Avalonia;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;

namespace Laminar.Avalonia.SelectAndMove.GestureRecognizers;

public abstract class GestureRecognizerBase : AvaloniaObject, IGestureRecognizer
{
    protected IPointer? Pointer { get; set; }

    protected IInputElement? Target { get; private set; }

    protected IGestureRecognizerActionsDispatcher? Actions { get; private set; }

    public void Initialize(IInputElement target, IGestureRecognizerActionsDispatcher actions)
    {
        Target = target;
        Actions = actions;
        PostInitialize();
    }

    public void PointerCaptureLost(IPointer pointer)
    {
        if (pointer == Pointer)
        {
            Pointer = null;
            EndGesture();
        }
    }

    public void PointerMoved(PointerEventArgs e)
    {
        if (e.Pointer == Pointer && Actions is not null)
        {
            Actions.Capture(e.Pointer, this);
            TrackedPointerMoved(e);
        }
    }

    public abstract void PointerPressed(PointerPressedEventArgs e);

    public virtual void PointerReleased(PointerReleasedEventArgs e)
    {
        PointerCaptureLost(e.Pointer);
    }

    protected virtual void EndGesture() { }

    protected abstract void TrackedPointerMoved(PointerEventArgs e);

    protected virtual void PostInitialize() { }

    protected void Track(IPointer pointer)
    {
        Pointer = pointer;
    }
}
