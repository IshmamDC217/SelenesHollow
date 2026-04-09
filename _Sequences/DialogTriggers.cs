namespace SelenesHollow;

// Maps cell coordinates to one-shot lines of self-talk. As Selene's foot
// crosses into a new cell, the trigger fires once (then never again unless the
// game is restarted via F5).
public class DialogTriggers
{
    private const int   CELL = 96;
    private static readonly Vector2 OFFSET = new(16f, 16f);

    private readonly AmbientDialog _dialog;
    private readonly Dictionary<Point, string> _byCell;
    private readonly HashSet<Point> _fired = new();
    private Point _lastCell = new(-1, -1);

    public DialogTriggers(AmbientDialog dialog)
    {
        _dialog = dialog;
        _byCell = new Dictionary<Point, string>
        {
            // Wake-up area, just south of the fountain courtyard
            { new Point(14, 11), "Where... where am I?" },
            { new Point(14, 12), "I don't remember coming here." },
            { new Point(14, 13), "This place... it's familiar somehow." },

            // Approaching the temple (north of the courtyard)
            { new Point(13,  8), "These pillars are older than the trees." },
            { new Point(14,  7), "The temple... I shouldn't go inside." },

            // Western garden
            { new Point( 7, 14), "A garden? Out here?" },
            { new Point( 4, 14), "These flowers... they smell like home." },
            { new Point( 3, 12), "Someone tended this place once." },

            // Eastern ruins
            { new Point(20, 13), "Ruins. Whoever built this is long gone." },
            { new Point(24, 13), "The stone is warm. That can't be right." },
            { new Point(27, 13), "I've gone far enough this way." },

            // Southern grove and chalice
            { new Point(14, 15), "The path goes south. I should follow it." },
            { new Point(14, 16), "What's that, glinting up ahead?" },

            // Forest fringes
            { new Point( 8,  3), "The forest is so quiet here." },
            { new Point(22,  3), "I shouldn't lose sight of the fountain." },
        };
    }

    public void Reset()
    {
        _fired.Clear();
        _lastCell = new Point(-1, -1);
    }

    public void Check(Vector2 footPos)
    {
        // Cell that contains the foot pixel. Foot Y is at the BOTTOM of the
        // cell, so subtract 1 before dividing to bias toward the cell above.
        int cx = (int)((footPos.X - OFFSET.X) / CELL);
        int cy = (int)((footPos.Y - OFFSET.Y - 1) / CELL);
        var p = new Point(cx, cy);
        if (p == _lastCell) return;
        _lastCell = p;
        if (_fired.Contains(p)) return;
        if (_byCell.TryGetValue(p, out var line))
        {
            _dialog.Show(line);
            _fired.Add(p);
        }
    }
}
