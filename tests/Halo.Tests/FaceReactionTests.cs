using Halo.Widgets;

namespace Halo.Tests;

// The face's answer to a click: a poke on the face is a giggle, a click anywhere else is surprise.
public class FaceReactionTests
{
    private static readonly Face.Look Awake = Face.Look.Awake;
    private const long T0 = 10_000;

    [Fact]
    public void No_reaction_leaves_the_look_alone()
    {
        var none = default(FaceReaction);
        Assert.Equal(Awake, none.Apply(Awake, T0));
        Assert.False(none.Alive(T0));
        Assert.Equal((0f, 0f), none.Body(T0));
    }

    [Fact]
    public void Surprise_turns_the_eyes_round_then_lets_them_go()
    {
        var r = new FaceReaction(FaceReaction.Kind.Surprise, T0);
        Assert.True(r.Apply(Awake, T0 + 200).Round > 0.95f);     // held round mid-way
        Assert.True(r.Apply(Awake, T0 + 520).Round < 0.2f);      // nearly back by the end
        Assert.Equal(0f, r.Apply(Awake, T0 + FaceReaction.SurpriseMs).Round);
        Assert.False(r.Alive(T0 + FaceReaction.SurpriseMs));
    }

    [Fact]
    public void Surprise_does_not_move_the_head()
        => Assert.Equal((0f, 0f), new FaceReaction(FaceReaction.Kind.Surprise, T0).Body(T0 + 200));

    [Fact]
    public void Giggle_squeezes_the_eyes_twice_and_the_second_is_smaller()
    {
        var r = new FaceReaction(FaceReaction.Kind.Giggle, T0);
        float first = r.Apply(Awake, T0 + (long)(0.23f * FaceReaction.GiggleMs)).Open;
        float between = r.Apply(Awake, T0 + (long)(0.42f * FaceReaction.GiggleMs)).Open;
        float second = r.Apply(Awake, T0 + (long)(0.63f * FaceReaction.GiggleMs)).Open;
        Assert.True(first < 0.4f);              // squeezed to a slit
        Assert.True(between > 0.9f);            // open again between the two
        Assert.True(second > first && second < 0.6f);
    }

    [Fact]
    public void Giggle_bounces_the_head_and_settles()
    {
        var r = new FaceReaction(FaceReaction.Kind.Giggle, T0);
        float peak = 0f;
        for (long ms = 0; ms < FaceReaction.GiggleMs; ms += 10)
            peak = MathF.Max(peak, MathF.Abs(r.Body(T0 + ms).Squash));
        Assert.True(peak > 0.05f);
        Assert.True(MathF.Abs(r.Body(T0 + FaceReaction.GiggleMs - 20).Squash) < 0.01f);
        Assert.Equal((0f, 0f), r.Body(T0 + FaceReaction.GiggleMs));
    }

    [Fact]
    public void A_reaction_never_closes_eyes_that_were_already_shut_further_than_asked()
    {
        // mid-blink the idle schedule has Open near 0; the reaction scales it, so it cannot reopen a blink
        var blink = Awake with { Open = 0.05f };
        Assert.True(new FaceReaction(FaceReaction.Kind.Giggle, T0).Apply(blink, T0 + 200).Open <= 0.05f);
    }
}
