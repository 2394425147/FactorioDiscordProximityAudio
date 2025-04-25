namespace Client.VisualComponents
{
    internal sealed class DoubleBufferedListBox : ListBox
    {
        public DoubleBufferedListBox() : base()
        {
            DoubleBuffered = true;
        }
    }
}
