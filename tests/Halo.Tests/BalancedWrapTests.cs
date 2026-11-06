using Halo.Widgets;

namespace Halo.Tests;

// Notification bodies wrap balanced: the same words, broken near the middle instead of leaving one word on
// the last line.
public class BalancedWrapTests
{
    // a fake measurer: every character is 1 unit wide, words greedy-wrapped at the given width
    private static string[] Wrap(string text, float width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var cand = line.Length == 0 ? w : line + " " + w;
            if (line.Length > 0 && cand.Length > width) { lines.Add(line); line = w; }
            else line = cand;
        }
        lines.Add(line);
        return [.. lines];
    }

    private const string Orphan = "Answers get vaguer from here and compact when you can.";

    [Fact]
    public void Greedy_leaves_an_orphan_and_balanced_does_not()
    {
        var greedy = Wrap(Orphan, 50);
        Assert.Equal(2, greedy.Length);
        Assert.Equal("can.", greedy[1]);                      // the defect, reproduced

        var bal = NotifBanner.Balance(w => Wrap(Orphan, w), 50);
        Assert.Equal(2, bal.Length);                          // never MORE lines than greedy
        Assert.True(bal[1].Split(' ').Length >= 3);           // the last line carries real words
        Assert.True(System.Math.Abs(bal[0].Length - bal[1].Length) < 12);
        Assert.Equal(Orphan, string.Join(" ", bal));          // the same words, in order
    }

    [Fact]
    public void One_line_stays_one_line()
        => Assert.Single(NotifBanner.Balance(w => Wrap("Bad internet", w), 50));

    [Fact]
    public void Long_text_is_left_to_greedy()
    {
        string text = string.Join(" ", Enumerable.Repeat("word", 80));
        Assert.Equal(Wrap(text, 30), NotifBanner.Balance(w => Wrap(text, w), 30));
    }
}
