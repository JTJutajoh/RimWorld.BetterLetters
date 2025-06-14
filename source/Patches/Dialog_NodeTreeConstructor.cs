namespace BetterLetters.Patches
{
    internal class Dialog_NodeTreeConstructor
    {
        public static void ConstructorPostfix()
        {
            // This is a very simple patch.
            // All it does is clear the stored reference to the current letter so that any non-letter dialogs don't display a pin button when they shouldn't.
            DialogDrawNodePatch.CurrentLetter = null;
        }
    }
}
