using UnityEngine;

namespace Site44
{
    // Each locked hatch and its keycard share a colour, and the colour is also a name, so the pairing never depends on seeing it.
    // The palette is chosen to stay distinguishable with the common kinds of colour blindness.
    public static class KeyColours
    {
        private static readonly Color[] Colours =
        {
            new Color(0.00f, 0.45f, 0.70f),   // blue
            new Color(0.90f, 0.62f, 0.00f),   // orange
            new Color(0.00f, 0.62f, 0.45f),   // green
            new Color(0.80f, 0.47f, 0.65f),   // pink
            new Color(0.94f, 0.89f, 0.26f)    // yellow
        };

        private static readonly string[] Names = { "blue", "orange", "green", "pink", "yellow" };

        public static Color Colour(int index)
        {
            return Colours[index % Colours.Length];
        }

        // More than five locks on a level repeat the colours, so later ones are numbered: "blue 2".
        public static string Name(int index)
        {
            int round = index / Names.Length;
            return Names[index % Names.Length] + (round > 0 ? " " + (round + 1) : "");
        }
    }
}
