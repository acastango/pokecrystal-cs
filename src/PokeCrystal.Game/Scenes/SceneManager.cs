namespace PokeCrystal.Game.Scenes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeCrystal.Rendering;

/// <summary>
/// Manages the active scene. Transitions between scenes are wrapped in a
/// FadeToBlack → swap → FadeFromBlack sequence driven by the palette manager.
/// </summary>
public sealed class SceneManager
{
    private readonly IPaletteManager _palette;

    private IScene? _current;
    private IScene? _pendingScene;

    // Transition state
    private bool _transitioning;
    private float _transitionFadeSec = 0.3f;

    public SceneManager(IPaletteManager palette) => _palette = palette;

    public IScene? Current => _current;

    /// <summary>
    /// Replace the active scene immediately (no fade). Use for first load.
    /// </summary>
    public void SetImmediate(IScene scene)
    {
        _current?.OnExit();
        _current = scene;
        _current.OnEnter();
    }

    /// <summary>
    /// Transition to a new scene using fade-to-black / fade-from-black.
    /// </summary>
    public void Transition(IScene scene)
    {
        if (_transitioning) return;
        _pendingScene = scene;
        _transitioning = true;
        _palette.FadeToBlack(_transitionFadeSec);
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _palette.Update(dt);

        if (_transitioning && _palette.IsFadedToBlack)
        {
            _current?.OnExit();
            _current = _pendingScene;
            _pendingScene = null;
            _transitioning = false;
            _current?.OnEnter();
            _palette.FadeFromBlack(_transitionFadeSec);
        }

        _current?.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _current?.Draw(spriteBatch);
    }
}
