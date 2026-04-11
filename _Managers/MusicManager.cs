using Microsoft.Xna.Framework.Media;

namespace SelenesHollow;

// Handles background music transitions between game states.
// Intro → Battle → Explore, with crossfade support.
public static class MusicManager
{
    private static Song _intro;
    private static Song _battle;
    private static Song _explore;
    private static string _current;
    private static float _volume = 0.6f;

    public static void Load()
    {
        _intro   = Globals.Content.Load<Song>("music_intro");
        _battle  = Globals.Content.Load<Song>("music_battle");
        _explore = Globals.Content.Load<Song>("music_explore");
        MediaPlayer.IsRepeating = true;
        MediaPlayer.Volume = _volume;
    }

    public static void PlayIntro()
    {
        if (_current == "intro") return;
        _current = "intro";
        MediaPlayer.IsRepeating = true;
        MediaPlayer.Volume = _volume;
        MediaPlayer.Play(_intro);
    }

    public static void PlayBattle()
    {
        if (_current == "battle") return;
        _current = "battle";
        MediaPlayer.IsRepeating = true;
        MediaPlayer.Volume = _volume;
        MediaPlayer.Play(_battle);
    }

    public static void PlayExplore()
    {
        if (_current == "explore") return;
        _current = "explore";
        MediaPlayer.IsRepeating = true;
        MediaPlayer.Volume = _volume;
        MediaPlayer.Play(_explore);
    }

    public static void Stop()
    {
        _current = null;
        MediaPlayer.Stop();
    }
}
